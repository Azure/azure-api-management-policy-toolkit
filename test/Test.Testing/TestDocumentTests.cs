// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text;
using System.Reflection;

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring.Expressions;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Document;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Data;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Services;

using Newtonsoft.Json.Linq;

using Test.Emulator.Emulator.Policies;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing;

[TestClass]
public class TestDocumentTests
{
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void TokenCorrection_BackendObservationPrecedesOutboundButAllCountersWaitForOuterCompletion(bool azure)
    {
        var context = SettlementTest.CreateContext();
        var calls = context.Services.Resolve<RateLimitStore>()!;
        var tokens = new TokenLimitCounterStore();
        context.Services.Register(tokens);
        var clock = context.Services.Resolve<TimeProvider>()!;
        var config = new TokenLimitConfig
        {
            CounterKey = "runner",
            EstimatePromptToken = false,
            TokensPerMinute = 10,
            TokensConsumedVariableName = "consumed",
            TokensConsumedHeaderName = "X-Tokens"
        };
        var final = "{\"answer\":\"caf\u00e9\"}";
        var test = new TestDocument(new ExecutionTestDocument
        {
            InboundAction = section =>
            {
                if (azure) section.AzureOpenAiTokenLimit(config);
                else section.LlmTokenLimit(config);
                section.RateLimitByKey(SettlementTest.DeferredRate());
                SettlementTest.ApplyQuota(section);
            },
            BackendAction = _ => context.Response.Body.Content = """{"usage":{"prompt_tokens":4,"completion_tokens":2}}""",
            OutboundAction = section =>
            {
                context.Variables["consumed"].Should().Be(6L);
                section.SetHeader("X-Copied", [context.Variables["consumed"].ToString()!]);
                section.SetBody(final);
            }
        })
        { Context = context };
        var requestId = context.RequestId;

        test.RunRequest(request =>
        {
            request.RunAll();
            request.RunRequest(_ => { });
            context.Variables["consumed"].Should().Be(6L);
            calls.GetCallCount("rate-limit-by-key:settlement").Should().Be(0);
            calls.GetBandwidth("quota-by-key:volume").Should().Be(0);
            tokens.GetRateTokens("runner", clock.GetUtcNow()).Should().Be(0);
        });

        context.Response.Body.Content.Should().Be(final);
        context.Response.Headers["X-Copied"].Should().Equal("6");
        context.Response.Headers["X-Tokens"].Should().Equal("6");
        context.RequestId.Should().Be(requestId);
        calls.GetCallCount("rate-limit-by-key:settlement").Should().Be(2);
        calls.GetBandwidth("quota-by-key:volume").Should().Be(Encoding.UTF8.GetByteCount(final));
        tokens.GetRateTokens("runner", clock.GetUtcNow()).Should().Be(6);
        Assert.ThrowsExactly<InvalidOperationException>(test.RunBackend);
        test.RunRequest(_ => { });
        tokens.GetRateTokens("runner", clock.GetUtcNow()).Should().Be(6);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void TokenCorrection_InboundTerminalSkipsUsageAndStillSettlesExistingDeferredLimits(bool azure)
    {
        var context = SettlementTest.CreateContext();
        var calls = context.Services.Resolve<RateLimitStore>()!;
        var tokens = new TokenLimitCounterStore();
        context.Services.Register(tokens);
        var config = new TokenLimitConfig
        {
            CounterKey = "runner",
            EstimatePromptToken = false,
            TokensPerMinute = 10,
            TokensConsumedVariableName = "consumed",
            TokensConsumedHeaderName = "X-Tokens"
        };
        var test = new TestDocument(new ExecutionTestDocument
        {
            InboundAction = section =>
            {
                if (azure) section.AzureOpenAiTokenLimit(config);
                else section.LlmTokenLimit(config);
                section.RateLimitByKey(SettlementTest.DeferredRate());
                section.ReturnResponse(new ReturnResponseConfig
                {
                    Status = new StatusConfig { Code = 204, Reason = "No Content" }
                });
            },
            BackendAction = _ => Assert.Fail("Terminal inbound must skip backend."),
            OutboundAction = _ => Assert.Fail("Terminal inbound must skip outbound.")
        })
        { Context = context };

        test.RunAll();

        context.Response.StatusCode.Should().Be(204);
        context.ResponseTerminated.Should().BeTrue();
        context.Response.Headers.Should().NotContainKey("X-Tokens");
        context.Variables["consumed"].Should().Be(0L);
        calls.GetCallCount("rate-limit-by-key:settlement").Should().Be(2);
        tokens.GetRateTokens("runner", context.Services.Resolve<TimeProvider>()!.GetUtcNow()).Should().Be(0);
    }

    [TestMethod]
    public void ShouldUseBasicAuthenticationForRequestsFromInternalIp()
    {
        var document = new OperationDocument();
        var test = new TestDocument(document) { Context = { Request = { IpAddress = "10.0.0.1" } } };

        test.RunInbound();

        var authValue = test.Context.Request
            .Headers.Should().ContainKey("Authorization")
            .WhoseValue.Should().ContainSingle().Subject;
        authValue.Should().StartWith("Basic ");
        var token = authValue["Basic ".Length..];
        token = Encoding.UTF8.GetString(Convert.FromBase64String(token));
        token.Should().Be("{{username}}:{{password}}");
    }

    [TestMethod]
    public void ShouldReturnResponseForInternalIpAndMockSubscription()
    {
        var document = new OperationDocument();
        var test = new TestDocument(document)
        {
            Context = { Request = { IpAddress = "10.0.0.1" }, Subscription = { Name = "asdfgh-mock" } }
        };

        test.RunInbound();

        var response = test.Context.Response;
        response.StatusCode.Should().Be(200);
        response.StatusReason.Should().Be("Mocked OK");
        test.Context.Request
            .Headers.Should().NotContainKey("Authorization");
    }

    [TestMethod]
    public void ShouldUseManagedIdentityTokenAuthenticationForRequestsFromExternalIp()
    {
        var document = new OperationDocument();
        var test = new TestDocument(document) { Context = { Request = { IpAddress = "11.0.0.1" } } };
        test.SetupInbound().AuthenticationManagedIdentity().ReturnsToken("testTokenValue");

        test.RunInbound();

        var authValue = test.Context.Request
            .Headers.Should().ContainKey("Authorization")
            .WhoseValue.Should().HaveCount(1).And.Subject.First()!;
        authValue.Should().StartWith("Bearer ");
        var token = authValue["Bearer ".Length..];
        var variableToken = test.Context
            .Variables.Should().ContainKey("testToken")
            .WhoseValue.Should().BeOfType<string>().Subject;
        token.Should().Be(variableToken).And.Be("testTokenValue");
    }

    [TestMethod]
    public void ShouldForwardRequest()
    {
        var document = new OperationDocument();
        var test = new TestDocument(document);
        int called = 0;
        test.SetupBackend().ForwardRequest().WithCallback((_, _) => called++);

        test.RunBackend();

        called.Should().Be(1);
    }

    [TestMethod]
    public void ShouldRewriteBody()
    {
        var document = new OperationDocument();
        var initial = JObject.Parse(
            """
            {
                "title": "Software Engineer",
                "location": "Redmond",
                "secret": "42",
                "name": "John Doe"
            }
            """);
        var test = new TestDocument(document) { Context = { Response = { Body = { Content = initial.ToString() } } } };

        test.RunOutbound();

        var body = test.Context.Response.Body.Content;
        var expected = JObject.Parse(
            """
            {
                "title": "Software Engineer",
                "name": "John Doe"
            }
            """);
        Assert.IsTrue(JToken.DeepEquals(
            JObject.Parse(body),
            expected
        ));
    }

    [TestMethod]
    public void RunAllSettlesDeferredRatesOnlyAfterOutbound()
    {
        var context = SettlementTest.CreateContext();
        var store = context.Services.Resolve<RateLimitStore>()!;
        var requestId = context.RequestId;
        var sections = new List<string>();
        var test = new TestDocument(new ExecutionTestDocument
        {
            InboundAction = section =>
            {
                section.RateLimitByKey(SettlementTest.DeferredRate());
                sections.Add("inbound");
                store.GetCallCount("rate-limit-by-key:settlement").Should().Be(0);
                section.ExpressionContext.RequestId.Should().Be(requestId);
            },
            BackendAction = section =>
            {
                sections.Add("backend");
                store.GetCallCount("rate-limit-by-key:settlement").Should().Be(0);
                section.ExpressionContext.RequestId.Should().Be(requestId);
            },
            OutboundAction = section =>
            {
                sections.Add("outbound");
                store.GetCallCount("rate-limit-by-key:settlement").Should().Be(0);
                section.ExpressionContext.RequestId.Should().Be(requestId);
                section.SetBody("final");
            }
        })
        { Context = context };

        test.RunAll();

        sections.Should().Equal("inbound", "backend", "outbound");
        store.GetCallCount("rate-limit-by-key:settlement").Should().Be(2);
        context.Variables["remaining"].Should().Be(8);
        context.Response.Body.Content.Should().Be("final");
        context.RequestId.Should().Be(requestId);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void RunAllAccountsFinalResponseBytesInsteadOfIntermediateBodies(bool keyed)
    {
        var context = SettlementTest.CreateContext();
        var store = context.Services.Resolve<RateLimitStore>()!;
        var key = keyed ? "quota-by-key:volume" : $"quota:sub:{context.Subscription.Id}";
        context.Request.Body.Content = new string('r', 128);
        var test = new TestDocument(new ExecutionTestDocument
        {
            InboundAction = section =>
            {
                SettlementTest.ApplyQuota(section, keyed);
                context.Response.Body.Content = new string('i', 4096);
            },
            BackendAction = _ => context.Response.Body.Content = new string('b', 8192),
            OutboundAction = section =>
            {
                store.GetBandwidth(key).Should().Be(128);
                section.SetBody(new string('\u00e9', 256));
            }
        })
        { Context = context };

        test.RunAll();

        store.GetBandwidth(key).Should().Be(640);
        context.Request.Body.Consumed.Should().BeFalse();
        context.Response.Body.Consumed.Should().BeFalse();
    }

    [TestMethod]
    public void RunAllSettlesEarlyReturnAndPreservesItsResponse()
    {
        var context = SettlementTest.CreateContext();
        var store = context.Services.Resolve<RateLimitStore>()!;
        var limiter = new RecordingRateLimiter();
        context.Services.Register<IRateLimiter>(limiter);
        var test = new TestDocument(new ExecutionTestDocument
        {
            InboundAction = section =>
            {
                section.RateLimitByKey(SettlementTest.DeferredRate());
                SettlementTest.ApplyQuota(section);
                section.ReturnResponse(new ReturnResponseConfig
                {
                    Status = new StatusConfig { Code = 202, Reason = "Accepted" },
                    Headers = [new HeaderConfig { Name = "X-Final", Values = ["preserved"] }],
                    Body = new BodyConfig { Content = "early response" }
                });
            },
            BackendAction = _ => Assert.Fail("Backend must not execute."),
            OutboundAction = _ => Assert.Fail("Outbound must not execute.")
        })
        { Context = context };

        test.RunAll();
        test.RunRequest(_ => { });
        test.CompleteLimiterResponse();

        store.GetCallCount("rate-limit-by-key:settlement").Should().Be(2);
        store.GetBandwidth("quota-by-key:volume").Should().Be(14);
        limiter.Calls.Where(call => call.Key == "settlement").Should().Equal(("settlement", 0), ("settlement", 2));
        context.Response.StatusCode.Should().Be(202);
        context.Response.StatusReason.Should().Be("Accepted");
        context.Response.Headers["X-Final"].Should().Equal("preserved");
        context.Response.Body.Content.Should().Be("early response");
        context.ResponseTerminated.Should().BeTrue();
    }

    [TestMethod]
    public void RunAllKeepsInvokeRequestSectionOnlyUntilFinalOutbound()
    {
        var context = SettlementTest.CreateContext();
        var store = context.Services.Resolve<RateLimitStore>()!;
        var sections = new List<string>();
        var test = new TestDocument(new ExecutionTestDocument
        {
            InboundAction = section =>
            {
                section.RateLimitByKey(SettlementTest.DeferredRate());
                section.InvokeRequest(new InvokeRequestConfig());
                section.SetVariable("after-invoke", true);
            },
            BackendAction = _ =>
            {
                sections.Add("backend");
                store.GetCallCount("rate-limit-by-key:settlement").Should().Be(0);
            },
            OutboundAction = section =>
            {
                sections.Add("outbound");
                store.GetCallCount("rate-limit-by-key:settlement").Should().Be(0);
                section.SetBody("outbound response");
            }
        })
        { Context = context };
        test.SetupInbound().InvokeRequest().WithCallback((gateway, _) => gateway.Response.StatusCode = 203);

        test.RunAll();

        sections.Should().Equal("backend", "outbound");
        context.Variables.Should().NotContainKey("after-invoke");
        context.ResponseTerminated.Should().BeFalse();
        context.Response.StatusCode.Should().Be(203);
        context.Response.Body.Content.Should().Be("outbound response");
        store.GetCallCount("rate-limit-by-key:settlement").Should().Be(2);
    }

    [TestMethod]
    public void OuterRunRequestDefersSettlementUntilOnErrorFinishes()
    {
        var context = SettlementTest.CreateContext();
        var store = context.Services.Resolve<RateLimitStore>()!;
        var expected = new InvalidOperationException("backend failed");
        var test = new TestDocument(new ExecutionTestDocument
        {
            InboundAction = section =>
            {
                section.RateLimitByKey(SettlementTest.DeferredRate());
                SettlementTest.ApplyQuota(section);
            },
            BackendAction = _ => throw expected,
            OutboundAction = _ => Assert.Fail("Outbound must not execute after an error."),
            OnErrorAction = section =>
            {
                store.GetCallCount("rate-limit-by-key:settlement").Should().Be(0);
                store.GetBandwidth("quota-by-key:volume").Should().Be(0);
                section.SetStatus(new StatusConfig { Code = 500, Reason = "Handled" });
                section.SetBody(new string('\u00e9', 100));
            }
        })
        { Context = context };

        test.RunRequest(request =>
        {
            Assert.ThrowsExactly<InvalidOperationException>(request.RunAll).Should().BeSameAs(expected);
            store.GetCallCount("rate-limit-by-key:settlement").Should().Be(0);
            request.RunRequest(inner => inner.RunOnError());
            store.GetCallCount("rate-limit-by-key:settlement").Should().Be(0);
        });

        store.GetCallCount("rate-limit-by-key:settlement").Should().Be(2);
        store.GetBandwidth("quota-by-key:volume").Should().Be(200);
        context.Response.StatusCode.Should().Be(500);
    }

    [TestMethod]
    public void UnhandledRunAllErrorLeavesPendingWorkAndRestoresOwnerDepth()
    {
        var context = SettlementTest.CreateContext();
        var store = context.Services.Resolve<RateLimitStore>()!;
        var expected = new InvalidOperationException("execution failed");
        var test = new TestDocument(new ExecutionTestDocument
        {
            InboundAction = section => section.RateLimitByKey(SettlementTest.DeferredRate()),
            BackendAction = _ => throw expected,
            OnErrorAction = section => section.SetBody("handled")
        })
        { Context = context };

        Assert.ThrowsExactly<InvalidOperationException>(test.RunAll).Should().BeSameAs(expected);
        store.GetCallCount("rate-limit-by-key:settlement").Should().Be(0);
        test.RunRequest(request => request.RunOnError());

        store.GetCallCount("rate-limit-by-key:settlement").Should().Be(2);
        context.Response.Body.Content.Should().Be("handled");
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void StandaloneSectionsStayIndependentUntilAnExplicitRequestBoundary(bool terminate)
    {
        var context = SettlementTest.CreateContext();
        var store = context.Services.Resolve<RateLimitStore>()!;
        var sections = new List<string>();
        var test = new TestDocument(new ExecutionTestDocument
        {
            InboundAction = section =>
            {
                section.RateLimitByKey(SettlementTest.DeferredRate());
                if (terminate)
                {
                    section.ReturnResponse(new ReturnResponseConfig());
                }
            },
            BackendAction = _ => sections.Add("backend"),
            OutboundAction = section =>
            {
                sections.Add("outbound");
                section.SetBody("outbound");
            },
            OnErrorAction = section =>
            {
                sections.Add("on-error");
                section.SetBody("last manual body");
            }
        })
        { Context = context };

        test.RunInbound();
        test.RunBackend();
        test.RunOutbound();
        test.RunOnError();

        sections.Should().Equal("backend", "outbound", "on-error");
        store.GetCallCount("rate-limit-by-key:settlement").Should().Be(0);
        test.RunRequest(_ => { });
        store.GetCallCount("rate-limit-by-key:settlement").Should().Be(2);
        context.Response.Body.Content.Should().Be("last manual body");
        context.ResponseTerminated.Should().Be(terminate);
    }

    [TestMethod]
    public void DuplicateRequestCompletionDoesNotChargeCountersOrBandwidthAgain()
    {
        var context = SettlementTest.CreateContext();
        var store = context.Services.Resolve<RateLimitStore>()!;
        var test = new TestDocument(new ExecutionTestDocument
        {
            InboundAction = section =>
            {
                section.RateLimitByKey(SettlementTest.DeferredRate());
                SettlementTest.ApplyQuota(section);
            },
            OutboundAction = section => section.SetBody("final")
        })
        { Context = context };

        test.RunAll();
        context.Response.Body.Content = "changed after completion";
        test.RunRequest(_ => { });
        test.RunRequest(_ => { });
        test.CompleteLimiterResponse();

        store.GetCallCount("rate-limit-by-key:settlement").Should().Be(2);
        store.GetBandwidth("quota-by-key:volume").Should().Be(5);
    }

    [TestMethod]
    public void LateSectionWorkOnACompletedLimiterRequestErrorsBeforeAdmission()
    {
        var context = SettlementTest.CreateContext();
        var store = context.Services.Resolve<RateLimitStore>()!;
        var limiter = new RecordingRateLimiter();
        context.Services.Register<IRateLimiter>(limiter);
        var test = new TestDocument(new ExecutionTestDocument
        {
            InboundAction = section => section.RateLimitByKey(SettlementTest.DeferredRate())
        })
        { Context = context };
        test.RunAll();

        var error = Assert.ThrowsExactly<InvalidOperationException>(
            () => test.RunRequest(request => request.RunInbound()));

        error.Message.Should().Contain("completed").And.Contain("RequestId");
        Assert.ThrowsExactly<InvalidOperationException>(test.RunInbound);
        limiter.Calls.Should().Equal(("settlement", 0), ("settlement", 2));
        store.GetCallCount("rate-limit-by-key:settlement").Should().Be(2);
        test.RunRequest(_ => { });
    }

    [TestMethod]
    public void ANewRequestIdReopensTheContextAfterSuccessfulSettlement()
    {
        var context = SettlementTest.CreateContext();
        var store = context.Services.Resolve<RateLimitStore>()!;
        var test = new TestDocument(new ExecutionTestDocument
        {
            InboundAction = section => section.RateLimitByKey(SettlementTest.DeferredRate())
        })
        { Context = context };
        test.RunAll();
        var firstId = context.RequestId;
        context.RequestId = Guid.NewGuid();

        test.RunAll();

        context.RequestId.Should().NotBe(firstId);
        store.GetCallCount("rate-limit-by-key:settlement").Should().Be(4);
    }

    [TestMethod]
    [DataRow("direct")]
    [DataRow("request")]
    [DataRow("nested")]
    public void LateDeferredWorkThroughARetainedProxyErrorsBeforeAdmission(string invocation)
    {
        var context = SettlementTest.CreateContext();
        var store = context.Services.Resolve<RateLimitStore>()!;
        var limiter = new RecordingRateLimiter();
        context.Services.Register<IRateLimiter>(limiter);
        IInboundContext? retained = null;
        var test = new TestDocument(new ExecutionTestDocument
        {
            InboundAction = section =>
            {
                retained = section;
                section.RateLimitByKey(SettlementTest.DeferredRate());
            }
        })
        { Context = context };
        test.RunAll();

        var error = Assert.ThrowsExactly<InvalidOperationException>(() => InvokeRetainedPolicy(
            test, () => retained!.RateLimitByKey(SettlementTest.DeferredRate()), invocation));

        error.Message.Should().Contain("completed").And.Contain("RequestId");
        limiter.Calls.Should().Equal(("settlement", 0), ("settlement", 2));
        context.Variables["remaining"].Should().Be(8);
        test.RunRequest(_ => { });
        store.GetCallCount("rate-limit-by-key:settlement").Should().Be(2);
    }

    [TestMethod]
    public void RejectedLateProxyWorkLeavesEmptyDuplicateCompletionSafe()
    {
        var context = SettlementTest.CreateContext();
        var store = context.Services.Resolve<RateLimitStore>()!;
        var limiter = new RecordingRateLimiter();
        context.Services.Register<IRateLimiter>(limiter);
        IInboundContext? retained = null;
        var test = new TestDocument(new ExecutionTestDocument
        {
            InboundAction = section =>
            {
                retained = section;
                section.RateLimitByKey(SettlementTest.DeferredRate());
            }
        })
        { Context = context };
        test.RunAll();
        var called = false;

        var error = Assert.ThrowsExactly<InvalidOperationException>(
            () => retained!.RateLimitByKey(SettlementTest.DeferredRate()));
        test.RunRequest(_ => called = true);

        error.Message.Should().Contain("completed").And.Contain("RequestId");
        called.Should().BeTrue();
        limiter.Calls.Should().Equal(("settlement", 0), ("settlement", 2));
        store.GetCallCount("rate-limit-by-key:settlement").Should().Be(2);
    }

    [TestMethod]
    [DataRow("RateLimitByKey", "direct")]
    [DataRow("RateLimitByKey", "request")]
    [DataRow("RateLimitByKey", "nested")]
    [DataRow("RateLimit", "direct")]
    [DataRow("RateLimit", "request")]
    [DataRow("RateLimit", "nested")]
    [DataRow("QuotaByKey", "direct")]
    [DataRow("QuotaByKey", "request")]
    [DataRow("QuotaByKey", "nested")]
    [DataRow("Quota", "direct")]
    [DataRow("Quota", "request")]
    [DataRow("Quota", "nested")]
    public void LateImmediateCountersThroughARetainedProxyAreRejectedBeforeAnyMutation(
        string policy, string invocation)
    {
        var context = SettlementTest.CreateContext();
        var store = context.Services.Resolve<RateLimitStore>()!;
        var limiter = new RecordingRateLimiter();
        context.Services.Register<IRateLimiter>(limiter);
        IInboundContext? retained = null;
        var test = new TestDocument(new ExecutionTestDocument
        {
            InboundAction = section =>
            {
                retained = section;
                section.RateLimitByKey(SettlementTest.DeferredRate());
            },
            OutboundAction = section => section.SetBody("final")
        })
        { Context = context };
        test.RunAll();
        var requestId = context.RequestId;
        var variables = context.Variables.ToArray();
        var headers = context.Response.Headers.ToDictionary(pair => pair.Key, pair => pair.Value.ToArray());

        var error = Assert.ThrowsExactly<InvalidOperationException>(() => InvokeRetainedPolicy(
            test, () => SettlementTest.ApplyImmediateCounter(retained!, policy), invocation));

        error.Message.Should().Contain("completed").And.Contain("RequestId");
        limiter.Calls.Should().Equal(("settlement", 0), ("settlement", 2));
        store.GetCallCount("rate-limit-by-key:settlement").Should().Be(2);
        store.GetCallCount($"sub:{context.Subscription.Id}").Should().Be(0);
        store.GetCallCount("quota-by-key:volume").Should().Be(0);
        store.GetCallCount($"quota:sub:{context.Subscription.Id}").Should().Be(0);
        store.GetBandwidth("quota-by-key:volume").Should().Be(0);
        store.GetBandwidth($"quota:sub:{context.Subscription.Id}").Should().Be(0);
        context.Variables.Should().Equal(variables);
        context.Response.Headers.Should().BeEquivalentTo(headers);
        context.Response.StatusCode.Should().Be(200);
        context.Response.Body.Content.Should().Be("final");
        context.ResponseTerminated.Should().BeFalse();
        context.RequestId.Should().Be(requestId);
        test.RunRequest(_ => { });
        limiter.Calls.Should().Equal(("settlement", 0), ("settlement", 2));
    }

    [TestMethod]
    [DataRow("direct")]
    [DataRow("request")]
    [DataRow("nested")]
    public void LateRetainedPoliciesCannotEvaluateMockPredicatesOrCallbacks(string invocation)
    {
        var context = SettlementTest.CreateContext();
        var store = context.Services.Resolve<RateLimitStore>()!;
        var predicateCalls = 0;
        var callbackCalls = 0;
        IInboundContext? retained = null;
        var test = new TestDocument(new ExecutionTestDocument
        {
            InboundAction = section =>
            {
                retained = section;
                section.RateLimitByKey(SettlementTest.DeferredRate());
            }
        })
        { Context = context };
        test.SetupInbound().RateLimitByKey((_, config) =>
        {
            predicateCalls++;
            return config.IncrementAfterResponse == false;
        }).WithCallback((gateway, _) =>
        {
            callbackCalls++;
            gateway.Variables["late"] = true;
        });
        test.RunAll();
        predicateCalls.Should().Be(1);

        Assert.ThrowsExactly<InvalidOperationException>(() => InvokeRetainedPolicy(
            test, () => SettlementTest.ApplyImmediateCounter(retained!, "RateLimitByKey"), invocation));

        predicateCalls.Should().Be(1);
        callbackCalls.Should().Be(0);
        context.Variables.Should().NotContainKey("late");
        store.GetCallCount("rate-limit-by-key:settlement").Should().Be(2);
    }

    [TestMethod]
    [DataRow("RateLimitByKey")]
    [DataRow("RateLimit")]
    [DataRow("QuotaByKey")]
    [DataRow("Quota")]
    public void FreshRequestIdAllowsImmediateCountersThroughTheRetainedProxy(string policy)
    {
        var context = SettlementTest.CreateContext();
        var store = context.Services.Resolve<RateLimitStore>()!;
        var limiter = new RecordingRateLimiter();
        context.Services.Register<IRateLimiter>(limiter);
        IInboundContext? retained = null;
        var test = new TestDocument(new ExecutionTestDocument
        {
            InboundAction = section =>
            {
                retained = section;
                section.RateLimitByKey(SettlementTest.DeferredRate());
            }
        })
        { Context = context };
        test.RunAll();
        context.RequestId = Guid.NewGuid();

        test.RunRequest(request => request.RunRequest(
            _ => SettlementTest.ApplyImmediateCounter(retained!, policy)));
        test.RunRequest(_ => { });

        store.GetCallCount("rate-limit-by-key:settlement").Should().Be(policy == "RateLimitByKey" ? 4 : 2);
        store.GetCallCount($"sub:{context.Subscription.Id}").Should().Be(policy == "RateLimit" ? 1 : 0);
        store.GetCallCount("quota-by-key:volume").Should().Be(policy == "QuotaByKey" ? 2 : 0);
        store.GetCallCount($"quota:sub:{context.Subscription.Id}").Should().Be(policy == "Quota" ? 1 : 0);
        limiter.Calls.Should().HaveCount(3);
    }

    [TestMethod]
    public void StandaloneRetainedProxyCanAdmitBeforeExplicitRequestCompletion()
    {
        var context = SettlementTest.CreateContext();
        var store = context.Services.Resolve<RateLimitStore>()!;
        var limiter = new RecordingRateLimiter();
        context.Services.Register<IRateLimiter>(limiter);
        IInboundContext? retained = null;
        var test = new TestDocument(new ExecutionTestDocument
        {
            InboundAction = section =>
            {
                retained = section;
                section.RateLimitByKey(SettlementTest.DeferredRate());
            }
        })
        { Context = context };

        test.RunInbound();
        SettlementTest.ApplyImmediateCounter(retained!, "RateLimitByKey");
        store.GetCallCount("rate-limit-by-key:settlement").Should().Be(2);
        test.RunRequest(_ => { });

        store.GetCallCount("rate-limit-by-key:settlement").Should().Be(4);
        limiter.Calls.Should().Equal(("settlement", 0), ("settlement", 2), ("settlement", 2));
    }

    [TestMethod]
    [DataRow("inbound")]
    [DataRow("backend")]
    [DataRow("outbound")]
    [DataRow("on-error")]
    public void CompletedRequestProxiesAllowMetadataReadsButRejectPoliciesInEverySection(string sectionName)
    {
        var context = SettlementTest.CreateContext();
        IHaveExpressionContext? retained = null;
        Func<object>? withId = null;
        Action? mutate = null;
        void Retain(string section, IHaveExpressionContext proxy, Func<object> metadata, Action policy)
        {
            if (section == sectionName)
            {
                retained = proxy;
                withId = metadata;
                mutate = policy;
            }
        }

        var test = new TestDocument(new ExecutionTestDocument
        {
            InboundAction = section =>
            {
                Retain("inbound", section, () => section.WithId("late"), () => section.SetVariable("late", true));
                section.RateLimitByKey(SettlementTest.DeferredRate());
            },
            BackendAction = section => Retain(
                "backend", section, () => section.WithId("late"), () => section.SetVariable("late", true)),
            OutboundAction = section => Retain(
                "outbound", section, () => section.WithId("late"), () => section.SetVariable("late", true)),
            OnErrorAction = section => Retain(
                "on-error", section, () => section.WithId("late"), () => section.SetVariable("late", true))
        })
        { Context = context };
        test.RunOnError();
        test.RunAll();

        retained!.ExpressionContext.Should().BeSameAs(context);
        withId!().Should().BeSameAs(retained);
        var error = Assert.ThrowsExactly<InvalidOperationException>(() => mutate!());

        error.Message.Should().Contain("completed").And.Contain("RequestId");
        context.Variables.Should().NotContainKey("late");
        test.RunRequest(_ => { });
    }

    [TestMethod]
    public void RetainedProxyCannotExecuteConcurrentlyWithARequestOwner()
    {
        var context = SettlementTest.CreateContext();
        var store = context.Services.Resolve<RateLimitStore>()!;
        var limiter = new RecordingRateLimiter();
        context.Services.Register<IRateLimiter>(limiter);
        IInboundContext? retained = null;
        var test = new TestDocument(new ExecutionTestDocument
        {
            InboundAction = section =>
            {
                retained = section;
                section.RateLimitByKey(SettlementTest.DeferredRate());
            }
        })
        { Context = context };
        test.RunInbound();

        test.RunRequest(_ =>
        {
            var attempt = Task.Run(() => Assert.ThrowsExactly<InvalidOperationException>(
                () => SettlementTest.ApplyImmediateCounter(retained!, "RateLimitByKey")));
            attempt.Wait(TimeSpan.FromSeconds(10)).Should().BeTrue();
            attempt.GetAwaiter().GetResult().Message.Should().Contain("Concurrent");
            store.GetCallCount("rate-limit-by-key:settlement").Should().Be(0);
        });

        store.GetCallCount("rate-limit-by-key:settlement").Should().Be(2);
        limiter.Calls.Should().Equal(("settlement", 0), ("settlement", 2));
    }

    [TestMethod]
    public void RequestIdChangesInsideAnOwnerErrorBeforePendingSettlement()
    {
        var context = SettlementTest.CreateContext();
        var store = context.Services.Resolve<RateLimitStore>()!;
        var initialId = context.RequestId;
        var test = new TestDocument(new ExecutionTestDocument
        {
            InboundAction = section => section.RateLimitByKey(SettlementTest.DeferredRate())
        })
        { Context = context };

        var error = Assert.ThrowsExactly<InvalidOperationException>(() => test.RunRequest(request =>
        {
            request.RunInbound();
            context.RequestId = Guid.NewGuid();
        }));

        error.Message.Should().Contain("RequestId");
        context.RequestId.Should().NotBe(initialId);
        store.GetCallCount("rate-limit-by-key:settlement").Should().Be(0);
        context.RequestId = initialId;
        test.RunRequest(_ => { });
        store.GetCallCount("rate-limit-by-key:settlement").Should().Be(2);
    }

    [TestMethod]
    public void NestedOwnerValidatesRequestIdEvenWhenItsErrorIsHandledOutside()
    {
        var context = SettlementTest.CreateContext();
        var store = context.Services.Resolve<RateLimitStore>()!;
        var initialId = context.RequestId;
        var test = new TestDocument(new ExecutionTestDocument
        {
            InboundAction = section => section.RateLimitByKey(SettlementTest.DeferredRate())
        })
        { Context = context };

        test.RunRequest(request =>
        {
            request.RunInbound();
            Assert.ThrowsExactly<InvalidOperationException>(() => request.RunRequest(_ =>
                context.RequestId = Guid.NewGuid()));
            store.GetCallCount("rate-limit-by-key:settlement").Should().Be(0);
            context.RequestId = initialId;
        });

        store.GetCallCount("rate-limit-by-key:settlement").Should().Be(2);
    }

    [TestMethod]
    public void ProviderSettlementFailuresPropagateWithoutClosingTheRequest()
    {
        var context = SettlementTest.CreateContext();
        var store = context.Services.Resolve<RateLimitStore>()!;
        var requestId = context.RequestId;
        var inboundRuns = 0;
        var fail = true;
        var expected = new InvalidOperationException("provider failed");
        var limiter = new RecordingRateLimiter((_, permits) =>
        {
            if (permits > 0 && fail)
            {
                throw expected;
            }
            return true;
        });
        context.Services.Register<IRateLimiter>(limiter);
        var test = new TestDocument(new ExecutionTestDocument
        {
            InboundAction = section =>
            {
                inboundRuns++;
                section.RateLimitByKey(SettlementTest.DeferredRate());
            }
        })
        { Context = context };

        Assert.ThrowsExactly<InvalidOperationException>(test.RunAll).Should().BeSameAs(expected);
        HasPendingCounterResponse(context).Should().BeTrue();
        store.GetCallCount("rate-limit-by-key:settlement").Should().Be(0);
        fail = false;
        test.RunRequest(_ => { });

        store.GetCallCount("rate-limit-by-key:settlement").Should().Be(2);
        HasPendingCounterResponse(context).Should().BeFalse();
        context.RequestId.Should().Be(requestId);
        inboundRuns.Should().Be(1);
        limiter.Calls.Should().Equal(("settlement", 0), ("settlement", 2), ("settlement", 2));
        test.RunRequest(_ => { });
        store.GetCallCount("rate-limit-by-key:settlement").Should().Be(2);
        limiter.Calls.Should().HaveCount(3);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void PayloadSettlementFailuresPropagateWithoutClosingTheRequest(bool keyed)
    {
        var context = SettlementTest.CreateContext();
        var store = context.Services.Resolve<RateLimitStore>()!;
        var key = keyed ? "quota-by-key:volume" : $"quota:sub:{context.Subscription.Id}";
        var requestId = context.RequestId;
        var inboundRuns = 0;
        var test = new TestDocument(new ExecutionTestDocument
        {
            InboundAction = section =>
            {
                inboundRuns++;
                SettlementTest.ApplyQuota(section, keyed);
            },
            OutboundAction = _ =>
            {
                context.Response.Body.Content = null;
                context.Response.Headers["Content-Length"] = ["invalid"];
            }
        })
        { Context = context };

        Assert.ThrowsExactly<FormatException>(test.RunAll);
        HasPendingCounterResponse(context).Should().BeTrue();
        store.GetBandwidth(key).Should().Be(0);
        context.Response.Headers["Content-Length"] = ["12"];
        test.RunRequest(_ => { });

        store.GetBandwidth(key).Should().Be(12);
        store.GetCallCount(key).Should().Be(1);
        HasPendingCounterResponse(context).Should().BeFalse();
        context.RequestId.Should().Be(requestId);
        inboundRuns.Should().Be(1);
        test.RunRequest(_ => { });
        store.GetBandwidth(key).Should().Be(12);
        store.GetCallCount(key).Should().Be(1);
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(1)]
    [DataRow(2)]
    public void CompletionOnlyProviderRetriesRetainFailedRateAndDoNotReplaySuccessfulPrefix(int failureIndex)
    {
        var context = SettlementTest.CreateContext();
        var store = context.Services.Resolve<RateLimitStore>()!;
        var requestId = context.RequestId;
        var inboundRuns = 0;
        var fail = true;
        var expected = new InvalidOperationException("provider retry required");
        var limiter = new RecordingRateLimiter((key, permits) =>
        {
            if (key == $"operation-{failureIndex}" && permits > 0 && fail)
            {
                throw expected;
            }

            return true;
        });
        context.Services.Register<IRateLimiter>(limiter);
        var test = new TestDocument(new ExecutionTestDocument
        {
            InboundAction = section =>
            {
                inboundRuns++;
                for (var i = 0; i < 3; i++)
                {
                    section.RateLimitByKey(SettlementTest.DeferredRate($"operation-{i}") with { IncrementCount = i + 1 });
                }
            }
        })
        { Context = context };

        Assert.ThrowsExactly<InvalidOperationException>(test.RunAll).Should().BeSameAs(expected);
        HasPendingCounterResponse(context).Should().BeTrue();
        Assert.ThrowsExactly<InvalidOperationException>(() => test.RunRequest(_ => { })).Should().BeSameAs(expected);
        for (var i = 0; i < 3; i++)
        {
            store.GetCallCount($"rate-limit-by-key:operation-{i}").Should().Be(i < failureIndex ? i + 1 : 0);
        }

        context.RequestId = Guid.NewGuid();
        Assert.ThrowsExactly<InvalidOperationException>(() => test.RunRequest(_ => { })).Message.Should().Contain("RequestId");
        HasPendingCounterResponse(context).Should().BeTrue();
        context.RequestId = requestId;
        fail = false;
        test.RunRequest(_ => { });
        test.RunRequest(_ => { });

        for (var i = 0; i < 3; i++)
        {
            store.GetCallCount($"rate-limit-by-key:operation-{i}").Should().Be(i + 1);
            limiter.Calls.Count(call => call.Key == $"operation-{i}" && call.Permits > 0)
                .Should().Be(i == failureIndex ? 3 : 1);
        }

        inboundRuns.Should().Be(1);
        context.RequestId.Should().Be(requestId);
        HasPendingCounterResponse(context).Should().BeFalse();
    }

    [TestMethod]
    [DataRow(false, "invalid")]
    [DataRow(false, "-1")]
    [DataRow(false, "9223372036854775808")]
    [DataRow(true, "invalid")]
    [DataRow(true, "-1")]
    [DataRow(true, "9223372036854775808")]
    public void CompletionOnlyPayloadRetryRetainsQuotaWithoutReplayingSettledRate(bool keyed, string invalidLength)
    {
        var context = SettlementTest.CreateContext();
        var store = context.Services.Resolve<RateLimitStore>()!;
        var limiter = new RecordingRateLimiter();
        context.Services.Register<IRateLimiter>(limiter);
        var requestId = context.RequestId;
        var inboundRuns = 0;
        var quotaKey = keyed ? "quota-by-key:volume" : $"quota:sub:{context.Subscription.Id}";
        context.Request.Body.Content = "caf\u00e9";
        var headers = new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["remaining"] = ["99"],
            ["REMAINING"] = ["98"],
            ["total"] = ["99"]
        };
        context.Response.Headers = headers;
        var test = new TestDocument(new ExecutionTestDocument
        {
            InboundAction = section =>
            {
                inboundRuns++;
                section.RateLimitByKey(SettlementTest.DeferredRate() with
                {
                    RemainingCallsHeaderName = "Remaining",
                    TotalCallsHeaderName = "Total"
                });
                SettlementTest.ApplyQuota(section, keyed);
            },
            OutboundAction = _ =>
            {
                context.Response.Body.Content = null;
                headers["content-length"] = [invalidLength];
            }
        })
        { Context = context };

        Assert.ThrowsExactly<FormatException>(test.RunAll);
        HasPendingCounterResponse(context).Should().BeTrue();
        store.GetCallCount("rate-limit-by-key:settlement").Should().Be(2);
        store.GetCallCount(quotaKey).Should().Be(1);
        store.GetBandwidth(quotaKey).Should().Be(5);
        headers.Keys.Count(key => key.Equals("Remaining", StringComparison.OrdinalIgnoreCase)).Should().Be(1);
        headers["Remaining"].Should().Equal("8");
        headers["Total"].Should().Equal("10");
        Assert.ThrowsExactly<FormatException>(() => test.RunRequest(_ => { }));
        store.GetCallCount("rate-limit-by-key:settlement").Should().Be(2);

        context.RequestId = Guid.NewGuid();
        Assert.ThrowsExactly<InvalidOperationException>(() => test.RunRequest(_ => { })).Message.Should().Contain("RequestId");
        context.RequestId = requestId;
        headers["content-length"] = ["12"];
        test.RunRequest(_ => { });
        test.RunRequest(_ => { });

        context.Response.Headers.Should().BeSameAs(headers);
        headers.Comparer.Should().BeSameAs(StringComparer.Ordinal);
        store.GetCallCount("rate-limit-by-key:settlement").Should().Be(2);
        store.GetCallCount(quotaKey).Should().Be(1);
        store.GetBandwidth(quotaKey).Should().Be(17);
        limiter.Calls.Count(call => call.Key == "settlement" && call.Permits > 0).Should().Be(1);
        HasPendingCounterResponse(context).Should().BeFalse();
        inboundRuns.Should().Be(1);
        context.RequestId.Should().Be(requestId);
    }

    [TestMethod]
    public void CompletionOnlyRetryDoesNotReplaySuccessfulResponseBandwidthOperations()
    {
        var context = SettlementTest.CreateContext();
        var store = context.Services.Resolve<RateLimitStore>()!;
        var clock = new FailingSettlementClock();
        context.Services.Register<TimeProvider>(clock);
        var inboundRuns = 0;
        var test = new TestDocument(new ExecutionTestDocument
        {
            InboundAction = section =>
            {
                inboundRuns++;
                for (var i = 0; i < 3; i++)
                {
                    section.QuotaByKey(new QuotaByKeyConfig
                    {
                        CounterKey = $"response-{i}",
                        Calls = 10,
                        Bandwidth = 1,
                        RenewalPeriod = 300
                    });
                }
            }
        })
        { Context = context };
        test.RunInbound();
        context.Response.Body.Content = null;
        context.Response.Headers["Content-Length"] = ["12"];
        clock.FailAtRead = clock.Reads + 2;

        Assert.ThrowsExactly<InvalidOperationException>(() => test.RunRequest(_ => { })).Should().BeSameAs(clock.Error);
        HasPendingCounterResponse(context).Should().BeTrue();
        store.GetBandwidth("quota-by-key:response-0").Should().Be(12);
        store.GetBandwidth("quota-by-key:response-1").Should().Be(0);
        store.GetBandwidth("quota-by-key:response-2").Should().Be(0);
        clock.FailAtRead = null;
        test.RunRequest(_ => { });
        test.RunRequest(_ => { });

        for (var i = 0; i < 3; i++)
        {
            store.GetBandwidth($"quota-by-key:response-{i}").Should().Be(12);
            store.GetCallCount($"quota-by-key:response-{i}").Should().Be(1);
        }

        HasPendingCounterResponse(context).Should().BeFalse();
        inboundRuns.Should().Be(1);
    }

    [TestMethod]
    public void ReentrantCompletionCannotDrainTheRateOperationBeingSettled()
    {
        var context = SettlementTest.CreateContext();
        var store = context.Services.Resolve<RateLimitStore>()!;
        var reenter = true;
        var inboundRuns = 0;
        context.Services.Register<IRateLimiter>(new RecordingRateLimiter((_, permits) =>
        {
            if (permits > 0 && reenter)
            {
                context.CompleteLimiterResponse();
            }

            return true;
        }));
        var test = new TestDocument(new ExecutionTestDocument
        {
            InboundAction = section =>
            {
                inboundRuns++;
                section.RateLimitByKey(SettlementTest.DeferredRate());
            }
        })
        { Context = context };

        Assert.ThrowsExactly<InvalidOperationException>(test.RunAll).Message.Should().Contain("already");
        HasPendingCounterResponse(context).Should().BeTrue();
        store.GetCallCount("rate-limit-by-key:settlement").Should().Be(0);
        reenter = false;
        test.RunRequest(_ => { });
        test.RunRequest(_ => { });

        HasPendingCounterResponse(context).Should().BeFalse();
        store.GetCallCount("rate-limit-by-key:settlement").Should().Be(2);
        inboundRuns.Should().Be(1);
    }

    [TestMethod]
    public void AllDeferredCountersSettleAfterAVetoBeforeFinalBandwidthIsMeasured()
    {
        var context = SettlementTest.CreateContext();
        var store = context.Services.Resolve<RateLimitStore>()!;
        var limiter = new RecordingRateLimiter((key, permits) => key != "deny" || permits == 0);
        context.Services.Register<IRateLimiter>(limiter);
        context.Request.Body.Content = new string('r', 32);
        var test = new TestDocument(new ExecutionTestDocument
        {
            InboundAction = section =>
            {
                section.RateLimitByKey(SettlementTest.DeferredRate("deny"));
                section.RateLimitByKey(SettlementTest.DeferredRate("allow"));
                SettlementTest.ApplyQuota(section);
            },
            OutboundAction = section => section.SetBody(new string('x', 4096))
        })
        { Context = context };

        test.RunAll();

        context.Response.StatusCode.Should().Be(429);
        context.ResponseTerminated.Should().BeTrue();
        context.Response.Body.Content.Should().BeEmpty();
        store.GetCallCount("rate-limit-by-key:deny").Should().Be(0);
        store.GetCallCount("rate-limit-by-key:allow").Should().Be(2);
        store.GetBandwidth("quota-by-key:volume").Should().Be(32);
        limiter.Calls.Should().Contain([("deny", 2), ("allow", 2)]);
    }

    [TestMethod]
    public void NonLimiterRunAllRemainsReusableWithTheSameRequestId()
    {
        var context = SettlementTest.CreateContext();
        var requestId = context.RequestId;
        var calls = new List<string>();
        var test = new TestDocument(ExecutionTest.FlowDocument("document", calls)) { Context = context };

        test.RunAll();
        test.RunAll();
        test.RunOnError();

        calls.Should().HaveCount(14);
        context.RequestId.Should().Be(requestId);
        context.Services.Resolve<PolicyCounterService>().Should().BeNull();
    }

    [TestMethod]
    public void NullRequestCallbackIsRejectedWithoutExecutingOrCompleting()
    {
        var test = new ExecutionTestDocument().AsTestDocument();

        Assert.ThrowsExactly<ArgumentNullException>(() => test.RunRequest(null!));
    }

    private static bool HasPendingCounterResponse(GatewayContext context)
    {
        var service = context.Services.Resolve<PolicyCounterService>();
        service.Should().NotBeNull();
        var property = typeof(PolicyCounterService).GetProperty("HasPendingResponse", BindingFlags.Instance | BindingFlags.NonPublic);
        property.Should().NotBeNull();
        return property!.GetValue(service).Should().BeOfType<bool>().Subject;
    }

    private sealed class FailingSettlementClock : TimeProvider
    {
        public int Reads { get; private set; }
        public int? FailAtRead { get; set; }
        public InvalidOperationException Error { get; } = new("clock failed during response settlement");

        public override DateTimeOffset GetUtcNow()
        {
            Reads++;
            if (Reads == FailAtRead)
            {
                throw Error;
            }

            return new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        }
    }

    private static void InvokeRetainedPolicy(TestDocument test, Action policy, string invocation)
    {
        switch (invocation)
        {
            case "direct": policy(); break;
            case "request": test.RunRequest(_ => policy()); break;
            case "nested": test.RunRequest(request => request.RunRequest(_ => policy())); break;
            default: throw new ArgumentOutOfRangeException(nameof(invocation), invocation, null);
        }
    }

    class OperationDocument : IDocument
    {
        public void Inbound(IInboundContext context)
        {
            context.Base();
            if (IsFromCompanyIp(context.ExpressionContext))
            {
                if (IsMockSubscription(context.ExpressionContext))
                {
                    context.ReturnResponse(new ReturnResponseConfig
                    {
                        Status = new StatusConfig { Code = 200, Reason = "Mocked OK" },
                    });
                }

                context.AuthenticationBasic("{{username}}", "{{password}}");
            }
            else
            {
                context.AuthenticationManagedIdentity(new ManagedIdentityAuthenticationConfig()
                {
                    Resource = "https://management.azure.com/",
                    OutputTokenVariableName = "testToken",
                });
                context.SetHeader("Authorization", Bearer(context.ExpressionContext));
            }
        }

        public void Backend(IBackendContext context)
        {
            context.ForwardRequest();
        }

        public void Outbound(IOutboundContext context)
        {
            context.Base();
            context.SetBody(FilterSecrets(context.ExpressionContext));
        }

        public bool IsFromCompanyIp(IExpressionContext context)
            => context.Request.IpAddress.StartsWith("10.0.0.");

        public bool IsMockSubscription(IExpressionContext context)
            => context.Subscription.Name.EndsWith("-mock");

        public string Bearer(IExpressionContext context)
            => $"Bearer {context.Variables["testToken"]}";

        [Expression]
        public string FilterSecrets(IExpressionContext context)
        {
            var body = context.Response.Body.As<JObject>();
            foreach (var internalProperty in new string[] { "location", "secret" })
            {
                if (body.ContainsKey(internalProperty))
                {
                    body.Remove(internalProperty);
                }
            }

            return body.ToString();
        }
    }
}