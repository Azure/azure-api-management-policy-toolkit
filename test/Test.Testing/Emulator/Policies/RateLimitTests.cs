// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Globalization;

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Document;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Data;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Services;

namespace Test.Emulator.Emulator.Policies;

[TestClass]
public class RateLimitTests
{
    class SimpleRateLimit : IDocument
    {
        public void Inbound(IInboundContext context)
        {
            context.RateLimit(new RateLimitConfig { Calls = 3, RenewalPeriod = 60 });
        }
    }

    class RateLimitWithHeaders : IDocument
    {
        public void Inbound(IInboundContext context)
        {
            context.RateLimit(new RateLimitConfig
            {
                Calls = 5,
                RenewalPeriod = 30,
                RemainingCallsHeaderName = "X-RateLimit-Remaining",
                TotalCallsHeaderName = "X-RateLimit-Limit",
                RetryAfterHeaderName = "Retry-After",
                RetryAfterVariableName = "retryAfter",
                RemainingCallsVariableName = "remainingCalls"
            });
        }
    }

    class RateLimitThenSetHeader : IDocument
    {
        public void Inbound(IInboundContext context)
        {
            context.RateLimit(new RateLimitConfig { Calls = 1, RenewalPeriod = 60 });
            context.SetHeader("X-After-RateLimit", "executed");
        }
    }

    class RateLimitWithApiScope : IDocument
    {
        public void Inbound(IInboundContext context)
        {
            context.RateLimit(new RateLimitConfig
            {
                Calls = 100,
                RenewalPeriod = 60,
                Apis =
                [
                    new ApiRateLimit { Name = "orders-api", Calls = 2, RenewalPeriod = 60 }
                ]
            });
        }
    }

    class RateLimitWithApiAndOperationScope : IDocument
    {
        public void Inbound(IInboundContext context)
        {
            context.RateLimit(new RateLimitConfig
            {
                Calls = 100,
                RenewalPeriod = 60,
                Apis =
                [
                    new ApiRateLimit
                    {
                        Name = "orders-api",
                        Calls = 50,
                        RenewalPeriod = 60,
                        Operations =
                        [
                            new OperationRateLimit { Name = "create-order", Calls = 2, RenewalPeriod = 60 }
                        ]
                    }
                ]
            });
        }
    }

    class RateLimitWithApiById : IDocument
    {
        public void Inbound(IInboundContext context)
        {
            context.RateLimit(new RateLimitConfig
            {
                Calls = 100,
                RenewalPeriod = 60,
                Apis =
                [
                    new ApiRateLimit { Id = "api-123", Calls = 2, RenewalPeriod = 60 }
                ]
            });
        }
    }

    class RateLimitWithApiScopeAndHeaders : IDocument
    {
        public void Inbound(IInboundContext context)
        {
            context.RateLimit(new RateLimitConfig
            {
                Calls = 100,
                RenewalPeriod = 60,
                RemainingCallsHeaderName = "X-RateLimit-Remaining",
                Apis =
                [
                    new ApiRateLimit { Name = "orders-api", Calls = 2, RenewalPeriod = 60 }
                ]
            });
        }
    }

    [TestMethod]
    public void RateLimit_UnderLimit()
    {
        var test = new SimpleRateLimit().AsTestDocument();

        test.RunInbound();

        test.Context.Response.StatusCode.Should().NotBe(429);
    }

    [TestMethod]
    public void RateLimit_ExceedsLimit_PerSubscription()
    {
        var test = new SimpleRateLimit().AsTestDocument();
        var subKey = $"sub:{test.Context.Subscription.Id}";
        test.SetupRateLimitStore().SetCount(subKey, 3);

        test.RunInbound();

        test.Context.Response.StatusCode.Should().Be(429);
        test.Context.Response.StatusReason.Should().Be("Too Many Requests");
    }

    [TestMethod]
    public void RateLimit_ResetAndRetry()
    {
        var test = new SimpleRateLimit().AsTestDocument();
        var subKey = $"sub:{test.Context.Subscription.Id}";
        test.SetupRateLimitStore().SetCount(subKey, 3);

        test.RunInbound();
        test.Context.Response.StatusCode.Should().Be(429);

        test.SetupRateLimitStore().Reset();

        test.RunInbound();
        test.Context.Response.StatusCode.Should().NotBe(429);
    }

