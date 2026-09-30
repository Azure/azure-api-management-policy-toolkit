// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Document;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Data;

namespace Test.Emulator.Emulator.Policies;

[TestClass]
public class RateLimitByKeyTests
{
    private static RateLimitByKeyConfig Config => new()
    {
        CounterKey = "customer",
        Calls = 5,
        RenewalPeriod = 60,
        RemainingCallsHeaderName = "Remaining",
        RemainingCallsVariableName = "remaining",
        TotalCallsHeaderName = "Total",
        RetryAfterVariableName = "retry"
    };

    [TestMethod]
    public void RateLimitByKey_CountsActualIncrementAndWritesOutputs()
    {
        var test = LimiterTestHarness.Create(context =>
        {
            context.RateLimitByKey(Config with { IncrementCount = 2 });
            context.SetVariable("after", true);
        });

        test.RunInbound();

        test.Context.Response.StatusCode.Should().Be(200);
        test.Context.Variables["after"].Should().Be(true);
        test.Context.Variables["remaining"].Should().Be(3);
        test.Context.Response.Headers["Remaining"].Should().Equal("3");
        test.Context.Response.Headers["Total"].Should().Equal("5");
        test.SetupRateLimitStore().GetCount("rate-limit-by-key:customer").Should().Be(2);
    }

    [TestMethod]
    public void RateLimitByKey_FalseConditionDoesNotConsumePermitsOrReduceRemaining()
    {
        var limiter = new RecordingRateLimiter();
        var test = LimiterTestHarness.Create(context =>
            context.RateLimitByKey(Config with { IncrementCondition = false, IncrementCount = 4 }), limiter: limiter);

        test.RunInbound();

        test.Context.Variables["remaining"].Should().Be(5);
        test.Context.Response.Headers["Remaining"].Should().Equal("5");
        test.SetupRateLimitStore().GetCount("rate-limit-by-key:customer").Should().Be(0);
        limiter.Calls.Should().Equal(("customer", 0));
    }

    [TestMethod]
    public void RateLimitByKey_EvaluatesExpressionKeysLimitsAndIncrements()
    {
        var test = LimiterTestHarness.Create(context => context.RateLimitByKey(new RateLimitByKeyConfig
        {
            CounterKey = context.ExpressionContext.Request.IpAddress,
            Calls = (int)context.ExpressionContext.Variables["calls"],
            RenewalPeriod = (int)context.ExpressionContext.Variables["period"],
            IncrementCount = (int)context.ExpressionContext.Variables["increment"],
            IncrementCondition = (bool)context.ExpressionContext.Variables["count"],
            RemainingCallsVariableName = "remaining"
        }));
        test.Context.Variables["calls"] = 6;
        test.Context.Variables["period"] = 60;
        test.Context.Variables["increment"] = 3;
        test.Context.Variables["count"] = true;
        test.Context.Request.IpAddress = "192.0.2.1";

        test.RunInbound();
        test.SetupRateLimitStore().GetCount("rate-limit-by-key:192.0.2.1").Should().Be(3);
        test.Context.Variables["remaining"].Should().Be(3);

        test.Context.Request.IpAddress = "192.0.2.2";
        test.Context.Variables["count"] = false;
        LimiterTestHarness.RunRequest(test);
        test.SetupRateLimitStore().GetCount("rate-limit-by-key:192.0.2.2").Should().Be(0);
        test.Context.Variables["remaining"].Should().Be(6);
    }

    [TestMethod]
    public void RateLimitByKey_RejectsIncrementThatWouldExceedBudget()
    {
        var test = LimiterTestHarness.Create(context => context.RateLimitByKey(Config with
        {
            Calls = 3,
            IncrementCount = 2,
            RetryAfterHeaderName = "Custom-Retry"
        }));
        test.RunInbound();

        LimiterTestHarness.RunRequest(test);

        test.Context.Response.StatusCode.Should().Be(429);
        test.Context.ResponseTerminated.Should().BeTrue();
        test.Context.Response.Headers["Custom-Retry"].Should().Equal("60");
        test.Context.Response.Headers.Should().NotContainKey("Retry-After");
        test.Context.Response.Headers["Remaining"].Should().Equal("0");
        test.Context.Variables["retry"].Should().Be(60);
        test.Context.Variables["remaining"].Should().Be(0);
        test.SetupRateLimitStore().GetCount("rate-limit-by-key:customer").Should().Be(2);
    }

