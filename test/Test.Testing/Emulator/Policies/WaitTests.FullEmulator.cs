// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections;
using System.Collections.Concurrent;
using System.Net;
using System.Reflection;

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring.Expressions;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Document;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Data;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Expressions;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Services;

using Authorization = Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring.Expressions.Authorization;

namespace Test.Emulator.Emulator.Policies;

public partial class WaitTests
{
    [TestMethod]
    [DataRow(nameof(IInboundContext), false)]
    [DataRow(nameof(IBackendContext), false)]
    [DataRow(nameof(IOutboundContext), false)]
    [DataRow(nameof(IOnErrorContext), false)]
    [DataRow(nameof(IInboundContext), true)]
    [DataRow(nameof(IBackendContext), true)]
    [DataRow(nameof(IOutboundContext), true)]
    [DataRow(nameof(IOnErrorContext), true)]
    public async Task FullWait_ChoosePublishesSharedMessageEffectsBeforeEitherBranchCompletes(
        string section, bool fragment)
    {
        var test = fragment
            ? new FragmentHost().AsTestDocument().RegisterFragment("wait-fragment", new FullMessageFragment())
            : new FullMessageDocument().AsTestDocument();
        var client = new ControlledWaitHttpClient();
        test.Context.Services.Register<IHttpClient>(client);
        test.Context.Variables["enabled"] = true;
        var execution = RunWaitAsync(() => RunSection(test, section));

        try
        {
            await Task.WhenAll(client.First.Started.Task, client.Second.Started.Task).WaitAsync(s_waitTimeout);
            execution.IsCompleted.Should().BeFalse();
            var message = section is nameof(IInboundContext) or nameof(IBackendContext)
                ? (MockMessage)test.Context.Request : test.Context.Response;
            message.Headers["X-first"].Should().Equal("first");
            message.Headers["X-second"].Should().Equal("second");
            message.Body.Content.Should().BeOneOf("first", "second");
            test.Context.Variables.Should().NotContainKey("first-local").And.NotContainKey("second-local");
            client.Second.Complete(HttpStatusCode.Accepted);
            client.First.Complete(HttpStatusCode.Created);
            await execution.WaitAsync(s_waitTimeout);

            test.Context.Variables["first-local"].Should().Be("first");
            test.Context.Variables["second-local"].Should().Be("second");
            test.Context.Variables["after-wait"].Should().Be(true);
        }
        finally
        {
            client.ReleaseRemaining();
            await ObserveWaitExecution(execution);
        }
    }

    [TestMethod]
    [DataRow(nameof(IInboundContext))]
    [DataRow(nameof(IBackendContext))]
    [DataRow(nameof(IOutboundContext))]
    [DataRow(nameof(IOnErrorContext))]
    public void FullWait_CopiesEveryRegisteredSectionHandlerAndItsSetupCollections(string section)
    {
        var test = CreateTypedWait("all", fragment: false);
        var original = GetFullWaitHandlers(test.Context, section);
        var callbacks = 0;
        void Inspect(GatewayContext branch, SendRequestConfig config)
        {
            var handlers = (IDictionary)typeof(GatewayContext)
                .GetProperty("CurrentSectionHandlers", BindingFlags.Instance | BindingFlags.NonPublic)!
                .GetValue(branch)!;
            handlers.Count.Should().Be(original.Count);
            foreach (DictionaryEntry entry in original)
            {
                var copy = handlers[entry.Key]!;
                copy.Should().BeOfType(entry.Value!.GetType()).And.NotBeSameAs(entry.Value);
                foreach (var property in entry.Value.GetType().GetProperties(BindingFlags.Instance
                             | BindingFlags.Public | BindingFlags.NonPublic))
                {
                    if (property.GetValue(entry.Value) is IList source)
                    {
                        var setup = (IList)property.GetValue(copy)!;
                        setup.Should().NotBeSameAs(source);
                        setup.Count.Should().Be(source.Count);
                    }
                }
            }
            branch.Variables[config.ResponseVariableName] = true;
            Interlocked.Increment(ref callbacks);
        }
        SetupFullWaitSend(test, section, Inspect);

        RunSection(test, section);

        callbacks.Should().Be(2);
        test.Context.Variables["first"].Should().Be(true);
        test.Context.Variables["second"].Should().Be(true);
    }

    [TestMethod]
    public async Task FullWait_AnyPreservesAlreadyExecutedLoserSideEffectsButNotItsVariableDictionary()
    {
        var test = new InboundWaitAction(context => context.Wait("any",
            branch =>
            {
                if (true)
                {
                    branch.SetVariable("loser-local", true);
                    branch.SetHeader("X-Loser", "before");
                    branch.CacheStoreValue(new CacheStoreValueConfig { Key = "loser", Value = "stored", Duration = 60 });
                    branch.SendRequest(WaitRequest("first"));
                    branch.SetHeader("X-Loser", "late");
                }
            },
            branch =>
            {
                if (true)
                {
                    branch.SetVariable("winner-local", true);
                    branch.SetHeader("X-Winner", "before");
                    branch.SendRequest(WaitRequest("second"));
                }
            })).AsTestDocument();
        var client = new ControlledWaitHttpClient();
        test.Context.Services.Register<IHttpClient>(client);
        var execution = RunWaitAsync(test.RunInbound);

        try
        {
            await Task.WhenAll(client.First.Started.Task, client.Second.Started.Task).WaitAsync(s_waitTimeout);
            client.Second.Complete(HttpStatusCode.OK);
            await execution.WaitAsync(s_waitTimeout);
            client.First.Token.IsCancellationRequested.Should().BeTrue();
            test.Context.Request.Headers["X-Loser"].Should().Equal("before");
            test.Context.Request.Headers["X-Winner"].Should().Equal("before");
            test.Context.Variables.Should().NotContainKey("loser-local");
            test.Context.Variables["winner-local"].Should().Be(true);
            (await test.SetupCacheStore().GetAsync("loser")).Should().Be("stored");
            var late = client.First.Complete(HttpStatusCode.OK);
            await late.Disposed.Task.WaitAsync(s_waitTimeout);
            test.Context.Request.Headers["X-Loser"].Should().Equal("before");
        }
        finally
        {
            client.ReleaseRemaining();
            await ObserveWaitExecution(execution);
        }
    }