    [TestMethod]
    public void RateLimit_SetsRemainingCallsHeader()
    {
        var test = new RateLimitWithHeaders().AsTestDocument();

        test.RunInbound();

        test.Context.Response.Headers.Should().ContainKey("X-RateLimit-Remaining");
        test.Context.Response.Headers.Should().ContainKey("X-RateLimit-Limit");
        test.Context.Variables.Should().ContainKey("remainingCalls");
    }

    [TestMethod]
    public void RateLimit_SetsRetryAfterOnExceeded()
    {
        var test = new RateLimitWithHeaders().AsTestDocument();
        var subKey = $"sub:{test.Context.Subscription.Id}";
        test.SetupRateLimitStore().SetCount(subKey, 5);

        test.RunInbound();

        test.Context.Response.StatusCode.Should().Be(429);
        test.Context.Response.Headers.Should().ContainKey("Retry-After");
        test.Context.Variables.Should().ContainKey("retryAfter");
    }

    [TestMethod]
    public void RateLimit_TerminatesSectionOnExceeded()
    {
        var test = new RateLimitThenSetHeader().AsTestDocument();
        var subKey = $"sub:{test.Context.Subscription.Id}";
        test.SetupRateLimitStore().SetCount(subKey, 1);
        var headerExecuted = false;
        test.SetupInbound().SetHeader().WithCallback((_, _, _) => headerExecuted = true);

        test.RunInbound();

        headerExecuted.Should().BeFalse();
        test.Context.Response.StatusCode.Should().Be(429);
    }

    [TestMethod]
    public void RateLimit_Callback()
    {
        var test = new SimpleRateLimit().AsTestDocument();
        var callbackExecuted = false;
        test.SetupInbound().RateLimit().WithCallback((_, config) =>
        {
            callbackExecuted = true;
            config.Calls.Should().Be(3);
            config.RenewalPeriod.Should().Be(60);
        });

        test.RunInbound();

        callbackExecuted.Should().BeTrue();
    }

    [TestMethod]
    public void RateLimit_PerApi_ExceedsLimit()
    {
        var test = new RateLimitWithApiScope().AsTestDocument();
        test.Context.Api.Name = "orders-api";
        var apiKey = $"sub:{test.Context.Subscription.Id}:api:orders-api";
        test.SetupRateLimitStore().SetCount(apiKey, 2);

        test.RunInbound();

        test.Context.Response.StatusCode.Should().Be(429);
    }

    [TestMethod]
    public void RateLimit_PerApi_UnderLimit()
    {
        var test = new RateLimitWithApiScope().AsTestDocument();
        test.Context.Api.Name = "orders-api";

        test.RunInbound();

        test.Context.Response.StatusCode.Should().NotBe(429);
    }

    [TestMethod]
    public void RateLimit_PerApi_DifferentApiNotAffected()
    {
        var test = new RateLimitWithApiScope().AsTestDocument();
        test.Context.Api.Name = "other-api";

        test.RunInbound();

        test.Context.Response.StatusCode.Should().NotBe(429);
    }

    [TestMethod]
    public void RateLimit_PerApi_ById()
    {
        var test = new RateLimitWithApiById().AsTestDocument();
        test.Context.Api.Id = "api-123";
        var apiKey = $"sub:{test.Context.Subscription.Id}:api:api-123";
        test.SetupRateLimitStore().SetCount(apiKey, 2);

        test.RunInbound();

        test.Context.Response.StatusCode.Should().Be(429);
    }

    [TestMethod]
    public void RateLimit_PerOperation_ExceedsLimit()
    {
        var test = new RateLimitWithApiAndOperationScope().AsTestDocument();
        test.Context.Api.Name = "orders-api";
        test.Context.Operation.Name = "create-order";
        var opKey = $"sub:{test.Context.Subscription.Id}:api:orders-api:op:create-order";
        test.SetupRateLimitStore().SetCount(opKey, 2);

        test.RunInbound();

        test.Context.Response.StatusCode.Should().Be(429);
    }

    [TestMethod]
    public void RateLimit_PerOperation_DifferentOperationNotAffected()
    {
        var test = new RateLimitWithApiAndOperationScope().AsTestDocument();
        test.Context.Api.Name = "orders-api";
        test.Context.Operation.Name = "get-order";

        test.RunInbound();

        test.Context.Response.StatusCode.Should().NotBe(429);
    }

    [TestMethod]
    public void RateLimit_SubscriptionLevelExceeded_ApiLevelUnder()
    {
        var test = new RateLimitWithApiScope().AsTestDocument();
        test.Context.Api.Name = "orders-api";
        var subKey = $"sub:{test.Context.Subscription.Id}";
        test.SetupRateLimitStore().SetCount(subKey, 100);

        test.RunInbound();

        test.Context.Response.StatusCode.Should().Be(429);
    }

