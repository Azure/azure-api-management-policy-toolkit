// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Globalization;
using System.Net;
using System.Text.Json;

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Document;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Data;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Expressions;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Services;

namespace Test.Emulator.Emulator.Policies;

[TestClass]
public class LlmTokenLimitTests
{
    protected virtual bool UseAzureOpenAi => false;

    private string ExpectedPolicy => UseAzureOpenAi
        ? nameof(IInboundContext.AzureOpenAiTokenLimit)
        : nameof(IInboundContext.LlmTokenLimit);

    private static TokenLimitConfig RateConfig => new()
    {
        CounterKey = "customer",
        EstimatePromptToken = false,
        TokensPerMinute = 10,
        RetryAfterHeaderName = "X-Retry",
        RetryAfterVariableName = "retry",
        RemainingTokensHeaderName = "X-Remaining",
        RemainingTokensVariableName = "remaining",
        TokensConsumedHeaderName = "X-Tokens",
        TokensConsumedVariableName = "consumed"
    };

    private static TokenLimitConfig QuotaConfig => RateConfig with
    {
        TokensPerMinute = null,
        TokenQuota = 10,
        TokenQuotaPeriod = "Daily",
        RemainingTokensHeaderName = null,
        RemainingTokensVariableName = null,
        RemainingQuotaTokensHeaderName = "X-Quota",
        RemainingQuotaTokensVariableName = "quota"
    };

    private static TokenLimitConfig BothConfig => RateConfig with
    {
        TokenQuota = 10,
        TokenQuotaPeriod = "Daily",
        RemainingQuotaTokensHeaderName = "X-Quota",
        RemainingQuotaTokensVariableName = "quota"
    };

    [TestMethod]
    public void TokenLimit_ObservesBackendUsageBeforeOutboundAndSettlesOnlyAtCompletion()
    {
        var clock = new TokenClock();
        var store = new TokenLimitCounterStore();
        var test = CreateTest(BothConfig, store, clock,
            backend: section =>
            {
                section.ExpressionContext.Variables["consumed"].Should().Be(0L);
                store.GetRateTokens("customer", clock.GetUtcNow()).Should().Be(0);
                ((GatewayContext)section.ExpressionContext).Response.Headers.Should().NotContainKey("X-Tokens");
                ((GatewayContext)section.ExpressionContext).Response.Body.Content = UsageJson(4, 3);
            },
            outbound: section =>
            {
                section.ExpressionContext.Variables["consumed"].Should().Be(7L);
                store.GetRateTokens("customer", clock.GetUtcNow()).Should().Be(0);
                section.SetBody("""
                    {"usage":{"prompt_tokens":4,"completion_tokens":3,"total_tokens":99,
                    "prompt_tokens_details":{"cached_tokens":2},"completion_tokens_details":{"reasoning_tokens":2}}}
                    """);
            });
        var requestId = test.Context.RequestId;

        test.RunAll();

        store.GetRateTokens("customer", clock.GetUtcNow()).Should().Be(7);
        store.GetQuotaTokens("customer", "Daily", clock.GetUtcNow()).Should().Be(7);
        AssertOutputs(test, 7, 3, 3);
        test.Context.Response.StatusCode.Should().Be(200);
        test.Context.RequestId.Should().Be(requestId);
        test.Context.Response.Body.Content.Should().Contain("\"total_tokens\":99");
    }