    [TestMethod]
    public void FullWait_RetryFragmentsAndProvidersExecuteAgainstTheBranchContext()
    {
        var scheduler = new VirtualRetryScheduler();
        var test = new InboundWaitAction(context => context.Wait("all", branch =>
        {
            if (true)
            {
                branch.SetVariable("attempt", 0);
                branch.Retry(new RetryConfig
                {
                    Condition = true,
                    Count = 3,
                    Interval = 1,
                    ConditionEvaluator = () => (int)branch.ExpressionContext.Variables["attempt"] < 3
                }, () =>
                {
                    branch.SetVariable("attempt", (int)branch.ExpressionContext.Variables["attempt"] + 1);
                    branch.IncludeFragment("full-fragment");
                });
                branch.AuthenticationManagedIdentity(new ManagedIdentityAuthenticationConfig
                {
                    Resource = "https://resource.example",
                    OutputTokenVariableName = "token"
                });
            }
        })).AsTestDocument().RegisterFragment("full-fragment", new FullVariableFragment());
        test.Context.Services.Register<IRetryScheduler>(scheduler);
        test.SetupInbound().AuthenticationManagedIdentity().ReturnsToken("provided-token");
        test.SetupInbound().SetVariable((_, name, _) => name == "fragment-variable")
            .WithCallback((branch, name, _) => branch.Variables[name] = "fragment setup");

        test.RunInbound();

        test.Context.Variables["attempt"].Should().Be(3);
        test.Context.Variables["fragment-variable"].Should().Be("fragment setup");
        test.Context.Variables["token"].Should().Be("provided-token");
        scheduler.Delays.Should().HaveCount(2);
    }

    [TestMethod]
    public void FullWait_WaitInsideRetryRemainsProhibitedInsideAChooseBranch()
    {
        var test = new InboundWaitAction(context => context.Wait("all", branch =>
        {
            if (true)
            {
                branch.Retry(new RetryConfig { Condition = false, Count = 1, Interval = 1 },
                    () => branch.Wait("all", nested => nested.SetVariable("invalid", true)));
            }
        })).AsTestDocument();

        var error = Assert.ThrowsExactly<PolicyException>(test.RunInbound);

        error.Policy.Should().Be(nameof(IInboundContext.Wait));
        error.InnerException.Should().BeOfType<InvalidOperationException>();
        error.Message.Should().Contain("retry");
        test.Context.Variables.Should().NotContainKey("invalid");
    }

    [TestMethod]
    [DataRow("all")]
    [DataRow("any")]
    public void FullWait_ReturnResponseEndsOnlyItsClonedChildPipelineAndSharesTheResponse(string mode)
    {
        var test = new InboundWaitAction(context =>
        {
            context.Wait(mode, branch =>
            {
                if (true)
                {
                    branch.SetVariable("before-return", true);
                    branch.ReturnResponse(new ReturnResponseConfig
                    {
                        Status = new StatusConfig { Code = 202, Reason = "Accepted" },
                        Body = new BodyConfig { Content = "child response" }
                    });
                    branch.SetVariable("after-return", true);
                }
            });
            context.SetVariable("after-wait", true);
        }).AsTestDocument();

        test.RunInbound();

        test.Context.Response.StatusCode.Should().Be(202);
        test.Context.Response.Body.Content.Should().Be("child response");
        test.Context.ResponseTerminated.Should().BeFalse("the gateway does not back-propagate the child's pipeline stage");
        test.Context.Variables["before-return"].Should().Be(true);
        test.Context.Variables.Should().NotContainKey("after-return");
        test.Context.Variables["after-wait"].Should().Be(true);
    }

    [TestMethod]
    [DataRow("all")]
    [DataRow("any")]
    public void FullWait_ErrorPropagatesItsBranchVariablesAndErrorContext(string mode)
    {
        var test = new InboundWaitAction(context => context.Wait(mode, branch =>
        {
            if (true)
            {
                branch.SetVariable("before-error", "kept");
                branch.GetAuthorizationContext(new GetAuthorizationContextConfig
                {
                    ProviderId = "provider",
                    AuthorizationId = "connection",
                    ContextVariableName = "authorization"
                });
            }
        })).AsTestDocument();
        var expected = new HttpRequestException("provider failed");
        test.Context.Services.Register<IAuthorizationProvider>(new FullAuthorizationProvider((_, _) =>
            Task.FromException<Authorization>(expected)));

        var error = Assert.ThrowsExactly<PolicyException>(test.RunInbound);

        error.Policy.Should().Be(nameof(IInboundContext.GetAuthorizationContext));
        error.Section.Should().Be(nameof(IInboundContext));
        error.InnerException.Should().BeSameAs(expected);
        test.Context.Variables["before-error"].Should().Be("kept");
        test.Context.LastError.Reason.Should().Be("AuthorizationAcquisitionFailed");
        test.Context.Response.StatusCode.Should().Be(500);
    }