    [TestMethod]
    public void RateLimit_CountersNotIncrementedOnExceeded()
    {
        var test = new SimpleRateLimit().AsTestDocument();
        var subKey = $"sub:{test.Context.Subscription.Id}";
        test.SetupRateLimitStore().SetCount(subKey, 3);

        test.RunInbound();

        test.Context.Response.StatusCode.Should().Be(429);
        // Counter should NOT have been incremented since limit was already exceeded
        test.SetupRateLimitStore().GetCount(subKey).Should().Be(3);
    }

    [TestMethod]
    public void RateLimit_CountersIncrementedOnSuccess()
    {
        var test = new SimpleRateLimit().AsTestDocument();
        var subKey = $"sub:{test.Context.Subscription.Id}";

        test.RunInbound();

        test.SetupRateLimitStore().GetCount(subKey).Should().Be(1);
    }

    [TestMethod]
    public void RateLimit_RemainingCallsIsMinAcrossScopes()
    {
        var test = new RateLimitWithHeaders().AsTestDocument();
        test.Context.Api.Name = "orders-api";

        // Use a config with API-level limit lower than subscription
        var apiScopeTest = new TestDocument(new RateLimitWithApiScopeAndHeaders())
        {
            Context = { Api = { Name = "orders-api" } }
        };
        var apiKey = $"sub:{apiScopeTest.Context.Subscription.Id}:api:orders-api";
        apiScopeTest.SetupRateLimitStore().SetCount(apiKey, 1);

        apiScopeTest.RunInbound();

        // Remaining should reflect the tightest scope (API: 2 calls, 1 used, so 0 remaining)
        apiScopeTest.Context.Response.Headers.Should().ContainKey("X-RateLimit-Remaining");
        var remaining = apiScopeTest.Context.Response.Headers["X-RateLimit-Remaining"];
        remaining.Should().Contain("0");
    }

    [TestMethod]
    public void RateLimit_RenewsAtExactBoundaryAndRoundsRetryUp()
    {
        var clock = new LimiterTestTimeProvider();
        var test = LimiterTestHarness.Create(context => context.RateLimit(new RateLimitConfig
        {
            Calls = 1,
            RenewalPeriod = 60,
            RemainingCallsHeaderName = "Remaining",
            RemainingCallsVariableName = "remaining",
            RetryAfterVariableName = "retry"
        }), clock: clock);
        var key = $"sub:{test.Context.Subscription.Id}";

        test.RunInbound();
        test.SetupRateLimitStore().GetCount(key).Should().Be(1);
        test.Context.Variables["remaining"].Should().Be(0);

        clock.Advance(TimeSpan.FromSeconds(59.1));
        LimiterTestHarness.RunRequest(test);
        test.Context.Response.StatusCode.Should().Be(429);
        test.Context.Response.Headers["Retry-After"].Should().Equal("1");
        test.Context.Response.Headers["Remaining"].Should().Equal("0");
        test.Context.Variables["retry"].Should().Be(1);
        test.Context.ResponseTerminated.Should().BeTrue();
        test.SetupRateLimitStore().GetCount(key).Should().Be(1);

        clock.Advance(TimeSpan.FromSeconds(0.9));
        LimiterTestHarness.RunRequest(test);
        test.Context.Response.StatusCode.Should().Be(200);
        test.SetupRateLimitStore().GetCount(key).Should().Be(1);
        test.Context.Response.Headers.Should().NotContainKey("Retry-After");
    }

    [TestMethod]
    public void RateLimit_ExpiresIndividualCallsInSlidingWindow()
    {
        var clock = new LimiterTestTimeProvider();
        var test = LimiterTestHarness.Create(context =>
            context.RateLimit(new RateLimitConfig { Calls = 2, RenewalPeriod = 60 }), clock: clock);
        var key = $"sub:{test.Context.Subscription.Id}";

        test.RunInbound();
        clock.Advance(TimeSpan.FromSeconds(30));
        LimiterTestHarness.RunRequest(test);
        clock.Advance(TimeSpan.FromSeconds(30));
        LimiterTestHarness.RunRequest(test);

        test.Context.Response.StatusCode.Should().Be(200);
        test.SetupRateLimitStore().GetCount(key).Should().Be(2);

        LimiterTestHarness.RunRequest(test);
        test.Context.Response.StatusCode.Should().Be(429);
        test.Context.Response.Headers["Retry-After"].Should().Equal("30");
    }