    [TestMethod]
    public void TokenLimit_FalseEstimationAllowsTheOverageResponseThenBlocksTheNextRequest()
    {
        var clock = new TokenClock();
        var store = new TokenLimitCounterStore();
        var first = CreateTest(RateConfig, store, clock, backend: SetUsage(8, 5));
        first.Context.Services.Register<ITokenLimitPromptEstimator>(new PromptEstimator(_ =>
            throw new InvalidOperationException("False estimation must not call a tokenizer.")));

        first.RunAll();
        var next = CreateTest(RateConfig, store, clock, backend: _ => Assert.Fail("Blocked backend."));
        next.RunAll();

        first.Context.Response.StatusCode.Should().Be(200);
        first.Context.Response.Body.Content.Should().Be(UsageJson(8, 5));
        AssertOutputs(first, 13, 0);
        next.Context.Response.StatusCode.Should().Be(429);
        next.Context.Response.StatusReason.Should().Be("Too Many Requests");
        next.Context.ResponseTerminated.Should().BeTrue();
        next.Context.Response.Headers["X-Retry"].Should().Equal("60");
        next.Context.Response.Headers.Should().NotContainKey("Retry-After").And.NotContainKey("X-Tokens");
        next.Context.Variables["retry"].Should().Be(60);
        store.GetRateTokens("customer", clock.GetUtcNow()).Should().Be(13);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void TokenLimit_ExactThresholdAllowsThisResponseAndRejectsTheNextAdmission(bool quota)
    {
        var clock = new TokenClock();
        var store = new TokenLimitCounterStore();
        var config = quota ? QuotaConfig : RateConfig;
        var first = CreateTest(config, store, clock, backend: SetUsage(6, 4));
        first.RunAll();
        var next = CreateTest(config, store, clock, backend: _ => Assert.Fail("Blocked backend."));

        next.RunAll();

        first.Context.Response.StatusCode.Should().Be(200);
        next.Context.Response.StatusCode.Should().Be(quota ? 403 : 429);
        next.Context.Response.StatusReason.Should().Be(quota ? "Forbidden" : "Too Many Requests");
        next.Context.ResponseTerminated.Should().BeTrue();
        next.Context.Response.Headers[quota ? "X-Quota" : "X-Remaining"].Should().Equal("0");
        next.Context.Response.Headers["X-Retry"].Should().Equal(quota ? "43200" : "60");
        next.Context.Variables["retry"].Should().Be(quota ? 43200 : 60);
    }

    [TestMethod]
    [DataRow(10, 100, 429)]
    [DataRow(100, 10, 403)]
    [DataRow(10, 10, 429)]
    public void TokenLimit_RateAndQuotaAreEnforcedIndependentlyAndTogether(int rate, int quota, int status)
    {
        var clock = new TokenClock();
        var store = new TokenLimitCounterStore();
        var config = BothConfig with { TokensPerMinute = rate, TokenQuota = quota };
        CreateTest(config, store, clock, backend: SetUsage(6, 4)).RunAll();
        var blocked = CreateTest(config, store, clock, backend: _ => Assert.Fail("Blocked backend."));

        blocked.RunAll();

        blocked.Context.Response.StatusCode.Should().Be(status);
        store.GetRateTokens("customer", clock.GetUtcNow()).Should().Be(10);
        store.GetQuotaTokens("customer", "Daily", clock.GetUtcNow()).Should().Be(10);
    }

    [TestMethod]
    public void TokenLimit_RateResetDoesNotResetTheFixedQuota()
    {
        var clock = new TokenClock();
        var store = new TokenLimitCounterStore();
        CreateTest(BothConfig, store, clock, backend: SetUsage(6, 4)).RunAll();
        clock.Advance(TimeSpan.FromMinutes(1));
        var blocked = CreateTest(BothConfig, store, clock, backend: _ => Assert.Fail("Quota-blocked backend."));

        blocked.RunAll();

        blocked.Context.Response.StatusCode.Should().Be(403);
        store.GetRateTokens("customer", clock.GetUtcNow()).Should().Be(0);
        store.GetQuotaTokens("customer", "Daily", clock.GetUtcNow()).Should().Be(10);
        blocked.Context.Variables["remaining"].Should().Be(10L);
        clock.Set("2026-10-01T00:00:00Z");
        var renewed = CreateTest(BothConfig, store, clock, backend: SetUsage(1, 1));
        renewed.RunAll();
        AssertOutputs(renewed, 2, 8, 8);
    }

    [TestMethod]
    public void TokenLimit_RateUsesAnExactRollingMinuteAndCeilingRetrySeconds()
    {
        var clock = new TokenClock();
        var store = new TokenLimitCounterStore();
        CreateTest(RateConfig, store, clock, backend: SetUsage(4, 2)).RunAll();
        clock.Advance(TimeSpan.FromSeconds(30));
        CreateTest(RateConfig, store, clock, backend: SetUsage(3, 1)).RunAll();
        clock.Advance(TimeSpan.FromMilliseconds(29100));
        var blocked = CreateTest(RateConfig, store, clock);
        blocked.RunAll();
        blocked.Context.Response.StatusCode.Should().Be(429);
        blocked.Context.Response.Headers["X-Retry"].Should().Equal("1");
        clock.Advance(TimeSpan.FromMilliseconds(900));
        var renewed = CreateTest(RateConfig, store, clock, backend: SetUsage(0, 0));

        renewed.RunAll();

        renewed.Context.Response.StatusCode.Should().Be(200);
        AssertOutputs(renewed, 0, 6);
        store.GetRateTokens("customer", clock.GetUtcNow()).Should().Be(4);
        clock.Advance(TimeSpan.FromSeconds(30));
        store.GetRateTokens("customer", clock.GetUtcNow()).Should().Be(0);
    }

    [TestMethod]
    [DataRow("Hourly", "2026-09-30T12:59:59Z", "2026-09-30T13:00:00Z")]
    [DataRow("Daily", "2026-09-30T23:59:59Z", "2026-10-01T00:00:00Z")]
    [DataRow("Daily", "2026-10-01T01:59:59+02:00", "2026-10-01T02:00:00+02:00")]
    [DataRow("Weekly", "2026-09-27T23:59:59Z", "2026-09-28T00:00:00Z")]
    [DataRow("Monthly", "2028-02-29T23:59:59Z", "2028-03-01T00:00:00Z")]
    [DataRow("Monthly", "2026-01-31T23:59:59Z", "2026-02-01T00:00:00Z")]
    [DataRow("Yearly", "2026-12-31T23:59:59Z", "2027-01-01T00:00:00Z")]
    public void TokenLimit_QuotaRenewsAtTheFixedUtcBoundary(string period, string before, string boundary)
    {
        var clock = new TokenClock(ParseTime(before));
        var store = new TokenLimitCounterStore();
        var config = QuotaConfig with { TokenQuotaPeriod = period };
        CreateTest(config, store, clock, backend: SetUsage(6, 4)).RunAll();
        var blocked = CreateTest(config, store, clock);
        blocked.RunAll();
        blocked.Context.Response.StatusCode.Should().Be(403);
        blocked.Context.Response.Headers["X-Retry"].Should().Equal("1");
        clock.Set(boundary);
        var renewed = CreateTest(config, store, clock, backend: SetUsage(1, 0));

        renewed.RunAll();

        renewed.Context.Response.StatusCode.Should().Be(200);
        renewed.Context.Variables["quota"].Should().Be(9L);
        store.GetQuotaTokens("customer", period, clock.GetUtcNow()).Should().Be(1);
    }

    [TestMethod]
    public void TokenLimit_LateCompletionBelongsToTheResponseCompletionWindows()
    {
        var clock = new TokenClock(ParseTime("2026-09-30T23:59:59Z"));
        var store = new TokenLimitCounterStore();
        var test = CreateTest(BothConfig with { EstimatePromptToken = true }, store, clock);
        test.Context.Services.Register<ITokenLimitPromptEstimator>(new PromptEstimator(_ => 4));
        test.RunInbound();
        clock.Advance(TimeSpan.FromSeconds(61));

        test.RunRequest(request =>
        {
            request.RunBackend();
            request.RunOutbound();
        });

        store.GetRateTokens("customer", clock.GetUtcNow()).Should().Be(6);
        store.GetQuotaTokens("customer", "Daily", clock.GetUtcNow()).Should().Be(6);
        AssertOutputs(test, 6, 4, 4);
    }

    [TestMethod]
    public void TokenLimit_WeeklyHistorySurvivesTheNewYearUntilTheMondayBoundary()
    {
        var clock = new TokenClock(ParseTime("2026-12-31T12:00:00Z"));
        var store = new TokenLimitCounterStore();
        var config = QuotaConfig with { TokenQuotaPeriod = "Weekly" };
        CreateTest(config, store, clock, backend: SetUsage(6, 4)).RunAll();
        clock.Set("2027-01-01T12:00:00Z");
        var blocked = CreateTest(config, store, clock);

        blocked.RunAll();

        blocked.Context.Response.StatusCode.Should().Be(403);
        store.GetQuotaTokens("customer", "Weekly", clock.GetUtcNow()).Should().Be(10);
        clock.Set("2027-01-04T00:00:00Z");
        var renewed = CreateTest(config, store, clock);
        renewed.RunAll();
        renewed.Context.Response.StatusCode.Should().Be(200);
        store.GetQuotaTokens("customer", "Weekly", clock.GetUtcNow()).Should().Be(6);
    }

    [TestMethod]
    public void TokenLimit_SharedStoreSharesTheSameKeyAcrossBothPolicyAliases()
    {
        var clock = new TokenClock();
        var store = new TokenLimitCounterStore();
        var first = CreateTest(RateConfig, store, clock, backend: SetUsage(6, 4));
        var same = CreateTest(RateConfig, store, clock, useAzureOpenAi: !UseAzureOpenAi,
            backend: _ => Assert.Fail("Shared-key backend."));
        var other = CreateTest(RateConfig with { CounterKey = "other" }, store, clock);

        first.RunAll();
        same.RunAll();
        other.RunAll();

        same.Context.Response.StatusCode.Should().Be(429);
        other.Context.Response.StatusCode.Should().Be(200);
        store.GetRateTokens("customer", clock.GetUtcNow()).Should().Be(10);
        store.GetRateTokens("other", clock.GetUtcNow()).Should().Be(6);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void TokenLimit_AllScopesAndAliasesShareOneActualChargePerKey(bool nested)
    {
        var clock = new TokenClock();
        var store = new TokenLimitCounterStore();
        var builder = PolicyPipelineBuilder.Create().ConfigureContext(context => Configure(context, store, clock));
        foreach (var scope in Enum.GetValues<PolicyScope>())
        {
            var config = BothConfig with
            {
                TokensPerMinute = 100,
                TokenQuota = 100,
                TokensConsumedVariableName = $"consumed-{scope}",
                RemainingTokensVariableName = $"remaining-{scope}",
                RemainingQuotaTokensVariableName = $"quota-{scope}"
            };
            builder.AddPolicy(scope, new TokenDocument
            {
                InboundAction = section =>
                {
                    Invoke(section, config, scope == PolicyScope.Operation ? !UseAzureOpenAi : UseAzureOpenAi);
                    section.Base();
                },
                BackendAction = section =>
                {
                    section.Base();
                    if (scope == PolicyScope.Operation)
                    {
                        ((GatewayContext)section.ExpressionContext).Response.Body.Content = UsageJson(1, 1);
                    }
                },
                OutboundAction = section =>
                {
                    section.Base();
                    if (scope == PolicyScope.Global)
                    {
                        section.SetBody(UsageJson(8, 3));
                    }
                }
            });
        }
        var pipeline = builder.Build();

        if (nested)
        {
            pipeline.RunAllNested();
        }
        else
        {
            pipeline.RunAll();
        }

        store.GetRateTokens("customer", clock.GetUtcNow()).Should().Be(2);
        store.GetQuotaTokens("customer", "Daily", clock.GetUtcNow()).Should().Be(2);
        foreach (var scope in Enum.GetValues<PolicyScope>())
        {
            pipeline.Context.Variables[$"consumed-{scope}"].Should().Be(2L);
            pipeline.Context.Variables[$"remaining-{scope}"].Should().Be(98L);
            pipeline.Context.Variables[$"quota-{scope}"].Should().Be(98L);
        }
    }

    [TestMethod]
    public void TokenLimit_RateOnlyAndQuotaOnlyPoliciesShareObservedHistory()
    {
        var clock = new TokenClock();
        var store = new TokenLimitCounterStore();
        CreateTest(RateConfig, store, clock).RunAll();
        var quota = CreateTest(QuotaConfig, store, clock, useAzureOpenAi: !UseAzureOpenAi,
            backend: SetUsage(3, 1));

        quota.RunAll();

        quota.Context.Response.StatusCode.Should().Be(200);
        quota.Context.Variables["quota"].Should().Be(0L);
        store.GetRateTokens("customer", clock.GetUtcNow()).Should().Be(10);
        store.GetQuotaTokens("customer", "Daily", clock.GetUtcNow()).Should().Be(10);
        var blocked = CreateTest(QuotaConfig, store, clock);
        blocked.RunAll();
        blocked.Context.Response.StatusCode.Should().Be(403);
    }

    [TestMethod]
    public void TokenLimit_DefaultStoresAreIsolatedAndKeysAreOrdinal()
    {
        var first = CreateTest(RateConfig, backend: SetUsage(6, 4));
        var second = CreateTest(RateConfig);
        first.RunAll();
        second.RunAll();
        second.Context.Response.StatusCode.Should().Be(200);
        var store = new TokenLimitCounterStore();
        var clock = new TokenClock();
        CreateTest(RateConfig, store, clock, backend: SetUsage(6, 4)).RunAll();
        var differentCase = CreateTest(RateConfig with { CounterKey = "Customer" }, store, clock);
        differentCase.RunAll();
        differentCase.Context.Response.StatusCode.Should().Be(200);
    }

    [TestMethod]
    public void TokenLimit_AutoRegistersAnIsolatedStoreWhenNoStoreWasInjected()
    {
        var clock = new TokenClock();
        var context = new GatewayContext();
        context.Services.Register<TimeProvider>(clock);
        var test = new TestDocument(new TokenDocument
        {
            InboundAction = section => Invoke(section, RateConfig, UseAzureOpenAi),
            BackendAction = SetUsage(4, 2)
        })
        { Context = context };

        test.RunAll();

        context.Services.Resolve<TokenLimitCounterStore>().Should().NotBeNull();
        context.Services.Resolve<TokenLimitCounterStore>()!.GetRateTokens("customer", clock.GetUtcNow())
            .Should().Be(6);
        AssertOutputs(test, 6, 4);
    }

    [TestMethod]
    public void TokenLimit_DifferentKeysInOneRequestEachReceiveOneActualCharge()
    {
        var clock = new TokenClock();
        var store = new TokenLimitCounterStore();
        var test = CreateTest(BothConfig, store, clock, inbound: section =>
        {
            Invoke(section, BothConfig, UseAzureOpenAi);
            Invoke(section, BothConfig with { CounterKey = "other" }, !UseAzureOpenAi);
        });

        test.RunAll();

        store.GetRateTokens("customer", clock.GetUtcNow()).Should().Be(6);
        store.GetRateTokens("other", clock.GetUtcNow()).Should().Be(6);
        store.GetQuotaTokens("customer", "Daily", clock.GetUtcNow()).Should().Be(6);
        store.GetQuotaTokens("other", "Daily", clock.GetUtcNow()).Should().Be(6);
    }

    [TestMethod]
    public void TokenLimit_EstimatorIsRequiredRatherThanApproximatingTextLengths()
    {
        var test = CreateTest(RateConfig with { EstimatePromptToken = true });
        test.Context.Request.Body.Content = new string('x', 10000);

        var error = AssertPolicyError<InvalidOperationException>(test, test.RunInbound);

        error.Message.Should().Contain(nameof(ITokenLimitPromptEstimator));
        test.Context.Variables.Should().NotContainKey("consumed");
    }

    [TestMethod]
    public void TokenLimit_UsesTheInjectedPromptEstimateAndReconcilesToActualUsage()
    {
        var clock = new TokenClock();
        var store = new TokenLimitCounterStore();
        var estimator = new PromptEstimator(_ => 4);
        var test = CreateTest(BothConfig with { EstimatePromptToken = true }, store, clock,
            backend: section =>
            {
                section.ExpressionContext.Variables["consumed"].Should().Be(4L);
                section.ExpressionContext.Variables["remaining"].Should().Be(6L);
                section.ExpressionContext.Variables["quota"].Should().Be(6L);
                ((GatewayContext)section.ExpressionContext).Response.Headers.Should().NotContainKey("X-Tokens");
                ((GatewayContext)section.ExpressionContext).Response.Body.Content = UsageJson(3, 3);
            });
        test.Context.Services.Register<ITokenLimitPromptEstimator>(estimator);

        test.RunAll();

        estimator.CallCount.Should().Be(1);
        estimator.LastContext.Should().BeSameAs(test.Context);
        store.GetRateTokens("customer", clock.GetUtcNow()).Should().Be(6);
        AssertOutputs(test, 6, 4, 4);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void TokenLimit_OversizedEstimatedPromptStopsBeforeTheBackend(bool quota)
    {
        var config = (quota ? QuotaConfig : RateConfig) with { EstimatePromptToken = true };
        var test = CreateTest(config, backend: _ => Assert.Fail("Oversized prompt reached backend."));
        test.Context.Services.Register<ITokenLimitPromptEstimator>(new PromptEstimator(_ => 11));

        test.RunAll();

        test.Context.Response.StatusCode.Should().Be(quota ? 403 : 429);
        test.Context.ResponseTerminated.Should().BeTrue();
        test.Context.Response.Headers.Should().NotContainKey("X-Tokens");
        test.Context.Variables["consumed"].Should().Be(0L);
    }

    [TestMethod]
    public void TokenLimit_SameKeyPromptReservationsAreNotDuplicatedAcrossAliases()
    {
        var clock = new TokenClock();
        var store = new TokenLimitCounterStore();
        var config = BothConfig with { EstimatePromptToken = true };
        var test = CreateTest(config, store, clock, inbound: section =>
        {
            Invoke(section, config, UseAzureOpenAi);
            Invoke(section, config, !UseAzureOpenAi);
        }, backend: SetUsage(6, 4));
        test.Context.Services.Register<ITokenLimitPromptEstimator>(new PromptEstimator(_ => 10));

        test.RunAll();

        test.Context.Response.StatusCode.Should().Be(200);
        AssertOutputs(test, 10, 0, 0);
        store.GetRateTokens("customer", clock.GetUtcNow()).Should().Be(10);
    }

    [TestMethod]
    public void TokenLimit_PendingPromptReservationsGateOtherContextsAndAreReleasedAtCompletion()
    {
        var clock = new TokenClock();
        var store = new TokenLimitCounterStore();
        var config = RateConfig with { EstimatePromptToken = true };
        var first = CreateTest(config, store, clock, backend: SetUsage(1, 1));
        var blocked = CreateTest(config, store, clock, backend: _ => Assert.Fail("Reserved backend."));
        first.Context.Services.Register<ITokenLimitPromptEstimator>(new PromptEstimator(_ => 6));
        blocked.Context.Services.Register<ITokenLimitPromptEstimator>(new PromptEstimator(_ => 6));
        first.RunInbound();

        blocked.RunAll();
        first.RunRequest(request => request.RunBackend());

        blocked.Context.Response.StatusCode.Should().Be(429);
        first.Context.Variables["consumed"].Should().Be(2L);
        first.Context.Variables["remaining"].Should().Be(8L);
        var next = CreateTest(config, store, clock, backend: SetUsage(1, 1));
        next.Context.Services.Register<ITokenLimitPromptEstimator>(new PromptEstimator(_ => 6));
        next.RunAll();
        next.Context.Response.StatusCode.Should().Be(200);
        store.GetRateTokens("customer", clock.GetUtcNow()).Should().Be(4);
    }

    [TestMethod]
    public void TokenLimit_RejectingAnotherKeyReleasesEarlierPromptReservations()
    {
        var clock = new TokenClock();
        var store = new TokenLimitCounterStore();
        var config = RateConfig with { EstimatePromptToken = true };
        var test = CreateTest(config, store, clock, inbound: section =>
        {
            Invoke(section, config, UseAzureOpenAi);
            Invoke(section, config with { CounterKey = "other", TokensPerMinute = 2 }, !UseAzureOpenAi);
        }, backend: _ => Assert.Fail("Rejected request reached backend."));
        test.Context.Services.Register<ITokenLimitPromptEstimator>(new PromptEstimator(_ => 3));

        test.RunAll();
        var next = CreateTest(config, store, clock);
        next.Context.Services.Register<ITokenLimitPromptEstimator>(new PromptEstimator(_ => 10));
        next.RunAll();

        test.Context.Response.StatusCode.Should().Be(429);
        next.Context.Response.StatusCode.Should().Be(200);
        store.GetRateTokens("customer", clock.GetUtcNow()).Should().Be(6);
        store.GetRateTokens("other", clock.GetUtcNow()).Should().Be(0);
    }

    [TestMethod]
    [DataRow(-1L)]
    public void TokenLimit_InvalidEstimateIsExplicitAndDoesNotReserve(long estimate)
    {
        var test = CreateTest(RateConfig with { EstimatePromptToken = true });
        test.Context.Services.Register<ITokenLimitPromptEstimator>(new PromptEstimator(_ => estimate));

        AssertPolicyError<ArgumentOutOfRangeException>(test, test.RunInbound);
        test.Context.Variables.Should().NotContainKey("consumed");
    }

    [TestMethod]
    public void TokenLimit_EstimatorFailureIsSurfaced()
    {
        var test = CreateTest(RateConfig with { EstimatePromptToken = true });
        test.Context.Services.Register<ITokenLimitPromptEstimator>(new PromptEstimator(_ =>
            throw new InvalidOperationException("Tokenizer failed.")));

        AssertPolicyError<InvalidOperationException>(test, test.RunInbound).Message.Should().Be("Tokenizer failed.");
    }

    [TestMethod]
    [DataRow("""{"usage":{"prompt_tokens":4,"completion_tokens":2,"total_tokens":90}}""")]
    [DataRow("""{"usage":{"input_tokens":4,"output_tokens":2}}""")]
    [DataRow("""{"usage":{"input_tokens":4,"output_tokens":2,"cache_read_input_tokens":9}}""")]
    [DataRow("""{"usageMetadata":{"promptTokenCount":4,"candidatesTokenCount":2,"thoughtsTokenCount":9}}""")]
    public void TokenLimit_UsesActualSupportedJsonUsageAndNeverDoubleCountsOtherCategories(string body)
    {
        var test = CreateTest(RateConfig, backend: section =>
            ((GatewayContext)section.ExpressionContext).Response.Body.Content = body);
        var provider = new UsageProvider(_ => throw new InvalidOperationException("JSON usage takes precedence."));
        test.Context.Services.Register<ILlmTokenUsageProvider>(provider);

        test.RunAll();

        AssertOutputs(test, 6, 4);
        provider.CallCount.Should().Be(0);
        test.Context.Response.Body.Content.Should().Be(body);
    }

    [TestMethod]
    public void TokenLimit_ObservedProviderRunsOnceBeforeOutboundNotDuringSettlement()
    {
        var test = CreateTest(RateConfig, backend: section =>
        {
            var context = (GatewayContext)section.ExpressionContext;
            context.Response.Headers["X-Backend"] = ["done"];
            context.Response.Body.Content = """{"answer":"backend"}""";
        }, outbound: section =>
        {
            section.ExpressionContext.Variables["consumed"].Should().Be(5L);
            section.SetBody("""{"answer":"final"}""");
        });
        var provider = new UsageProvider(context =>
        {
            context.Response.Body.Content.Should().Be("""{"answer":"backend"}""");
            context.Response.Headers["X-Backend"].Should().Equal("done");
            return new LlmTokenUsage
            {
                PromptTokens = 3,
                CompletionTokens = 2,
                TotalTokens = 99,
                AdditionalTokens = new Dictionary<string, long> { ["Reasoning Tokens"] = 7 }
            };
        });
        test.Context.Services.Register<ILlmTokenUsageProvider>(provider);

        test.RunRequest(request =>
        {
            request.RunAll();
            provider.CallCount.Should().Be(1);
            request.RunRequest(_ => provider.CallCount.Should().Be(1));
        });
        test.RunRequest(_ => { });

        provider.CallCount.Should().Be(1);
        provider.LastContext.Should().BeSameAs(test.Context);
        AssertOutputs(test, 5, 5);
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("{}")]
    [DataRow("""{"usage":null}""")]
    [DataRow("""{"usage":{}}""")]
    public void TokenLimit_MissingUsageIsExplicitAndCanBeCorrectedBeforeRetryingCompletion(string? body)
    {
        var clock = new TokenClock();
        var store = new TokenLimitCounterStore();
        var test = CreateTest(RateConfig, store, clock, backend: section =>
            ((GatewayContext)section.ExpressionContext).Response.Body.Content = body);

        var error = AssertPolicyError<InvalidOperationException>(test, test.RunAll);

        error.Message.Should().Contain(nameof(ILlmTokenUsageProvider));
        store.GetRateTokens("customer", clock.GetUtcNow()).Should().Be(0);
        test.Context.Services.Resolve<TokenLimitService>()!.HasPendingResponse.Should().BeTrue();
        test.Context.Response.Body.Content = UsageJson(2, 1);
        test.RunRequest(_ => { });
        AssertOutputs(test, 3, 7);
    }

    [TestMethod]
    public void TokenLimit_NullOrFailingUsageProvidersDoNotInventZeroUsage()
    {
        var test = CreateTest(RateConfig, backend: _ => { });
        test.Context.Services.Register<ILlmTokenUsageProvider>(new UsageProvider(_ => null));
        AssertPolicyError<InvalidOperationException>(test, test.RunAll);
        test.Context.Services.Register<ILlmTokenUsageProvider>(new UsageProvider(_ =>
            throw new InvalidOperationException("Usage provider failed.")));
        AssertPolicyError<InvalidOperationException>(test, () => test.RunRequest(_ => { }))
            .Message.Should().Be("Usage provider failed.");
        test.Context.Services.Resolve<TokenLimitService>()!.HasPendingResponse.Should().BeTrue();
    }

    [TestMethod]
    [DataRow("""{"usage":{"total_tokens":6}}""")]
    [DataRow("""{"usage":{"prompt_tokens":6}}""")]
    [DataRow("""{"usage":{"completion_tokens":6}}""")]
    [DataRow("""{"usage":{"reasoning_tokens":6}}""")]
    [DataRow("""{"usage":{"prompt_tokens":-1,"completion_tokens":2}}""")]
    [DataRow("""{"usage":{"prompt_tokens":1,"completion_tokens":1.5}}""")]
    [DataRow("""{"usage":{"prompt_tokens":"1","completion_tokens":2}}""")]
    [DataRow("""{"usage":{"prompt_tokens":1,"input_tokens":1,"completion_tokens":2}}""")]
    [DataRow("""{"usage":{"prompt_tokens":1,"completion_tokens":2,"completion_tokens_details":[]}}""")]
    [DataRow("""{"usageMetadata":{"promptTokenCount":1,"candidatesTokenCount":null}}""")]
    public void TokenLimit_InvalidOrIncompleteUsageDoesNotFallBackOrMutateCounters(string body)
    {
        var clock = new TokenClock();
        var store = new TokenLimitCounterStore();
        var test = CreateTest(BothConfig, store, clock, backend: section =>
            ((GatewayContext)section.ExpressionContext).Response.Body.Content = body);
        var provider = new UsageProvider(_ => new LlmTokenUsage { PromptTokens = 1, CompletionTokens = 1 });
        test.Context.Services.Register<ILlmTokenUsageProvider>(provider);

        AssertPolicyError<ArgumentException>(test, test.RunAll);

        provider.CallCount.Should().Be(0);
        store.GetRateTokens("customer", clock.GetUtcNow()).Should().Be(0);
        store.GetQuotaTokens("customer", "Daily", clock.GetUtcNow()).Should().Be(0);
        test.Context.Response.Body.Content.Should().Be(body);
    }

    [TestMethod]
    [DataRow("{")]
    [DataRow("not-json")]
    public void TokenLimit_MalformedJsonDoesNotFallBack(string body)
    {
        var test = CreateTest(RateConfig, backend: section =>
            ((GatewayContext)section.ExpressionContext).Response.Body.Content = body);
        var provider = new UsageProvider(_ => new LlmTokenUsage { PromptTokens = 1, CompletionTokens = 1 });
        test.Context.Services.Register<ILlmTokenUsageProvider>(provider);

        AssertPolicyError<JsonException>(test, test.RunAll);

        provider.CallCount.Should().Be(0);
    }

    [TestMethod]
    [DataRow("[]")]
    [DataRow("null")]
    [DataRow("3")]
    public void TokenLimit_UnsupportedResponseShapeIsExplicit(string body)
    {
        var test = CreateTest(RateConfig, backend: section =>
            ((GatewayContext)section.ExpressionContext).Response.Body.Content = body);

        AssertPolicyError<ArgumentException>(test, test.RunAll);
    }

    [TestMethod]
    [DynamicData(nameof(InvalidProviderUsages))]
    public void TokenLimit_InvalidObservedProviderUsageIsAtomic(LlmTokenUsage usage)
    {
        var clock = new TokenClock();
        var store = new TokenLimitCounterStore();
        var test = CreateTest(BothConfig, store, clock, backend: _ => { });
        test.Context.Services.Register<ILlmTokenUsageProvider>(new UsageProvider(_ => usage));

        AssertPolicyError<ArgumentException>(test, test.RunAll);

        store.GetRateTokens("customer", clock.GetUtcNow()).Should().Be(0);
        store.GetQuotaTokens("customer", "Daily", clock.GetUtcNow()).Should().Be(0);
    }

    [TestMethod]
    public void TokenLimit_ObservedCountsLargerThanInt32RemainExact()
    {
        var clock = new TokenClock();
        var store = new TokenLimitCounterStore();
        var test = CreateTest(BothConfig with { TokensPerMinute = int.MaxValue, TokenQuota = int.MaxValue },
            store, clock, backend: SetUsage(int.MaxValue, int.MaxValue));

        test.RunAll();

        AssertOutputs(test, 4_294_967_294L, 0, 0);
        store.GetRateTokens("customer", clock.GetUtcNow()).Should().Be(4_294_967_294L);
        store.GetQuotaTokens("customer", "Daily", clock.GetUtcNow()).Should().Be(4_294_967_294L);
    }

    [TestMethod]
    [DataRow("IInboundContext", true)]
    [DataRow("IFragmentContext", true)]
    [DataRow("IBackendContext", false)]
    [DataRow("IOutboundContext", false)]
    [DataRow("IOnErrorContext", false)]
    public void TokenLimit_PreservesTheAuthoredSectionOnlyApi(string sectionName, bool available)
    {
        var section = typeof(IInboundContext).Assembly.GetType(
            $"Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring.{sectionName}")!;

        (section.GetMethod(ExpectedPolicy) is not null).Should().Be(available);
    }

    [TestMethod]
    public void TokenLimit_UsageArithmeticOverflowIsExplicitAndAtomic()
    {
        var clock = new TokenClock();
        var store = new TokenLimitCounterStore();
        var test = CreateTest(BothConfig, store, clock, backend: _ => { });
        test.Context.Services.Register<ILlmTokenUsageProvider>(new UsageProvider(_ =>
            new LlmTokenUsage { PromptTokens = long.MaxValue, CompletionTokens = 1 }));

        AssertPolicyError<OverflowException>(test, test.RunAll);

        store.GetRateTokens("customer", clock.GetUtcNow()).Should().Be(0);
        store.GetQuotaTokens("customer", "Daily", clock.GetUtcNow()).Should().Be(0);
    }

    [TestMethod]
    public void TokenLimit_ZeroObservedUsageDoesNotConsumeAnything()
    {
        var clock = new TokenClock();
        var store = new TokenLimitCounterStore();
        var test = CreateTest(BothConfig, store, clock, backend: SetUsage(0, 0));
        test.RunAll();
        NextRequest(test);

        test.RunAll();

        AssertOutputs(test, 0, 10, 10);
        store.GetRateTokens("customer", clock.GetUtcNow()).Should().Be(0);
    }

    [TestMethod]
    public void TokenLimit_FailedResponseWithObservedUsageIsCountedWithoutReplacingTheFailure()
    {
        var test = CreateTest(RateConfig, backend: section =>
        {
            var context = (GatewayContext)section.ExpressionContext;
            context.Response.StatusCode = 503;
            context.Response.StatusReason = "Backend Unavailable";
            context.Response.Body.Content = UsageJson(3, 1);
        });

        test.RunAll();

        test.Context.Response.StatusCode.Should().Be(503);
        test.Context.Response.StatusReason.Should().Be("Backend Unavailable");
        AssertOutputs(test, 4, 6);
    }

    [TestMethod]
    public void TokenLimit_FailureWithoutUsageRequiresExplicitObservedZeroRatherThanAssumingIt()
    {
        var test = CreateTest(RateConfig, backend: section =>
        {
            var context = (GatewayContext)section.ExpressionContext;
            context.Response.StatusCode = 500;
            context.Response.Body.Content = """{"error":"backend failed"}""";
        });
        AssertPolicyError<InvalidOperationException>(test, test.RunAll);
        test.Context.Services.Register<ILlmTokenUsageProvider>(new UsageProvider(_ =>
            new LlmTokenUsage { PromptTokens = 0, CompletionTokens = 0 }));

        test.RunRequest(_ => { });

        test.Context.Response.StatusCode.Should().Be(500);
        test.Context.Response.Body.Content.Should().Be("""{"error":"backend failed"}""");
        AssertOutputs(test, 0, 10);
    }

    [TestMethod]
    [DataRow("inbound")]
    [DataRow("backend")]
    [DataRow("outbound")]
    [DataRow("on-error")]
    public void TokenLimit_EarlyTerminalResponsesSettleAtTheOuterBoundary(string sectionName)
    {
        var config = RateConfig;
        var response = new ReturnResponseConfig
        {
            Status = new StatusConfig { Code = 202, Reason = "Accepted" },
            Headers = [new HeaderConfig { Name = "X-Final", Values = ["retained"] }],
            Body = new BodyConfig { Content = UsageJson(2, 1) }
        };
        var test = CreateTest(config, inbound: section =>
        {
            Invoke(section, config, UseAzureOpenAi);
            if (sectionName == "inbound") section.ReturnResponse(response);
        }, backend: section =>
        {
            if (sectionName == "backend") section.ReturnResponse(response);
            ((GatewayContext)section.ExpressionContext).Response.Body.Content = UsageJson(1, 1);
        }, outbound: section =>
        {
            if (sectionName == "outbound") section.ReturnResponse(response);
        }, onError: section => section.ReturnResponse(response));

        if (sectionName == "on-error")
        {
            test.RunRequest(request =>
            {
                request.RunInbound();
                request.RunOnError();
            });
        }
        else
        {
            test.RunAll();
        }

        test.Context.Response.StatusCode.Should().Be(202);
        test.Context.ResponseTerminated.Should().BeTrue();
        test.Context.Response.Headers["X-Final"].Should().Equal("retained");
        test.Context.Response.Body.Content.Should().Be(UsageJson(2, 1));
        if (sectionName is "inbound" or "backend" or "on-error")
        {
            test.Context.Variables["consumed"].Should().Be(0L);
            test.Context.Variables["remaining"].Should().Be(10L);
            test.Context.Response.Headers.Should().NotContainKey("X-Tokens");
        }
        else
        {
            AssertOutputs(test, 2, 8);
        }
    }

    [TestMethod]
    public void TokenLimit_EarlyTerminalWithoutBackendDoesNotRequireAnObservedZeroFixture()
    {
        var test = CreateTest(RateConfig, inbound: section =>
        {
            Invoke(section, RateConfig, UseAzureOpenAi);
            section.ReturnResponse(new ReturnResponseConfig
            {
                Status = new StatusConfig { Code = 202, Reason = "Accepted" }
            });
        }, backend: _ => Assert.Fail("Terminal response reached backend."));
        test.RunAll();

        test.Context.Response.StatusCode.Should().Be(202);
        test.Context.ResponseTerminated.Should().BeTrue();
        test.Context.Variables["consumed"].Should().Be(0L);
        test.Context.Variables["remaining"].Should().Be(10L);
        test.Context.Response.Headers.Should().NotContainKey("X-Tokens");
    }

    [TestMethod]
    public void TokenLimit_InvokeRequestRemainsSectionOnlyAndCountsActualBackendUsage()
    {
        var config = RateConfig;
        var test = CreateTest(config, inbound: section =>
        {
            Invoke(section, config, UseAzureOpenAi);
            section.InvokeRequest(new InvokeRequestConfig());
            section.SetVariable("after-invoke", true);
        }, outbound: section => section.SetBody(UsageJson(3, 1)));
        test.SetupInbound().InvokeRequest().WithCallback((context, _) =>
            context.Response.Body.Content = UsageJson(1, 1));

        test.RunAll();

        test.Context.ResponseTerminated.Should().BeFalse();
        test.Context.Variables.Should().NotContainKey("after-invoke");
        AssertOutputs(test, 6, 4);
    }

    [TestMethod]
    public void TokenLimit_UnhandledErrorsLeavePendingUsageUntilOnErrorCompletes()
    {
        var clock = new TokenClock();
        var store = new TokenLimitCounterStore();
        var expected = new InvalidOperationException("Backend failed.");
        var test = CreateTest(RateConfig, store, clock, backend: _ => throw expected,
            onError: section => section.SetBody(UsageJson(3, 1)));
        var requestId = test.Context.RequestId;
        Assert.ThrowsExactly<InvalidOperationException>(test.RunAll).Should().BeSameAs(expected);
        store.GetRateTokens("customer", clock.GetUtcNow()).Should().Be(0);
        test.Context.Services.Resolve<TokenLimitService>()!.HasPendingResponse.Should().BeTrue();
        test.Context.RequestId = Guid.NewGuid();
        AssertPolicyError<InvalidOperationException>(test, test.RunInbound)
            .Message.Should().Contain("RequestId");
        test.Context.RequestId = requestId;

        test.RunRequest(request => request.RunRequest(inner => inner.RunOnError()));

        test.Context.RequestId.Should().Be(requestId);
        AssertOutputs(test, 4, 6);
        store.GetRateTokens("customer", clock.GetUtcNow()).Should().Be(4);
    }

    [TestMethod]
    public void TokenLimit_FinalOnErrorPostprocessingDoesNotReplaceObservedBackendUsage()
    {
        var test = CreateTest(RateConfig, onError: section => section.SetBody(UsageJson(6, 2)));

        test.RunRequest(request =>
        {
            request.RunAll();
            request.Context.Variables["consumed"].Should().Be(6L);
            request.RunOnError();
        });

        AssertOutputs(test, 6, 4);
    }

    [TestMethod]
    public void TokenLimit_StandaloneSectionsRequireAnExplicitCompletionBoundary()
    {
        var clock = new TokenClock();
        var store = new TokenLimitCounterStore();
        var test = CreateTest(RateConfig, store, clock);
        test.RunInbound();
        test.RunBackend();
        test.RunOutbound();
        store.GetRateTokens("customer", clock.GetUtcNow()).Should().Be(0);
        test.Context.Variables["consumed"].Should().Be(6L);

        test.RunRequest(_ => { });

        store.GetRateTokens("customer", clock.GetUtcNow()).Should().Be(6);
        AssertOutputs(test, 6, 4);
    }

    [TestMethod]
    public void TokenLimit_DuplicateCompletionIsIdempotentAndAFreshRequestIdCountsAgain()
    {
        var clock = new TokenClock();
        var store = new TokenLimitCounterStore();
        var test = CreateTest(RateConfig with { TokensPerMinute = 100 }, store, clock);
        test.RunAll();
        var firstId = test.Context.RequestId;
        test.Context.Response.Body.Content = UsageJson(80, 20);
        test.RunRequest(_ => { });
        test.Context.Services.Resolve<TokenLimitService>()!.CompleteResponse();
        store.GetRateTokens("customer", clock.GetUtcNow()).Should().Be(6);
        test.Context.Variables["consumed"].Should().Be(6L);
        NextRequest(test);

        test.RunAll();

        test.Context.RequestId.Should().NotBe(firstId);
        store.GetRateTokens("customer", clock.GetUtcNow()).Should().Be(12);
        test.Context.Variables["consumed"].Should().Be(6L);
    }

    [TestMethod]
    [DataRow("direct")]
    [DataRow("request")]
    [DataRow("nested")]
    public void TokenLimit_LateRetainedProxyWorkCannotRunMockPredicatesOrMutateDeepState(string invocation)
    {
        var config = RateConfig;
        IInboundContext? retained = null;
        var test = CreateTest(config, inbound: section =>
        {
            retained = section;
            Invoke(section, config, UseAzureOpenAi);
        });
        var predicates = 0;
        var callbacks = 0;
        SetupCallback(test, (_, candidate) =>
        {
            predicates++;
            return candidate.CounterKey == "late";
        }, (_, _) => callbacks++);
        test.RunAll();
        var body = test.Context.Response.Body.Content;
        var headers = test.Context.Response.Headers.ToDictionary(pair => pair.Key, pair => pair.Value.ToArray());
        var variables = test.Context.Variables.ToArray();
        var requestId = test.Context.RequestId;
        Action late = () => Invoke(retained!, config with { CounterKey = "late" }, UseAzureOpenAi);

        var error = Assert.ThrowsExactly<InvalidOperationException>(() =>
        {
            if (invocation == "direct") late();
            else if (invocation == "request") test.RunRequest(_ => late());
            else test.RunRequest(request => request.RunRequest(_ => late()));
        });

        error.Message.Should().Contain("completed").And.Contain("RequestId");
        predicates.Should().Be(1);
        callbacks.Should().Be(0);
        test.Context.Response.Body.Content.Should().Be(body);
        test.Context.Response.Headers.Should().BeEquivalentTo(headers);
        test.Context.Variables.Should().Equal(variables);
        test.Context.RequestId.Should().Be(requestId);
        test.RunRequest(_ => { });
    }

    [TestMethod]
    public void TokenLimit_RequestIdCannotChangeDuringRequestExecution()
    {
        var clock = new TokenClock();
        var store = new TokenLimitCounterStore();
        var test = CreateTest(RateConfig, store, clock, backend: section =>
            ((GatewayContext)section.ExpressionContext).RequestId = Guid.NewGuid());

        Assert.ThrowsExactly<InvalidOperationException>(test.RunAll).Message.Should().Contain("RequestId");

        store.GetRateTokens("customer", clock.GetUtcNow()).Should().Be(0);
    }

    [TestMethod]
    public void TokenLimit_UsageProviderCannotChangeRequestIdentityOrRegisterLateWork()
    {
        var clock = new TokenClock();
        var store = new TokenLimitCounterStore();
        IInboundContext? retained = null;
        var test = CreateTest(RateConfig, store, clock, inbound: section =>
        {
            retained = section;
            Invoke(section, RateConfig, UseAzureOpenAi);
        }, backend: _ => { });
        var requestId = test.Context.RequestId;
        test.Context.Services.Register<ILlmTokenUsageProvider>(new UsageProvider(context =>
        {
            context.RequestId = Guid.NewGuid();
            return new LlmTokenUsage { PromptTokens = 1, CompletionTokens = 1 };
        }));
        AssertPolicyError<InvalidOperationException>(test, test.RunAll).Message.Should().Contain("RequestId");
        store.GetRateTokens("customer", clock.GetUtcNow()).Should().Be(0);
        test.Context.RequestId = requestId;
        test.Context.Services.Register<ILlmTokenUsageProvider>(new UsageProvider(_ =>
        {
            Invoke(retained!, RateConfig with { CounterKey = "late" }, UseAzureOpenAi);
            return new LlmTokenUsage { PromptTokens = 1, CompletionTokens = 1 };
        }));

        AssertPolicyError<InvalidOperationException>(test, () => test.RunRequest(_ => { }))
            .Message.Should().Contain("settlement");
        store.GetRateTokens("customer", clock.GetUtcNow()).Should().Be(0);
        store.GetRateTokens("late", clock.GetUtcNow()).Should().Be(0);
    }

    [TestMethod]
    public async Task TokenLimit_ConcurrentExecutionOnOneContextIsRejected()
    {
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var test = CreateTest(RateConfig, backend: section =>
        {
            entered.Set();
            release.Wait(TimeSpan.FromSeconds(10)).Should().BeTrue();
            ((GatewayContext)section.ExpressionContext).Response.Body.Content = UsageJson(2, 1);
        });
        var running = Task.Run(test.RunAll);
        try
        {
            entered.Wait(TimeSpan.FromSeconds(10)).Should().BeTrue();
            Assert.ThrowsExactly<InvalidOperationException>(test.RunAll)
                .Message.Should().Contain("Concurrent");
        }
        finally
        {
            release.Set();
            await running;
        }

        AssertOutputs(test, 3, 7);
    }

    [TestMethod]
    public void TokenLimit_ConcurrentUnestimatedRequestsMayOverrunButSettlementIsAtomic()
    {
        var clock = new TokenClock();
        var store = new TokenLimitCounterStore();
        var requests = Enumerable.Range(0, 16).Select(_ => CreateTest(BothConfig, store, clock)).ToArray();
        foreach (var test in requests) test.RunInbound();

        Parallel.ForEach(requests, test => test.RunRequest(request => request.RunBackend()));

        requests.Should().OnlyContain(test => test.Context.Response.StatusCode == 200);
        store.GetRateTokens("customer", clock.GetUtcNow()).Should().Be(96);
        store.GetQuotaTokens("customer", "Daily", clock.GetUtcNow()).Should().Be(96);
        var next = CreateTest(BothConfig, store, clock);
        next.RunAll();
        next.Context.Response.StatusCode.Should().Be(429);
    }

    [TestMethod]
    public void TokenLimit_ConcurrentEstimatedAdmissionsReserveAtomically()
    {
        var clock = new TokenClock();
        var store = new TokenLimitCounterStore();
        var requests = Enumerable.Range(0, 16).Select(_ =>
        {
            var test = CreateTest(BothConfig with { EstimatePromptToken = true }, store, clock,
                backend: SetUsage(2, 1));
            test.Context.Services.Register<ITokenLimitPromptEstimator>(new PromptEstimator(_ => 3));
            return test;
        }).ToArray();

        Parallel.ForEach(requests, test => test.RunInbound());

        requests.Count(test => test.Context.Response.StatusCode == 200).Should().Be(3);
        foreach (var test in requests)
        {
            test.RunRequest(request =>
            {
                if (!request.Context.ResponseTerminated) request.RunBackend();
            });
        }
        store.GetRateTokens("customer", clock.GetUtcNow()).Should().Be(9);
        store.GetQuotaTokens("customer", "Daily", clock.GetUtcNow()).Should().Be(9);
    }

    [TestMethod]
    public void TokenLimit_SettlesBeforeExistingDeferredLimiterCanReplaceTheResponse()
    {
        var clock = new TokenClock();
        var store = new TokenLimitCounterStore();
        var callStore = new RateLimitStore();
        var limiter = new RecordingRateLimiter((key, permits) => key != "calls" || permits == 0);
        var test = CreateTest(BothConfig, store, clock, inbound: section =>
        {
            Invoke(section, BothConfig, UseAzureOpenAi);
            section.RateLimitByKey(new RateLimitByKeyConfig
            {
                CounterKey = "calls",
                Calls = 10,
                RenewalPeriod = 60,
                IncrementAfterResponse = true,
                RemainingCallsVariableName = "calls-remaining"
            });
            section.QuotaByKey(new QuotaByKeyConfig
            {
                CounterKey = "bytes",
                Calls = 100,
                Bandwidth = 10,
                RenewalPeriod = 60
            });
        }, backend: SetUsage(3, 1), outbound: section => section.SetBody(UsageJson(30, 10)));
        test.Context.Services.Register(callStore).Register<IRateLimiter>(limiter);

        test.RunAll();

        test.Context.Response.StatusCode.Should().Be(429);
        test.Context.Response.Body.Content.Should().BeEmpty();
        test.Context.Variables["consumed"].Should().Be(4L);
        store.GetRateTokens("customer", clock.GetUtcNow()).Should().Be(4);
        store.GetQuotaTokens("customer", "Daily", clock.GetUtcNow()).Should().Be(4);
        callStore.GetCallCount("rate-limit-by-key:calls").Should().Be(0);
        callStore.GetBandwidth("quota-by-key:bytes")
            .Should().Be(System.Text.Encoding.UTF8.GetByteCount(test.Context.Request.Body.Content!));
    }

    [TestMethod]
    public void TokenLimit_UsageFailureDoesNotPrematurelySettleExistingLimiters()
    {
        var callStore = new RateLimitStore();
        var test = CreateTest(RateConfig, inbound: section =>
        {
            Invoke(section, RateConfig, UseAzureOpenAi);
            section.RateLimitByKey(new RateLimitByKeyConfig
            {
                CounterKey = "calls",
                Calls = 10,
                RenewalPeriod = 60,
                IncrementAfterResponse = true
            });
        }, backend: _ => { });
        test.Context.Services.Register(callStore);

        AssertPolicyError<InvalidOperationException>(test, test.RunAll);

        callStore.GetCallCount("rate-limit-by-key:calls").Should().Be(0);
        test.Context.Response.Body.Content = UsageJson(2, 1);
        test.RunRequest(_ => { });
        callStore.GetCallCount("rate-limit-by-key:calls").Should().Be(1);
        AssertOutputs(test, 3, 7);
    }

    [TestMethod]
    public void TokenLimit_RegisteredDependenciesDoNotMakeNoPolicyRequestsFail()
    {
        var test = CreateTest(RateConfig, inbound: _ => { }, backend: _ => { });
        test.Context.Services.Register<ILlmTokenUsageProvider>(new UsageProvider(_ =>
            throw new InvalidOperationException("No token policy was invoked.")));
        test.Context.Services.Register<ITokenLimitPromptEstimator>(new PromptEstimator(_ =>
            throw new InvalidOperationException("No token policy was invoked.")));
        test.Context.Services.Register(new TokenLimitService(test.Context));

        test.RunAll();
        test.RunAll();

        test.Context.Response.StatusCode.Should().Be(200);
        test.Context.Variables.Should().NotContainKey("consumed");
    }

    [TestMethod]
    public void TokenLimit_RequestLocalServiceCannotBeSharedAcrossGatewayContexts()
    {
        var owner = new GatewayContext();
        var test = CreateTest(RateConfig);
        test.Context.Services.Register(new TokenLimitService(owner));

        AssertPolicyError<InvalidOperationException>(test, test.RunInbound)
            .Message.Should().Contain("another").And.Contain(nameof(TokenLimitCounterStore));
    }

    [TestMethod]
    public void TokenLimit_ReplacingAPendingStoreIsAnExplicitError()
    {
        var test = CreateTest(RateConfig);
        test.RunInbound();
        test.RunBackend();
        test.Context.Services.Register(new TokenLimitCounterStore());

        AssertPolicyError<InvalidOperationException>(test, () => test.RunRequest(_ => { }))
            .Message.Should().Contain(nameof(TokenLimitCounterStore));
    }

    [TestMethod]
    public void TokenLimit_ReplacingAPendingClockIsAnExplicitError()
    {
        var test = CreateTest(RateConfig);
        test.RunInbound();
        test.RunBackend();
        test.Context.Services.Register<TimeProvider>(new TokenClock());

        AssertPolicyError<InvalidOperationException>(test, () => test.RunRequest(_ => { }))
            .Message.Should().Contain(nameof(TimeProvider));
    }

    [TestMethod]
    public void TokenLimit_CallbackOverridesInvalidConfigAndMissingDependencies()
    {
        var config = RateConfig with { CounterKey = "", EstimatePromptToken = true, TokensPerMinute = -1 };
        var test = CreateTest(config, backend: _ => { });
        test.Context.Request.Body.Content = "{";
        SetupCallback(test, (_, _) => true, (context, actual) =>
        {
            actual.Should().BeSameAs(config);
            context.Variables["callback"] = true;
        });

        test.RunAll();

        test.Context.Variables["callback"].Should().Be(true);
        test.Context.Services.Resolve<TokenLimitService>().Should().BeNull();
    }

    [TestMethod]
    public void TokenLimit_PredicateMocksSelectOnlyMatchingInvocations()
    {
        var config = RateConfig;
        var test = CreateTest(config, inbound: section =>
        {
            Invoke(section, config with { CounterKey = "mock" }, UseAzureOpenAi);
            Invoke(section, config, UseAzureOpenAi);
        });
        var callbacks = new List<string>();
        SetupCallback(test, (_, candidate) => candidate.CounterKey == "unmatched", (_, _) => Assert.Fail());
        SetupCallback(test, (_, candidate) => candidate.CounterKey == "mock",
            (_, candidate) => callbacks.Add(candidate.CounterKey));

        test.RunAll();

        callbacks.Should().Equal("mock");
        AssertOutputs(test, 6, 4);
    }

    [TestMethod]
    public void TokenLimit_CallbackErrorsRemainPolicyErrors()
    {
        var test = CreateTest(RateConfig);
        SetupCallback(test, (_, _) => true, (_, _) => throw new InvalidOperationException("Mock failed."));

        AssertPolicyError<InvalidOperationException>(test, test.RunAll).Message.Should().Be("Mock failed.");
    }

    [TestMethod]
    public void TokenLimit_CallbackTerminationStillStopsCoordinatedExecution()
    {
        var test = CreateTest(RateConfig, backend: _ => Assert.Fail("Terminated callback backend."));
        SetupCallback(test, (_, _) => true, (context, _) =>
        {
            context.Response.StatusCode = 403;
            throw new FinishSectionProcessingException();
        });

        test.RunAll();

        test.Context.Response.StatusCode.Should().Be(403);
        test.Context.ResponseTerminated.Should().BeTrue();
        test.Context.Services.Resolve<TokenLimitService>().Should().BeNull();
    }

    [TestMethod]
    public void TokenLimit_ExpressionsAreMaterializedAtEachInboundInvocation()
    {
        var clock = new TokenClock();
        var store = new TokenLimitCounterStore();
        var test = CreateTest(RateConfig, store, clock, inbound: section =>
            Invoke(section, BothConfig with
            {
                CounterKey = section.ExpressionContext.Request.IpAddress,
                EstimatePromptToken = (bool)section.ExpressionContext.Variables["estimate"],
                TokensPerMinute = (int)section.ExpressionContext.Variables["rate"],
                TokenQuota = (int)section.ExpressionContext.Variables["budget"]
            }, UseAzureOpenAi));
        test.Context.Variables["estimate"] = false;
        test.Context.Variables["rate"] = 12;
        test.Context.Variables["budget"] = 15;
        test.Context.Request.IpAddress = "192.0.2.1";
        test.RunAll();
        AssertOutputs(test, 6, 6, 9);
        NextRequest(test);
        test.Context.Request.IpAddress = "192.0.2.2";
        test.Context.Variables["estimate"] = true;
        test.Context.Variables["rate"] = 20;
        test.Context.Variables["budget"] = 25;
        test.Context.Services.Register<ITokenLimitPromptEstimator>(new PromptEstimator(_ => 3));

        test.RunAll();

        AssertOutputs(test, 6, 14, 19);
        store.GetRateTokens("192.0.2.1", clock.GetUtcNow()).Should().Be(6);
        store.GetRateTokens("192.0.2.2", clock.GetUtcNow()).Should().Be(6);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void TokenLimit_StreamingIsExplicitlyUnsupportedRatherThanEstimatedFromLengths(bool estimate)
    {
        var test = CreateTest(RateConfig with { EstimatePromptToken = estimate });
        test.Context.Request.Body.Content = """{"stream":true,"messages":[{"role":"user","content":"hello"}]}""";
        test.Context.Services.Register<ITokenLimitPromptEstimator>(new PromptEstimator(_ => 1));

        AssertPolicyError<NotSupportedException>(test, test.RunAll).Message.Should().Contain("stream");
    }

    [TestMethod]
    public void TokenLimit_EventStreamResponseIsNotSilentlyParsedOrBilledAsZero()
    {
        var test = CreateTest(RateConfig, backend: section =>
        {
            var context = (GatewayContext)section.ExpressionContext;
            context.Response.Headers = new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                ["cOnTeNt-TyPe"] = ["text/event-stream; charset=utf-8"]
            };
            context.Response.Body.Content = "data: {}\n\ndata: [DONE]\n";
        });
        test.Context.Services.Register<ILlmTokenUsageProvider>(new UsageProvider(_ =>
            new LlmTokenUsage { PromptTokens = 1, CompletionTokens = 1 }));

        AssertPolicyError<NotSupportedException>(test, test.RunAll).Message.Should().Contain("stream");
    }

    [TestMethod]
    [DataRow("""{"messages":[{"content":[{"type":"image_url","image_url":{"url":"offline://image"}}]}]}""")]
    [DataRow("""{"input":[{"content":[{"type":"input_image","image_url":"offline://image"}]}]}""")]
    [DataRow("""{"messages":[{"content":[{"type":"image","source":{"type":"base64","data":"AA=="}}]}]}""")]
    [DataRow("""{"contents":[{"parts":[{"inlineData":{"mimeType":"image/png","data":"AA=="}}]}]}""")]
    public void TokenLimit_VisionUsesActualUsageButDoesNotFabricateAnImageEstimate(string requestBody)
    {
        var actual = CreateTest(RateConfig);
        actual.Context.Request.Body.Content = requestBody;
        actual.RunAll();
        AssertOutputs(actual, 6, 4);
        var estimated = CreateTest(RateConfig with { EstimatePromptToken = true });
        estimated.Context.Request.Body.Content = requestBody;
        var estimator = new PromptEstimator(_ => 1200);
        estimated.Context.Services.Register<ITokenLimitPromptEstimator>(estimator);

        AssertPolicyError<NotSupportedException>(estimated, estimated.RunAll)
            .Message.Should().Contain("image");
        estimator.CallCount.Should().Be(0);
    }

    [TestMethod]
    [DynamicData(nameof(InvalidConfigurations))]
    public void TokenLimit_InvalidConfigIsExplicitAndPreservesExistingDeepState(TokenLimitConfig config)
    {
        var test = CreateTest(config);
        var headers = new Dictionary<string, string[]>(StringComparer.Ordinal) { ["X-Preserved"] = ["value"] };
        var nested = new Dictionary<string, object> { ["values"] = new[] { 1, 2, 3 } };
        test.Context.Response.Headers = headers;
        test.Context.Response.Body.Content = "original body";
        test.Context.Response.StatusCode = 207;
        test.Context.Response.StatusReason = "Original";
        test.Context.Variables["nested"] = nested;
        var variables = test.Context.Variables;
        var requestId = test.Context.RequestId;

        AssertPolicyError<ArgumentException>(test, test.RunInbound);

        test.Context.Response.Headers.Should().BeSameAs(headers);
        headers.Should().ContainSingle().Which.Key.Should().Be("X-Preserved");
        test.Context.Response.Body.Content.Should().Be("original body");
        test.Context.Response.StatusCode.Should().Be(207);
        test.Context.Response.StatusReason.Should().Be("Original");
        test.Context.Variables.Should().BeSameAs(variables).And.ContainSingle();
        test.Context.Variables["nested"].Should().BeSameAs(nested);
        test.Context.RequestId.Should().Be(requestId);
    }

    [TestMethod]
    public void TokenLimit_PreservesInjectedHeaderIdentityComparerAndUnrelatedDeepState()
    {
        var clock = new TokenClock();
        var store = new TokenLimitCounterStore();
        var test = CreateTest(BothConfig, store, clock, backend: _ => { });
        var request = test.Context.Request;
        var response = test.Context.Response;
        var requestBody = request.Body;
        var responseBody = response.Body;
        var requestHeaders = new Dictionary<string, string[]>(StringComparer.Ordinal) { ["X-Request"] = ["keep"] };
        var untouched = new[] { "first", "second" };
        var responseHeaders = new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["x-remaining"] = ["stale"],
            ["X-REMAINING"] = ["duplicate"],
            ["x-tokens"] = ["stale"],
            ["X-Untouched"] = untouched
        };
        request.Headers = requestHeaders;
        response.Headers = responseHeaders;
        responseBody.Content = UsageJson(3, 1);
        requestBody.As<string>();
        responseBody.As<string>();
        var variables = test.Context.Variables;
        var nested = new Dictionary<string, object> { ["value"] = new[] { 1, 2 } };
        variables["nested"] = nested;
        var api = test.Context.Api;
        var lastError = test.Context.LastError;
        var requestId = test.Context.RequestId;

        test.RunAll();

        test.Context.Request.Should().BeSameAs(request);
        test.Context.Response.Should().BeSameAs(response);
        request.Body.Should().BeSameAs(requestBody);
        response.Body.Should().BeSameAs(responseBody);
        requestBody.Consumed.Should().BeTrue();
        responseBody.Consumed.Should().BeTrue();
        request.Headers.Should().BeSameAs(requestHeaders);
        response.Headers.Should().BeSameAs(responseHeaders);
        responseHeaders.Comparer.Should().BeSameAs(StringComparer.Ordinal);
        responseHeaders.Keys.Count(key => key.Equals("X-Remaining", StringComparison.OrdinalIgnoreCase)).Should().Be(1);
        responseHeaders.Keys.Count(key => key.Equals("X-Tokens", StringComparison.OrdinalIgnoreCase)).Should().Be(1);
        responseHeaders["X-Untouched"].Should().BeSameAs(untouched);
        variables.Should().BeSameAs(test.Context.Variables);
        variables["nested"].Should().BeSameAs(nested);
        test.Context.Api.Should().BeSameAs(api);
        test.Context.LastError.Should().BeSameAs(lastError);
        test.Context.RequestId.Should().Be(requestId);
        response.Body.Content.Should().Be(UsageJson(3, 1));
        AssertOutputs(test, 4, 6, 6);
    }

    [TestMethod]
    public void TokenLimit_DefaultHeadersAreCaseInsensitiveAndRejectionPreservesInjectedDictionary()
    {
        var test = CreateTest(RateConfig);
        test.RunAll();
        test.Context.Response.Headers["x-tokens"].Should().Equal("6");
        NextRequest(test);
        test.RunAll();
        NextRequest(test);
        var headers = new Dictionary<string, string[]>(StringComparer.Ordinal) { ["old"] = ["value"] };
        test.Context.Response.Headers = headers;

        test.RunAll();

        test.Context.Response.StatusCode.Should().Be(429);
        test.Context.Response.Headers.Should().BeSameAs(headers);
        headers.Comparer.Should().BeSameAs(StringComparer.Ordinal);
        headers.Should().NotContainKey("old");
        headers["X-Retry"].Should().Equal("60");
    }

    [TestMethod]
    public void TokenLimit_DefaultRetryAfterHeaderIsWrittenOnlyOnRejection()
    {
        var clock = new TokenClock();
        var store = new TokenLimitCounterStore();
        var config = RateConfig with { RetryAfterHeaderName = null };
        var first = CreateTest(config, store, clock, backend: SetUsage(6, 4));
        first.RunAll();
        first.Context.Response.Headers.Should().NotContainKey("Retry-After");
        var blocked = CreateTest(config, store, clock);

        blocked.RunAll();

        blocked.Context.Response.Headers["retry-after"].Should().Equal("60");
        blocked.Context.Response.Headers.Should().NotContainKey("X-Retry");
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void TokenLimit_AllNumericOutputsReplaceOrdinalAliasesAtFinalUsage(bool estimated)
    {
        var clock = new TokenClock();
        var store = new TokenLimitCounterStore();
        var test = CreateTest(BothConfig with { EstimatePromptToken = estimated }, store, clock, backend: SetUsage(3, 1));
        if (estimated)
        {
            test.Context.Services.Register<ITokenLimitPromptEstimator>(new PromptEstimator(_ => 2));
        }

        var unrelated = new[] { "caf\u00e9", "\ud83d\ude42" };
        var headers = new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["x-remaining"] = ["99"],
            ["X-REMAINING"] = ["98"],
            ["x-quota"] = ["99"],
            ["X-QUOTA"] = ["98"],
            ["x-tokens"] = ["99"],
            ["X-TOKENS"] = ["98"],
            ["Content-Type"] = ["application/json; charset=utf-8"],
            ["X-Unrelated"] = unrelated
        };
        test.Context.Response.Headers = headers;

        test.RunAll();

        test.Context.Response.Headers.Should().BeSameAs(headers);
        headers.Comparer.Should().BeSameAs(StringComparer.Ordinal);
        foreach (var name in new[] { "X-Remaining", "X-Quota", "X-Tokens" })
        {
            headers.Keys.Count(key => key.Equals(name, StringComparison.OrdinalIgnoreCase)).Should().Be(1);
            headers.Keys.Should().Contain(name);
        }

        headers["X-Unrelated"].Should().BeSameAs(unrelated);
        headers["Content-Type"].Should().Equal("application/json; charset=utf-8");
        test.Context.Response.Body.Content.Should().Be(UsageJson(3, 1));
        AssertOutputs(test, 4, 6, 6);
        store.GetRateTokens("customer", clock.GetUtcNow()).Should().Be(4);
        store.GetQuotaTokens("customer", "Daily", clock.GetUtcNow()).Should().Be(4);
    }

    [TestMethod]
    public void TokenLimit_NumericHeaderReplacementPreservesInt64ConsumedValues()
    {
        var clock = new TokenClock();
        var store = new TokenLimitCounterStore();
        var test = CreateTest(RateConfig, store, clock, backend: SetUsage(int.MaxValue, 1));
        var headers = new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["x-tokens"] = ["99"],
            ["X-TOKENS"] = ["98"]
        };
        test.Context.Response.Headers = headers;

        test.RunAll();

        test.Context.Response.StatusCode.Should().Be(200);
        test.Context.Response.Headers.Should().BeSameAs(headers);
        headers.Comparer.Should().BeSameAs(StringComparer.Ordinal);
        headers.Keys.Count(name => name.Equals("X-Tokens", StringComparison.OrdinalIgnoreCase)).Should().Be(1);
        headers["X-Tokens"].Should().Equal("2147483648");
        test.Context.Variables["consumed"].Should().Be(2147483648L);
        store.GetRateTokens("customer", clock.GetUtcNow()).Should().Be(2147483648L);
    }

    [TestMethod]
    [DataRow("document")]
    [DataRow("flat")]
    [DataRow("nested")]
    [DataRow("outer")]
    [DataRow("terminal-sections")]
    public void TokenLimit_Correction_PreBackendTerminalReleasesReservationsWithoutUsage(string mode)
    {
        var clock = new TokenClock();
        var store = new TokenLimitCounterStore();
        var config = BothConfig with { EstimatePromptToken = true };
        var document = new TokenDocument
        {
            InboundAction = section =>
            {
                Invoke(section, config, UseAzureOpenAi);
                section.ReturnResponse(new ReturnResponseConfig
                {
                    Status = new StatusConfig { Code = 204, Reason = "No Content" },
                    Headers = [new HeaderConfig { Name = "X-Early", Values = ["preserved"] }]
                });
            },
            BackendAction = _ => Assert.Fail("No backend may run after the inbound terminal response."),
            OutboundAction = _ => Assert.Fail("No outbound may run after the inbound terminal response.")
        };
        var (test, run) = CreateCorrectionFlow(mode, document, store, clock);
        var estimator = new PromptEstimator(_ => 6);
        var provider = new UsageProvider(_ => throw new InvalidOperationException("No LLM call occurred."));
        test.Context.Services.Register<ITokenLimitPromptEstimator>(estimator)
            .Register<ILlmTokenUsageProvider>(provider);
        var requestId = test.Context.RequestId;

        run();
        test.RunRequest(_ => { });

        test.Context.RequestId.Should().Be(requestId);
        test.Context.Response.StatusCode.Should().Be(204);
        test.Context.ResponseTerminated.Should().BeTrue();
        test.Context.Response.Headers["X-Early"].Should().Equal("preserved");
        test.Context.Response.Headers.Should().NotContainKey("X-Tokens");
        test.Context.Variables["consumed"].Should().Be(0L);
        test.Context.Variables["remaining"].Should().Be(10L);
        test.Context.Variables["quota"].Should().Be(10L);
        test.Context.Services.Resolve<TokenLimitService>()!.HasPendingResponse.Should().BeFalse();
        provider.CallCount.Should().Be(0);
        store.GetRateTokens("customer", clock.GetUtcNow()).Should().Be(0);
        store.GetQuotaTokens("customer", "Daily", clock.GetUtcNow()).Should().Be(0);
        var next = CreateTest(config, store, clock, backend: SetUsage(1, 1), useAzureOpenAi: !UseAzureOpenAi);
        next.Context.Services.Register<ITokenLimitPromptEstimator>(new PromptEstimator(_ => 6));
        next.RunAll();
        next.Context.Response.StatusCode.Should().Be(200);
        AssertOutputs(next, 2, 8, 8);
    }

    [TestMethod]
    [DataRow("document", false, false)]
    [DataRow("document", true, true)]
    [DataRow("flat", false, true)]
    [DataRow("flat", true, false)]
    [DataRow("nested", false, false)]
    [DataRow("nested", true, true)]
    [DataRow("sections", false, true)]
    [DataRow("independent", true, true)]
    public void TokenLimit_Correction_ActualOutputsPrecedeOutboundAndRewritesDoNotChangeUsage(
        string mode, bool estimate, bool rewrite)
    {
        var clock = new TokenClock();
        var store = new TokenLimitCounterStore();
        var config = BothConfig with { EstimatePromptToken = estimate };
        var rewritten = "{\"answer\":\"caf\u00e9\"}";
        var document = new TokenDocument
        {
            InboundAction = section => Invoke(section, config, UseAzureOpenAi),
            BackendAction = section =>
            {
                section.ExpressionContext.Variables["consumed"].Should().Be(estimate ? 4L : 0L);
                SetUsage(4, 2)(section);
            },
            OutboundAction = section =>
            {
                var context = (GatewayContext)section.ExpressionContext;
                context.Variables["consumed"].Should().Be(6L);
                context.Response.Headers["X-Tokens"].Should().Equal("6");
                store.GetRateTokens("customer", clock.GetUtcNow()).Should().Be(0);
                store.GetQuotaTokens("customer", "Daily", clock.GetUtcNow()).Should().Be(0);
                section.SetHeader("X-Copied-Consumed",
                    [((long)section.ExpressionContext.Variables["consumed"]).ToString(CultureInfo.InvariantCulture)]);
                if (rewrite) section.SetBody(rewritten);
            }
        };
        var (test, run) = CreateCorrectionFlow(mode, document, store, clock);
        test.Context.Services.Register<ITokenLimitPromptEstimator>(new PromptEstimator(_ => 4));
        var provider = new UsageProvider(_ => throw new InvalidOperationException("Do not replace observed backend usage."));
        test.Context.Services.Register<ILlmTokenUsageProvider>(provider);
        var headers = test.Context.Response.Headers;

        run();

        AssertOutputs(test, 6, 4, 4);
        test.Context.Response.Headers.Should().BeSameAs(headers);
        test.Context.Response.Headers["X-Copied-Consumed"].Should().Equal("6");
        test.Context.Response.Body.Content.Should().Be(rewrite ? rewritten : UsageJson(4, 2));
        store.GetRateTokens("customer", clock.GetUtcNow()).Should().Be(6);
        store.GetQuotaTokens("customer", "Daily", clock.GetUtcNow()).Should().Be(6);
        provider.CallCount.Should().Be(0);
    }

    [TestMethod]
    [DataRow("document")]
    [DataRow("flat")]
    [DataRow("nested")]
    [DataRow("sections")]
    [DataRow("independent")]
    public void TokenLimit_Correction_InterleavedKeysPublishInGlobalOrderButChargeEachKeyOnce(string mode)
    {
        var clock = new TokenClock();
        var store = new TokenLimitCounterStore();
        var first = BothConfig with { CounterKey = "A", TokensPerMinute = 100, TokenQuota = 100 };
        var middle = BothConfig with { CounterKey = "B", TokensPerMinute = 200, TokenQuota = 200 };
        var document = new TokenDocument
        {
            InboundAction = section =>
            {
                Invoke(section, first, UseAzureOpenAi);
                Invoke(section, middle, !UseAzureOpenAi);
                Invoke(section, first, UseAzureOpenAi);
            },
            BackendAction = SetUsage(4, 2)
        };
        var (test, run) = CreateCorrectionFlow(mode, document, store, clock);

        run();

        AssertOutputs(test, 6, 94, 94);
        foreach (var key in new[] { "A", "B" })
        {
            store.GetRateTokens(key, clock.GetUtcNow()).Should().Be(6);
            store.GetQuotaTokens(key, "Daily", clock.GetUtcNow()).Should().Be(6);
        }
    }

    [TestMethod]
    [DataRow("document")]
    [DataRow("flat")]
    [DataRow("nested")]
    [DataRow("independent")]
    public void TokenLimit_Correction_BackendTerminalBeforeAnyCallReleasesUnusedReservation(string mode)
    {
        var clock = new TokenClock();
        var store = new TokenLimitCounterStore();
        var document = new TokenDocument
        {
            InboundAction = section => Invoke(section, BothConfig with { EstimatePromptToken = true }, UseAzureOpenAi),
            BackendAction = section => section.ReturnResponse(new ReturnResponseConfig
            {
                Status = new StatusConfig { Code = 500, Reason = "Failed backend" }
            }),
            OutboundAction = _ => { }
        };
        var (test, run) = CreateCorrectionFlow(mode, document, store, clock);
        test.Context.Services.Register<ITokenLimitPromptEstimator>(new PromptEstimator(_ => 6));

        run();

        test.Context.Response.StatusCode.Should().Be(500);
        test.Context.Services.Resolve<TokenLimitService>()!.HasPendingResponse.Should().BeFalse();
        test.Context.Variables["consumed"].Should().Be(0L);
        test.Context.Response.Headers.Should().NotContainKey("X-Tokens");
        store.GetRateTokens("customer", clock.GetUtcNow()).Should().Be(0);
        store.GetQuotaTokens("customer", "Daily", clock.GetUtcNow()).Should().Be(0);
    }

    [TestMethod]
    [DataRow("{")]
    [DataRow("""{"usage":{"prompt_tokens":-1,"completion_tokens":5}}""")]
    [DataRow("not-json")]
    public void TokenLimit_Correction_OutboundPayloadCannotInvalidateObservedBackendUsage(string rewritten)
    {
        var test = CreateTest(BothConfig, outbound: section => section.SetBody(rewritten));

        test.RunAll();

        AssertOutputs(test, 6, 4, 4);
        test.Context.Response.Body.Content.Should().Be(rewritten);
    }

    [TestMethod]
    public void TokenLimit_Correction_IndependentBackendRunsDespiteEarlierTerminalAndMustBeCounted()
    {
        var clock = new TokenClock();
        var store = new TokenLimitCounterStore();
        var document = new TokenDocument
        {
            InboundAction = section =>
            {
                Invoke(section, BothConfig, UseAzureOpenAi);
                section.ReturnResponse(new ReturnResponseConfig
                {
                    Status = new StatusConfig { Code = 204, Reason = "No Content" }
                });
            },
            BackendAction = SetUsage(4, 2),
            OutboundAction = section => section.SetBody("""{"answer":"independent"}""")
        };
        var (test, run) = CreateCorrectionFlow("independent", document, store, clock);

        run();

        AssertOutputs(test, 6, 4, 4);
        test.Context.ResponseTerminated.Should().BeTrue();
        store.GetRateTokens("customer", clock.GetUtcNow()).Should().Be(6);
    }

    [TestMethod]
    public void TokenLimit_Correction_InboundInvokeBeforeAnotherScopeTerminatesIsNotAssumedUnused()
    {
        var clock = new TokenClock();
        var store = new TokenLimitCounterStore();
        var pipeline = PolicyPipelineBuilder.Create()
            .ConfigureContext(context => Configure(context, store, clock))
            .AddPolicy(PolicyScope.Global, new TokenDocument
            {
                InboundAction = section =>
                {
                    Invoke(section, BothConfig, UseAzureOpenAi);
                    section.InvokeRequest(new InvokeRequestConfig());
                }
            })
            .AddPolicy(PolicyScope.Operation, new TokenDocument
            {
                InboundAction = section => section.ReturnResponse(new ReturnResponseConfig
                {
                    Status = new StatusConfig { Code = 202, Reason = "Accepted" },
                    Body = new BodyConfig { Content = """{"answer":"rewritten after invoke"}""" }
                })
            }).Build();
        var test = new TestDocument(new TokenDocument()) { Context = pipeline.Context };
        test.SetupInbound().InvokeRequest().WithCallback((context, _) => context.Response.Body.Content = UsageJson(4, 2));

        pipeline.RunAll();

        AssertOutputs(test, 6, 4, 4);
        store.GetRateTokens("customer", clock.GetUtcNow()).Should().Be(6);
        pipeline.Context.Response.StatusCode.Should().Be(202);
    }

    [TestMethod]
    public void TokenLimit_Correction_CancellationAlsoPreservesInterleavedOutputOrder()
    {
        var first = RateConfig with { CounterKey = "A", TokensPerMinute = 100, EstimatePromptToken = true };
        var middle = first with { CounterKey = "B", TokensPerMinute = 200 };
        var rejected = first with
        {
            CounterKey = "C",
            TokensPerMinute = 1,
            RemainingTokensHeaderName = null,
            RemainingTokensVariableName = null,
            TokensConsumedHeaderName = null,
            TokensConsumedVariableName = null
        };
        var test = CreateTest(first, inbound: section =>
        {
            Invoke(section, first, UseAzureOpenAi);
            Invoke(section, middle, UseAzureOpenAi);
            Invoke(section, first, UseAzureOpenAi);
            Invoke(section, rejected, UseAzureOpenAi);
        });
        test.Context.Services.Register<ITokenLimitPromptEstimator>(new PromptEstimator(_ => 2));

        test.RunAll();

        test.Context.Response.StatusCode.Should().Be(429);
        test.Context.Variables["remaining"].Should().Be(100L);
        test.Context.Response.Headers["X-Remaining"].Should().Equal("100");
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void TokenLimit_Correction_OutboundCannotSupplyMissingBackendUsage(bool failed)
    {
        var outboundRan = false;
        var test = CreateTest(BothConfig, backend: section =>
        {
            var context = (GatewayContext)section.ExpressionContext;
            context.Response.StatusCode = failed ? 500 : 200;
            context.Response.Body.Content = """{"answer":"no observed usage"}""";
        }, outbound: section =>
        {
            outboundRan = true;
            section.SetBody(UsageJson(4, 2));
        });

        AssertPolicyError<InvalidOperationException>(test, test.RunAll).Message.Should().Contain(nameof(ILlmTokenUsageProvider));

        outboundRan.Should().BeFalse();
        test.Context.Services.Resolve<TokenLimitService>()!.HasPendingResponse.Should().BeTrue();
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void TokenLimit_Correction_InterleavedScopeInvocationsRetainChronologicalOutputOrder(bool nested)
    {
        var clock = new TokenClock();
        var store = new TokenLimitCounterStore();
        var first = BothConfig with { CounterKey = "A", TokensPerMinute = 100, TokenQuota = 100 };
        var middle = BothConfig with { CounterKey = "B", TokensPerMinute = 200, TokenQuota = 200 };
        var builder = PolicyPipelineBuilder.Create().ConfigureContext(context => Configure(context, store, clock));
        foreach (var scope in new[] { PolicyScope.Global, PolicyScope.Api, PolicyScope.Operation })
        {
            builder.AddPolicy(scope, new TokenDocument
            {
                InboundAction = section =>
                {
                    Invoke(section, scope == PolicyScope.Api ? middle : first,
                        scope == PolicyScope.Api ? !UseAzureOpenAi : UseAzureOpenAi);
                    section.Base();
                },
                BackendAction = section =>
                {
                    section.Base();
                    if (scope == PolicyScope.Operation) SetUsage(4, 2)(section);
                },
                OutboundAction = section => section.Base()
            });
        }
        var pipeline = builder.Build();
        var test = new TestDocument(new TokenDocument()) { Context = pipeline.Context };

        if (nested) pipeline.RunAllNested();
        else pipeline.RunAll();

        AssertOutputs(test, 6, 94, 94);
        store.GetRateTokens("A", clock.GetUtcNow()).Should().Be(6);
        store.GetRateTokens("B", clock.GetUtcNow()).Should().Be(6);
    }

    [TestMethod]
    [DataRow("document")]
    [DataRow("flat")]
    [DataRow("nested")]
    [DataRow("sections")]
    [DataRow("independent")]
    public void TokenLimit_HttpBoundary_PreCallBackendReturnReleasesEstimateAndSettlesOtherLimits(string mode)
    {
        var clock = new TokenClock();
        var tokens = new TokenLimitCounterStore();
        var calls = new RateLimitStore();
        var config = BothConfig with { EstimatePromptToken = true };
        var document = new TokenDocument
        {
            InboundAction = section =>
            {
                Invoke(section, config, UseAzureOpenAi);
                RegisterHttpBoundaryLimits(section);
            },
            BackendAction = section =>
            {
                section.ReturnResponse(new ReturnResponseConfig
                {
                    Status = new StatusConfig { Code = 204, Reason = "No Content" },
                    Headers = [new HeaderConfig { Name = "X-Pre-Call", Values = ["preserved"] }]
                });
                section.ForwardRequest();
            }
        };
        var (test, run) = CreateCorrectionFlow(mode, document, tokens, clock);
        var client = CreateUsageClient(4, 2);
        var provider = new UsageProvider(_ => throw new InvalidOperationException("A pre-call return has no LLM usage."));
        test.Context.Services.Register(calls).Register<IHttpClient>(client)
            .Register<ITokenLimitPromptEstimator>(new PromptEstimator(_ => 6))
            .Register<ILlmTokenUsageProvider>(provider);
        var requestId = test.Context.RequestId;
        var requestBytes = System.Text.Encoding.UTF8.GetByteCount(test.Context.Request.Body.Content!);

        run();
        test.RunRequest(_ => { });
        test.CompleteLimiterResponse();

        client.Calls.Should().Be(0);
        provider.CallCount.Should().Be(0);
        test.Context.RequestId.Should().Be(requestId);
        test.Context.Response.StatusCode.Should().Be(204);
        test.Context.Response.Headers["X-Pre-Call"].Should().Equal("preserved");
        test.Context.Response.Headers.Should().NotContainKey("X-Tokens");
        test.Context.Variables["consumed"].Should().Be(0L);
        test.Context.Variables["remaining"].Should().Be(10L);
        test.Context.Variables["quota"].Should().Be(10L);
        tokens.GetRateTokens("customer", clock.GetUtcNow()).Should().Be(0);
        tokens.GetQuotaTokens("customer", "Daily", clock.GetUtcNow()).Should().Be(0);
        calls.GetCallCount("rate-limit-by-key:http-boundary").Should().Be(2);
        calls.GetBandwidth("quota-by-key:http-boundary").Should().Be(requestBytes);
        test.Context.Services.Resolve<TokenLimitService>()!.HasPendingResponse.Should().BeFalse();
        var next = CreateTest(config, tokens, clock, backend: section => section.ForwardRequest(),
            useAzureOpenAi: !UseAzureOpenAi);
        next.Context.Services.Register<IHttpClient>(CreateUsageClient(1, 1))
            .Register<ITokenLimitPromptEstimator>(new PromptEstimator(_ => 6));
        next.RunAll();
        AssertOutputs(next, 2, 8, 8);
        next.Context.Response.StatusCode.Should().Be(200);
    }

    [TestMethod]
    [DataRow("document", false)]
    [DataRow("document", true)]
    [DataRow("flat", false)]
    [DataRow("flat", true)]
    [DataRow("nested", false)]
    [DataRow("nested", true)]
    [DataRow("sections", false)]
    [DataRow("sections", true)]
    [DataRow("independent", false)]
    [DataRow("independent", true)]
    public void TokenLimit_HttpBoundary_ResponseIsCapturedBeforeLaterFailureAndOnErrorRewrite(
        string mode, bool failOnStatus)
    {
        var clock = new TokenClock();
        var tokens = new TokenLimitCounterStore();
        var calls = new RateLimitStore();
        var expected = new InvalidOperationException("Failure after the copied LLM response.");
        var rewritten = "<handled>caf\u00e9</handled>";
        var document = new TokenDocument
        {
            InboundAction = section =>
            {
                Invoke(section, BothConfig with { EstimatePromptToken = true }, UseAzureOpenAi);
                RegisterHttpBoundaryLimits(section);
            },
            BackendAction = section =>
            {
                section.ForwardRequest(new ForwardRequestConfig { FailOnErrorStatusCode = failOnStatus });
                ((GatewayContext)section.ExpressionContext).Variables["consumed-before-error"] =
                    section.ExpressionContext.Variables["consumed"];
                throw expected;
            },
            OnErrorAction = section =>
            {
                section.SetStatus(new StatusConfig { Code = 502, Reason = "Handled" });
                section.SetHeader("X-On-Error", ["preserved"]);
                section.SetBody(rewritten);
            }
        };
        var (test, run) = CreateCorrectionFlow(mode, document, tokens, clock);
        var client = CreateUsageClient(4, 2, failOnStatus ? HttpStatusCode.ServiceUnavailable : HttpStatusCode.OK);
        var provider = new UsageProvider(_ => throw new InvalidOperationException("Do not re-read rewritten responses."));
        test.Context.Services.Register(calls).Register<IHttpClient>(client)
            .Register<ITokenLimitPromptEstimator>(new PromptEstimator(_ => 6))
            .Register<ILlmTokenUsageProvider>(provider);
        var headers = test.Context.Response.Headers;
        var requestId = test.Context.RequestId;
        var requestBytes = System.Text.Encoding.UTF8.GetByteCount(test.Context.Request.Body.Content!);

        test.RunRequest(request =>
        {
            if (failOnStatus)
            {
                var failure = Assert.ThrowsExactly<PolicyException>(run);
                failure.Policy.Should().Be(nameof(IBackendContext.ForwardRequest));
                failure.InnerException.Should().BeOfType<HttpRequestException>();
            }
            else if (mode == "nested")
            {
                var failure = Assert.ThrowsExactly<PolicyException>(run);
                failure.Policy.Should().Be(nameof(IBackendContext.Base));
                failure.InnerException.Should().BeSameAs(expected);
            }
            else
            {
                Assert.ThrowsExactly<InvalidOperationException>(run).Should().BeSameAs(expected);
            }

            tokens.GetRateTokens("customer", clock.GetUtcNow()).Should().Be(0);
            calls.GetCallCount("rate-limit-by-key:http-boundary").Should().Be(0);
            request.RunRequest(inner => inner.RunOnError());
            calls.GetCallCount("rate-limit-by-key:http-boundary").Should().Be(0);
        });
        test.RunRequest(_ => { });
        test.CompleteLimiterResponse();

        client.Calls.Should().Be(1);
        provider.CallCount.Should().Be(0);
        test.Context.RequestId.Should().Be(requestId);
        test.Context.Response.Headers.Should().BeSameAs(headers);
        test.Context.Response.Headers["X-On-Error"].Should().Equal("preserved");
        test.Context.Response.StatusCode.Should().Be(502);
        test.Context.Response.StatusReason.Should().Be("Handled");
        test.Context.Response.Body.Content.Should().Be(rewritten);
        AssertOutputs(test, 6, 4, 4);
        if (!failOnStatus) test.Context.Variables["consumed-before-error"].Should().Be(6L);
        tokens.GetRateTokens("customer", clock.GetUtcNow()).Should().Be(6);
        tokens.GetQuotaTokens("customer", "Daily", clock.GetUtcNow()).Should().Be(6);
        calls.GetCallCount("rate-limit-by-key:http-boundary").Should().Be(2);
        calls.GetBandwidth("quota-by-key:http-boundary").Should().Be(
            requestBytes + System.Text.Encoding.UTF8.GetByteCount(rewritten));
    }

    [TestMethod]
    [DataRow("document")]
    [DataRow("flat")]
    [DataRow("nested")]
    [DataRow("sections")]
    public void TokenLimit_HttpBoundary_BackendRewriteCannotReplaceCopiedResponseUsage(string mode)
    {
        var clock = new TokenClock();
        var tokens = new TokenLimitCounterStore();
        var rewritten = "{\"answer\":\"caf\u00e9\"}";
        var document = new TokenDocument
        {
            InboundAction = section => Invoke(section, BothConfig, UseAzureOpenAi),
            BackendAction = section =>
            {
                section.ForwardRequest();
                section.ExpressionContext.Variables["consumed"].Should().Be(6L);
                ((GatewayContext)section.ExpressionContext).Response.Body.Content = rewritten;
            },
            OutboundAction = section =>
                section.SetHeader("X-Copied", [section.ExpressionContext.Variables["consumed"].ToString()!])
        };
        var (test, run) = CreateCorrectionFlow(mode, document, tokens, clock);
        var client = CreateUsageClient(4, 2);
        test.Context.Services.Register<IHttpClient>(client);

        run();

        AssertOutputs(test, 6, 4, 4);
        client.Calls.Should().Be(1);
        test.Context.Response.Body.Content.Should().Be(rewritten);
        test.Context.Response.Headers["X-Copied"].Should().Equal("6");
        tokens.GetRateTokens("customer", clock.GetUtcNow()).Should().Be(6);
    }

    [TestMethod]
    [DataRow("document")]
    [DataRow("flat")]
    [DataRow("nested")]
    public void TokenLimit_HttpBoundary_ReturnAfterActualResponseStillCountsTheCopiedUsage(string mode)
    {
        var clock = new TokenClock();
        var tokens = new TokenLimitCounterStore();
        var document = new TokenDocument
        {
            InboundAction = section => Invoke(section, BothConfig, UseAzureOpenAi),
            BackendAction = section =>
            {
                section.ForwardRequest();
                section.ReturnResponse(new ReturnResponseConfig
                {
                    Status = new StatusConfig { Code = 204, Reason = "After backend" }
                });
            }
        };
        var (test, run) = CreateCorrectionFlow(mode, document, tokens, clock);
        var client = CreateUsageClient(4, 2);
        test.Context.Services.Register<IHttpClient>(client);

        run();

        client.Calls.Should().Be(1);
        test.Context.Response.StatusCode.Should().Be(204);
        AssertOutputs(test, 6, 4, 4);
        tokens.GetRateTokens("customer", clock.GetUtcNow()).Should().Be(6);
    }

    [TestMethod]
    [DataRow("{}")]
    [DataRow("""{"usage":{"prompt_tokens":-1,"completion_tokens":2}}""")]
    public void TokenLimit_HttpBoundary_UnusableActualResponseCannotBeAssumedUnusedAfterOnError(string body)
    {
        var clock = new TokenClock();
        var tokens = new TokenLimitCounterStore();
        var document = new TokenDocument
        {
            InboundAction = section => Invoke(section, BothConfig with { EstimatePromptToken = true }, UseAzureOpenAi),
            BackendAction = section => section.ForwardRequest(),
            OnErrorAction = section => section.ReturnResponse(new ReturnResponseConfig
            {
                Status = new StatusConfig { Code = 502, Reason = "Handled" }
            })
        };
        var (test, run) = CreateCorrectionFlow("document", document, tokens, clock);
        var client = new SendRequestTests.RecordingHttpClient((_, _) => Task.FromResult(
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body) }));
        test.Context.Services.Register<IHttpClient>(client)
            .Register<ITokenLimitPromptEstimator>(new PromptEstimator(_ => 6));

        AssertPolicyError<InvalidOperationException>(test, () => test.RunRequest(request =>
        {
            var failure = Assert.ThrowsExactly<PolicyException>(run);
            failure.Policy.Should().Be(ExpectedPolicy);
            request.RunOnError();
        }));

        client.Calls.Should().Be(1);
        tokens.GetRateTokens("customer", clock.GetUtcNow()).Should().Be(0);
        test.Context.Services.Resolve<TokenLimitService>()!.HasPendingResponse.Should().BeTrue();
    }

    [TestMethod]
    public void TokenLimit_HttpBoundary_UnobservedBackendExceptionIsNotAProvenUnusedTerminal()
    {
        var expected = new InvalidOperationException("Unresolved backend failure.");
        var test = CreateTest(BothConfig with { EstimatePromptToken = true }, backend: _ => throw expected,
            onError: section => section.ReturnResponse(new ReturnResponseConfig
            {
                Status = new StatusConfig { Code = 502, Reason = "Handled" }
            }));
        test.Context.Services.Register<ITokenLimitPromptEstimator>(new PromptEstimator(_ => 6));

        AssertPolicyError<InvalidOperationException>(test, () => test.RunRequest(request =>
        {
            Assert.ThrowsExactly<InvalidOperationException>(request.RunAll).Should().BeSameAs(expected);
            request.RunOnError();
        }));

        test.Context.Services.Resolve<TokenLimitService>()!.HasPendingResponse.Should().BeTrue();
    }

    [TestMethod]
    [DataRow("document")]
    [DataRow("flat")]
    [DataRow("nested")]
    [DataRow("sections")]
    [DataRow("independent")]
    public void TokenLimit_SnapshotLifetime_InboundResponseSurvivesBackendShortCircuit(string mode)
    {
        var clock = new TokenClock();
        var tokens = new TokenLimitCounterStore();
        var calls = new RateLimitStore();
        var document = new TokenDocument
        {
            InboundAction = section =>
            {
                Invoke(section, BothConfig with { EstimatePromptToken = true }, UseAzureOpenAi);
                RegisterHttpBoundaryLimits(section);
                section.InvokeRequest(new InvokeRequestConfig());
                section.SetVariable("after-inbound-invoke", true);
            },
            BackendAction = section =>
            {
                ((GatewayContext)section.ExpressionContext).Variables["consumed-at-backend-entry"] =
                    section.ExpressionContext.Variables["consumed"];
                section.ReturnResponse(new ReturnResponseConfig
                {
                    Status = new StatusConfig { Code = 204, Reason = "No further backend" },
                    Headers = [new HeaderConfig { Name = "X-Terminal", Values = ["preserved"] }]
                });
                section.ForwardRequest();
            }
        };
        var (test, run) = CreateCorrectionFlow(mode, document, tokens, clock);
        var client = CreateUsageClient(4, 2);
        var provider = new UsageProvider(_ => throw new InvalidOperationException("The one observed response is sufficient."));
        test.Context.Services.Register(calls).Register<IHttpClient>(client)
            .Register<ITokenLimitPromptEstimator>(new PromptEstimator(_ => 6))
            .Register<ILlmTokenUsageProvider>(provider);
        var requestId = test.Context.RequestId;
        var requestBytes = System.Text.Encoding.UTF8.GetByteCount(test.Context.Request.Body.Content!);

        run();
        test.RunRequest(_ => { });
        test.CompleteLimiterResponse();

        client.Calls.Should().Be(1);
        provider.CallCount.Should().Be(0);
        test.Context.RequestId.Should().Be(requestId);
        test.Context.Variables.Should().NotContainKey("after-inbound-invoke");
        test.Context.Variables["consumed-at-backend-entry"].Should().Be(6L);
        test.Context.Response.StatusCode.Should().Be(204);
        test.Context.Response.StatusReason.Should().Be("No further backend");
        test.Context.Response.Headers["X-Terminal"].Should().Equal("preserved");
        test.Context.Response.Body.Content.Should().BeNullOrEmpty();
        AssertOutputs(test, 6, 4, 4);
        tokens.GetRateTokens("customer", clock.GetUtcNow()).Should().Be(6);
        tokens.GetQuotaTokens("customer", "Daily", clock.GetUtcNow()).Should().Be(6);
        calls.GetCallCount("rate-limit-by-key:http-boundary").Should().Be(2);
        calls.GetBandwidth("quota-by-key:http-boundary").Should().Be(requestBytes);
        test.Context.Services.Resolve<TokenLimitService>()!.HasPendingResponse.Should().BeFalse();
    }

    [TestMethod]
    [DataRow("document")]
    [DataRow("flat")]
    [DataRow("nested")]
    [DataRow("sections")]
    [DataRow("independent")]
    public void TokenLimit_SnapshotLifetime_ARealLaterForwardResponseReplacesInboundUsage(string mode)
    {
        var clock = new TokenClock();
        var tokens = new TokenLimitCounterStore();
        var calls = new RateLimitStore();
        var config = BothConfig with { TokensPerMinute = 20, TokenQuota = 20 };
        var document = new TokenDocument
        {
            InboundAction = section =>
            {
                Invoke(section, config, UseAzureOpenAi);
                RegisterHttpBoundaryLimits(section);
                section.InvokeRequest(new InvokeRequestConfig());
            },
            BackendAction = section =>
            {
                section.ExpressionContext.Variables["consumed"].Should().Be(6L);
                section.ForwardRequest();
                section.ExpressionContext.Variables["consumed"].Should().Be(9L);
            },
            OutboundAction = section => section.SetBody("""{"answer":"after the second response"}""")
        };
        var (test, run) = CreateCorrectionFlow(mode, document, tokens, clock);
        var responses = 0;
        var client = new SendRequestTests.RecordingHttpClient((_, _) =>
        {
            responses++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responses == 1 ? UsageJson(4, 2) : UsageJson(5, 4))
            });
        });
        test.Context.Services.Register(calls).Register<IHttpClient>(client);

        run();

        client.Calls.Should().Be(2);
        AssertOutputs(test, 9, 11, 11);
        tokens.GetRateTokens("customer", clock.GetUtcNow()).Should().Be(9);
        tokens.GetQuotaTokens("customer", "Daily", clock.GetUtcNow()).Should().Be(9);
        calls.GetCallCount("rate-limit-by-key:http-boundary").Should().Be(2);
    }

    [TestMethod]
    [DataRow("document")]
    [DataRow("flat")]
    [DataRow("nested")]
    [DataRow("sections")]
    public void TokenLimit_SnapshotLifetime_AnEmptyBackendSectionKeepsTheObservedInboundUsage(string mode)
    {
        var clock = new TokenClock();
        var tokens = new TokenLimitCounterStore();
        var document = new TokenDocument
        {
            InboundAction = section =>
            {
                Invoke(section, BothConfig, UseAzureOpenAi);
                section.InvokeRequest(new InvokeRequestConfig());
            },
            BackendAction = _ => { },
            OutboundAction = section =>
            {
                section.ExpressionContext.Variables["consumed"].Should().Be(6L);
                section.SetBody("""{"answer":"no new backend call"}""");
            }
        };
        var (test, run) = CreateCorrectionFlow(mode, document, tokens, clock);
        var client = CreateUsageClient(4, 2);
        test.Context.Services.Register<IHttpClient>(client);

        run();

        client.Calls.Should().Be(1);
        AssertOutputs(test, 6, 4, 4);
        tokens.GetRateTokens("customer", clock.GetUtcNow()).Should().Be(6);
    }

    [TestMethod]
    [DataRow("document")]
    [DataRow("flat")]
    [DataRow("nested")]
    [DataRow("sections")]
    public void TokenLimit_SnapshotLifetime_NewRequestIdClearsPriorUsageWithoutLeakingConsumption(string mode)
    {
        var clock = new TokenClock();
        var tokens = new TokenLimitCounterStore();
        var calls = new RateLimitStore();
        var config = BothConfig with { TokensPerMinute = 100, TokenQuota = 100, EstimatePromptToken = true };
        var send = true;
        var document = new TokenDocument
        {
            InboundAction = section =>
            {
                Invoke(section, config, UseAzureOpenAi);
                RegisterHttpBoundaryLimits(section);
                if (send) section.InvokeRequest(new InvokeRequestConfig());
            },
            BackendAction = section => section.ReturnResponse(new ReturnResponseConfig
            {
                Status = new StatusConfig { Code = 204, Reason = "Finished" }
            })
        };
        var (test, run) = CreateCorrectionFlow(mode, document, tokens, clock);
        var client = CreateUsageClient(4, 2);
        test.Context.Services.Register(calls).Register<IHttpClient>(client)
            .Register<ITokenLimitPromptEstimator>(new PromptEstimator(_ => 6));
        run();
        var firstId = test.Context.RequestId;
        send = false;
        NextRequest(test);

        run();

        test.Context.RequestId.Should().NotBe(firstId);
        client.Calls.Should().Be(1);
        test.Context.Variables["consumed"].Should().Be(0L);
        test.Context.Response.Headers.Should().NotContainKey("X-Tokens");
        test.Context.Response.StatusCode.Should().Be(204);
        tokens.GetRateTokens("customer", clock.GetUtcNow()).Should().Be(6);
        tokens.GetQuotaTokens("customer", "Daily", clock.GetUtcNow()).Should().Be(6);
        test.Context.Variables["remaining"].Should().Be(94L);
        test.Context.Variables["quota"].Should().Be(94L);
        calls.GetCallCount("rate-limit-by-key:http-boundary").Should().Be(4);
    }

    [TestMethod]
    [DataRow("{}")]
    [DataRow("""{"usage":{"prompt_tokens":-1,"completion_tokens":2}}""")]
    public void TokenLimit_SnapshotLifetime_FailedLaterObservationCannotSilentlyReuseEarlierCounts(string body)
    {
        var clock = new TokenClock();
        var tokens = new TokenLimitCounterStore();
        var document = new TokenDocument
        {
            InboundAction = section =>
            {
                Invoke(section, BothConfig, UseAzureOpenAi);
                section.InvokeRequest(new InvokeRequestConfig());
            },
            BackendAction = section => section.ForwardRequest(),
            OnErrorAction = section => section.ReturnResponse(new ReturnResponseConfig
            {
                Status = new StatusConfig { Code = 502, Reason = "Missing second usage" }
            })
        };
        var (test, run) = CreateCorrectionFlow("document", document, tokens, clock);
        var responses = 0;
        var client = new SendRequestTests.RecordingHttpClient((_, _) =>
        {
            responses++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responses == 1 ? UsageJson(4, 2) : body)
            });
        });
        test.Context.Services.Register<IHttpClient>(client);

        AssertPolicyError<InvalidOperationException>(test, () => test.RunRequest(request =>
        {
            Assert.ThrowsExactly<PolicyException>(run).Policy.Should().Be(ExpectedPolicy);
            request.RunOnError();
        }));

        client.Calls.Should().Be(2);
        tokens.GetRateTokens("customer", clock.GetUtcNow()).Should().Be(0);
        test.Context.Services.Resolve<TokenLimitService>()!.HasPendingResponse.Should().BeTrue();
    }

    [TestMethod]
    public void TokenLimit_SnapshotLifetime_FreshRequestWithMissingUsageDoesNotReuseTheOldSnapshot()
    {
        var clock = new TokenClock();
        var tokens = new TokenLimitCounterStore();
        var config = BothConfig with { TokensPerMinute = 100, TokenQuota = 100 };
        var test = CreateTest(config, tokens, clock, backend: section => section.ForwardRequest());
        var responses = 0;
        var client = new SendRequestTests.RecordingHttpClient((_, _) =>
        {
            responses++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responses == 1 ? UsageJson(4, 2) : "{}")
            });
        });
        test.Context.Services.Register<IHttpClient>(client);
        test.RunAll();
        NextRequest(test);

        AssertPolicyError<InvalidOperationException>(test, test.RunAll);

        client.Calls.Should().Be(2);
        tokens.GetRateTokens("customer", clock.GetUtcNow()).Should().Be(6);
        test.Context.Services.Resolve<TokenLimitService>()!.HasPendingResponse.Should().BeTrue();
    }

    [TestMethod]
    [DataRow("flat", false, false)]
    [DataRow("flat", false, true)]
    [DataRow("flat", true, false)]
    [DataRow("flat", true, true)]
    [DataRow("nested", false, false)]
    [DataRow("nested", false, true)]
    [DataRow("nested", true, false)]
    [DataRow("nested", true, true)]
    public void TokenLimit_StateMachine_LaterRejectionKeepsEarlierObservedAdmission(
        string mode, bool quota, bool estimate)
    {
        var clock = new TokenClock();
        var tokens = new TokenLimitCounterStore();
        var calls = new RateLimitStore();
        var basis = quota ? QuotaConfig with { TokenQuota = 6 } : RateConfig with { TokensPerMinute = 6 };
        var admitted = basis with
        {
            CounterKey = "A",
            EstimatePromptToken = estimate,
            RetryAfterHeaderName = "X-A-Retry",
            RetryAfterVariableName = "retry-A",
            TokensConsumedHeaderName = "X-A-Tokens",
            TokensConsumedVariableName = "consumed-A",
            RemainingTokensHeaderName = quota ? null : "X-A-Remaining",
            RemainingTokensVariableName = quota ? null : "remaining-A",
            RemainingQuotaTokensHeaderName = quota ? "X-A-Quota" : null,
            RemainingQuotaTokensVariableName = quota ? "quota-A" : null
        };
        var denied = basis with
        {
            CounterKey = "B",
            RetryAfterHeaderName = "X-B-Retry",
            RetryAfterVariableName = "retry-B",
            TokensConsumedHeaderName = "X-B-Tokens",
            TokensConsumedVariableName = "consumed-B",
            RemainingTokensHeaderName = quota ? null : "X-B-Remaining",
            RemainingTokensVariableName = quota ? null : "remaining-B",
            RemainingQuotaTokensHeaderName = quota ? "X-B-Quota" : null,
            RemainingQuotaTokensVariableName = quota ? "quota-B" : null
        };
        CreateTest(denied, tokens, clock).RunAll();
        Action<IInboundContext> admit = section =>
        {
            Invoke(section, admitted, UseAzureOpenAi);
            RegisterHttpBoundaryLimits(section);
            section.InvokeRequest(new InvokeRequestConfig());
        };
        Action<IInboundContext> reject = section =>
        {
            Invoke(section, denied, !UseAzureOpenAi);
            Assert.Fail("The exhausted key did not terminate its section.");
        };
        var pipeline = PolicyPipelineBuilder.Create()
            .ConfigureContext(context => Configure(context, tokens, clock))
            .AddPolicy(PolicyScope.Global, new TokenDocument
            {
                InboundAction = mode == "flat" ? admit : section =>
                {
                    section.Base();
                    reject(section);
                },
                BackendAction = _ => Assert.Fail("Denied request reached backend."),
                OutboundAction = _ => Assert.Fail("Denied request reached outbound.")
            })
            .AddPolicy(PolicyScope.Operation, new TokenDocument
            {
                InboundAction = mode == "flat" ? reject : admit,
                BackendAction = _ => Assert.Fail("Denied request reached backend."),
                OutboundAction = _ => Assert.Fail("Denied request reached outbound.")
            })
            .Build();
        var client = CreateUsageClient(4, 2);
        var provider = new UsageProvider(_ => throw new InvalidOperationException("Captured usage must survive rejection."));
        pipeline.Context.Services.Register(calls).Register<IHttpClient>(client)
            .Register<ITokenLimitPromptEstimator>(new PromptEstimator(_ => 6))
            .Register<ILlmTokenUsageProvider>(provider);
        var requestId = pipeline.Context.RequestId;
        var requestBytes = System.Text.Encoding.UTF8.GetByteCount(pipeline.Context.Request.Body.Content!);

        pipeline.RunRequest(request =>
        {
            if (mode == "flat") request.RunAll();
            else request.RunAllNested();
            pipeline.Context.Variables["consumed-A"].Should().Be(6L);
            tokens.GetRateTokens("A", clock.GetUtcNow()).Should().Be(0);
            pipeline.Context.Services.Resolve<TokenLimitService>()!.HasPendingResponse.Should().BeTrue();
        });
        pipeline.RunRequest(_ => { });
        pipeline.Context.Services.Resolve<TokenLimitService>()!.CompleteResponse();
        pipeline.Context.CompleteLimiterResponse();

        client.Calls.Should().Be(1);
        provider.CallCount.Should().Be(0);
        pipeline.Context.RequestId.Should().Be(requestId);
        pipeline.Context.Response.StatusCode.Should().Be(quota ? 403 : 429);
        pipeline.Context.Response.StatusReason.Should().Be(quota ? "Forbidden" : "Too Many Requests");
        pipeline.Context.ResponseTerminated.Should().BeTrue();
        pipeline.Context.Response.Body.Content.Should().BeNullOrEmpty();
        pipeline.Context.Response.Headers["X-B-Retry"].Should().Equal(quota ? "43200" : "60");
        pipeline.Context.Variables["retry-B"].Should().Be(quota ? 43200 : 60);
        pipeline.Context.Variables["consumed-A"].Should().Be(6L);
        pipeline.Context.Response.Headers["X-A-Tokens"].Should().Equal("6");
        pipeline.Context.Variables[quota ? "quota-A" : "remaining-A"].Should().Be(0L);
        pipeline.Context.Variables["consumed-B"].Should().Be(0L);
        pipeline.Context.Response.Headers.Should().NotContainKey("X-B-Tokens");
        foreach (var key in new[] { "A", "B" })
        {
            tokens.GetRateTokens(key, clock.GetUtcNow()).Should().Be(6);
            tokens.GetQuotaTokens(key, "Daily", clock.GetUtcNow()).Should().Be(6);
        }
        calls.GetCallCount("rate-limit-by-key:http-boundary").Should().Be(2);
        calls.GetCallCount("quota-by-key:http-boundary").Should().Be(1);
        calls.GetBandwidth("quota-by-key:http-boundary").Should().Be(requestBytes);
        pipeline.Context.Services.Resolve<TokenLimitService>()!.HasPendingResponse.Should().BeFalse();
        var next = CreateTest(admitted with { EstimatePromptToken = false }, tokens, clock,
            backend: section => section.ForwardRequest(), useAzureOpenAi: !UseAzureOpenAi);
        var nextClient = CreateUsageClient(1, 1);
        next.Context.Services.Register<IHttpClient>(nextClient);
        next.RunAll();
        next.Context.Response.StatusCode.Should().Be(quota ? 403 : 429);
        nextClient.Calls.Should().Be(0);
    }

    [TestMethod]
    [DynamicData(nameof(BackendPostprocessingCases))]
    public void TokenLimit_StateMachine_BackendFormattingDoesNotDeclareAnotherResponse(string mode, string body)
    {
        var clock = new TokenClock();
        var tokens = new TokenLimitCounterStore();
        var calls = new RateLimitStore();
        var document = new TokenDocument
        {
            InboundAction = section =>
            {
                Invoke(section, BothConfig with { EstimatePromptToken = true }, UseAzureOpenAi);
                RegisterHttpBoundaryLimits(section);
                section.InvokeRequest(new InvokeRequestConfig());
            },
            BackendAction = section => ((GatewayContext)section.ExpressionContext).Response.Body.Content = body,
            OutboundAction = section =>
            {
                section.ExpressionContext.Variables["consumed"].Should().Be(6L);
                section.SetHeader("X-Copied-Consumed", ["6"]);
            }
        };
        var (test, run) = CreateCorrectionFlow(mode, document, tokens, clock);
        var client = CreateUsageClient(4, 2);
        var provider = new UsageProvider(_ => throw new InvalidOperationException("Formatting is not a new LLM response."));
        test.Context.Services.Register(calls).Register<IHttpClient>(client)
            .Register<ITokenLimitPromptEstimator>(new PromptEstimator(_ => 6))
            .Register<ILlmTokenUsageProvider>(provider);
        var headers = new Dictionary<string, string[]>(StringComparer.Ordinal);
        test.Context.Response.Headers = headers;
        var response = test.Context.Response;
        var variables = test.Context.Variables;
        var nested = new Dictionary<string, object> { ["items"] = new[] { 1, 2, 3 } };
        variables["nested"] = nested;
        var requestId = test.Context.RequestId;
        var requestBody = test.Context.Request.Body.Content;

        run();
        test.RunRequest(_ => { });
        test.CompleteLimiterResponse();

        client.Calls.Should().Be(1);
        provider.CallCount.Should().Be(0);
        test.Context.RequestId.Should().Be(requestId);
        test.Context.Response.Should().BeSameAs(response);
        test.Context.Response.Headers.Should().BeSameAs(headers);
        headers.Comparer.Should().BeSameAs(StringComparer.Ordinal);
        test.Context.Variables.Should().BeSameAs(variables);
        variables["nested"].Should().BeSameAs(nested);
        nested["items"].Should().BeEquivalentTo(new[] { 1, 2, 3 });
        test.Context.Request.Body.Content.Should().Be(requestBody);
        test.Context.Response.Body.Content.Should().Be(body);
        test.Context.Response.Headers["X-Copied-Consumed"].Should().Equal("6");
        AssertOutputs(test, 6, 4, 4);
        tokens.GetRateTokens("customer", clock.GetUtcNow()).Should().Be(6);
        tokens.GetQuotaTokens("customer", "Daily", clock.GetUtcNow()).Should().Be(6);
        calls.GetCallCount("rate-limit-by-key:http-boundary").Should().Be(2);
        calls.GetBandwidth("quota-by-key:http-boundary").Should().Be(
            System.Text.Encoding.UTF8.GetByteCount(requestBody!) + System.Text.Encoding.UTF8.GetByteCount(body));
        test.Context.Services.Resolve<TokenLimitService>()!.HasPendingResponse.Should().BeFalse();
    }

    [TestMethod]
    [DataRow("document", "request-stream")]
    [DataRow("flat", "request-stream")]
    [DataRow("nested", "request-stream")]
    [DataRow("sections", "request-stream")]
    [DataRow("document", "response-stream")]
    [DataRow("flat", "response-stream")]
    [DataRow("nested", "response-stream")]
    [DataRow("sections", "response-stream")]
    [DataRow("document", "estimated-image")]
    [DataRow("flat", "estimated-image")]
    [DataRow("nested", "estimated-image")]
    [DataRow("sections", "estimated-image")]
    public void TokenLimit_StateMachine_ManualObservationUsesTheSameSupportGuards(string mode, string unsupported)
    {
        var clock = new TokenClock();
        var tokens = new TokenLimitCounterStore();
        var document = new TokenDocument
        {
            InboundAction = section =>
            {
                Invoke(section, BothConfig with { EstimatePromptToken = unsupported == "estimated-image" }, UseAzureOpenAi);
                section.InvokeRequest(new InvokeRequestConfig());
            },
            BackendAction = section =>
            {
                var context = (GatewayContext)section.ExpressionContext;
                if (unsupported == "request-stream")
                {
                    context.Request.Body.Content = """{"stream":true,"messages":[{"role":"user","content":"later"}]}""";
                }
                else if (unsupported == "estimated-image")
                {
                    context.Request.Body.Content =
                        """{"messages":[{"content":[{"type":"image_url","image_url":{"url":"offline://image"}}]}]}""";
                }

                section.ForwardRequest(new ForwardRequestConfig { HttpVersion = "mock-only" });
                section.SetVariable("after-manual-response", true);
            }
        };
        var (test, run) = CreateCorrectionFlow(mode, document, tokens, clock);
        var client = CreateUsageClient(4, 2);
        var callbacks = 0;
        test.Context.Services.Register<IHttpClient>(client)
            .Register<ITokenLimitPromptEstimator>(new PromptEstimator(_ => 6));
        test.SetupBackend().ForwardRequest((_, config) => config?.HttpVersion == "unselected")
            .WithCallback((_, _) => Assert.Fail("Unselected callback ran."));
        test.SetupBackend().ForwardRequest((_, config) => config?.HttpVersion == "mock-only")
            .WithCallback((context, _) =>
            {
                callbacks++;
                context.Response.Body.Content = UsageJson(5, 4);
                if (unsupported == "response-stream")
                {
                    context.Response.Headers["cOnTeNt-TyPe"] = ["text/event-stream; charset=utf-8"];
                }
            });

        AssertPolicyError<NotSupportedException>(test, run).Message.Should().Contain(
            unsupported == "estimated-image" ? "image" : "stream");

        client.Calls.Should().Be(1);
        callbacks.Should().Be(1);
        test.Context.Variables.Should().NotContainKey("after-manual-response");
        test.Context.Variables["consumed"].Should().Be(6L);
        tokens.GetRateTokens("customer", clock.GetUtcNow()).Should().Be(0);
        tokens.GetQuotaTokens("customer", "Daily", clock.GetUtcNow()).Should().Be(0);
        test.Context.Services.Resolve<TokenLimitService>()!.HasPendingResponse.Should().BeTrue();
    }

    [TestMethod]
    [DataRow("flat", false, "<response />")]
    [DataRow("nested", false, "[]")]
    [DataRow("sections", false, "{broken")]
    [DataRow("document", true, "<response />")]
    [DataRow("flat", true, "[]")]
    [DataRow("nested", true, "{broken")]
    [DataRow("sections", true, "{}")]
    public void TokenLimit_StateMachine_ExplicitNewMalformedResponsesCannotReuseEarlierUsage(
        string mode, bool mocked, string body)
    {
        var clock = new TokenClock();
        var tokens = new TokenLimitCounterStore();
        var document = new TokenDocument
        {
            InboundAction = section =>
            {
                Invoke(section, BothConfig, UseAzureOpenAi);
                section.InvokeRequest(new InvokeRequestConfig());
            },
            BackendAction = section =>
            {
                section.ForwardRequest();
                section.SetVariable("after-new-response", true);
            }
        };
        var (test, run) = CreateCorrectionFlow(mode, document, tokens, clock);
        var responses = 0;
        var client = new SendRequestTests.RecordingHttpClient((_, _) =>
        {
            responses++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responses == 1 ? UsageJson(4, 2) : body)
            });
        });
        var callbacks = 0;
        var provider = new UsageProvider(_ => null);
        test.Context.Services.Register<IHttpClient>(client).Register<ILlmTokenUsageProvider>(provider);
        if (mocked)
        {
            test.SetupBackend().ForwardRequest().WithCallback((context, _) =>
            {
                callbacks++;
                context.Response.Body.Content = body;
            });
        }

        var error = AssertPolicyError<Exception>(test, run);

        if (body == "{}")
        {
            error.Should().BeOfType<InvalidOperationException>();
        }
        else if (body == "[]")
        {
            error.Should().BeOfType<ArgumentException>();
        }
        else
        {
            error.Should().BeAssignableTo<JsonException>();
        }
        client.Calls.Should().Be(mocked ? 1 : 2);
        callbacks.Should().Be(mocked ? 1 : 0);
        provider.CallCount.Should().Be(body == "{}" ? 1 : 0);
        test.Context.Variables.Should().NotContainKey("after-new-response");
        test.Context.Variables["consumed"].Should().Be(6L);
        tokens.GetRateTokens("customer", clock.GetUtcNow()).Should().Be(0);
        test.Context.Services.Resolve<TokenLimitService>()!.HasPendingResponse.Should().BeTrue();
    }

    [TestMethod]
    [DataRow("document", false)]
    [DataRow("flat", true)]
    [DataRow("nested", false)]
    [DataRow("sections", true)]
    public void TokenLimit_StateMachine_ValidManualUsageCanReplaceTheSnapshot(string mode, bool mocked)
    {
        var clock = new TokenClock();
        var tokens = new TokenLimitCounterStore();
        var document = new TokenDocument
        {
            InboundAction = section =>
            {
                Invoke(section, BothConfig, UseAzureOpenAi);
                section.InvokeRequest(new InvokeRequestConfig());
            },
            BackendAction = section =>
            {
                if (mocked)
                {
                    section.ForwardRequest();
                    section.ExpressionContext.Variables["consumed"].Should().Be(9L);
                    ((GatewayContext)section.ExpressionContext).Response.Body.Content = "<backend-formatted />";
                }
                else
                {
                    SetUsage(5, 4)(section);
                }
            },
            OutboundAction = section =>
            {
                section.ExpressionContext.Variables["consumed"].Should().Be(9L);
                section.SetBody("<outbound-formatted />");
            }
        };
        var (test, run) = CreateCorrectionFlow(mode, document, tokens, clock);
        var client = CreateUsageClient(4, 2);
        var provider = new UsageProvider(_ => throw new InvalidOperationException("Manual JSON usage is sufficient."));
        test.Context.Services.Register<IHttpClient>(client).Register<ILlmTokenUsageProvider>(provider);
        test.SetupBackend().ForwardRequest().WithCallback((context, _) =>
            context.Response.Body.Content = UsageJson(5, 4));

        run();
        test.RunRequest(_ => { });

        client.Calls.Should().Be(1);
        provider.CallCount.Should().Be(0);
        AssertOutputs(test, 9, 1, 1);
        tokens.GetRateTokens("customer", clock.GetUtcNow()).Should().Be(9);
        tokens.GetQuotaTokens("customer", "Daily", clock.GetUtcNow()).Should().Be(9);
        test.Context.Response.Body.Content.Should().Be("<outbound-formatted />");
        test.Context.Services.Resolve<TokenLimitService>()!.HasPendingResponse.Should().BeFalse();
    }

    public static IEnumerable<object[]> BackendPostprocessingCases()
    {
        foreach (var mode in new[] { "document", "flat", "nested", "sections", "independent" })
        {
            foreach (var body in new[] { "<answer>formatted</answer>", """[{"answer":"formatted"}]""",
                         """{"answer":"formatted"}""", "{not-json", "" })
            {
                yield return [mode, body];
            }
        }
    }

    public static IEnumerable<object[]> InvalidProviderUsages()
    {
        yield return [new LlmTokenUsage()];
        yield return [new LlmTokenUsage { TotalTokens = 2 }];
        yield return [new LlmTokenUsage { PromptTokens = 1 }];
        yield return [new LlmTokenUsage { CompletionTokens = 1 }];
        yield return [new LlmTokenUsage { PromptTokens = -1, CompletionTokens = 1 }];
        yield return [new LlmTokenUsage { PromptTokens = 1, CompletionTokens = -1 }];
        yield return [new LlmTokenUsage { PromptTokens = 1, CompletionTokens = 1, TotalTokens = -1 }];
        yield return [new LlmTokenUsage { PromptTokens = 1, CompletionTokens = 1, AdditionalTokens = null! }];
        yield return
        [
            new LlmTokenUsage
            {
                PromptTokens = 1, CompletionTokens = 1,
                AdditionalTokens = new Dictionary<string, long> { ["Reasoning Tokens"] = -1 }
            }
        ];
    }

    public static IEnumerable<object[]> InvalidConfigurations()
    {
        foreach (var key in new string?[] { null, "", " " })
        {
            yield return [RateConfig with { CounterKey = key! }];
        }
        yield return [RateConfig with { TokensPerMinute = null }];
        yield return [RateConfig with { TokensPerMinute = 0 }];
        yield return [RateConfig with { TokensPerMinute = -1 }];
        yield return [QuotaConfig with { TokenQuota = 0 }];
        yield return [QuotaConfig with { TokenQuota = -1 }];
        yield return [QuotaConfig with { TokenQuotaPeriod = null }];
        foreach (var period in new[] { "", " ", "day", "week", "month", "Fortnightly" })
        {
            yield return [QuotaConfig with { TokenQuotaPeriod = period }];
        }
        yield return [RateConfig with { TokenQuotaPeriod = "Daily" }];
        yield return [RateConfig with { RetryAfterHeaderName = "" }];
        yield return [RateConfig with { RetryAfterHeaderName = "bad\r\nheader" }];
        yield return [RateConfig with { RetryAfterVariableName = " " }];
        yield return [RateConfig with { RemainingTokensHeaderName = "bad header" }];
        yield return [RateConfig with { TokensConsumedHeaderName = "bad:header" }];
        yield return [RateConfig with { TokensConsumedVariableName = "" }];
        yield return [QuotaConfig with { RemainingTokensVariableName = "rate-without-limit" }];
        yield return [RateConfig with { RemainingQuotaTokensHeaderName = "quota-without-limit" }];
        yield return [RateConfig with { TokensConsumedHeaderName = "x-remaining" }];
        yield return [RateConfig with { TokensConsumedVariableName = "remaining" }];
    }

    private TestDocument CreateTest(
        TokenLimitConfig config,
        TokenLimitCounterStore? store = null,
        TokenClock? clock = null,
        Action<IInboundContext>? inbound = null,
        Action<IBackendContext>? backend = null,
        Action<IOutboundContext>? outbound = null,
        Action<IOnErrorContext>? onError = null,
        bool? useAzureOpenAi = null)
    {
        var context = new GatewayContext();
        Configure(context, store ?? new TokenLimitCounterStore(), clock ?? new TokenClock());
        return new TestDocument(new TokenDocument
        {
            InboundAction = inbound ?? (section => Invoke(section, config, useAzureOpenAi ?? UseAzureOpenAi)),
            BackendAction = backend ?? SetUsage(4, 2),
            OutboundAction = outbound,
            OnErrorAction = onError
        })
        { Context = context };
    }

    private static (TestDocument Test, Action Run) CreateCorrectionFlow(
        string mode, TokenDocument document, TokenLimitCounterStore store, TokenClock clock)
    {
        if (mode is "flat" or "nested" or "independent")
        {
            var pipeline = PolicyPipelineBuilder.Create()
                .ConfigureContext(context => Configure(context, store, clock))
                .AddPolicy(PolicyScope.Global, new TokenDocument
                {
                    InboundAction = section => section.Base(),
                    BackendAction = section => section.Base(),
                    OutboundAction = section => section.Base()
                })
                .AddPolicy(PolicyScope.Operation, document)
                .Build();
            var test = new TestDocument(document) { Context = pipeline.Context };
            Action run = mode switch
            {
                "flat" => pipeline.RunAll,
                "nested" => pipeline.RunAllNested,
                _ => () => pipeline.RunRequest(request =>
                {
                    request.RunInboundIndependent();
                    request.RunBackendIndependent();
                    request.RunOutboundIndependent();
                })
            };
            return (test, run);
        }

        var standalone = new TestDocument(document);
        Configure(standalone.Context, store, clock);
        Action execute = mode switch
        {
            "outer" => () => standalone.RunRequest(request => request.RunAll()),
            "terminal-sections" => () =>
            {
                standalone.RunInbound();
                standalone.RunRequest(_ => { });
            }
            ,
            "sections" => () =>
            {
                standalone.RunInbound();
                standalone.RunBackend();
                standalone.RunOutbound();
                standalone.RunRequest(_ => { });
            }
            ,
            _ => standalone.RunAll
        };
        return (standalone, execute);
    }

    private static void Configure(GatewayContext context, TokenLimitCounterStore store, TokenClock clock)
    {
        context.Services.Register(store).Register<TimeProvider>(clock);
        context.Request.Body.Content = """{"model":"offline-fixture","messages":[{"role":"user","content":"hello"}]}""";
    }

    private static SendRequestTests.RecordingHttpClient CreateUsageClient(
        long prompt, long completion, HttpStatusCode status = HttpStatusCode.OK) =>
        new((_, _) => Task.FromResult(new HttpResponseMessage(status)
        {
            Content = new StringContent(UsageJson(prompt, completion), System.Text.Encoding.UTF8, "application/json")
        }));

    private static void RegisterHttpBoundaryLimits(IInboundContext section)
    {
        section.RateLimitByKey(new RateLimitByKeyConfig
        {
            CounterKey = "http-boundary",
            Calls = 100,
            RenewalPeriod = 60,
            IncrementCount = 2,
            IncrementAfterResponse = true,
            RemainingCallsVariableName = "calls-remaining"
        });
        section.QuotaByKey(new QuotaByKeyConfig
        {
            CounterKey = "http-boundary",
            Calls = 100,
            Bandwidth = 10,
            RenewalPeriod = 60
        });
    }

    private static void Invoke(IInboundContext section, TokenLimitConfig config, bool useAzureOpenAi)
    {
        if (useAzureOpenAi) section.AzureOpenAiTokenLimit(config);
        else section.LlmTokenLimit(config);
    }

    private void SetupCallback(
        TestDocument test,
        Func<GatewayContext, TokenLimitConfig, bool> predicate,
        Action<GatewayContext, TokenLimitConfig> callback)
    {
        if (UseAzureOpenAi) test.SetupInbound().AzureOpenAiTokenLimit(predicate).WithCallback(callback);
        else test.SetupInbound().LlmTokenLimit(predicate).WithCallback(callback);
    }

    private TException AssertPolicyError<TException>(TestDocument test, Action action) where TException : Exception
    {
        var error = action.Should().Throw<PolicyException>().Which;
        error.Policy.Should().Be(ExpectedPolicy);
        error.Section.Should().Be(nameof(IInboundContext));
        return error.InnerException.Should().BeAssignableTo<TException>().Subject;
    }

    private static void AssertOutputs(TestDocument test, long consumed, long remaining, long? quota = null)
    {
        test.Context.Variables["consumed"].Should().Be(consumed);
        test.Context.Response.Headers["X-Tokens"].Should().Equal(consumed.ToString(CultureInfo.InvariantCulture));
        test.Context.Variables["remaining"].Should().Be(remaining);
        test.Context.Response.Headers["X-Remaining"].Should().Equal(remaining.ToString(CultureInfo.InvariantCulture));
        if (quota is not null)
        {
            test.Context.Variables["quota"].Should().Be(quota.Value);
            test.Context.Response.Headers["X-Quota"].Should().Equal(quota.Value.ToString(CultureInfo.InvariantCulture));
        }
    }

    private static Action<IBackendContext> SetUsage(long prompt, long completion) => section =>
        ((GatewayContext)section.ExpressionContext).Response.Body.Content = UsageJson(prompt, completion);

    private static string UsageJson(long prompt, long completion) =>
        JsonSerializer.Serialize(new { usage = new { prompt_tokens = prompt, completion_tokens = completion } });

    private static DateTimeOffset ParseTime(string value) => DateTimeOffset.Parse(value, CultureInfo.InvariantCulture);

    private static void NextRequest(TestDocument test)
    {
        test.Context.RequestId = Guid.NewGuid();
        test.Context.ResponseTerminated = false;
        test.Context.Response.StatusCode = 200;
        test.Context.Response.StatusReason = "OK";
        test.Context.Response.Headers.Clear();
        test.Context.Response.Body.Content = null;
    }

    private sealed class TokenDocument : IDocument
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

    private sealed class TokenClock : TimeProvider
    {
        private long _ticks;

        public TokenClock() : this(ParseTime("2026-09-30T12:00:00Z")) { }
        public TokenClock(DateTimeOffset now) => _ticks = now.UtcTicks;
        public override DateTimeOffset GetUtcNow() => new(Interlocked.Read(ref _ticks), TimeSpan.Zero);
        public void Advance(TimeSpan amount) => Interlocked.Add(ref _ticks, amount.Ticks);
        public void Set(string now) => Interlocked.Exchange(ref _ticks, ParseTime(now).UtcTicks);
    }

    private sealed class PromptEstimator(Func<GatewayContext, long> estimate) : ITokenLimitPromptEstimator
    {
        public int CallCount { get; private set; }
        public GatewayContext? LastContext { get; private set; }
        public long EstimatePromptTokens(GatewayContext context)
        {
            CallCount++;
            LastContext = context;
            return estimate(context);
        }
    }

    private sealed class UsageProvider(Func<GatewayContext, LlmTokenUsage?> getUsage) : ILlmTokenUsageProvider
    {
        public int CallCount { get; private set; }
        public GatewayContext? LastContext { get; private set; }
        public LlmTokenUsage? GetUsage(GatewayContext context)
        {
            CallCount++;
            LastContext = context;
            return getUsage(context);
        }
    }
}