    [TestMethod]
    public void FullWait_SharesImmediateAndDeferredRateQuotaAccountingWithTheLogicalRequest()
    {
        var store = new RateLimitStore();
        var document = new FullRequestDocument(context =>
        {
            context.RateLimitByKey(new RateLimitByKeyConfig
            {
                CounterKey = "parent",
                Calls = 100,
                RenewalPeriod = 60,
                IncrementAfterResponse = true
            });
            context.Wait("all",
                branch =>
                {
                    if (true)
                    {
                        branch.RateLimitByKey(new RateLimitByKeyConfig
                        {
                            CounterKey = "branch",
                            Calls = 100,
                            RenewalPeriod = 60,
                            IncrementAfterResponse = true
                        });
                        branch.QuotaByKey(new QuotaByKeyConfig
                        {
                            CounterKey = "quota",
                            Calls = 100,
                            Bandwidth = 100,
                            RenewalPeriod = 60
                        });
                        branch.SetVariable("first-accounted", true);
                    }
                },
                branch =>
                {
                    if (true)
                    {
                        branch.RateLimitByKey(new RateLimitByKeyConfig
                        {
                            CounterKey = "branch",
                            Calls = 100,
                            RenewalPeriod = 60
                        });
                        branch.QuotaByKey(new QuotaByKeyConfig
                        {
                            CounterKey = "quota",
                            Calls = 100,
                            Bandwidth = 100,
                            RenewalPeriod = 60
                        });
                        branch.SetVariable("second-accounted", true);
                    }
                });
        }, backend: context => ((GatewayContext)context.ExpressionContext).Response.Body.Content = new string('b', 1024));
        var test = document.AsTestDocument();
        test.Context.Request.Body.Content = new string('a', 1024);
        test.Context.Services.Register(store);

        test.RunAll();

        store.GetCount("rate-limit-by-key:parent").Should().Be(1);
        store.GetCount("rate-limit-by-key:branch").Should().Be(2);
        store.GetCount("quota-by-key:quota").Should().Be(1);
        store.GetBandwidth("quota-by-key:quota").Should().Be(2048);
        test.Context.Variables["first-accounted"].Should().Be(true);
        test.Context.Variables["second-accounted"].Should().Be(true);
        test.Context.Services.Resolve<PolicyCounterService>()!.HasPendingForFullWait().Should().BeFalse();
    }

    [TestMethod]
    public void FullWait_TokenLimitsUseBranchOwnedServicesAndOneSharedRequestLedger()
    {
        var store = new TokenLimitCounterStore();
        var config = new TokenLimitConfig
        {
            CounterKey = "shared",
            TokensPerMinute = 100,
            EstimatePromptToken = false,
            TokensConsumedVariableName = "tokens",
            RemainingTokensHeaderName = "X-Tokens"
        };
        var test = new FullRequestDocument(context =>
        {
            context.LlmTokenLimit(config);
            context.Wait("all",
                branch => { if (true) { branch.LlmTokenLimit(config); branch.SetVariable("one", true); } },
                branch => { if (true) { branch.AzureOpenAiTokenLimit(config); branch.SetVariable("two", true); } });
        }, backend: context => ((GatewayContext)context.ExpressionContext).Response.Body.Content =
            """{"usage":{"prompt_tokens":3,"completion_tokens":2}}""").AsTestDocument();
        test.Context.Services.Register(store);
        test.Context.Request.Body.Content = """{"messages":[{"role":"user","content":"hello"}]}""";

        test.RunAll();

        store.GetRateTokens("shared", DateTimeOffset.UtcNow).Should().Be(5);
        test.Context.Variables["tokens"].Should().Be(5L);
        test.Context.Variables["one"].Should().Be(true);
        test.Context.Variables["two"].Should().Be(true);
        test.Context.Response.Headers["X-Tokens"].Should().Equal("95");
        test.Context.Services.Resolve<TokenLimitService>()!.HasPendingResponse.Should().BeFalse();
    }

    [TestMethod]
    public async Task FullWait_LimitConcurrencyUsesOneSharedLimiterAndReleasesCanceledPermits()
    {
        var limiter = new KeyedConcurrencyLimiter();
        var entered = WaitSignal<bool>();
        var test = new InboundWaitAction(context => context.Wait("any",
            branch =>
            {
                if (true)
                {
                    branch.LimitConcurrency(new LimitConcurrencyConfig { Key = "shared", MaxCount = 1 }, () =>
                    {
                        entered.SetResult(true);
                        branch.SendRequest(WaitRequest("first"));
                    });
                }
            },
            branch =>
            {
                if (true)
                {
                    entered.Task.GetAwaiter().GetResult();
                    branch.LimitConcurrency(new LimitConcurrencyConfig { Key = "shared", MaxCount = 1 },
                        () => Assert.Fail("The second branch must be denied."));
                }
            })).AsTestDocument();
        var client = new ControlledWaitHttpClient();
        test.Context.Services.Register<IConcurrencyLimiter>(limiter);
        test.Context.Services.Register<IHttpClient>(client);
        var execution = RunWaitAsync(test.RunInbound);

        try
        {
            await client.First.Started.Task.WaitAsync(s_waitTimeout);
            await execution.WaitAsync(s_waitTimeout);
            client.First.Token.IsCancellationRequested.Should().BeTrue();
            test.Context.Response.StatusCode.Should().Be(429);
            test.Context.ResponseTerminated.Should().BeFalse();
            var late = client.First.Complete(HttpStatusCode.OK);
            await late.Disposed.Task.WaitAsync(s_waitTimeout);
            var deadline = DateTime.UtcNow + s_waitTimeout;
            while (limiter.GetCount("shared") != 0 && DateTime.UtcNow < deadline)
            {
                await Task.Delay(10);
            }
            limiter.GetCount("shared").Should().Be(0);
        }
        finally
        {
            client.ReleaseRemaining();
            await ObserveWaitExecution(execution);
        }
    }