    [TestMethod]
    public void RateLimit_UsesEachScopesRenewalAndDoesNotPartiallyIncrement()
    {
        var clock = new LimiterTestTimeProvider();
        var test = LimiterTestHarness.Create(context => context.RateLimit(new RateLimitConfig
        {
            Calls = 10,
            RenewalPeriod = 120,
            RemainingCallsVariableName = "remaining",
            Apis =
            [
                new ApiRateLimit
                {
                    Name = "orders",
                    Calls = 3,
                    RenewalPeriod = 60,
                    Operations = [new OperationRateLimit { Name = "create", Calls = 1, RenewalPeriod = 10 }]
                }
            ]
        }), clock: clock);
        test.Context.Api.Name = "orders";
        test.Context.Operation.Name = "create";
        var subscription = $"sub:{test.Context.Subscription.Id}";
        var api = $"{subscription}:api:orders";
        var operation = $"{api}:op:create";

        test.RunInbound();
        clock.Advance(TimeSpan.FromSeconds(9.5));
        LimiterTestHarness.RunRequest(test);
        test.Context.Response.Headers["Retry-After"].Should().Equal("1");
        test.SetupRateLimitStore().GetCount(subscription).Should().Be(1);
        test.SetupRateLimitStore().GetCount(api).Should().Be(1);
        test.SetupRateLimitStore().GetCount(operation).Should().Be(1);

        clock.Advance(TimeSpan.FromSeconds(0.5));
        LimiterTestHarness.RunRequest(test);
        test.Context.Response.StatusCode.Should().Be(200);
        test.SetupRateLimitStore().GetCount(subscription).Should().Be(2);
        test.SetupRateLimitStore().GetCount(api).Should().Be(2);
        test.SetupRateLimitStore().GetCount(operation).Should().Be(1);
        test.Context.Variables["remaining"].Should().Be(0);
    }

    [TestMethod]
    public void RateLimit_SharesInjectedStoreButSeparatesSubscriptions()
    {
        var store = new RateLimitStore();
        var clock = new LimiterTestTimeProvider();
        var first = LimiterTestHarness.Create(context =>
            context.RateLimit(new RateLimitConfig { Calls = 1, RenewalPeriod = 60 }), store, clock);
        var second = LimiterTestHarness.Create(context =>
            context.RateLimit(new RateLimitConfig { Calls = 1, RenewalPeriod = 60 }), store, clock);
        first.Context.Subscription.Id = "first";
        second.Context.Subscription.Id = "second";

        first.RunInbound();
        second.RunInbound();
        LimiterTestHarness.RunRequest(first);

        first.Context.Response.StatusCode.Should().Be(429);
        second.Context.Response.StatusCode.Should().Be(200);
        store.GetCount("sub:first").Should().Be(1);
        store.GetCount("sub:second").Should().Be(1);
    }

    [TestMethod]
    public void RateLimit_IdTakesPrecedenceOverName()
    {
        var test = LimiterTestHarness.Create(context => context.RateLimit(new RateLimitConfig
        {
            Calls = 3,
            RenewalPeriod = 60,
            Apis = [new ApiRateLimit { Id = "other", Name = "orders", Calls = 0, RenewalPeriod = 60 }]
        }));
        test.Context.Api.Name = "orders";

        test.RunInbound();

        test.Context.Response.StatusCode.Should().Be(200);
        test.SetupRateLimitStore().GetCount($"sub:{test.Context.Subscription.Id}").Should().Be(1);
        test.SetupRateLimitStore().GetCount($"sub:{test.Context.Subscription.Id}:api:other").Should().Be(0);
    }

    [TestMethod]
    public void RateLimit_InjectedLimiterPreservesCountersOutputsAndLocalLimits()
    {
        var limiter = new RecordingRateLimiter();
        var test = LimiterTestHarness.Create(context => context.RateLimit(new RateLimitConfig
        {
            Calls = 2,
            RenewalPeriod = 60,
            RemainingCallsHeaderName = "Remaining",
            RemainingCallsVariableName = "remaining",
            TotalCallsHeaderName = "Total"
        }), limiter: limiter);
        var key = $"sub:{test.Context.Subscription.Id}";

        test.RunInbound();
        test.Context.Response.Headers["Remaining"].Should().Equal("1");
        test.Context.Response.Headers["Total"].Should().Equal("2");
        test.Context.Variables["remaining"].Should().Be(1);
        test.SetupRateLimitStore().GetCount(key).Should().Be(1);

        LimiterTestHarness.RunRequest(test);
        LimiterTestHarness.RunRequest(test);

        test.Context.Response.StatusCode.Should().Be(429);
        test.SetupRateLimitStore().GetCount(key).Should().Be(2);
        limiter.Calls.Should().Equal(
            ($"rate-limit:{test.Context.Subscription.Id}", 1),
            ($"rate-limit:{test.Context.Subscription.Id}", 1));
    }

