// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Document;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Data;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Services;

namespace Test.Emulator.Emulator.Policies;

[TestClass]
public class PolicyPipelineTests
{
    [TestMethod]
    [DataRow(false, false, false)]
    [DataRow(false, false, true)]
    [DataRow(false, true, false)]
    [DataRow(false, true, true)]
    [DataRow(true, false, false)]
    [DataRow(true, false, true)]
    [DataRow(true, true, false)]
    [DataRow(true, true, true)]
    public void CallbackOwnership_FlatAndNestedForwardReplacementsKeepExactlyTheCallbackHeaders(
        bool nested, bool replace, bool ignoreCase)
    {
        var pipeline = PolicyPipelineBuilder.Create()
            .ConfigureContext(context =>
            {
                context.Request.Headers["Origin"] = [FinalHeaderBoundaryTest.Origin];
                context.Response.Headers = new Dictionary<string, string[]>(
                    ignoreCase ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
            })
            .AddPolicy(PolicyScope.Global, new ExecutionTestDocument
            {
                InboundAction = section =>
                {
                    section.Cors(FinalHeaderBoundaryTest.Cors());
                    section.Base();
                },
                BackendAction = section => section.Base(),
                OutboundAction = section => section.Base()
            })
            .AddPolicy(PolicyScope.Operation, new ExecutionTestDocument
            {
                BackendAction = section => section.ForwardRequest(),
                OutboundAction = section => section.Base()
            }).Build();
        var setup = new TestDocument(new ExecutionTestDocument()) { Context = pipeline.Context };
        var callbacks = 0;
        setup.SetupBackend().ForwardRequest().WithCallback((context, _) =>
        {
            callbacks++;
            FinalHeaderBoundaryTest.SetCallbackOwnedResponse(context, replace, ignoreCase);
        });
        pipeline.RunRequest(request =>
        {
            request.RunRequest(inner => SettlementTest.RunAll(inner, nested));
            FinalHeaderBoundaryTest.AssertCallbackOwnedResponse(request.Context);
            request.Context.Services.Resolve<IHttpClient>().Should().BeNull();
        });

        callbacks.Should().Be(1);
        FinalHeaderBoundaryTest.AssertCallbackOwnedResponse(pipeline.Context);
        pipeline.Context.Services.Resolve<IHttpClient>().Should().BeNull();
    }

    [TestMethod]
    [DataRow(false, false, false)]
    [DataRow(false, false, true)]
    [DataRow(false, true, false)]
    [DataRow(false, true, true)]
    [DataRow(true, false, false)]
    [DataRow(true, false, true)]
    [DataRow(true, true, false)]
    [DataRow(true, true, true)]
    public void FinalCorsExposure_AllScopeOutboundAndOuterChangesAppearOnlyAtSuccessfulCompletion(
        bool nested, bool enclosingOwner, bool ignoreCase)
    {
        var pipeline = PolicyPipelineBuilder.Create()
            .ConfigureContext(context => FinalHeaderBoundaryTest.ConfigureContext(context, ignoreCase))
            .AddPolicy(PolicyScope.Global, new ExecutionTestDocument
            {
                InboundAction = section =>
                {
                    section.Cors(FinalHeaderBoundaryTest.Cors());
                    section.Base();
                },
                BackendAction = section => section.Base(),
                OutboundAction = section =>
                {
                    section.Base();
                    section.SetHeader("X-Global", ["final"]);
                    section.SetHeader("Set-Cookie2", ["private=2"]);
                }
            })
            .AddPolicy(PolicyScope.Operation, new ExecutionTestDocument
            {
                InboundAction = section => section.Base(),
                BackendAction = section => section.ForwardRequest(),
                OutboundAction = section =>
                {
                    section.SetHeader("X-Outbound", ["final"]);
                    section.RemoveHeader("X-Actual");
                    section.Base();
                }
            }).Build();
        var headers = pipeline.Context.Response.Headers;

        if (enclosingOwner) pipeline.RunRequest(request =>
        {
            request.RunRequest(inner => SettlementTest.RunAll(inner, nested));
            FinalHeaderBoundaryTest.Exposure(request.Context).Should().Contain("X-Actual")
                .And.NotContain("X-Outbound", "X-Global");
            request.Context.Response.Headers["X-Outer"] = ["last"];
        });
        else SettlementTest.RunAll(pipeline, nested);

        pipeline.Context.Response.Headers.Should().BeSameAs(headers);
        FinalHeaderBoundaryTest.AssertFinalExposure(pipeline.Context);
        FinalHeaderBoundaryTest.Exposure(pipeline.Context).Should().Contain("X-Outbound", "X-Global").And.NotContain("X-Actual");
        if (enclosingOwner) FinalHeaderBoundaryTest.Exposure(pipeline.Context).Should().Contain("X-Outer");
    }

    [TestMethod]
    [DataRow(false, false, false)]
    [DataRow(false, false, true)]
    [DataRow(false, true, false)]
    [DataRow(false, true, true)]
    [DataRow(true, false, false)]
    [DataRow(true, false, true)]
    [DataRow(true, true, false)]
    [DataRow(true, true, true)]
    public void FinalCorsExposure_OnErrorCompletionOnlyRecoveryRefreshesAfterAllTokenAndLimiterOutputs(
        bool nested, bool backend, bool azure)
    {
        var pipeline = PolicyPipelineBuilder.Create()
            .ConfigureContext(context =>
            {
                FinalHeaderBoundaryTest.ConfigureContext(context, ignoreCase: false, body: "{}");
                context.Services.Register(new TokenLimitCounterStore());
                context.Response.Headers["X-Initial"] = ["initial"];
                context.Response.Body.Content = "{}";
            })
            .AddPolicy(PolicyScope.Global, new ExecutionTestDocument
            {
                InboundAction = section => section.Base(),
                BackendAction = section => section.Base(),
                OnErrorAction = section =>
                {
                    section.Base();
                    section.SetHeader("X-Global-Recovery", ["final"]);
                }
            })
            .AddPolicy(PolicyScope.Operation, new ExecutionTestDocument
            {
                InboundAction = section => FinalHeaderBoundaryTest.Admit(section, azure),
                BackendAction = section => section.ForwardRequest(),
                OnErrorAction = section =>
                {
                    section.SetBody(FinalHeaderBoundaryTest.Usage);
                    section.SetHeader("X-Recovered", ["final"]);
                    section.RemoveHeader("X-Actual");
                    section.RemoveHeader("X-Deferred");
                    section.Base();
                }
            }).Build();
        var context = pipeline.Context;
        var requestId = context.RequestId;
        var tokens = context.Services.Resolve<TokenLimitCounterStore>()!;

        Assert.ThrowsExactly<PolicyException>(() => pipeline.RunRequest(request =>
        {
            request.RunRequest(inner =>
            {
                ExecutionTest.RunSection(inner, "inbound", nested);
                if (backend) ExecutionTest.RunSection(inner, "backend", nested);
            });
        }));
        var checkpoint = FinalHeaderBoundaryTest.Exposure(context);
        Assert.ThrowsExactly<PolicyException>(() => pipeline.RunRequest(_ => { }));
        FinalHeaderBoundaryTest.Exposure(context).Should().Equal(checkpoint);
        tokens.GetRateTokens("final-headers", context.Services.Resolve<TimeProvider>()!.GetUtcNow()).Should().Be(0);
        pipeline.RunRequest(request =>
        {
            request.RunRequest(inner => ExecutionTest.RunSection(inner, "on-error", nested));
            FinalHeaderBoundaryTest.Exposure(context).Should().Equal(checkpoint);
            context.Response.Headers.Should().NotContainKey("X-Token-Consumed");
        });

        context.RequestId.Should().Be(requestId);
        context.Response.Headers["X-Token-Consumed"].Should().Equal("3");
        context.Response.Headers["X-Deferred"].Should().Equal("8");
        FinalHeaderBoundaryTest.AssertFinalExposure(context);
        FinalHeaderBoundaryTest.Exposure(context).Should().Contain(
            "X-Token-Consumed", "X-Deferred", "X-Recovered", "X-Global-Recovery");
        tokens.GetRateTokens("final-headers", context.Services.Resolve<TimeProvider>()!.GetUtcNow()).Should().Be(3);
        pipeline.RunRequest(_ => { });
        tokens.GetRateTokens("final-headers", context.Services.Resolve<TimeProvider>()!.GetUtcNow()).Should().Be(3);
    }

    [TestMethod]
    public void FinalCorsExposure_IndependentSectionsRefreshOnlyAtAnExplicitOuterBoundary()
    {
        var pipeline = PolicyPipelineBuilder.Create()
            .ConfigureContext(context => FinalHeaderBoundaryTest.ConfigureContext(context))
            .AddPolicy(PolicyScope.Global, new ExecutionTestDocument
            {
                InboundAction = section => section.Cors(FinalHeaderBoundaryTest.Cors()),
                OutboundAction = section => section.SetHeader("X-Global", ["final"])
            })
            .AddPolicy(PolicyScope.Operation, new ExecutionTestDocument
            {
                BackendAction = section => section.ForwardRequest(),
                OutboundAction = section =>
                {
                    section.SetHeader("X-Outbound", ["final"]);
                    section.RemoveHeader("X-Actual");
                }
            }).Build();

        pipeline.RunInboundIndependent();
        pipeline.RunBackendIndependent();
        pipeline.RunOutboundIndependent();

        FinalHeaderBoundaryTest.Exposure(pipeline.Context).Should().Contain("X-Actual").And.NotContain("X-Outbound");
        pipeline.RunRequest(_ => { });
        FinalHeaderBoundaryTest.AssertFinalExposure(pipeline.Context);
    }

    [TestMethod]
    [DataRow(false, false)]
    [DataRow(false, true)]
    [DataRow(true, false)]
    [DataRow(true, true)]
    public void FinalCorsExposure_TerminalCallbackResponseKeepsItsOwnHeadersAcrossAllFrames(bool nested, bool seeded)
    {
        var pipeline = PolicyPipelineBuilder.Create()
            .ConfigureContext(context =>
            {
                FinalHeaderBoundaryTest.ConfigureContext(context);
                if (seeded) context.Response.Headers["X-Initial"] = ["initial"];
            })
            .AddPolicy(PolicyScope.Global, new ExecutionTestDocument
            {
                InboundAction = section =>
                {
                    section.Cors(FinalHeaderBoundaryTest.Cors());
                    section.Base();
                },
                BackendAction = section => section.Base()
            })
            .AddPolicy(PolicyScope.Operation, new ExecutionTestDocument
            {
                BackendAction = section => section.ReturnResponse(new ReturnResponseConfig()),
                OutboundAction = _ => Assert.Fail("Terminal callback must skip outbound.")
            }).Build();
        var setup = new TestDocument(new ExecutionTestDocument()) { Context = pipeline.Context };
        setup.SetupBackend().ReturnResponse().WithCallback((context, _) =>
        {
            context.Response.Headers.Clear();
            context.Response.Headers["X-Owned"] = ["terminal"];
            context.Response.StatusCode = 202;
        });

        SettlementTest.RunAll(pipeline, nested);

        pipeline.Context.Response.Headers.Should().ContainSingle().Which.Key.Should().Be("X-Owned");
        pipeline.Context.Response.StatusCode.Should().Be(202);
        pipeline.Context.ResponseTerminated.Should().BeTrue();
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void FinalCorsExposure_GenericSettlementFailureRefreshesOnlyAfterTheFinalCompletionRetry(bool nested)
    {
        var fail = true;
        var expected = new InvalidOperationException("limiter completion failed");
        var pipeline = PolicyPipelineBuilder.Create()
            .ConfigureContext(context =>
            {
                FinalHeaderBoundaryTest.ConfigureContext(context, body: FinalHeaderBoundaryTest.Usage);
                context.Services.Register(new TokenLimitCounterStore());
                context.Services.Register<IRateLimiter>(new RecordingRateLimiter((_, permits) =>
                {
                    if (fail && permits > 0) throw expected;
                    return true;
                }));
            })
            .AddPolicy(PolicyScope.Operation, new ExecutionTestDocument
            {
                InboundAction = section => FinalHeaderBoundaryTest.Admit(section),
                BackendAction = section => section.ForwardRequest(),
                OutboundAction = section =>
                {
                    section.SetHeader("X-Outbound", ["final"]);
                    section.RemoveHeader("X-Actual");
                    section.RemoveHeader("X-Deferred");
                }
            }).Build();

        Assert.ThrowsExactly<InvalidOperationException>(() => SettlementTest.RunAll(pipeline, nested)).Should().BeSameAs(expected);
        FinalHeaderBoundaryTest.Exposure(pipeline.Context).Should().Contain("X-Actual").And.NotContain("X-Outbound");
        pipeline.Context.Response.Headers["X-Recovered"] = ["completion-only"];
        fail = false;
        pipeline.RunRequest(_ => { });

        FinalHeaderBoundaryTest.AssertFinalExposure(pipeline.Context);
        FinalHeaderBoundaryTest.Exposure(pipeline.Context).Should().Contain("X-Outbound", "X-Deferred", "X-Recovered");
        pipeline.Context.Services.Resolve<RateLimitStore>()!.GetCallCount("rate-limit-by-key:final-headers").Should().Be(2);
    }

    [TestMethod]
    [DataRow(false, false)]
    [DataRow(false, true)]
    [DataRow(true, false)]
    [DataRow(true, true)]
    public async Task CacheRefresh_AsyncValueBlocksPreservePipelineTerminationAndFinalSettlement(
        bool nested, bool terminal)
    {
        var calls = new RateLimitStore();
        var tokens = new TokenLimitCounterStore();
        var cache = CacheRefreshTestCache.Asynchronous(new CacheStore());
        var visited = new List<string>();
        var pipeline = PolicyPipelineBuilder.Create()
            .ConfigureContext(context =>
            {
                SettlementTest.ConfigureContext(context, calls);
                context.Services.Register(tokens).Register<ICache>(cache);
            })
            .AddPolicy(PolicyScope.Global, new ExecutionTestDocument
            {
                InboundAction = section =>
                {
                    section.Base();
                    visited.Add("global-after");
                },
                BackendAction = section => section.Base(),
                OutboundAction = section =>
                {
                    section.Base();
                    section.SetBody("final");
                }
            })
            .AddPolicy(PolicyScope.Operation, new ExecutionTestDocument
            {
                InboundAction = section =>
                {
                    section.LlmTokenLimit(new TokenLimitConfig
                    {
                        CounterKey = "cache-pipeline",
                        EstimatePromptToken = false,
                        TokensPerMinute = 10,
                        TokensConsumedVariableName = "consumed",
                        TokensConsumedHeaderName = "X-Tokens"
                    });
                    section.CacheValue(new CacheValueConfig { Key = "key", VariableName = "result" }, () =>
                    {
                        section.RateLimitByKey(SettlementTest.DeferredRate());
                        SettlementTest.ApplyQuota(section);
                        if (terminal)
                        {
                            section.ReturnResponse(new ReturnResponseConfig
                            {
                                Status = new StatusConfig { Code = 202, Reason = "Accepted" },
                                Body = new BodyConfig { Content = "returned" }
                            });
                        }
                        section.SetVariable("result", "computed");
                    });
                    visited.Add("operation-after");
                },
                BackendAction = section =>
                {
                    visited.Add("backend");
                    calls.GetCallCount("rate-limit-by-key:settlement").Should().Be(0);
                    section.ForwardRequest();
                },
                OutboundAction = section =>
                {
                    visited.Add("outbound");
                    calls.GetCallCount("rate-limit-by-key:settlement").Should().Be(0);
                    section.Base();
                }
            }).Build();
        var setup = new TestDocument(new ExecutionTestDocument()) { Context = pipeline.Context };
        setup.SetupBackend().ForwardRequest().WithCallback((context, _) =>
            context.Response.Body.Content = """{"usage":{"prompt_tokens":4,"completion_tokens":2}}""");
        var requestId = pipeline.Context.RequestId;

        await CacheRefreshTestCache.OnOwnerThread(() => pipeline.RunRequest(request =>
        {
            SettlementTest.RunAll(request, nested);
            calls.GetCallCount("rate-limit-by-key:settlement").Should().Be(0);
        })).WaitAsync(TimeSpan.FromSeconds(5));

        pipeline.Context.RequestId.Should().Be(requestId);
        calls.GetCallCount("rate-limit-by-key:settlement").Should().Be(2);
        calls.GetBandwidth("quota-by-key:volume").Should().Be(terminal ? 8 : 5);
        pipeline.Context.Response.Body.Content.Should().Be(terminal ? "returned" : "final");
        pipeline.Context.ResponseTerminated.Should().Be(terminal);
        tokens.GetRateTokens("cache-pipeline", pipeline.Context.Services.Resolve<TimeProvider>()!.GetUtcNow())
            .Should().Be(terminal ? 0 : 6);
        if (terminal)
        {
            pipeline.Context.Response.StatusCode.Should().Be(202);
            pipeline.Context.Variables.Should().NotContainKey("result");
            if (nested) visited.Should().BeEmpty();
            else visited.Should().Equal("global-after");
        }
        else
        {
            pipeline.Context.Variables["result"].Should().Be("computed");
            pipeline.Context.Response.Headers["X-Tokens"].Should().Equal("6");
        }
        pipeline.RunRequest(_ => { });
        calls.GetCallCount("rate-limit-by-key:settlement").Should().Be(2);
    }

    [TestMethod]
    public async Task CacheRefresh_RetainedFactoryCannotMutateAfterRequestCompletion()
    {
        Func<object?, CancellationToken, Task<CacheValueFactoryResult>>? retained = null;
        var calls = new RateLimitStore();
        var cache = new CacheRefreshTestCache(new CacheStore())
        {
            DynamicGet = (_, factory, _, _) =>
            {
                retained = factory;
                return Task.FromResult(new CacheValueResult("cached", false, false));
            }
        };
        var pipeline = PolicyPipelineBuilder.Create()
            .ConfigureContext(context =>
            {
                SettlementTest.ConfigureContext(context, calls);
                context.Services.Register<ICache>(cache);
            })
            .AddPolicy(PolicyScope.Operation, new ExecutionTestDocument
            {
                InboundAction = section =>
                {
                    section.RateLimitByKey(SettlementTest.DeferredRate());
                    section.CacheValue(new CacheValueConfig { Key = "key", VariableName = "result" },
                        () => section.SetVariable("result", "late"));
                }
            }).Build();
        await CacheRefreshTestCache.OnOwnerThread(pipeline.RunAll).WaitAsync(TimeSpan.FromSeconds(5));
        pipeline.Context.Variables["result"].Should().Be("cached");

        Func<Task> late = async () => await retained!(null, CancellationToken.None);
        await late.Should().ThrowAsync<InvalidOperationException>().WithMessage("*completed*");

        pipeline.Context.Variables["result"].Should().Be("cached");
        calls.GetCallCount("rate-limit-by-key:settlement").Should().Be(2);
        pipeline.RunRequest(_ => { });
    }

    [TestMethod]
    [DataRow(false, false)]
    [DataRow(false, true)]
    [DataRow(true, false)]
    [DataRow(true, true)]
    public void TokenCorrection_BackendSnapshotSurvivesAllScopeFramesAndFinalResponseRewrites(bool nested, bool azure)
    {
        var calls = new RateLimitStore();
        var tokens = new TokenLimitCounterStore();
        var config = new TokenLimitConfig
        {
            CounterKey = "pipeline",
            EstimatePromptToken = false,
            TokensPerMinute = 10,
            TokensConsumedVariableName = "consumed",
            TokensConsumedHeaderName = "X-Tokens"
        };
        var final = "{\"answer\":\"caf\u00e9\"}";
        var pipeline = PolicyPipelineBuilder.Create()
            .ConfigureContext(context =>
            {
                SettlementTest.ConfigureContext(context, calls);
                context.Services.Register(tokens);
            })
            .AddPolicy(PolicyScope.Global, new ExecutionTestDocument
            {
                InboundAction = section => section.Base(),
                BackendAction = section => section.Base(),
                OutboundAction = section =>
                {
                    section.Base();
                    section.ExpressionContext.Variables["consumed"].Should().Be(6L);
                    section.SetBody(final);
                }
            })
            .AddPolicy(PolicyScope.Operation, new ExecutionTestDocument
            {
                InboundAction = section =>
                {
                    if (azure) section.AzureOpenAiTokenLimit(config);
                    else section.LlmTokenLimit(config);
                    section.RateLimitByKey(SettlementTest.DeferredRate());
                    SettlementTest.ApplyQuota(section);
                },
                BackendAction = section =>
                    ((GatewayContext)section.ExpressionContext).Response.Body.Content =
                        """{"usage":{"prompt_tokens":4,"completion_tokens":2}}""",
                OutboundAction = section =>
                {
                    section.ExpressionContext.Variables["consumed"].Should().Be(6L);
                    section.SetHeader("X-Copied", [section.ExpressionContext.Variables["consumed"].ToString()!]);
                    section.Base();
                }
            }).Build();
        var context = pipeline.Context;
        var clock = context.Services.Resolve<TimeProvider>()!;
        var requestId = context.RequestId;

        pipeline.RunRequest(request =>
        {
            SettlementTest.RunAll(request, nested);
            request.RunRequest(_ => { });
            tokens.GetRateTokens("pipeline", clock.GetUtcNow()).Should().Be(0);
            calls.GetCallCount("rate-limit-by-key:settlement").Should().Be(0);
        });

        context.Response.Body.Content.Should().Be(final);
        context.Response.Headers["X-Copied"].Should().Equal("6");
        context.Response.Headers["X-Tokens"].Should().Equal("6");
        context.RequestId.Should().Be(requestId);
        tokens.GetRateTokens("pipeline", clock.GetUtcNow()).Should().Be(6);
        calls.GetCallCount("rate-limit-by-key:settlement").Should().Be(2);
        calls.GetBandwidth("quota-by-key:volume").Should().Be(System.Text.Encoding.UTF8.GetByteCount(final));
        Assert.ThrowsExactly<InvalidOperationException>(() => ExecutionTest.RunSection(pipeline, "backend", nested));
        pipeline.RunRequest(_ => { });
        tokens.GetRateTokens("pipeline", clock.GetUtcNow()).Should().Be(6);
    }

    [TestMethod]
    [DataRow(false, false)]
    [DataRow(false, true)]
    [DataRow(true, false)]
    [DataRow(true, true)]
    public void TokenCorrection_InboundTerminalWithoutBackendDoesNotRequireUsage(bool nested, bool azure)
    {
        var tokens = new TokenLimitCounterStore();
        var calls = new RateLimitStore();
        var pipeline = PolicyPipelineBuilder.Create()
            .ConfigureContext(context =>
            {
                SettlementTest.ConfigureContext(context, calls);
                context.Services.Register(tokens);
            })
            .AddPolicy(PolicyScope.Global, new ExecutionTestDocument { InboundAction = section => section.Base() })
            .AddPolicy(PolicyScope.Operation, new ExecutionTestDocument
            {
                InboundAction = section =>
                {
                    var config = new TokenLimitConfig
                    {
                        CounterKey = "pipeline",
                        EstimatePromptToken = false,
                        TokensPerMinute = 10,
                        TokensConsumedVariableName = "consumed",
                        TokensConsumedHeaderName = "X-Tokens"
                    };
                    if (azure) section.AzureOpenAiTokenLimit(config);
                    else section.LlmTokenLimit(config);
                    section.RateLimitByKey(SettlementTest.DeferredRate());
                    section.ReturnResponse(new ReturnResponseConfig
                    {
                        Status = new StatusConfig { Code = 204, Reason = "No Content" }
                    });
                },
                BackendAction = _ => Assert.Fail("Backend must be skipped.")
            }).Build();

        SettlementTest.RunAll(pipeline, nested);

        pipeline.Context.Response.StatusCode.Should().Be(204);
        pipeline.Context.Response.Headers.Should().NotContainKey("X-Tokens");
        pipeline.Context.Variables["consumed"].Should().Be(0L);
        tokens.GetRateTokens("pipeline", pipeline.Context.Services.Resolve<TimeProvider>()!.GetUtcNow()).Should().Be(0);
        calls.GetCallCount("rate-limit-by-key:settlement").Should().Be(2);
    }

    [TestMethod]
    [DataRow("inbound", "Global,Workspace,Product,Api,Operation")]
    [DataRow("backend", "Global,Workspace,Product,Api,Operation")]
    [DataRow("outbound", "Operation,Api,Product,Workspace,Global")]
    [DataRow("on-error", "Operation,Api,Product,Workspace,Global")]
    public void PolicyPipeline_ExecutesAllScopesInTheDocumentedSectionOrder(string section, string order)
    {
        var calls = new List<string>();
        var pipeline = CreatePipeline(calls);

        ExecutionTest.RunSection(pipeline, section);

        calls.Should().Equal(order.Split(',').SelectMany(scope => new[]
        {
            $"{scope}:{section}:before", $"{scope}:{section}:after"
        }));
    }

    [TestMethod]
    [DataRow("inbound", false, "Global,Workspace,Product")]
    [DataRow("backend", false, "Global,Workspace,Product")]
    [DataRow("outbound", false, "Operation,Api,Product")]
    [DataRow("on-error", false, "Operation,Api,Product")]
    [DataRow("inbound", true, "Global,Workspace,Product")]
    [DataRow("backend", true, "Global,Workspace,Product")]
    [DataRow("outbound", true, "Operation,Api,Product")]
    [DataRow("on-error", true, "Operation,Api,Product")]
    public void PolicyPipeline_ReturnResponseStopsRemainingScopesAndUnwindsNestedBase(
        string section, bool nested, string visited)
    {
        var calls = new List<string>();
        var pipeline = CreatePipeline(calls, nested, section);

        ExecutionTest.RunSection(pipeline, section, nested);

        var scopes = visited.Split(',');
        var expected = nested
            ? scopes.Select(scope => $"{scope}:{section}:before")
            : scopes.SelectMany(scope => scope == "Product"
                ? new[] { $"{scope}:{section}:before" }
                : new[] { $"{scope}:{section}:before", $"{scope}:{section}:after" });
        calls.Should().Equal(expected);
        pipeline.Context.Response.StatusCode.Should().Be(202);
        pipeline.Context.Response.Body.Content.Should().Be("returned");
        pipeline.Context.ResponseTerminated.Should().BeTrue();
    }

    [TestMethod]
    [DataRow("inbound", false)]
    [DataRow("backend", false)]
    [DataRow("outbound", false)]
    [DataRow("on-error", false)]
    [DataRow("inbound", true)]
    [DataRow("backend", true)]
    [DataRow("outbound", true)]
    [DataRow("on-error", true)]
    public void PolicyPipeline_TerminationPreventsEveryLaterCoordinatedSection(string section, bool nested)
    {
        var calls = new List<string>();
        var pipeline = PolicyPipelineBuilder.Create()
            .AddPolicy(PolicyScope.Operation,
                ExecutionTest.FlowDocument("Operation", calls, nested, section))
            .Build();

        ExecutionTest.RunSection(pipeline, section, nested);
        pipeline.RunAll();
        pipeline.RunAllNested();
        pipeline.RunOnError();
        pipeline.RunOnErrorNested();

        calls.Should().Equal($"Operation:{section}:before");
        pipeline.Context.ResponseTerminated.Should().BeTrue();
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void PolicyPipeline_MockResponseStopsSubsequentScopesAndSections(bool nested)
    {
        var calls = new List<string>();
        var pipeline = PolicyPipelineBuilder.Create()
            .AddPolicy(PolicyScope.Global, new ExecutionTestDocument
            {
                InboundAction = context => context.MockResponse(new MockResponseConfig { StatusCode = 202 })
            })
            .AddPolicy(PolicyScope.Operation, ExecutionTest.FlowDocument("Operation", calls, nested))
            .Build();

        if (nested)
        {
            pipeline.RunAllNested();
        }
        else
        {
            pipeline.RunAll();
        }
        pipeline.RunOnError();

        calls.Should().BeEmpty();
        pipeline.Context.Response.StatusCode.Should().Be(202);
        pipeline.Context.ResponseTerminated.Should().BeTrue();
    }

    [TestMethod]
    [DataRow("inbound", false)]
    [DataRow("backend", false)]
    [DataRow("outbound", false)]
    [DataRow("on-error", false)]
    [DataRow("inbound", true)]
    [DataRow("backend", true)]
    [DataRow("outbound", true)]
    [DataRow("on-error", true)]
    public void PolicyPipeline_DirectTerminationSignalsAreNotSilentlySectionOnly(string section, bool nested)
    {
        var calls = new List<string>();
        var first = section is "inbound" or "backend" ? PolicyScope.Global : PolicyScope.Operation;
        var last = section is "inbound" or "backend" ? PolicyScope.Operation : PolicyScope.Global;
        var pipeline = PolicyPipelineBuilder.Create()
            .AddPolicy(first, new ExecutionTestDocument
            {
                InboundAction = _ => throw new FinishSectionProcessingException(),
                BackendAction = _ => throw new FinishSectionProcessingException(),
                OutboundAction = _ => throw new FinishSectionProcessingException(),
                OnErrorAction = _ => throw new FinishSectionProcessingException()
            })
            .AddPolicy(last, ExecutionTest.FlowDocument("later", calls, nested))
            .Build();

        ExecutionTest.RunSection(pipeline, section, nested);
        pipeline.RunAll();
        pipeline.RunAllNested();
        pipeline.RunOnError();

        calls.Should().BeEmpty();
        pipeline.Context.ResponseTerminated.Should().BeTrue();
    }

    [TestMethod]
    [DataRow("inbound", "Global,Workspace,Product,Api,Operation")]
    [DataRow("backend", "Global,Workspace,Product,Api,Operation")]
    [DataRow("outbound", "Operation,Api,Product,Workspace,Global")]
    [DataRow("on-error", "Operation,Api,Product,Workspace,Global")]
    public void PolicyPipeline_InvokeRequestStopsOnlyItsCurrentScopeSection(string section, string visited)
    {
        var calls = new List<string>();
        var pipeline = CreatePipeline(calls, false, section, true);
        SetupInvokeRequest(pipeline.Context);

        ExecutionTest.RunSection(pipeline, section);

        calls.Should().Equal(visited.Split(',').SelectMany(scope => scope == "Product"
            ? new[] { $"{scope}:{section}:before" }
            : new[] { $"{scope}:{section}:before", $"{scope}:{section}:after" }));
        pipeline.Context.Response.StatusCode.Should().Be(203);
        pipeline.Context.ResponseTerminated.Should().BeFalse();

        calls.Clear();
        pipeline.RunAll();
        pipeline.RunOnError();
        calls.Should().Contain(["Operation:backend:before", "Global:outbound:after", "Global:on-error:after"]);
        pipeline.Context.ResponseTerminated.Should().BeFalse();
    }

    [TestMethod]
    [DataRow("inbound")]
    [DataRow("backend")]
    [DataRow("outbound")]
    [DataRow("on-error")]
    public void PolicyPipeline_NestedInvokeRequestDoesNotTerminateCallerScopesOrLaterSections(string section)
    {
        var calls = new List<string>();
        var pipeline = CreatePipeline(calls, true, section, true);
        SetupInvokeRequest(pipeline.Context);

        ExecutionTest.RunSection(pipeline, section, true);

        var first = section is "inbound" or "backend" ? "Global" : "Operation";
        var second = section is "inbound" or "backend" ? "Workspace" : "Api";
        calls.Should().Equal(
            $"{first}:{section}:before", $"{second}:{section}:before", $"Product:{section}:before",
            $"{second}:{section}:after", $"{first}:{section}:after");
        pipeline.Context.ResponseTerminated.Should().BeFalse();

        calls.Clear();
        pipeline.RunAllNested();
        pipeline.RunOnErrorNested();
        calls.Should().Contain(["Global:backend:before", "Operation:outbound:before", "Operation:on-error:before"]);
        pipeline.Context.ResponseTerminated.Should().BeFalse();
    }

    [TestMethod]
    [DataRow("inbound")]
    [DataRow("backend")]
    [DataRow("outbound")]
    public void PolicyPipeline_IndependentExecutionIgnoresTerminationFromEarlierScopesAndSections(string section)
    {
        var calls = new List<string>();
        var pipeline = CreatePipeline(calls, false, section);

        ExecutionTest.RunIndependent(pipeline, section);

        calls.Should().Contain([$"Global:{section}:before", $"Operation:{section}:before"]);
        calls.Should().NotContain($"Product:{section}:after");
        pipeline.Context.ResponseTerminated.Should().BeTrue();

        calls.Clear();
        pipeline.RunInboundIndependent();
        pipeline.RunBackendIndependent();
        pipeline.RunOutboundIndependent();
        calls.Should().Contain(["Global:inbound:before", "Operation:backend:before", "Global:outbound:before"]);
    }

    [TestMethod]
    public void PolicyPipeline_IndependentInboundReportsIsolatedFailuresAndStillRunsLaterScopes()
    {
        var calls = new List<string>();
        var traces = new List<string>();
        var pipeline = PolicyPipelineBuilder.Create()
            .ConfigureContext(context => context.Trace = traces.Add)
            .AddPolicy(PolicyScope.Global, new ExecutionTestDocument
            {
                InboundAction = _ => throw new InvalidOperationException("isolated failure")
            })
            .AddPolicy(PolicyScope.Operation, ExecutionTest.FlowDocument("Operation", calls))
            .Build();

        pipeline.RunInboundIndependent();

        calls.Should().Equal("Operation:inbound:before", "Operation:inbound:after");
        traces.Should().ContainSingle().Which.Should().Contain("Global").And.Contain("isolated failure");
        pipeline.Context.ResponseTerminated.Should().BeFalse();
    }

    [TestMethod]
    [DataRow("inbound", false)]
    [DataRow("backend", false)]
    [DataRow("outbound", false)]
    [DataRow("on-error", false)]
    [DataRow("inbound", true)]
    [DataRow("backend", true)]
    [DataRow("outbound", true)]
    [DataRow("on-error", true)]
    public void PolicyPipeline_CoordinatedExecutionDoesNotSwallowDocumentFailures(string section, bool nested)
    {
        var pipeline = PolicyPipelineBuilder.Create()
            .AddPolicy(PolicyScope.Operation, new ExecutionTestDocument
            {
                InboundAction = _ => throw new InvalidOperationException("document failure"),
                BackendAction = _ => throw new InvalidOperationException("document failure"),
                OutboundAction = _ => throw new InvalidOperationException("document failure"),
                OnErrorAction = _ => throw new InvalidOperationException("document failure")
            })
            .Build();

        var error = Assert.ThrowsExactly<InvalidOperationException>(
            () => ExecutionTest.RunSection(pipeline, section, nested));

        error.Message.Should().Be("document failure");
        pipeline.Context.ResponseTerminated.Should().BeFalse();
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void PolicyPipeline_SettlesOnlyAfterAllScopesAndFinalOutboundBody(bool nested)
    {
        var store = new RateLimitStore();
        var requestIds = new List<Guid>();
        var builder = PolicyPipelineBuilder.Create()
            .ConfigureContext(context => SettlementTest.ConfigureContext(context, store));
        foreach (var scope in new[] { PolicyScope.Global, PolicyScope.Operation })
        {
            var name = scope.ToString();
            builder.AddPolicy(scope, new ExecutionTestDocument
            {
                InboundAction = section =>
                {
                    requestIds.Add(section.ExpressionContext.RequestId);
                    section.RateLimitByKey(SettlementTest.DeferredRate(name));
                    SettlementTest.ApplyQuota(section);
                    section.Base();
                },
                BackendAction = section =>
                {
                    requestIds.Add(section.ExpressionContext.RequestId);
                    store.GetCallCount("rate-limit-by-key:Global").Should().Be(0);
                    store.GetCallCount("rate-limit-by-key:Operation").Should().Be(0);
                    section.Base();
                },
                OutboundAction = section =>
                {
                    requestIds.Add(section.ExpressionContext.RequestId);
                    store.GetCallCount("rate-limit-by-key:Global").Should().Be(0);
                    store.GetCallCount("rate-limit-by-key:Operation").Should().Be(0);
                    if (scope == PolicyScope.Operation)
                    {
                        section.SetBody(new string('i', 4096));
                    }
                    section.Base();
                    if (scope == PolicyScope.Global)
                    {
                        section.SetBody(new string('\u00e9', 128));
                    }
                }
            });
        }
        var pipeline = builder.Build();
        var requestId = pipeline.Context.RequestId;
        pipeline.Context.Request.Body.Content = new string('r', 32);

        SettlementTest.RunAll(pipeline, nested);

        requestIds.Should().HaveCount(6).And.OnlyContain(id => id == requestId);
        store.GetCallCount("rate-limit-by-key:Global").Should().Be(2);
        store.GetCallCount("rate-limit-by-key:Operation").Should().Be(2);
        store.GetBandwidth("quota-by-key:volume").Should().Be(288);
        pipeline.Context.Response.Body.Content.Should().Be(new string('\u00e9', 128));
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void PolicyPipeline_EarlyReturnCompletesAfterNestedUnwindingAndOnlyOnce(bool nested)
    {
        var store = new RateLimitStore();
        var calls = new List<string>();
        var pipeline = PolicyPipelineBuilder.Create()
            .ConfigureContext(context => SettlementTest.ConfigureContext(context, store))
            .AddPolicy(PolicyScope.Global, new ExecutionTestDocument
            {
                InboundAction = section =>
                {
                    section.RateLimitByKey(SettlementTest.DeferredRate());
                    SettlementTest.ApplyQuota(section);
                    section.Base();
                    calls.Add("outer after");
                }
            })
            .AddPolicy(PolicyScope.Operation, new ExecutionTestDocument
            {
                InboundAction = section =>
                {
                    store.GetCallCount("rate-limit-by-key:settlement").Should().Be(0);
                    section.ReturnResponse(new ReturnResponseConfig
                    {
                        Status = new StatusConfig { Code = 202, Reason = "Accepted" },
                        Body = new BodyConfig { Content = "terminal" }
                    });
                },
                BackendAction = _ => Assert.Fail("Backend must not execute.")
            })
            .Build();
        var setup = new TestDocument(new ExecutionTestDocument()) { Context = pipeline.Context };
        var callbacks = 0;
        setup.SetupInbound().Base().WithCallback(_ => callbacks++);

        SettlementTest.RunAll(pipeline, nested);
        SettlementTest.RunAll(pipeline, nested);
        pipeline.RunRequest(_ => { });
        pipeline.Context.CompleteLimiterResponse();

        callbacks.Should().Be(nested ? 0 : 1);
        calls.Should().Equal(nested ? [] : ["outer after"]);
        store.GetCallCount("rate-limit-by-key:settlement").Should().Be(2);
        store.GetBandwidth("quota-by-key:volume").Should().Be(8);
        pipeline.Context.Response.StatusCode.Should().Be(202);
        pipeline.Context.Response.Body.Content.Should().Be("terminal");
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void PolicyPipeline_InvokeRequestDoesNotCompleteBeforeLaterSections(bool nested)
    {
        var store = new RateLimitStore();
        var sections = new List<string>();
        var pipeline = PolicyPipelineBuilder.Create()
            .ConfigureContext(context => SettlementTest.ConfigureContext(context, store))
            .AddPolicy(PolicyScope.Operation, new ExecutionTestDocument
            {
                InboundAction = section =>
                {
                    section.RateLimitByKey(SettlementTest.DeferredRate());
                    section.InvokeRequest(new InvokeRequestConfig());
                    section.SetVariable("after-invoke", true);
                },
                BackendAction = _ =>
                {
                    store.GetCallCount("rate-limit-by-key:settlement").Should().Be(0);
                    sections.Add("backend");
                },
                OutboundAction = section =>
                {
                    store.GetCallCount("rate-limit-by-key:settlement").Should().Be(0);
                    sections.Add("outbound");
                    section.SetBody("final");
                }
            }).Build();
        SetupInvokeRequest(pipeline.Context);

        SettlementTest.RunAll(pipeline, nested);

        sections.Should().Equal("backend", "outbound");
        store.GetCallCount("rate-limit-by-key:settlement").Should().Be(2);
        pipeline.Context.Variables.Should().NotContainKey("after-invoke");
        pipeline.Context.ResponseTerminated.Should().BeFalse();
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void PolicyPipeline_OuterOwnerIncludesOnErrorAfterInnerExecutionFailure(bool nested)
    {
        var store = new RateLimitStore();
        var pipeline = PolicyPipelineBuilder.Create()
            .ConfigureContext(context => SettlementTest.ConfigureContext(context, store))
            .AddPolicy(PolicyScope.Global, new ExecutionTestDocument
            {
                InboundAction = section =>
                {
                    section.RateLimitByKey(SettlementTest.DeferredRate());
                    SettlementTest.ApplyQuota(section);
                    section.Base();
                },
                BackendAction = section => section.Base(),
                OnErrorAction = section =>
                {
                    section.Base();
                    store.GetCallCount("rate-limit-by-key:settlement").Should().Be(0);
                    section.SetBody(new string('\u00e9', 100));
                }
            })
            .AddPolicy(PolicyScope.Operation, new ExecutionTestDocument
            {
                BackendAction = section => section.SetVariable("fail", true),
                OnErrorAction = section =>
                {
                    section.SetBody(new string('i', 4096));
                    section.Base();
                }
            })
            .Build();
        var setup = new TestDocument(new ExecutionTestDocument()) { Context = pipeline.Context };
        setup.SetupBackend().SetVariable((_, name, _) => name == "fail")
            .WithCallback((_, _, _) => throw new InvalidOperationException("backend failure"));

        pipeline.RunRequest(request =>
        {
            var error = Assert.ThrowsExactly<PolicyException>(() => SettlementTest.RunAll(request, nested));
            error.Policy.Should().Be(nameof(IBackendContext.SetVariable));
            store.GetCallCount("rate-limit-by-key:settlement").Should().Be(0);
            request.RunRequest(inner => ExecutionTest.RunSection(inner, "on-error", nested));
            store.GetBandwidth("quota-by-key:volume").Should().Be(0);
        });

        store.GetCallCount("rate-limit-by-key:settlement").Should().Be(2);
        store.GetBandwidth("quota-by-key:volume").Should().Be(200);
        pipeline.Context.Response.Body.Content.Should().Be(new string('\u00e9', 100));
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void PolicyPipeline_EnclosingOwnerCanPostprocessAfterInnerRunAll(bool nested)
    {
        var store = new RateLimitStore();
        var pipeline = PolicyPipelineBuilder.Create()
            .ConfigureContext(context => SettlementTest.ConfigureContext(context, store))
            .AddPolicy(PolicyScope.Operation, new ExecutionTestDocument
            {
                InboundAction = section =>
                {
                    section.RateLimitByKey(SettlementTest.DeferredRate());
                    SettlementTest.ApplyQuota(section);
                },
                OutboundAction = section => section.SetBody(new string('i', 4096))
            }).Build();
        var document = new TestDocument(new ExecutionTestDocument()) { Context = pipeline.Context };

        document.RunRequest(_ =>
        {
            SettlementTest.RunAll(pipeline, nested);
            store.GetCallCount("rate-limit-by-key:settlement").Should().Be(0);
            store.GetBandwidth("quota-by-key:volume").Should().Be(0);
            pipeline.Context.Response.Body.Content = "postprocessed";
        });

        store.GetCallCount("rate-limit-by-key:settlement").Should().Be(2);
        store.GetBandwidth("quota-by-key:volume").Should().Be(13);
    }

    [TestMethod]
    public void PolicyPipeline_IndependentModesDoNotSettleBetweenScopesOrSections()
    {
        var store = new RateLimitStore();
        var pipeline = PolicyPipelineBuilder.Create()
            .ConfigureContext(context => SettlementTest.ConfigureContext(context, store))
            .AddPolicy(PolicyScope.Global, new ExecutionTestDocument
            {
                InboundAction = section =>
                {
                    section.RateLimitByKey(SettlementTest.DeferredRate("Global"));
                    section.ReturnResponse(new ReturnResponseConfig());
                },
                OutboundAction = section => section.SetBody("global final")
            })
            .AddPolicy(PolicyScope.Operation, new ExecutionTestDocument
            {
                InboundAction = section => section.RateLimitByKey(SettlementTest.DeferredRate("Operation")),
                BackendAction = _ => store.GetCallCount("rate-limit-by-key:Global").Should().Be(0),
                OutboundAction = section => section.SetBody("operation")
            }).Build();

        pipeline.RunRequest(request =>
        {
            request.RunInboundIndependent();
            request.RunBackendIndependent();
            request.RunOutboundIndependent();
            store.GetCallCount("rate-limit-by-key:Global").Should().Be(0);
            store.GetCallCount("rate-limit-by-key:Operation").Should().Be(0);
        });

        store.GetCallCount("rate-limit-by-key:Global").Should().Be(2);
        store.GetCallCount("rate-limit-by-key:Operation").Should().Be(2);
        pipeline.Context.Response.Body.Content.Should().Be("global final");
        pipeline.Context.ResponseTerminated.Should().BeTrue();
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void PolicyPipeline_NewRequestObservesQuotaDenialFromPriorFinalResponseBytes(bool nested)
    {
        var store = new RateLimitStore();
        var outboundCalls = 0;
        var pipeline = PolicyPipelineBuilder.Create()
            .ConfigureContext(context => SettlementTest.ConfigureContext(context, store))
            .AddPolicy(PolicyScope.Operation, new ExecutionTestDocument
            {
                InboundAction = section => section.QuotaByKey(new QuotaByKeyConfig
                {
                    CounterKey = "volume",
                    RenewalPeriod = 300,
                    Bandwidth = 1
                }),
                OutboundAction = section =>
                {
                    outboundCalls++;
                    section.SetBody(new string('\u00e9', 600));
                }
            }).Build();
        pipeline.Context.Request.Body.Content = new string('r', 16);

        SettlementTest.RunAll(pipeline, nested);
        store.GetBandwidth("quota-by-key:volume").Should().Be(1216);
        pipeline.Context.RequestId = Guid.NewGuid();
        pipeline.Context.Request.Body.Content = string.Empty;
        pipeline.Context.ResponseTerminated = false;
        SettlementTest.RunAll(pipeline, nested);
        SettlementTest.RunAll(pipeline, nested);

        outboundCalls.Should().Be(1);
        pipeline.Context.Response.StatusCode.Should().Be(403);
        pipeline.Context.ResponseTerminated.Should().BeTrue();
        pipeline.Context.Response.Body.Content.Should().BeEmpty();
        store.GetBandwidth("quota-by-key:volume").Should().Be(1216);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void PolicyPipeline_LateDeferredWorkErrorsAndNonLimiterReuseStillWorks(bool nested)
    {
        var store = new RateLimitStore();
        var pipeline = PolicyPipelineBuilder.Create()
            .ConfigureContext(context => SettlementTest.ConfigureContext(context, store))
            .AddPolicy(PolicyScope.Operation, new ExecutionTestDocument
            {
                InboundAction = section => section.RateLimitByKey(SettlementTest.DeferredRate())
            }).Build();
        SettlementTest.RunAll(pipeline, nested);

        Assert.ThrowsExactly<InvalidOperationException>(() => pipeline.RunRequest(request =>
            ExecutionTest.RunSection(request, "inbound", nested)));
        store.GetCallCount("rate-limit-by-key:settlement").Should().Be(2);

        var calls = new List<string>();
        var nonLimiter = PolicyPipelineBuilder.Create()
            .AddPolicy(PolicyScope.Operation, ExecutionTest.FlowDocument("Operation", calls, nested))
            .Build();
        SettlementTest.RunAll(nonLimiter, nested);
        SettlementTest.RunAll(nonLimiter, nested);
        nonLimiter.RunOnError();
        calls.Should().HaveCount(14);
    }

    [TestMethod]
    [DataRow(false, false, "RateLimitByKey")]
    [DataRow(false, true, "RateLimitByKey")]
    [DataRow(true, false, "RateLimitByKey")]
    [DataRow(true, true, "RateLimitByKey")]
    [DataRow(false, false, "RateLimit")]
    [DataRow(false, true, "RateLimit")]
    [DataRow(true, false, "RateLimit")]
    [DataRow(true, true, "RateLimit")]
    [DataRow(false, false, "QuotaByKey")]
    [DataRow(false, true, "QuotaByKey")]
    [DataRow(true, false, "QuotaByKey")]
    [DataRow(true, true, "QuotaByKey")]
    [DataRow(false, false, "Quota")]
    [DataRow(false, true, "Quota")]
    [DataRow(true, false, "Quota")]
    [DataRow(true, true, "Quota")]
    public void PolicyPipeline_RetainedImmediatePoliciesAreRejectedBeforeProviderOrCounterMutation(
        bool nested, bool nestedCallback, string policy)
    {
        var store = new RateLimitStore();
        var limiter = new RecordingRateLimiter();
        IInboundContext? retained = null;
        var pipeline = PolicyPipelineBuilder.Create()
            .ConfigureContext(context =>
            {
                SettlementTest.ConfigureContext(context, store);
                context.Services.Register<IRateLimiter>(limiter);
            })
            .AddPolicy(PolicyScope.Global, new ExecutionTestDocument
            {
                InboundAction = section => section.Base(),
                OutboundAction = section => section.SetBody("final")
            })
            .AddPolicy(PolicyScope.Operation, new ExecutionTestDocument
            {
                InboundAction = section =>
                {
                    retained = section;
                    section.RateLimitByKey(SettlementTest.DeferredRate());
                    SettlementTest.ApplyQuota(section);
                },
                OutboundAction = section => section.Base()
            }).Build();
        SettlementTest.RunAll(pipeline, nested);
        var context = pipeline.Context;
        var requestId = context.RequestId;
        var providerCalls = limiter.Calls.ToArray();
        var variables = context.Variables.ToArray();
        var headers = context.Response.Headers.ToDictionary(pair => pair.Key, pair => pair.Value.ToArray());

        var error = Assert.ThrowsExactly<InvalidOperationException>(() => pipeline.RunRequest(request =>
        {
            if (nestedCallback)
            {
                request.RunRequest(_ => SettlementTest.ApplyImmediateCounter(retained!, policy));
            }
            else
            {
                SettlementTest.ApplyImmediateCounter(retained!, policy);
            }
        }));

        error.Message.Should().Contain("completed").And.Contain("RequestId");
        limiter.Calls.Should().Equal(providerCalls);
        store.GetCallCount("rate-limit-by-key:settlement").Should().Be(2);
        store.GetCallCount($"sub:{context.Subscription.Id}").Should().Be(0);
        store.GetCallCount("quota-by-key:volume").Should().Be(1);
        store.GetCallCount($"quota:sub:{context.Subscription.Id}").Should().Be(0);
        store.GetBandwidth("quota-by-key:volume").Should().Be(5);
        context.Variables.Should().Equal(variables);
        context.Response.Headers.Should().BeEquivalentTo(headers);
        context.Response.StatusCode.Should().Be(200);
        context.Response.Body.Content.Should().Be("final");
        context.ResponseTerminated.Should().BeFalse();
        context.RequestId.Should().Be(requestId);
        pipeline.RunRequest(request => request.RunRequest(_ => { }));
        limiter.Calls.Should().Equal(providerCalls);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void PolicyPipeline_RequestIdChangesCannotSettlePendingWorkOntoAnotherRequest(bool nested)
    {
        var store = new RateLimitStore();
        var pipeline = PolicyPipelineBuilder.Create()
            .ConfigureContext(context => SettlementTest.ConfigureContext(context, store))
            .AddPolicy(PolicyScope.Operation, new ExecutionTestDocument
            {
                InboundAction = section => section.RateLimitByKey(SettlementTest.DeferredRate())
            }).Build();
        var initialId = pipeline.Context.RequestId;

        Assert.ThrowsExactly<InvalidOperationException>(() => pipeline.RunRequest(request =>
        {
            SettlementTest.RunAll(request, nested);
            request.Context.RequestId = Guid.NewGuid();
        }));

        store.GetCallCount("rate-limit-by-key:settlement").Should().Be(0);
        pipeline.Context.RequestId = initialId;
        pipeline.RunRequest(_ => { });
        store.GetCallCount("rate-limit-by-key:settlement").Should().Be(2);
    }

    [TestMethod]
    public void PolicyPipeline_NullRequestCallbackIsRejected()
    {
        var pipeline = PolicyPipelineBuilder.Create().Build();

        Assert.ThrowsExactly<ArgumentNullException>(() => pipeline.RunRequest(null!));
    }

    private static PolicyPipeline CreatePipeline(
        List<string> calls, bool nested = false, string? stopSection = null, bool invokeRequest = false)
    {
        var builder = PolicyPipelineBuilder.Create();
        foreach (var scope in new[]
                 {
                     PolicyScope.Operation, PolicyScope.Api, PolicyScope.Product,
                     PolicyScope.Workspace, PolicyScope.Global
                 })
        {
            builder.AddPolicy(scope, ExecutionTest.FlowDocument(
                scope.ToString(), calls, nested, scope == PolicyScope.Product ? stopSection : null, invokeRequest));
        }

        return builder.Build();
    }

    internal static void SetupInvokeRequest(GatewayContext context)
    {
        var setup = new TestDocument(new ExecutionTestDocument()) { Context = context };
        static void Callback(GatewayContext gateway, InvokeRequestConfig config)
        {
            gateway.Response.StatusCode = 203;
            gateway.Response.Body.Content = "invoked";
        }

        setup.SetupInbound().InvokeRequest().WithCallback(Callback);
        setup.SetupBackend().InvokeRequest().WithCallback(Callback);
        setup.SetupOutbound().InvokeRequest().WithCallback(Callback);
        setup.SetupOnError().InvokeRequest().WithCallback(Callback);
    }
}

internal sealed class ExecutionTestDocument : IDocument
{
    public Action<IInboundContext>? InboundAction { get; init; }
    public Action<IBackendContext>? BackendAction { get; init; }
    public Action<IOutboundContext>? OutboundAction { get; init; }
    public Action<IOnErrorContext>? OnErrorAction { get; init; }

    public void Inbound(IInboundContext context) => InboundAction?.Invoke(context);
    public void Backend(IBackendContext context) => BackendAction?.Invoke(context);
    public void Outbound(IOutboundContext context) => OutboundAction?.Invoke(context);
    public void OnError(IOnErrorContext context) => OnErrorAction?.Invoke(context);
}

internal static class ExecutionTest
{
    public static string SectionName(string section) => section switch
    {
        "inbound" => nameof(IInboundContext),
        "backend" => nameof(IBackendContext),
        "outbound" => nameof(IOutboundContext),
        "on-error" => nameof(IOnErrorContext),
        _ => throw new ArgumentOutOfRangeException(nameof(section), section, null)
    };

    public static void RunSection(TestDocument test, string section)
    {
        switch (section)
        {
            case "inbound": test.RunInbound(); break;
            case "backend": test.RunBackend(); break;
            case "outbound": test.RunOutbound(); break;
            case "on-error": test.RunOnError(); break;
            default: throw new ArgumentOutOfRangeException(nameof(section), section, null);
        }
    }

    public static void RunSection(PolicyPipeline pipeline, string section, bool nested = false)
    {
        switch (section, nested)
        {
            case ("inbound", false): pipeline.RunInbound(); break;
            case ("backend", false): pipeline.RunBackend(); break;
            case ("outbound", false): pipeline.RunOutbound(); break;
            case ("on-error", false): pipeline.RunOnError(); break;
            case ("inbound", true): pipeline.RunInboundNested(); break;
            case ("backend", true): pipeline.RunBackendNested(); break;
            case ("outbound", true): pipeline.RunOutboundNested(); break;
            case ("on-error", true): pipeline.RunOnErrorNested(); break;
            default: throw new ArgumentOutOfRangeException(nameof(section), section, null);
        }
    }

    public static void RunIndependent(PolicyPipeline pipeline, string section)
    {
        switch (section)
        {
            case "inbound": pipeline.RunInboundIndependent(); break;
            case "backend": pipeline.RunBackendIndependent(); break;
            case "outbound": pipeline.RunOutboundIndependent(); break;
            default: throw new ArgumentOutOfRangeException(nameof(section), section, null);
        }
    }

    public static ExecutionTestDocument FlowDocument(
        string name, List<string> calls, bool chain = false, string? stopSection = null, bool invokeRequest = false)
    {
        void Finish(Action<ReturnResponseConfig> returnResponse, Action<InvokeRequestConfig> invoke)
        {
            if (invokeRequest)
            {
                invoke(new InvokeRequestConfig { Url = "https://example.com/invoke" });
            }
            else
            {
                returnResponse(new ReturnResponseConfig
                {
                    Status = new StatusConfig { Code = 202, Reason = "Accepted" },
                    Body = new BodyConfig { Content = "returned" }
                });
            }
        }

        void Record(string section, Action @base, Action finish)
        {
            calls.Add($"{name}:{section}:before");
            if (section == stopSection)
            {
                finish();
            }
            if (chain)
            {
                @base();
            }
            calls.Add($"{name}:{section}:after");
        }

        return new ExecutionTestDocument
        {
            InboundAction = context => Record("inbound", () => context.WithId("base").Base(),
                () => Finish(context.ReturnResponse, context.InvokeRequest)),
            BackendAction = context => Record("backend", () => context.WithId("base").Base(),
                () => Finish(context.ReturnResponse, context.InvokeRequest)),
            OutboundAction = context => Record("outbound", () => context.WithId("base").Base(),
                () => Finish(context.ReturnResponse, context.InvokeRequest)),
            OnErrorAction = context => Record("on-error", () => context.WithId("base").Base(),
                () => Finish(context.ReturnResponse, context.InvokeRequest))
        };
    }
}

internal static class SettlementTest
{
    public static GatewayContext CreateContext()
    {
        var context = new GatewayContext();
        ConfigureContext(context, new RateLimitStore());
        return context;
    }

    public static void ConfigureContext(GatewayContext context, RateLimitStore store)
    {
        var clock = new LimiterTestTimeProvider();
        context.Services.Register<TimeProvider>(clock);
        context.Services.Register(store);
        context.Subscription.CreatedDate = clock.GetUtcNow().UtcDateTime;
    }

    public static RateLimitByKeyConfig DeferredRate(string key = "settlement") => new()
    {
        CounterKey = key,
        Calls = 10,
        RenewalPeriod = 60,
        IncrementCount = 2,
        IncrementAfterResponse = true,
        RemainingCallsVariableName = "remaining"
    };

    public static void ApplyImmediateCounter(IInboundContext context, string policy)
    {
        switch (policy)
        {
            case nameof(IInboundContext.RateLimitByKey):
                context.RateLimitByKey(DeferredRate() with
                {
                    IncrementAfterResponse = false,
                    RemainingCallsHeaderName = "X-Remaining",
                    TotalCallsHeaderName = "X-Total"
                });
                break;
            case nameof(IInboundContext.RateLimit):
                context.RateLimit(new RateLimitConfig
                {
                    Calls = 10,
                    RenewalPeriod = 60,
                    RemainingCallsVariableName = "remaining",
                    RemainingCallsHeaderName = "X-Remaining",
                    TotalCallsHeaderName = "X-Total"
                });
                break;
            case nameof(IInboundContext.QuotaByKey):
                context.QuotaByKey(new QuotaByKeyConfig
                {
                    CounterKey = "volume",
                    Calls = 10,
                    RenewalPeriod = 300,
                    IncrementCount = 2
                });
                break;
            case nameof(IInboundContext.Quota):
                context.Quota(new QuotaConfig { Calls = 10, RenewalPeriod = 300 });
                break;
            default: throw new ArgumentOutOfRangeException(nameof(policy), policy, null);
        }
    }

    public static void ApplyQuota(IInboundContext context, bool keyed = true)
    {
        if (keyed)
        {
            context.QuotaByKey(new QuotaByKeyConfig
            {
                CounterKey = "volume",
                Bandwidth = 64,
                RenewalPeriod = 300
            });
        }
        else
        {
            context.Quota(new QuotaConfig { Bandwidth = 64, RenewalPeriod = 300 });
        }
    }

    public static void RunAll(PolicyPipeline pipeline, bool nested)
    {
        if (nested)
        {
            pipeline.RunAllNested();
        }
        else
        {
            pipeline.RunAll();
        }
    }
}