    [TestMethod]
    public void FullWait_CacheValueDispatcherTelemetryAndMetricStoresAreNotDiscarded()
    {
        var test = new InboundWaitAction(context => context.Wait("all",
            branch =>
            {
                if (true)
                {
                    branch.CacheValue(new CacheValueConfig { Key = "one", VariableName = "one", ExpiresAfter = 60 },
                        () => branch.SetVariable("one", "cached value"));
                    branch.Trace(new TraceConfig { Source = "one", Message = "recorded" });
                    branch.LogToEventHub(new LogToEventHubConfig { LoggerId = "logger", Value = "one" });
                    branch.EmitMetric(new EmitMetricConfig
                    {
                        Name = "branch-metric",
                        Dimensions = [new MetricDimensionConfig { Name = "API ID" }],
                        Value = 1
                    });
                }
            },
            branch =>
            {
                if (true)
                {
                    branch.CacheValue(new CacheValueConfig { Key = "two", VariableName = "two", ExpiresAfter = 60 },
                        () => branch.SetVariable("two", "cached value"));
                    branch.Trace(new TraceConfig { Source = "two", Message = "recorded" });
                    branch.LogToEventHub(new LogToEventHubConfig { LoggerId = "logger", Value = "two" });
                    branch.EmitMetric(new EmitMetricConfig
                    {
                        Name = "branch-metric",
                        Dimensions = [new MetricDimensionConfig { Name = "API ID" }],
                        Value = 1
                    });
                }
            })).AsTestDocument();
        var logger = test.SetupLoggerStore().Add("logger");

        test.RunInbound();

        test.Context.Variables["one"].Should().Be("cached value");
        test.Context.Variables["two"].Should().Be("cached value");
        test.SetupCacheStore().GetAsync("one").GetAwaiter().GetResult().Should().Be("cached value");
        test.SetupCacheStore().GetAsync("two").GetAwaiter().GetResult().Should().Be("cached value");
        logger.Events.Should().HaveCount(2);
        test.SetupLoggerStore().Traces.Should().HaveCount(2);
        test.SetupLoggerStore().Metrics.Should().HaveCount(2);
    }

    [TestMethod]
    public async Task FullWait_HandlerSetupCollectionsAreFrozenBeforeBranchesStart()
    {
        var client = new ControlledWaitHttpClient();
        var test = new InboundWaitAction(context => context.Wait("all",
            branch => { if (true) { branch.SendRequest(WaitRequest("first")); branch.SetVariable("first-local", "original"); } },
            branch => { if (true) { branch.SendRequest(WaitRequest("second")); branch.SetVariable("second-local", "original"); } }))
            .AsTestDocument();
        test.Context.Services.Register<IHttpClient>(client);
        var execution = RunWaitAsync(test.RunInbound);

        try
        {
            await Task.WhenAll(client.First.Started.Task, client.Second.Started.Task).WaitAsync(s_waitTimeout);
            test.SetupInbound().SetVariable().WithCallback((branch, name, _) => branch.Variables[name] = "late setup");
            client.First.Complete(HttpStatusCode.OK);
            client.Second.Complete(HttpStatusCode.OK);
            await execution.WaitAsync(s_waitTimeout);

            test.Context.Variables["first-local"].Should().Be("original");
            test.Context.Variables["second-local"].Should().Be("original");
        }
        finally
        {
            client.ReleaseRemaining();
            await ObserveWaitExecution(execution);
        }
    }

    [TestMethod]
    [DataRow("authorization")]
    [DataRow("service-bus")]
    [DataRow("binding")]
    [DataRow("pubsub")]
    [DataRow("safety")]
    [DataRow("embedding")]
    [DataRow("rate")]
    [DataRow("cache-store")]
    [DataRow("cache-remove")]
    [DataRow("cache-value")]
    public async Task FullWait_AnyPropagatesCancellationToEveryAsyncServiceFamily(string family)
    {
        var provider = new FullPendingServices();
        var client = new ControlledWaitHttpClient();
        var test = new InboundWaitAction(context => context.Wait("any",
            branch => { if (true) { ExecuteFullPendingFamily(branch, family); branch.SetVariable("loser-late", true); } },
            branch => branch.SendRequest(WaitRequest("second")))).AsTestDocument();
        test.Context.Request.Body.Content = """{"messages":[{"role":"user","content":"hello"}]}""";
        provider.Register(test.Context.Services);
        test.Context.Services.Register<IHttpClient>(client);
        var execution = RunWaitAsync(test.RunInbound);

        try
        {
            await Task.WhenAll(provider.Started.Task, client.Second.Started.Task).WaitAsync(s_waitTimeout);
            client.Second.Complete(HttpStatusCode.OK);
            await execution.WaitAsync(s_waitTimeout);
            provider.Token.IsCancellationRequested.Should().BeTrue();
            test.Context.Variables.Should().NotContainKey("loser-late");
            provider.Release.TrySetException(new HttpRequestException("late service failure"));
            test.Context.Variables.Should().NotContainKey("loser-late");
        }
        finally
        {
            provider.Release.TrySetCanceled();
            client.ReleaseRemaining();
            await ObserveWaitExecution(execution);
        }
    }