    [TestMethod]
    public void RateLimit_InjectedScopeRejectionDoesNotIncrementLocalCounters()
    {
        var limiter = new RecordingRateLimiter((key, _) => !key.EndsWith(":api:orders", StringComparison.Ordinal));
        var test = LimiterTestHarness.Create(context => context.RateLimit(new RateLimitConfig
        {
            Calls = 10,
            RenewalPeriod = 60,
            RetryAfterHeaderName = "Custom-Retry",
            RetryAfterVariableName = "retry",
            RemainingCallsVariableName = "remaining",
            Apis = [new ApiRateLimit { Name = "orders", Calls = 2, RenewalPeriod = 30 }]
        }), limiter: limiter);
        test.Context.Api.Name = "orders";
        var key = $"sub:{test.Context.Subscription.Id}";

        test.RunInbound();

        test.Context.Response.StatusCode.Should().Be(429);
        test.Context.Response.Headers["Custom-Retry"].Should().Equal("30");
        test.Context.Response.Headers.Should().NotContainKey("Retry-After");
        test.Context.Variables["retry"].Should().Be(30);
        test.Context.Variables["remaining"].Should().Be(0);
        test.SetupRateLimitStore().GetCount(key).Should().Be(0);
        test.SetupRateLimitStore().GetCount($"{key}:api:orders").Should().Be(0);
        limiter.Calls.Should().HaveCount(2);
    }

    [TestMethod]
    public void RateLimit_InjectedServiceErrorsRemainPolicyErrors()
    {
        var limiter = new RecordingRateLimiter((_, _) => throw new InvalidOperationException("limiter failure"));
        var test = LimiterTestHarness.Create(context =>
            context.RateLimit(new RateLimitConfig { Calls = 1, RenewalPeriod = 60 }), limiter: limiter);

        var error = Assert.ThrowsException<PolicyException>(test.RunInbound);

        error.Policy.Should().Be(nameof(IInboundContext.RateLimit));
        error.InnerException.Should().BeOfType<InvalidOperationException>();
        test.SetupRateLimitStore().GetCount($"sub:{test.Context.Subscription.Id}").Should().Be(0);
    }

    [TestMethod]
    [DataRow(-1, 60)]
    [DataRow(1, 0)]
    [DataRow(1, -1)]
    public void RateLimit_RejectsInvalidConfiguration(int calls, int renewal)
    {
        var test = LimiterTestHarness.Create(context =>
            context.RateLimit(new RateLimitConfig { Calls = calls, RenewalPeriod = renewal }));

        var error = Assert.ThrowsException<PolicyException>(test.RunInbound);

        error.InnerException.Should().BeOfType<ArgumentOutOfRangeException>();
        test.SetupRateLimitStore().GetCount($"sub:{test.Context.Subscription.Id}").Should().Be(0);
    }

    [TestMethod]
    public void RateLimit_ZeroCallsClosesTheLimit()
    {
        var test = LimiterTestHarness.Create(context =>
            context.RateLimit(new RateLimitConfig { Calls = 0, RenewalPeriod = 60 }));

        test.RunInbound();

        test.Context.Response.StatusCode.Should().Be(429);
        test.Context.Response.Headers["Retry-After"].Should().Equal("60");
        test.SetupRateLimitStore().GetCount($"sub:{test.Context.Subscription.Id}").Should().Be(0);
    }

    [TestMethod]
    public void RateLimit_CallbackPredicateAndErrorBypassDefaultCounting()
    {
        var test = new SimpleRateLimit().AsTestDocument();
        test.SetupInbound().RateLimit((_, config) => config.Calls == 3).WithCallback((context, _) =>
        {
            context.Response.StatusCode = 503;
            context.Variables["mocked"] = true;
            throw new FinishSectionProcessingException();
        });

        test.RunInbound();

        test.Context.Response.StatusCode.Should().Be(503);
        test.Context.Variables["mocked"].Should().Be(true);
        test.SetupRateLimitStore().GetCount($"sub:{test.Context.Subscription.Id}").Should().Be(0);
    }