    [TestMethod]
    public void RateLimitByKey_ZeroIncrementPreservesBudget()
    {
        var test = LimiterTestHarness.Create(context => context.RateLimitByKey(Config with { IncrementCount = 0 }));

        test.RunInbound();

        test.Context.Response.StatusCode.Should().Be(200);
        test.Context.Variables["remaining"].Should().Be(5);
        test.SetupRateLimitStore().GetCount("rate-limit-by-key:customer").Should().Be(0);
    }

    [TestMethod]
    public void RateLimitByKey_LargeIncrementDoesNotOverflow()
    {
        var test = LimiterTestHarness.Create(context => context.RateLimitByKey(Config with
        {
            Calls = int.MaxValue,
            IncrementCount = int.MaxValue
        }));

        test.RunInbound();
        test.Context.Variables["remaining"].Should().Be(0);
        LimiterTestHarness.RunRequest(test);

        test.Context.Response.StatusCode.Should().Be(429);
        test.SetupRateLimitStore().GetCount("rate-limit-by-key:customer").Should().Be(int.MaxValue);
    }

    [TestMethod]
    public void RateLimitByKey_RenewsSlidingCallsWithInjectedClock()
    {
        var clock = new LimiterTestTimeProvider();
        var test = LimiterTestHarness.Create(context => context.RateLimitByKey(Config with { Calls = 2 }), clock: clock);
        test.RunInbound();
        clock.Advance(TimeSpan.FromSeconds(30));
        LimiterTestHarness.RunRequest(test);
        clock.Advance(TimeSpan.FromSeconds(30));

        LimiterTestHarness.RunRequest(test);

        test.Context.Response.StatusCode.Should().Be(200);
        test.SetupRateLimitStore().GetCount("rate-limit-by-key:customer").Should().Be(2);
        LimiterTestHarness.RunRequest(test);
        test.Context.Response.Headers["Retry-After"].Should().Equal("30");
    }

    [TestMethod]
    public void RateLimitByKey_SharedStoreSharesKeysAcrossContextsButNotDifferentKeys()
    {
        var store = new RateLimitStore();
        var clock = new LimiterTestTimeProvider();
        var first = LimiterTestHarness.Create(context =>
            context.RateLimitByKey(Config with { Calls = 1 }), store, clock);
        var same = LimiterTestHarness.Create(context =>
            context.RateLimitByKey(Config with { Calls = 1 }), store, clock);
        var other = LimiterTestHarness.Create(context =>
            context.RateLimitByKey(Config with { CounterKey = "other", Calls = 1 }), store, clock);

        first.RunInbound();
        same.RunInbound();
        other.RunInbound();

        same.Context.Response.StatusCode.Should().Be(429);
        other.Context.Response.StatusCode.Should().Be(200);
        store.GetCount("rate-limit-by-key:customer").Should().Be(1);
        store.GetCount("rate-limit-by-key:other").Should().Be(1);
    }

    [TestMethod]
    public void RateLimitByKey_KeyCannotCollideWithSubscriptionCounter()
    {
        var test = LimiterTestHarness.Create(context =>
        {
            context.RateLimit(new RateLimitConfig { Calls = 10, RenewalPeriod = 60 });
            context.RateLimitByKey(Config with { CounterKey = $"sub:{context.ExpressionContext.Subscription.Id}", Calls = 1 });
        });

        test.RunInbound();
        LimiterTestHarness.RunRequest(test);

        var key = $"sub:{test.Context.Subscription.Id}";
        test.Context.Response.StatusCode.Should().Be(429);
        test.SetupRateLimitStore().GetCount(key).Should().Be(2);
        test.SetupRateLimitStore().GetCount($"rate-limit-by-key:{key}").Should().Be(1);
    }

    [TestMethod]
    public void RateLimitByKey_InjectedLimiterHasDefaultPathParity()
    {
        var limiter = new RecordingRateLimiter();
        var test = LimiterTestHarness.Create(context =>
            context.RateLimitByKey(Config with { Calls = 3, IncrementCount = 2 }), limiter: limiter);

        test.RunInbound();
        test.Context.Response.Headers["Remaining"].Should().Equal("1");
        LimiterTestHarness.RunRequest(test);

        test.Context.Response.StatusCode.Should().Be(429);
        test.SetupRateLimitStore().GetCount("rate-limit-by-key:customer").Should().Be(2);
        limiter.Calls.Should().Equal(("customer", 2));
    }