    [TestMethod]
    public void FullWait_PreservesExistingExplicitHandlerLimitationsInsteadOfFallingBack()
    {
        var test = new InboundWaitAction(context => context.Wait("all", branch =>
        {
            if (true)
            {
                branch.SetVariable("before", true);
                branch.SetBody("template", new SetBodyConfig { Template = "liquid" });
            }
        })).AsTestDocument();

        var error = Assert.ThrowsExactly<PolicyException>(test.RunInbound);

        error.Policy.Should().Be(nameof(IInboundContext.SetBody));
        error.InnerException.Should().BeOfType<NotSupportedException>();
        error.Message.Should().Contain("liquid");
        test.Context.Variables["before"].Should().Be(true);
    }

    [TestMethod]
    public async Task FullWait_FinalSettlementCountsLoserAdmissionsWithoutPublishingItsOutputVariables()
    {
        var rates = new RateLimitStore();
        var tokens = new TokenLimitCounterStore();
        var client = new ControlledWaitHttpClient();
        var test = new FullRequestDocument(context => context.Wait("any",
            branch =>
            {
                if (true)
                {
                    branch.RateLimitByKey(new RateLimitByKeyConfig
                    {
                        CounterKey = "loser",
                        Calls = 100,
                        RenewalPeriod = 60,
                        IncrementAfterResponse = true,
                        RemainingCallsVariableName = "loser-remaining"
                    });
                    branch.LlmTokenLimit(new TokenLimitConfig
                    {
                        CounterKey = "loser",
                        TokensPerMinute = 100,
                        EstimatePromptToken = false,
                        TokensConsumedVariableName = "loser-tokens"
                    });
                    branch.SendRequest(WaitRequest("first"));
                }
            },
            branch => branch.SendRequest(WaitRequest("second"))),
            backend: context => ((GatewayContext)context.ExpressionContext).Response.Body.Content =
                """{"usage":{"prompt_tokens":3,"completion_tokens":2}}""").AsTestDocument();
        test.Context.Request.Body.Content = """{"messages":[{"role":"user","content":"hello"}]}""";
        test.Context.Services.Register(rates).Register(tokens).Register<IHttpClient>(client);
        var execution = RunWaitAsync(test.RunAll);

        try
        {
            await Task.WhenAll(client.First.Started.Task, client.Second.Started.Task).WaitAsync(s_waitTimeout);
            client.Second.Complete(HttpStatusCode.OK);
            await execution.WaitAsync(s_waitTimeout);

            rates.GetCount("rate-limit-by-key:loser").Should().Be(1);
            tokens.GetRateTokens("loser", DateTimeOffset.UtcNow).Should().Be(5);
            test.Context.Variables.Should().NotContainKey("loser-remaining").And.NotContainKey("loser-tokens");
        }
        finally
        {
            client.ReleaseRemaining();
            await ObserveWaitExecution(execution);
        }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void FullWait_SemanticCacheLookupStateIsSharedWithItsClonedOutboundStore(bool azure)
    {
        var cache = new CacheStore().WithExternalCacheSetup();
        var embedding = new FullEmbeddingProvider();
        var test = new FullSemanticDocument(azure).AsTestDocument();
        test.Context.Services.Register<ICache>(cache).Register<ISemanticCacheEmbeddingProvider>(embedding);
        test.Context.Request.Body.Content = """{"messages":[{"role":"user","content":"hello"}]}""";

        test.RunInbound();
        test.Context.Response.Body.Content = """{"choices":[{"message":{"role":"assistant","content":"cached"}}]}""";
        test.RunOutbound();
        var probe = new FullSemanticDocument(azure).AsTestDocument();
        probe.Context.Services.Register<ICache>(cache).Register<ISemanticCacheEmbeddingProvider>(embedding);
        probe.Context.Request.Body.Content = test.Context.Request.Body.Content;
        probe.RunInbound();

        test.Context.Variables["stored"].Should().Be(true);
        probe.Context.ResponseTerminated.Should().BeTrue();
        probe.Context.Response.Body.Content.Should().Be(test.Context.Response.Body.Content);
        var replay = new TestDocument(new FullSemanticStoreOnly(azure)) { Context = test.Context };
        Assert.ThrowsExactly<PolicyException>(replay.RunOutbound)
            .InnerException.Should().BeOfType<InvalidOperationException>();
    }

    [TestMethod]
    public async Task FullWait_OneWayOperationsRemainTrackedAndLinkedToCallerAfterTheBranchReturns()
    {
        using var cancellation = new CancellationTokenSource();
        var client = new ControlledWaitHttpClient();
        var test = new InboundWaitAction(context => context.Wait("all", branch =>
        {
            if (true)
            {
                branch.SendOneWayRequest(new SendOneWayRequestConfig { Url = "https://example.com/first" });
                branch.SetVariable("started", true);
            }
        })).AsTestDocument();
        var state = new HttpTransportState { CancellationToken = cancellation.Token };
        test.Context.Services.Register(state).Register<IHttpClient>(client);

        try
        {
            test.RunInbound();
            await client.First.Started.Task.WaitAsync(s_waitTimeout);
            state.PendingOneWayRequests.Should().HaveCount(1);
            test.Context.Variables["started"].Should().Be(true);
            cancellation.Cancel();
            Func<Task> drain = () => state.DrainAsync().WaitAsync(s_waitTimeout);
            await drain.Should().ThrowAsync<OperationCanceledException>();
            client.First.Token.IsCancellationRequested.Should().BeTrue();
        }
        finally
        {
            client.ReleaseRemaining();
            foreach (var pending in state.PendingOneWayRequests)
            {
                await ObserveWaitExecution(pending);
            }
        }
    }

    [TestMethod]
    public void FullWait_DoesNotIntroduceLimiterCompletionGuardsWhenNoLimiterPolicyRan()
    {
        var test = new InboundWaitAction(context => context.Wait("all",
            branch => branch.CacheLookupValue(new CacheLookupValueConfig
            {
                Key = "key",
                VariableName = "value",
                DefaultValue = "value"
            }))).AsTestDocument();
        var requestId = test.Context.RequestId;

        test.RunAll();
        test.RunAll();

        test.Context.RequestId.Should().Be(requestId);
        test.Context.Variables["value"].Should().Be("value");
        test.Context.Services.HasService<PolicyCounterService>().Should().BeFalse();
        test.Context.Services.HasService<TokenLimitService>().Should().BeFalse();
    }

    [TestMethod]
    [DataRow("body")]
    [DataRow("status")]
    [DataRow("replacement")]
    public async Task FullWait_ExplicitMessageWritesMustNotDisappearWhenEqualToTheOldSnapshot(string write)
    {
        var entered = WaitSignal<bool>();
        var release = WaitSignal<bool>();
        var client = new ControlledWaitHttpClient();
        var test = new FullOutboundAction(context => context.Wait("all",
            branch =>
            {
                if (true)
                {
                    if (write == "replacement")
                        branch.ReturnResponse(new ReturnResponseConfig());
                    else
                        branch.SetVariable("write", true);
                }
            },
            branch =>
            {
                if (true)
                {
                    entered.Task.GetAwaiter().GetResult();
                    branch.SetBody("sibling body");
                    branch.SetStatus(new StatusConfig { Code = 503, Reason = "Unavailable" });
                    branch.SetHeader("X-Sibling", "value");
                    branch.SendRequest(WaitRequest("second"));
                }
            })).AsTestDocument();
        test.Context.Response.Body.Content = "original body";
        test.Context.Services.Register<IHttpClient>(client);
        test.SetupOutbound().SetVariable().WithCallback((branch, name, value) =>
        {
            entered.SetResult(true);
            release.Task.GetAwaiter().GetResult();
            if (write == "body") branch.Response.Body.Content = "original body";
            else branch.Response.StatusCode = 200;
            branch.Variables[name] = value;
        });
        test.SetupOutbound().ReturnResponse((_, _) => true).WithCallback((branch, _) =>
        {
            entered.SetResult(true);
            release.Task.GetAwaiter().GetResult();
            branch.Response = new MockResponse { StatusCode = 200, StatusReason = "OK" };
        });
        var execution = RunWaitAsync(test.RunOutbound);

        try
        {
            await client.Second.Started.Task.WaitAsync(s_waitTimeout);
            test.Context.Response.StatusCode.Should().Be(503);
            release.SetResult(true);
            client.Second.Complete(HttpStatusCode.OK);
            await execution.WaitAsync(s_waitTimeout);

            if (write == "body")
                test.Context.Response.Body.Content.Should().Be("original body");
            else
                test.Context.Response.StatusCode.Should().Be(200);
            if (write == "replacement")
            {
                test.Context.Response.StatusReason.Should().Be("OK");
                test.Context.Response.Headers.Should().NotContainKey("X-Sibling");
                test.Context.Response.Body.Content.Should().BeNull();
            }
        }
        finally
        {
            release.TrySetResult(true);
            client.ReleaseRemaining();
            await ObserveWaitExecution(execution);
        }
    }

    private static IDictionary GetFullWaitHandlers(GatewayContext context, string section)
    {
        var field = section switch
        {
            nameof(IInboundContext) => "InboundProxy",
            nameof(IBackendContext) => "BackendProxy",
            nameof(IOutboundContext) => "OutboundProxy",
            nameof(IOnErrorContext) => "OnErrorProxy",
            _ => throw new ArgumentException("Unknown section.", nameof(section))
        };
        var proxy = typeof(GatewayContext).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(context)!;
        return (IDictionary)proxy.GetType().BaseType!.GetField("_handlers", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(proxy)!;
    }

    private static void SetupFullWaitSend(TestDocument test, string section, Action<GatewayContext, SendRequestConfig> callback)
    {
        switch (section)
        {
            case nameof(IInboundContext): test.SetupInbound().SendRequest().WithCallback(callback); break;
            case nameof(IBackendContext): test.SetupBackend().SendRequest().WithCallback(callback); break;
            case nameof(IOutboundContext): test.SetupOutbound().SendRequest().WithCallback(callback); break;
            case nameof(IOnErrorContext): test.SetupOnError().SendRequest().WithCallback(callback); break;
            default: throw new ArgumentException("Unknown section.", nameof(section));
        }
    }

    private static void FullMessageBranch(string name, Action<string, object> variable,
        Action<string, string[]> header, Action<string, SetBodyConfig?> body, Action<SendRequestConfig> send)
    {
        variable($"{name}-local", name);
        header($"X-{name}", [name]);
        body(name, null);
        send(WaitRequest(name));
    }

    private sealed class FullMessageDocument : IDocument
    {
        public void Inbound(IInboundContext context)
        {
            context.Wait("all",
                branch => { if ((bool)branch.ExpressionContext.Variables["enabled"]) FullMessageBranch("first", branch.SetVariable, branch.SetHeader, branch.SetBody, branch.SendRequest); },
                branch => { if ((bool)branch.ExpressionContext.Variables["enabled"]) FullMessageBranch("second", branch.SetVariable, branch.SetHeader, branch.SetBody, branch.SendRequest); });
            context.SetVariable("after-wait", true);
        }
        public void Backend(IBackendContext context)
        {
            context.Wait("all",
                branch => { if ((bool)branch.ExpressionContext.Variables["enabled"]) FullMessageBranch("first", branch.SetVariable, branch.SetHeader, branch.SetBody, branch.SendRequest); },
                branch => { if ((bool)branch.ExpressionContext.Variables["enabled"]) FullMessageBranch("second", branch.SetVariable, branch.SetHeader, branch.SetBody, branch.SendRequest); });
            context.SetVariable("after-wait", true);
        }
        public void Outbound(IOutboundContext context)
        {
            context.Wait("all",
                branch => { if ((bool)branch.ExpressionContext.Variables["enabled"]) FullMessageBranch("first", branch.SetVariable, branch.SetHeader, branch.SetBody, branch.SendRequest); },
                branch => { if ((bool)branch.ExpressionContext.Variables["enabled"]) FullMessageBranch("second", branch.SetVariable, branch.SetHeader, branch.SetBody, branch.SendRequest); });
            context.SetVariable("after-wait", true);
        }
        public void OnError(IOnErrorContext context)
        {
            context.Wait("all",
                branch => { if ((bool)branch.ExpressionContext.Variables["enabled"]) FullMessageBranch("first", branch.SetVariable, branch.SetHeader, branch.SetBody, branch.SendRequest); },
                branch => { if ((bool)branch.ExpressionContext.Variables["enabled"]) FullMessageBranch("second", branch.SetVariable, branch.SetHeader, branch.SetBody, branch.SendRequest); });
            context.SetVariable("after-wait", true);
        }
    }

    private sealed class FullMessageFragment : IFragment
    {
        public void Fragment(IFragmentContext context)
        {
            context.Wait("all",
                branch => { if ((bool)branch.ExpressionContext.Variables["enabled"]) FullMessageBranch("first", branch.SetVariable, branch.SetHeader, branch.SetBody, branch.SendRequest); },
                branch => { if ((bool)branch.ExpressionContext.Variables["enabled"]) FullMessageBranch("second", branch.SetVariable, branch.SetHeader, branch.SetBody, branch.SendRequest); });
            context.SetVariable("after-wait", true);
        }
    }

    private sealed class FullVariableFragment : IFragment
    {
        public void Fragment(IFragmentContext context) => context.SetVariable("fragment-variable", "original");
    }

    private sealed class FullRequestDocument(Action<IInboundContext> inbound, Action<IBackendContext>? backend = null) : IDocument
    {
        public void Inbound(IInboundContext context) => inbound(context);
        public void Backend(IBackendContext context) => backend?.Invoke(context);
        public void Outbound(IOutboundContext context) { }
        public void OnError(IOnErrorContext context) { }
    }

    private sealed class FullOutboundAction(Action<IOutboundContext> execute) : IDocument
    {
        public void Inbound(IInboundContext context) { }
        public void Outbound(IOutboundContext context) => execute(context);
        public void Backend(IBackendContext context) { }
        public void OnError(IOnErrorContext context) { }
    }

    private sealed class FullAuthorizationProvider(
        Func<AuthorizationRequest, CancellationToken, Task<Authorization>> invoke) : IAuthorizationProvider
    {
        public Task<Authorization> GetAuthorizationAsync(AuthorizationRequest request, CancellationToken cancellationToken = default) =>
            invoke(request, cancellationToken);
    }

    private sealed class FullEmbeddingProvider : ISemanticCacheEmbeddingProvider
    {
        public Task<IReadOnlyList<double>> GenerateAsync(SemanticCacheEmbeddingRequest request,
            CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<double>>([1d, 0d]);
    }

    private sealed class FullSemanticDocument(bool azure) : IDocument
    {
        public void Inbound(IInboundContext context)
        {
            var config = new SemanticCacheLookupConfig
            {
                EmbeddingsBackendId = "embedding",
                EmbeddingsBackendAuth = "system-assigned",
                ScoreThreshold = 0.5m
            };
            if (azure) context.AzureOpenAiSemanticCacheLookup(config);
            else context.LlmSemanticCacheLookup(config);
        }
        public void Outbound(IOutboundContext context)
        {
            context.Wait("all", branch =>
            {
                if (true)
                {
                    if (azure) branch.AzureOpenAiSemanticCacheStore(60);
                    else branch.LlmSemanticCacheStore(60);
                }
            });
            context.SetVariable("stored", true);
        }
        public void Backend(IBackendContext context) { }
        public void OnError(IOnErrorContext context) { }
    }

    private sealed class FullSemanticStoreOnly(bool azure) : IDocument
    {
        public void Inbound(IInboundContext context) { }
        public void Outbound(IOutboundContext context)
        {
            if (azure) context.AzureOpenAiSemanticCacheStore(60);
            else context.LlmSemanticCacheStore(60);
        }
        public void Backend(IBackendContext context) { }
        public void OnError(IOnErrorContext context) { }
    }

    private static void ExecuteFullPendingFamily(IInboundContext branch, string family)
    {
        switch (family)
        {
            case "authorization":
                branch.GetAuthorizationContext(new GetAuthorizationContextConfig { ProviderId = "provider", AuthorizationId = "connection", ContextVariableName = "auth" });
                break;
            case "service-bus":
                branch.SendServiceBusMessage(new SendServiceBusMessageConfig { QueueName = "queue", Payload = "message" });
                break;
            case "binding":
                branch.InvokeDarpBinding(new InvokeDarpBindingConfig { Name = "binding", Timeout = 60 });
                break;
            case "pubsub":
                branch.PublishToDarp(new PublishToDarpConfig { Topic = "component/topic", Content = "message", Timeout = 60 });
                break;
            case "safety":
                branch.LlmContentSafety(new LlmContentSafetyConfig { BackendId = "safety", ShieldPrompt = true });
                break;
            case "embedding":
                branch.LlmSemanticCacheLookup(new SemanticCacheLookupConfig
                {
                    EmbeddingsBackendId = "embedding",
                    EmbeddingsBackendAuth = "system-assigned",
                    ScoreThreshold = 0.5m
                });
                break;
            case "rate":
                branch.RateLimitByKey(new RateLimitByKeyConfig { CounterKey = "rate", Calls = 100, RenewalPeriod = 60 });
                break;
            case "cache-store":
                branch.CacheStoreValue(new CacheStoreValueConfig { Key = "key", Value = "value", Duration = 60 });
                break;
            case "cache-remove":
                branch.CacheRemoveValue(new CacheRemoveValueConfig { Key = "key" });
                break;
            case "cache-value":
                branch.CacheValue(new CacheValueConfig { Key = "key", VariableName = "value" }, () => branch.SetVariable("value", "value"));
                break;
            default: throw new ArgumentException("Unknown family.", nameof(family));
        }
    }

    private sealed class FullPendingServices : IAuthorizationProvider, IServiceBusMessageService,
        IDaprBindingService, IDaprPubSubService, ILlmContentSafetyEvaluator,
        ISemanticCacheEmbeddingProvider, IRateLimiter, ICache
    {
        internal TaskCompletionSource<bool> Started { get; } = WaitSignal<bool>();
        internal TaskCompletionSource<object> Release { get; } = WaitSignal<object>();
        internal CancellationToken Token { get; private set; }

        internal void Register(ServiceRegistry registry)
        {
            registry.Register<IAuthorizationProvider>(this).Register<IServiceBusMessageService>(this)
                .Register<IDaprBindingService>(this).Register<IDaprPubSubService>(this)
                .Register<ILlmContentSafetyEvaluator>(this).Register<ISemanticCacheEmbeddingProvider>(this)
                .Register<IRateLimiter>(this).Register<ICache>(this);
        }

        private async Task<T> Pending<T>(CancellationToken token)
        {
            Token = token;
            Started.SetResult(true);
            return (T)await Release.Task.ConfigureAwait(false);
        }
        public Task<Authorization> GetAuthorizationAsync(AuthorizationRequest request, CancellationToken cancellationToken = default) => Pending<Authorization>(cancellationToken);
        public Task SendAsync(ServiceBusMessageRequest request, CancellationToken cancellationToken = default) => Pending<object>(cancellationToken);
        public Task<IResponse> InvokeAsync(DaprBindingRequest request, CancellationToken cancellationToken = default) => Pending<IResponse>(cancellationToken);
        public Task<IResponse> PublishAsync(DaprPublishRequest request, CancellationToken cancellationToken = default) => Pending<IResponse>(cancellationToken);
        public Task<LlmContentSafetyEvaluationResult> EvaluateAsync(LlmContentSafetyEvaluationRequest request, CancellationToken cancellationToken = default) => Pending<LlmContentSafetyEvaluationResult>(cancellationToken);
        public Task<IReadOnlyList<double>> GenerateAsync(SemanticCacheEmbeddingRequest request, CancellationToken cancellationToken = default) => Pending<IReadOnlyList<double>>(cancellationToken);
        public Task<bool> TryConsumeAsync(string key, int permits = 1, CancellationToken cancellationToken = default) => Pending<bool>(cancellationToken);
        public Task<object?> GetAsync(string key, CancellationToken ct = default) => Pending<object?>(ct);
        public Task SetAsync(string key, object value, TimeSpan ttl, CancellationToken ct = default) => Pending<object>(ct);
        public Task RemoveAsync(string key, CancellationToken ct = default) => Pending<object>(ct);
        public Task<CacheValueResult> GetOrCreateAsync(string key, TimeSpan expiresAfter, TimeSpan? refreshAfter,
            Func<object?, CancellationToken, Task<object?>> valueFactory, CancellationToken ct = default) => Pending<CacheValueResult>(ct);
        public Task<CacheValueResult> GetOrCreateWithDynamicTtlAsync(string key,
            Func<object?, CancellationToken, Task<CacheValueFactoryResult>> valueFactory,
            bool forceRefresh = false, CancellationToken ct = default) => Pending<CacheValueResult>(ct);
    }
}

internal static class FullWaitCounterTestExtensions
{
    internal static bool HasPendingForFullWait(this PolicyCounterService service) =>
        (bool)typeof(PolicyCounterService).GetProperty("HasPendingResponse", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(service)!;
}