    [TestMethod]
    public void RateLimit_RetryWaitsForEveryExceededScope()
    {
        var test = LimiterTestHarness.Create(context => context.RateLimit(new RateLimitConfig
        {
            Calls = 1,
            RenewalPeriod = 120,
            Apis = [new ApiRateLimit { Name = "orders", Calls = 1, RenewalPeriod = 30 }]
        }));
        test.Context.Api.Name = "orders";
        var key = $"sub:{test.Context.Subscription.Id}";
        test.SetupRateLimitStore().SetCount(key, 1).SetCount($"{key}:api:orders", 1);

        test.RunInbound();

        test.Context.Response.Headers["Retry-After"].Should().Equal("120");
        test.SetupRateLimitStore().GetCount(key).Should().Be(1);
        test.SetupRateLimitStore().GetCount($"{key}:api:orders").Should().Be(1);
    }

    [TestMethod]
    public void RateLimit_NoSubscriptionDoesNotConsumeOrInvokeInjectedLimiter()
    {
        var limiter = new RecordingRateLimiter();
        var test = LimiterTestHarness.Create(context =>
            context.RateLimit(new RateLimitConfig { Calls = 1, RenewalPeriod = 60 }), limiter: limiter);
        test.Context.Subscription = null!;

        test.RunInbound();

        test.Context.Response.StatusCode.Should().Be(200);
        limiter.Calls.Should().BeEmpty();
    }

    [TestMethod]
    public void RateLimit_MaximumCounterDoesNotOverflow()
    {
        var test = LimiterTestHarness.Create(context =>
            context.RateLimit(new RateLimitConfig { Calls = int.MaxValue, RenewalPeriod = 60 }));
        var key = $"sub:{test.Context.Subscription.Id}";
        test.SetupRateLimitStore().SetCount(key, int.MaxValue - 1);

        test.RunInbound();
        LimiterTestHarness.RunRequest(test);

        test.Context.Response.StatusCode.Should().Be(429);
        test.SetupRateLimitStore().GetCount(key).Should().Be(int.MaxValue);
    }

    [TestMethod]
    public void RateLimitStore_RejectsNegativeSeedAndResetsSelectedCounterOnly()
    {
        var store = new RateLimitStore();
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => store.SetCount("invalid", -1));
        store.SetCount("first", 2).SetCount("second", 3).Reset("first");