    [TestMethod]
    public void RateLimitByKey_InjectedRejectionWritesOutputsAndStopsFollowingPolicy()
    {
        var limiter = new RecordingRateLimiter((_, _) => false);
        var test = LimiterTestHarness.Create(context =>
        {
            context.RateLimitByKey(Config);
            context.SetVariable("after", true);
        }, limiter: limiter);

        test.RunInbound();

        test.Context.Response.StatusCode.Should().Be(429);
        test.Context.Response.Headers["Retry-After"].Should().Equal("60");
        test.Context.Response.Headers["Remaining"].Should().Equal("0");
        test.Context.Response.Headers["Total"].Should().Equal("5");
        test.Context.Variables.Should().NotContainKey("after");
        test.SetupRateLimitStore().GetCount("rate-limit-by-key:customer").Should().Be(0);
    }

    [TestMethod]
    public void RateLimitByKey_DeferredIncrementsSettleAtResponseCompletion()
    {
        var test = LimiterTestHarness.Create(context => context.RateLimitByKey(Config with
        {
            Calls = 3,
            IncrementCount = 2,
            IncrementAfterResponse = true
        }));

        test.RunInbound();
        test.SetupRateLimitStore().GetCount("rate-limit-by-key:customer").Should().Be(0);
        test.Context.Variables["remaining"].Should().Be(3);

        test.CompleteLimiterResponse();

        test.SetupRateLimitStore().GetCount("rate-limit-by-key:customer").Should().Be(2);
        test.Context.Variables["remaining"].Should().Be(1);
        test.CompleteLimiterResponse();
        test.SetupRateLimitStore().GetCount("rate-limit-by-key:customer").Should().Be(2);
    }

    [TestMethod]
    public void RateLimitByKey_DeferredOverageIsCheckedOnNextRequest()
    {
        var test = LimiterTestHarness.Create(context => context.RateLimitByKey(Config with
        {
            Calls = 2,
            IncrementCount = 2,
            IncrementAfterResponse = true
        }));

        test.RunInbound();
        test.RunInbound();
        test.CompleteLimiterResponse();
        test.SetupRateLimitStore().GetCount("rate-limit-by-key:customer").Should().Be(4);
        test.Context.Variables["remaining"].Should().Be(0);

        LimiterTestHarness.RunRequest(test);

        test.Context.Response.StatusCode.Should().Be(429);
        test.SetupRateLimitStore().GetCount("rate-limit-by-key:customer").Should().Be(4);
    }

    [TestMethod]
    public void RateLimitByKey_DeferredInjectedLimiterUsesZeroForAdmissionAndActualPermitsAtCompletion()
    {
        var limiter = new RecordingRateLimiter();
        var test = LimiterTestHarness.Create(context => context.RateLimitByKey(Config with
        {
            IncrementCount = 2,
            IncrementAfterResponse = true
        }), limiter: limiter);

        test.RunInbound();
        limiter.Calls.Should().Equal(("customer", 0));
        test.CompleteLimiterResponse();

        limiter.Calls.Should().Equal(("customer", 0), ("customer", 2));
        test.SetupRateLimitStore().GetCount("rate-limit-by-key:customer").Should().Be(2);
        test.Context.Variables["remaining"].Should().Be(3);
    }

    [TestMethod]
    public void RateLimitByKey_DeferredInjectedRejectionIsExplicit()
    {
        var limiter = new RecordingRateLimiter((_, permits) => permits == 0);
        var test = LimiterTestHarness.Create(context =>
            context.RateLimitByKey(Config with { IncrementAfterResponse = true }), limiter: limiter);

        test.RunInbound();
        test.CompleteLimiterResponse();

        test.Context.Response.StatusCode.Should().Be(429);
        test.Context.ResponseTerminated.Should().BeTrue();
        test.Context.Response.Headers["Retry-After"].Should().Equal("60");
        test.SetupRateLimitStore().GetCount("rate-limit-by-key:customer").Should().Be(0);
    }