        store.GetCount("first").Should().Be(0);
        store.GetCount("second").Should().Be(3);
    }

    [TestMethod]
    [DataRow("RateLimit", "sub:shared", 429)]
    [DataRow("RateLimitByKey", "rate-limit-by-key:tenant", 429)]
    [DataRow("Quota", "quota:sub:shared", 403)]
    [DataRow("QuotaByKey", "quota-by-key:tenant", 403)]
    public void RateLimitStore_SetupSeedsReadsAndResetsInjectedStoreAcrossPolicies(string policy, string key, int deniedStatus)
    {
        var store = new RateLimitStore();
        var first = LimiterTestHarness.Create(context => ApplyStorePolicy(context, policy), store);
        var second = LimiterTestHarness.Create(context => ApplyStorePolicy(context, policy), store);
        first.Context.Subscription.Id = "shared";
        second.Context.Subscription.Id = "shared";
        first.SetupRateLimitStore().SetCount(key, 1).SetCount("unrelated", 3);

        first.RunInbound();

        first.Context.Response.StatusCode.Should().Be(deniedStatus);
        first.Context.ResponseTerminated.Should().BeTrue();
        store.GetCount(key).Should().Be(1);
        first.SetupRateLimitStore().Should().BeSameAs(store);
        second.SetupRateLimitStore().Should().BeSameAs(store);
        second.SetupRateLimitStore().GetCount(key).Should().Be(1);

        first.SetupRateLimitStore().Reset(key);
        second.SetupRateLimitStore().GetCount(key).Should().Be(0);
        second.SetupRateLimitStore().GetCount("unrelated").Should().Be(3);
        second.RunInbound();

        second.Context.Response.StatusCode.Should().Be(200);
        store.GetCount(key).Should().Be(1);
        first.SetupRateLimitStore().GetCount(key).Should().Be(1);

        second.SetupRateLimitStore().Reset();
        first.SetupRateLimitStore().GetCount(key).Should().Be(0);
        first.SetupRateLimitStore().GetCount("unrelated").Should().Be(0);
        LimiterTestHarness.RunRequest(first);
        first.Context.Response.StatusCode.Should().Be(200);
        store.GetCount(key).Should().Be(1);
    }

    [TestMethod]
    public void RateLimitStore_SetupStillUsesFallbackWhenNoStoreIsRegistered()
    {
        var test = LimiterTestHarness.Create(context =>
            context.RateLimit(new RateLimitConfig { Calls = 1, RenewalPeriod = 60 }));
        var fallback = test.SetupRateLimitStore();
        var key = $"sub:{test.Context.Subscription.Id}";
        test.Context.Services.Resolve<RateLimitStore>().Should().BeNull();
        fallback.SetCount(key, 1);

        test.RunInbound();

        test.Context.Response.StatusCode.Should().Be(429);
        test.SetupRateLimitStore().Should().BeSameAs(fallback);
        test.SetupRateLimitStore().Reset(key);
        LimiterTestHarness.RunRequest(test);
        test.Context.Response.StatusCode.Should().Be(200);
        fallback.GetCount(key).Should().Be(1);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void RateLimit_OutputsReplaceCaseVariantsWithoutReplacingHeaderDictionary(bool ignoreCase)
    {
        var comparer = ignoreCase ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        var unrelated = new[] { "first", "second" };
        var headers = new Dictionary<string, string[]>(comparer)
        {
            ["remaining"] = ["99"],
            ["REMAINING"] = ["98"],
            ["total"] = ["99"],
            ["TOTAL"] = ["98"],
            ["X-Unrelated"] = unrelated
        };
        var test = LimiterTestHarness.Create(context => context.RateLimit(new RateLimitConfig
        {
            Calls = 3,
            RenewalPeriod = 60,
            RemainingCallsHeaderName = "Remaining",
            RemainingCallsVariableName = "remaining",
            TotalCallsHeaderName = "Total"
        }));
        test.Context.Response.Headers = headers;
        var body = test.Context.Response.Body;
        body.Content = "caf\u00e9 \ud83d\ude42";

        test.RunInbound();

        test.Context.Response.Headers.Should().BeSameAs(headers);
        headers.Comparer.Should().BeSameAs(comparer);
        headers.Keys.Count(name => name.Equals("Remaining", StringComparison.OrdinalIgnoreCase)).Should().Be(1);
        headers.Keys.Count(name => name.Equals("Total", StringComparison.OrdinalIgnoreCase)).Should().Be(1);
        headers.Keys.Should().Contain("Remaining").And.Contain("Total");
        headers["Remaining"].Should().Equal("2");
        headers["Total"].Should().Equal("3");
        headers["X-Unrelated"].Should().BeSameAs(unrelated);
        test.Context.Variables["remaining"].Should().Be(2);
        test.Context.Response.Body.Should().BeSameAs(body);
        body.Content.Should().Be("caf\u00e9 \ud83d\ude42");
        body.Consumed.Should().BeFalse();
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("X-Retry")]
    public void RateLimit_RecoveryRemovesEveryCaseVariantOfConfiguredRetryHeader(string? retryHeader)
    {
        var name = retryHeader ?? "Retry-After";
        var unrelated = new[] { "retained" };
        var headers = new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            [name.ToLowerInvariant()] = ["99"],
            [name.ToUpperInvariant()] = ["98"],
            ["remaining"] = ["99"],
            ["X-Unrelated"] = unrelated
        };
        var test = LimiterTestHarness.Create(context => context.RateLimit(new RateLimitConfig
        {
            Calls = 3,
            RenewalPeriod = 60,
            RetryAfterHeaderName = retryHeader,
            RemainingCallsHeaderName = "Remaining"
        }));
        test.Context.Response.Headers = headers;
        test.Context.Response.StatusCode = 429;

        test.RunInbound();

        test.Context.Response.StatusCode.Should().Be(200);
        test.Context.Response.Headers.Should().BeSameAs(headers);
        headers.Comparer.Should().BeSameAs(StringComparer.Ordinal);
        headers.Keys.Should().NotContain(key => key.Equals(name, StringComparison.OrdinalIgnoreCase));
        headers.Keys.Count(key => key.Equals("Remaining", StringComparison.OrdinalIgnoreCase)).Should().Be(1);
        headers["Remaining"].Should().Equal("2");
        headers["X-Unrelated"].Should().BeSameAs(unrelated);
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("X-Retry")]
    public void RateLimit_RejectionWritesSingleCanonicalConfiguredOutputHeaders(string? retryHeader)
    {
        var name = retryHeader ?? "Retry-After";
        var headers = new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            [name.ToLowerInvariant()] = ["99"],
            [name.ToUpperInvariant()] = ["98"],
            ["remaining"] = ["99"],
            ["total"] = ["99"]
        };
        var test = LimiterTestHarness.Create(context => context.RateLimit(new RateLimitConfig
        {
            Calls = 0,
            RenewalPeriod = 60,
            RetryAfterHeaderName = retryHeader,
            RemainingCallsHeaderName = "Remaining",
            TotalCallsHeaderName = "Total"
        }));
        test.Context.Response.Headers = headers;

        test.RunInbound();

        test.Context.Response.StatusCode.Should().Be(429);
        test.Context.Response.Headers.Should().BeSameAs(headers);
        headers.Comparer.Should().BeSameAs(StringComparer.Ordinal);
        headers.Keys.Should().BeEquivalentTo(new[] { name, "Remaining", "Total" });
        headers[name].Should().Equal("60");
        headers["Remaining"].Should().Equal("0");
        headers["Total"].Should().Equal("0");
    }

    [TestMethod]
    public void RateLimit_HeaderReplacementUsesOrdinalCasingWithoutChangingUnicodeHeaders()
    {
        var previousCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR");
            var untouched = new[] { "caf\u00e9" };
            var headers = new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                ["limit"] = ["99"],
                ["LIMIT"] = ["98"],
                ["l\u0131m\u0131t"] = untouched
            };
            var test = LimiterTestHarness.Create(context => context.RateLimit(new RateLimitConfig
            {
                Calls = 1234,
                RenewalPeriod = 60,
                TotalCallsHeaderName = "Limit"
            }));
            test.Context.Response.Headers = headers;

            test.RunInbound();

            headers.Keys.Count(name => name.Equals("Limit", StringComparison.OrdinalIgnoreCase)).Should().Be(1);
            headers.Keys.Should().Contain("Limit");
            headers["Limit"].Should().Equal("1234");
            headers["l\u0131m\u0131t"].Should().BeSameAs(untouched);
            test.Context.Response.Headers.Should().BeSameAs(headers);
            headers.Comparer.Should().BeSameAs(StringComparer.Ordinal);
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
        }
    }

    private static void ApplyStorePolicy(IInboundContext context, string policy)
    {
        switch (policy)
        {
            case "RateLimit":
                context.RateLimit(new RateLimitConfig { Calls = 1, RenewalPeriod = 60 });
                break;
            case "RateLimitByKey":
                context.RateLimitByKey(new RateLimitByKeyConfig { CounterKey = "tenant", Calls = 1, RenewalPeriod = 60 });
                break;
            case "Quota":
                context.Quota(new QuotaConfig { Calls = 1, RenewalPeriod = 300 });
                break;
            case "QuotaByKey":
                context.QuotaByKey(new QuotaByKeyConfig { CounterKey = "tenant", Calls = 1, RenewalPeriod = 300 });
                break;
            default:
                throw new ArgumentException(policy);
        }
    }
}

internal sealed class LimiterTestTimeProvider : TimeProvider
{
    private DateTimeOffset _now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    public override DateTimeOffset GetUtcNow() => _now;

    public void Advance(TimeSpan elapsed)
    {
        if (elapsed < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(elapsed));
        }

        _now += elapsed;
    }
}

internal sealed class RecordingRateLimiter(Func<string, int, bool>? consume = null) : IRateLimiter
{
    public List<(string Key, int Permits)> Calls { get; } = [];

    public Task<bool> TryConsumeAsync(string key, int permits = 1, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Calls.Add((key, permits));
        return Task.FromResult(consume?.Invoke(key, permits) ?? true);
    }
}

internal static class LimiterTestHarness
{
    private sealed class Document(Action<IInboundContext> inbound) : IDocument
    {
        public void Inbound(IInboundContext context) => inbound(context);
    }

    public static TestDocument Create(
        Action<IInboundContext> inbound,
        RateLimitStore? store = null,
        TimeProvider? clock = null,
        IRateLimiter? limiter = null)
    {
        var test = new Document(inbound).AsTestDocument();
        clock ??= new LimiterTestTimeProvider();
        test.Context.Services.Register(clock);
        test.Context.Subscription.CreatedDate = clock.GetUtcNow().UtcDateTime;
        if (store is not null)
        {
            test.Context.Services.Register(store);
        }

        if (limiter is not null)
        {
            test.Context.Services.Register(limiter);
        }

        return test;
    }

    public static void RunRequest(TestDocument test)
    {
        test.Context.RequestId = Guid.NewGuid();
        test.Context.ResponseTerminated = false;
        test.Context.Response.StatusCode = 200;
        test.Context.Response.StatusReason = "OK";
        test.Context.Response.Headers.Clear();
        test.RunInbound();
    }
}