    [TestMethod]
    public void RateLimitByKey_CallbackPredicateCanOverrideAndSkipCounting()
    {
        var test = LimiterTestHarness.Create(context =>
        {
            context.RateLimitByKey(Config);
            context.RateLimitByKey(Config with { CounterKey = "other" });
        });
        test.SetupInbound().RateLimitByKey((_, config) => config.CounterKey == "customer")
            .WithCallback((context, config) => context.Variables["callback-key"] = config.CounterKey);

        test.RunInbound();

        test.Context.Variables["callback-key"].Should().Be("customer");
        test.SetupRateLimitStore().GetCount("rate-limit-by-key:customer").Should().Be(0);
        test.SetupRateLimitStore().GetCount("rate-limit-by-key:other").Should().Be(1);
    }

    [TestMethod]
    public void RateLimitByKey_CallbackErrorsKeepPolicyMetadata()
    {
        var test = LimiterTestHarness.Create(context => context.RateLimitByKey(Config));
        test.SetupInbound().RateLimitByKey().WithCallback((_, _) => throw new InvalidOperationException("mock failure"));

        var error = Assert.ThrowsException<PolicyException>(test.RunInbound);

        error.Policy.Should().Be(nameof(IInboundContext.RateLimitByKey));
        error.Section.Should().Be(nameof(IInboundContext));
        error.InnerException.Should().BeOfType<InvalidOperationException>();
        test.SetupRateLimitStore().GetCount("rate-limit-by-key:customer").Should().Be(0);
    }

    [TestMethod]
    [DataRow(-1, 60, 1)]
    [DataRow(1, 0, 1)]
    [DataRow(1, -1, 1)]
    [DataRow(1, 60, -1)]
    public void RateLimitByKey_RejectsNegativeOrInvalidConfiguration(int calls, int period, int increment)
    {
        var limiter = new RecordingRateLimiter();
        var test = LimiterTestHarness.Create(context => context.RateLimitByKey(Config with
        {
            Calls = calls,
            RenewalPeriod = period,
            IncrementCount = increment
        }), limiter: limiter);

        var error = Assert.ThrowsException<PolicyException>(test.RunInbound);

        error.InnerException.Should().BeOfType<ArgumentOutOfRangeException>();
        limiter.Calls.Should().BeEmpty();
    }

    [TestMethod]
    public void RateLimitByKey_RejectsEmptyKey()
    {
        var test = LimiterTestHarness.Create(context => context.RateLimitByKey(Config with { CounterKey = "" }));

        Assert.ThrowsException<PolicyException>(test.RunInbound).InnerException.Should().BeOfType<ArgumentException>();
    }

    [TestMethod]
    public void RateLimitByKey_ZeroCallsDeniesWithoutIncrementing()
    {
        var test = LimiterTestHarness.Create(context => context.RateLimitByKey(Config with { Calls = 0 }));

        test.RunInbound();

        test.Context.Response.StatusCode.Should().Be(429);
        test.SetupRateLimitStore().GetCount("rate-limit-by-key:customer").Should().Be(0);
    }

    [TestMethod]
    public void RateLimitByKey_FalseConditionDoesNotBypassAnExhaustedCounter()
    {
        var count = true;
        var test = LimiterTestHarness.Create(context =>
            context.RateLimitByKey(Config with { Calls = 1, IncrementCondition = count }));
        test.RunInbound();
        count = false;

        LimiterTestHarness.RunRequest(test);

        test.Context.Response.StatusCode.Should().Be(429);
        test.SetupRateLimitStore().GetCount("rate-limit-by-key:customer").Should().Be(1);
    }

    [TestMethod]
    public void RateLimitByKey_UnsettledResponseCannotBeSilentlyAppliedToAnotherRequest()
    {
        var test = LimiterTestHarness.Create(context =>
            context.RateLimitByKey(Config with { IncrementAfterResponse = true }));
        test.RunInbound();

        var error = Assert.ThrowsExactly<PolicyException>(() => LimiterTestHarness.RunRequest(test));

        error.InnerException.Should().BeOfType<InvalidOperationException>();
        test.SetupRateLimitStore().GetCount("rate-limit-by-key:customer").Should().Be(0);
    }
}