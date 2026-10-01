// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Document;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Data;

namespace Test.Emulator.Emulator.Policies;

[TestClass]
public class QuotaByKeyTests
{
    class SimpleQuotaByKey : IDocument
    {
        public void Inbound(IInboundContext context)
        {
            context.QuotaByKey(new QuotaByKeyConfig
            {
                CounterKey = "ip-based",
                RenewalPeriod = 3600,
                Calls = 1000,
            });
        }

        public void Backend(IBackendContext context) { }
        public void Outbound(IOutboundContext context) { }
        public void OnError(IOnErrorContext context) { }
    }

    [TestMethod]
    public void QuotaByKey_Inbound_ShouldExecuteWithoutError()
    {
        // Arrange
        var test = new TestDocument(new SimpleQuotaByKey());

        test.RunInbound();

        test.SetupRateLimitStore().GetCount("quota-by-key:ip-based").Should().Be(1);
    }

    [TestMethod]
    public void QuotaByKey_Inbound_Callback()
    {
        // Arrange
        var test = new TestDocument(new SimpleQuotaByKey());
        var executedCallback = false;

        test.SetupInbound().QuotaByKey().WithCallback((_, _) =>
        {
            executedCallback = true;
        });

        // Act
        test.RunInbound();

        // Assert
        executedCallback.Should().BeTrue();
    }

    [TestMethod]
    public void QuotaByKey_Inbound_CallbackWithPredicate()
    {
        // Arrange
        var test = new TestDocument(new SimpleQuotaByKey());
        var executedCallback = false;

        test.SetupInbound()
            .QuotaByKey((_, config) => config.CounterKey == "ip-based")
            .WithCallback((context, _) =>
            {
                executedCallback = true;
                context.Variables["quota-checked"] = true;
            });

        // Act
        test.RunInbound();

        // Assert
        executedCallback.Should().BeTrue();
        test.Context.Variables.Should().ContainKey("quota-checked")
            .WhoseValue.Should().Be(true);
    }

    [TestMethod]
    public void QuotaByKey_Inbound_PredicateNotMatching()
    {
        // Arrange
        var test = new TestDocument(new SimpleQuotaByKey());
        var executedCallback = false;

        test.SetupInbound()
            .QuotaByKey((_, config) => config.CounterKey == "other-key")
            .WithCallback((_, _) =>
            {
                executedCallback = true;
            });

        test.RunInbound();

        // Assert
        executedCallback.Should().BeFalse();
        test.SetupRateLimitStore().GetCount("quota-by-key:ip-based").Should().Be(1);
    }

    private static QuotaByKeyConfig Config => new()
    {
        CounterKey = "tenant",
        Calls = 5,
        RenewalPeriod = 300
    };

    [TestMethod]
    public void QuotaByKey_CountsWeightedCallsOnlyOncePerRequest()
    {
        var limiter = new RecordingRateLimiter();
        var test = LimiterTestHarness.Create(context =>
        {
            context.QuotaByKey(Config with { IncrementCount = 2 });
            context.QuotaByKey(Config with { IncrementCount = 2 });
            context.SetVariable("after", true);
        }, limiter: limiter);

        test.RunInbound();
        test.Context.Variables["after"].Should().Be(true);
        test.SetupRateLimitStore().GetCount("quota-by-key:tenant").Should().Be(2);
        LimiterTestHarness.RunRequest(test);
        test.SetupRateLimitStore().GetCount("quota-by-key:tenant").Should().Be(4);
        test.Context.Variables.Remove("after");
        LimiterTestHarness.RunRequest(test);

        test.Context.Response.StatusCode.Should().Be(403);
        test.Context.ResponseTerminated.Should().BeTrue();
        test.Context.Response.Headers["Retry-After"].Should().Equal("300");
        test.Context.Variables.Should().NotContainKey("after");
        test.SetupRateLimitStore().GetCount("quota-by-key:tenant").Should().Be(4);
        limiter.Calls.Should().Equal(("tenant", 2), ("tenant", 2));
    }

    [TestMethod]
    public void QuotaByKey_DuplicateAtExactBudgetDoesNotRejectSameRequest()
    {
        var test = LimiterTestHarness.Create(context =>
        {
            context.QuotaByKey(Config with { Calls = 2, IncrementCount = 2 });
            context.QuotaByKey(Config with { Calls = 2, IncrementCount = 2 });
            context.SetVariable("after", true);
        });

        test.RunInbound();

        test.Context.Response.StatusCode.Should().Be(200);
        test.Context.Variables["after"].Should().Be(true);
        test.SetupRateLimitStore().GetCount("quota-by-key:tenant").Should().Be(2);
    }

    [TestMethod]
    public void QuotaByKey_ExpressionKeysConditionsAndCountsAreEvaluated()
    {
        var test = LimiterTestHarness.Create(context => context.QuotaByKey(new QuotaByKeyConfig
        {
            CounterKey = context.ExpressionContext.Request.IpAddress,
            Calls = 6,
            RenewalPeriod = 300,
            IncrementCount = (int)context.ExpressionContext.Variables["increment"],
            IncrementCondition = (bool)context.ExpressionContext.Variables["count"]
        }));
        test.Context.Variables["increment"] = 3;
        test.Context.Variables["count"] = false;
        test.Context.Request.IpAddress = "192.0.2.1";

        test.RunInbound();
        test.SetupRateLimitStore().GetCount("quota-by-key:192.0.2.1").Should().Be(0);
        test.Context.Variables["count"] = true;
        LimiterTestHarness.RunRequest(test);
        test.SetupRateLimitStore().GetCount("quota-by-key:192.0.2.1").Should().Be(3);
        test.Context.Request.IpAddress = "192.0.2.2";
        LimiterTestHarness.RunRequest(test);

        test.SetupRateLimitStore().GetCount("quota-by-key:192.0.2.1").Should().Be(3);
        test.SetupRateLimitStore().GetCount("quota-by-key:192.0.2.2").Should().Be(3);
    }

    [TestMethod]
    public void QuotaByKey_FalseConditionSkipsCountingButStillChecksExhaustion()
    {
        var count = false;
        var limiter = new RecordingRateLimiter();
        var test = LimiterTestHarness.Create(context =>
            context.QuotaByKey(Config with { Calls = 3, IncrementCount = 3, IncrementCondition = count }), limiter: limiter);

        test.RunInbound();
        test.SetupRateLimitStore().GetCount("quota-by-key:tenant").Should().Be(0);
        count = true;
        LimiterTestHarness.RunRequest(test);
        count = false;
        LimiterTestHarness.RunRequest(test);

        test.Context.Response.StatusCode.Should().Be(403);
        test.SetupRateLimitStore().GetCount("quota-by-key:tenant").Should().Be(3);
        limiter.Calls.Should().Equal(("tenant", 0), ("tenant", 3));
    }

    [TestMethod]
    public void QuotaByKey_FalseConditionDoesNotPreventLaterMatchingPolicyFromCounting()
    {
        var test = LimiterTestHarness.Create(context =>
        {
            context.QuotaByKey(Config with { IncrementCondition = false });
            context.QuotaByKey(Config with { IncrementCondition = true, IncrementCount = 2 });
        });

        test.RunInbound();

        test.SetupRateLimitStore().GetCount("quota-by-key:tenant").Should().Be(2);
    }

    [TestMethod]
    public void QuotaByKey_FirstPeriodStartControlsFixedWindowAndRetry()
    {
        var clock = new LimiterTestTimeProvider();
        clock.Advance(TimeSpan.FromSeconds(15));
        var test = LimiterTestHarness.Create(context => context.QuotaByKey(Config with
        {
            Calls = 1,
            FirstPeriodStart = "2026-01-01T00:00:10Z"
        }), clock: clock);
        test.RunInbound();
        clock.Advance(TimeSpan.FromSeconds(294.1));
        LimiterTestHarness.RunRequest(test);
        test.Context.Response.Headers["Retry-After"].Should().Equal("1");
        clock.Advance(TimeSpan.FromSeconds(0.9));

        LimiterTestHarness.RunRequest(test);

        test.Context.Response.StatusCode.Should().Be(200);
        test.SetupRateLimitStore().GetCount("quota-by-key:tenant").Should().Be(1);
    }

    [TestMethod]
    public void QuotaByKey_DefaultFixedWindowRenewsAtBoundary()
    {
        var clock = new LimiterTestTimeProvider();
        var test = LimiterTestHarness.Create(context => context.QuotaByKey(Config with { Calls = 1 }), clock: clock);
        test.RunInbound();
        clock.Advance(TimeSpan.FromSeconds(299.5));
        LimiterTestHarness.RunRequest(test);
        test.Context.Response.Headers["Retry-After"].Should().Equal("1");
        clock.Advance(TimeSpan.FromSeconds(0.5));

        LimiterTestHarness.RunRequest(test);

        test.Context.Response.StatusCode.Should().Be(200);
        test.SetupRateLimitStore().GetCount("quota-by-key:tenant").Should().Be(1);
    }

    [TestMethod]
    public void QuotaByKey_LifetimeQuotaDoesNotRenew()
    {
        var clock = new LimiterTestTimeProvider();
        var test = LimiterTestHarness.Create(context =>
            context.QuotaByKey(Config with { Calls = 1, RenewalPeriod = 0 }), clock: clock);
        test.RunInbound();
        clock.Advance(TimeSpan.FromDays(36500));

        LimiterTestHarness.RunRequest(test);

        test.Context.Response.StatusCode.Should().Be(403);
        test.Context.Response.Headers.Should().NotContainKey("Retry-After");
        test.SetupRateLimitStore().GetCount("quota-by-key:tenant").Should().Be(1);
    }

    [TestMethod]
    public void QuotaByKey_BandwidthUsesKilobytesAndIncludesFinalResponse()
    {
        var test = LimiterTestHarness.Create(context =>
            context.QuotaByKey(Config with { Calls = null, Bandwidth = 1, IncrementCount = 2 }));
        test.Context.Request.Body.Content = new string('a', 512);
        test.RunInbound();
        test.Context.Response.Body.Content = new string('\u00e9', 256);
        test.CompleteLimiterResponse();
        test.Context.Request.Body.Consumed.Should().BeFalse();
        test.Context.Response.Body.Consumed.Should().BeFalse();
        test.SetupRateLimitStore().GetCount("quota-by-key:tenant").Should().Be(2);
        test.Context.Request.Body.Content = "";
        test.Context.Response.Body.Content = "";

        LimiterTestHarness.RunRequest(test);

        test.Context.Response.StatusCode.Should().Be(403);
        test.SetupRateLimitStore().GetCount("quota-by-key:tenant").Should().Be(2);
    }

    [TestMethod]
    public void QuotaByKey_FalseConditionSkipsBandwidthAndInjectedConsumption()
    {
        var limiter = new RecordingRateLimiter();
        var test = LimiterTestHarness.Create(context =>
            context.QuotaByKey(Config with { Calls = null, Bandwidth = 1, IncrementCondition = false }), limiter: limiter);
        test.Context.Request.Body.Content = new string('a', 1025);

        test.RunInbound();
        test.CompleteLimiterResponse();
        LimiterTestHarness.RunRequest(test);

        test.Context.Response.StatusCode.Should().Be(200);
        test.SetupRateLimitStore().GetCount("quota-by-key:tenant").Should().Be(0);
        limiter.Calls.Should().Equal(("tenant", 0), ("tenant", 0));
    }

    [TestMethod]
    public void QuotaByKey_DoesNotDoubleChargeResponseBandwidthWithinRequest()
    {
        var test = LimiterTestHarness.Create(context =>
        {
            context.QuotaByKey(Config with { Calls = null, Bandwidth = 1 });
            context.QuotaByKey(Config with { Calls = null, Bandwidth = 1 });
        });
        test.RunInbound();
        test.Context.Response.Body.Content = new string('a', 600);
        test.CompleteLimiterResponse();
        test.Context.Response.Body.Content = "";

        LimiterTestHarness.RunRequest(test);

        test.Context.Response.StatusCode.Should().Be(200);
        test.SetupRateLimitStore().GetCount("quota-by-key:tenant").Should().Be(2);
        test.CompleteLimiterResponse();
    }

    [TestMethod]
    public void QuotaByKey_SharedCounterIgnoresSubscriptionAndApiButSeparatesKeys()
    {
        var store = new RateLimitStore();
        var clock = new LimiterTestTimeProvider();
        var first = LimiterTestHarness.Create(context =>
            context.QuotaByKey(Config with { Calls = 1 }), store, clock);
        var same = LimiterTestHarness.Create(context =>
            context.QuotaByKey(Config with { Calls = 1 }), store, clock);
        var other = LimiterTestHarness.Create(context =>
            context.QuotaByKey(Config with { Calls = 1, CounterKey = "other" }), store, clock);
        same.Context.Subscription.Id = "other-subscription";
        same.Context.Api.Id = "other-api";

        first.RunInbound();
        same.RunInbound();
        other.RunInbound();

        same.Context.Response.StatusCode.Should().Be(403);
        other.Context.Response.StatusCode.Should().Be(200);
        store.GetCount("quota-by-key:tenant").Should().Be(1);
        store.GetCount("quota-by-key:other").Should().Be(1);
    }

    [TestMethod]
    public void QuotaByKey_KeyCannotCollideWithSubscriptionQuota()
    {
        var test = LimiterTestHarness.Create(context =>
        {
            context.Quota(new QuotaConfig { Calls = 10, RenewalPeriod = 300 });
            context.QuotaByKey(Config with { CounterKey = $"sub:{context.ExpressionContext.Subscription.Id}", Calls = 1 });
        });

        test.RunInbound();
        LimiterTestHarness.RunRequest(test);

        var key = $"sub:{test.Context.Subscription.Id}";
        test.Context.Response.StatusCode.Should().Be(403);
        test.SetupRateLimitStore().GetCount($"quota:{key}").Should().Be(2);
        test.SetupRateLimitStore().GetCount($"quota-by-key:{key}").Should().Be(1);
    }

    [TestMethod]
    public void QuotaByKey_InjectedRejectionUses403AndRetryAndDoesNotCount()
    {
        var limiter = new RecordingRateLimiter((_, _) => false);
        var test = LimiterTestHarness.Create(context => context.QuotaByKey(Config), limiter: limiter);

        test.RunInbound();

        test.Context.Response.StatusCode.Should().Be(403);
        test.Context.Response.Headers["Retry-After"].Should().Equal("300");
        test.SetupRateLimitStore().GetCount("quota-by-key:tenant").Should().Be(0);
        limiter.Calls.Should().Equal(("tenant", 1));
    }

    [TestMethod]
    public void QuotaByKey_CallbackErrorsKeepPolicyMetadata()
    {
        var test = LimiterTestHarness.Create(context => context.QuotaByKey(Config));
        test.SetupInbound().QuotaByKey().WithCallback((_, _) => throw new InvalidOperationException("mock failure"));

        var error = Assert.ThrowsException<PolicyException>(test.RunInbound);

        error.Policy.Should().Be(nameof(IInboundContext.QuotaByKey));
        error.InnerException.Should().BeOfType<InvalidOperationException>();
        test.SetupRateLimitStore().GetCount("quota-by-key:tenant").Should().Be(0);
    }

    [TestMethod]
    [DataRow(-1, 1, 300, 1)]
    [DataRow(1, -1, 300, 1)]
    [DataRow(1, 1, -1, 1)]
    [DataRow(1, 1, 300, -1)]
    public void QuotaByKey_RejectsInvalidConfiguration(int calls, int bandwidth, int period, int increment)
    {
        var limiter = new RecordingRateLimiter();
        var test = LimiterTestHarness.Create(context => context.QuotaByKey(Config with
        {
            Calls = calls,
            Bandwidth = bandwidth,
            RenewalPeriod = period,
            IncrementCount = increment
        }), limiter: limiter);

        Assert.ThrowsException<PolicyException>(test.RunInbound).InnerException.Should().BeOfType<ArgumentOutOfRangeException>();
        limiter.Calls.Should().BeEmpty();
    }

    [TestMethod]
    [DataRow("tomorrow")]
    [DataRow("2026-02-30T00:00:00Z")]
    public void QuotaByKey_RejectsInvalidFirstPeriodStart(string start)
    {
        var test = LimiterTestHarness.Create(context => context.QuotaByKey(Config with { FirstPeriodStart = start }));

        Assert.ThrowsException<PolicyException>(test.RunInbound).InnerException.Should().BeOfType<FormatException>();
    }

    [TestMethod]
    public void QuotaByKey_RequiresCallsOrBandwidthAndNonemptyKey()
    {
        var missing = LimiterTestHarness.Create(context =>
            context.QuotaByKey(Config with { Calls = null, Bandwidth = null }));
        var empty = LimiterTestHarness.Create(context => context.QuotaByKey(Config with { CounterKey = "" }));

        Assert.ThrowsException<PolicyException>(missing.RunInbound).InnerException.Should().BeOfType<ArgumentException>();
        Assert.ThrowsException<PolicyException>(empty.RunInbound).InnerException.Should().BeOfType<ArgumentException>();
    }

    [TestMethod]
    public void QuotaByKey_ZeroIncrementDoesNotConsumeCalls()
    {
        var test = LimiterTestHarness.Create(context => context.QuotaByKey(Config with { IncrementCount = 0 }));

        test.RunInbound();

        test.Context.Response.StatusCode.Should().Be(200);
        test.SetupRateLimitStore().GetCount("quota-by-key:tenant").Should().Be(0);
    }

    [TestMethod]
    public void QuotaByKey_LargeIncrementDoesNotOverflow()
    {
        var test = LimiterTestHarness.Create(context =>
            context.QuotaByKey(Config with { Calls = int.MaxValue, IncrementCount = int.MaxValue }));
        test.RunInbound();

        LimiterTestHarness.RunRequest(test);

        test.Context.Response.StatusCode.Should().Be(403);
        test.SetupRateLimitStore().GetCount("quota-by-key:tenant").Should().Be(int.MaxValue);
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void QuotaByKey_ZeroBudgetDeniesWithoutCounting(bool calls)
    {
        var test = LimiterTestHarness.Create(context => context.QuotaByKey(Config with
        {
            Calls = calls ? 0 : null,
            Bandwidth = calls ? null : 0
        }));

        test.RunInbound();

        test.Context.Response.StatusCode.Should().Be(403);
        test.SetupRateLimitStore().GetCount("quota-by-key:tenant").Should().Be(0);
    }

    [TestMethod]
    public void QuotaByKey_InjectedServiceErrorsKeepPolicyMetadata()
    {
        var test = LimiterTestHarness.Create(context => context.QuotaByKey(Config),
            limiter: new RecordingRateLimiter((_, _) => throw new InvalidOperationException("service failure")));

        var error = Assert.ThrowsExactly<PolicyException>(test.RunInbound);

        error.Policy.Should().Be(nameof(IInboundContext.QuotaByKey));
        error.InnerException.Should().BeOfType<InvalidOperationException>();
        test.SetupRateLimitStore().GetCount("quota-by-key:tenant").Should().Be(0);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void QuotaByKey_MixedDimensionsRejectOversizedRequest(bool bandwidthFirst)
    {
        var test = MixedQuotaDocument(bandwidthFirst);
        test.Context.Request.Body.Content = new string('\u00e9', 512) + "a";
        var requestId = test.Context.RequestId;

        test.RunInbound();

        test.Context.RequestId.Should().Be(requestId);
        test.Context.Response.StatusCode.Should().Be(403);
        test.Context.Response.Headers["Retry-After"].Should().Equal("300");
        test.Context.ResponseTerminated.Should().BeTrue();
        test.Context.Variables.Should().NotContainKey("after");
        test.SetupRateLimitStore().GetCount("quota-by-key:tenant").Should().Be(bandwidthFirst ? 0 : 1);
        test.SetupRateLimitStore().GetBandwidth("quota-by-key:tenant").Should().Be(0);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void QuotaByKey_MixedDimensionsAccountRequestAndResponseOnce(bool bandwidthFirst)
    {
        var limiter = new RecordingRateLimiter();
        var test = MixedQuotaDocument(bandwidthFirst, calls: 1, repetitions: 3, limiter: limiter);
        test.Context.Request.Body.Content = new string('\u00e9', 256);

        test.RunInbound();

        test.Context.Response.StatusCode.Should().Be(200);
        test.Context.Variables["after"].Should().Be(true);
        test.SetupRateLimitStore().GetCount("quota-by-key:tenant").Should().Be(1);
        test.SetupRateLimitStore().GetBandwidth("quota-by-key:tenant").Should().Be(512);
        limiter.Calls.Should().Equal(("tenant", 1));

        test.Context.Response.Body.Content = new string('\u00e9', 128);
        test.CompleteLimiterResponse();
        test.CompleteLimiterResponse();

        test.SetupRateLimitStore().GetCount("quota-by-key:tenant").Should().Be(1);
        test.SetupRateLimitStore().GetBandwidth("quota-by-key:tenant").Should().Be(768);
        test.Context.Variables.Remove("after");
        test.Context.Response.Body.Content = "";
        LimiterTestHarness.RunRequest(test);

        test.Context.Response.StatusCode.Should().Be(403);
        test.Context.Variables.Should().NotContainKey("after");
        test.SetupRateLimitStore().GetCount("quota-by-key:tenant").Should().Be(1);
        test.SetupRateLimitStore().GetBandwidth("quota-by-key:tenant").Should().Be(768);
        limiter.Calls.Should().Equal(("tenant", 1));
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void QuotaByKey_MixedDimensionsAllowExactBudgetWithoutDoubleCharging(bool bandwidthFirst)
    {
        var limiter = new RecordingRateLimiter();
        var test = MixedQuotaDocument(bandwidthFirst, calls: 1, repetitions: 2, limiter: limiter);
        test.Context.Request.Body.Content = new string('\u00e9', 512);

        test.RunInbound();
        test.CompleteLimiterResponse();

        test.Context.Response.StatusCode.Should().Be(200);
        test.Context.Variables["after"].Should().Be(true);
        test.SetupRateLimitStore().GetCount("quota-by-key:tenant").Should().Be(1);
        test.SetupRateLimitStore().GetBandwidth("quota-by-key:tenant").Should().Be(1024);
        limiter.Calls.Should().Equal(("tenant", 1));
    }

    [TestMethod]
    public void QuotaByKey_ZeroCallIncrementDoesNotMaskLaterMixedCallCharge()
    {
        var limiter = new RecordingRateLimiter();
        var test = LimiterTestHarness.Create(context =>
        {
            context.QuotaByKey(Config with { Calls = null, Bandwidth = 1, IncrementCount = 0 });
            context.QuotaByKey(Config with { Calls = 10, IncrementCount = 2 });
            context.QuotaByKey(Config with { Calls = 10, IncrementCount = 2 });
        }, limiter: limiter);
        test.Context.Request.Body.Content = new string('a', 512);

        test.RunInbound();
        test.CompleteLimiterResponse();

        test.Context.Response.StatusCode.Should().Be(200);
        test.SetupRateLimitStore().GetCount("quota-by-key:tenant").Should().Be(2);
        test.SetupRateLimitStore().GetBandwidth("quota-by-key:tenant").Should().Be(512);
        limiter.Calls.Should().Equal(("tenant", 0), ("tenant", 2));
    }

    [TestMethod]
    public void QuotaByKey_FalseBandwidthConditionDoesNotMaskLaterCharge()
    {
        var test = LimiterTestHarness.Create(context =>
        {
            context.QuotaByKey(Config with { Calls = 10 });
            context.QuotaByKey(Config with { Calls = null, Bandwidth = 1, IncrementCondition = false });
            context.QuotaByKey(Config with { Calls = null, Bandwidth = 1 });
            context.SetVariable("after", true);
        });
        test.Context.Request.Body.Content = new string('a', 1025);

        test.RunInbound();

        test.Context.Response.StatusCode.Should().Be(403);
        test.Context.Variables.Should().NotContainKey("after");
        test.SetupRateLimitStore().GetCount("quota-by-key:tenant").Should().Be(1);
        test.SetupRateLimitStore().GetBandwidth("quota-by-key:tenant").Should().Be(0);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void QuotaByKey_RejectionKeepsOrdinalDictionaryAndSingleRetryHeader(bool bandwidth)
    {
        var test = LimiterTestHarness.Create(context => context.QuotaByKey(Config with
        {
            Calls = bandwidth ? null : 0,
            Bandwidth = bandwidth ? 1 : null
        }));
        if (bandwidth)
        {
            test.Context.Request.Body.Content = new string('\u00e9', 512) + "a";
        }

        var headers = new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["retry-after"] = ["99"],
            ["RETRY-AFTER"] = ["98"]
        };
        test.Context.Response.Headers = headers;

        test.RunInbound();

        test.Context.Response.StatusCode.Should().Be(403);
        test.Context.Response.Headers.Should().BeSameAs(headers);
        headers.Comparer.Should().BeSameAs(StringComparer.Ordinal);
        headers.Should().ContainSingle().Which.Key.Should().Be("Retry-After");
        headers["Retry-After"].Should().Equal("300");
        test.Context.Request.Body.Consumed.Should().BeFalse();
        test.SetupRateLimitStore().GetCount("quota-by-key:tenant").Should().Be(0);
    }

    private static TestDocument MixedQuotaDocument(
        bool bandwidthFirst,
        int calls = 10,
        int repetitions = 1,
        RecordingRateLimiter? limiter = null)
    {
        var callLimit = Config with { Calls = calls };
        var bandwidthLimit = Config with { Calls = null, Bandwidth = 1 };
        var policies = bandwidthFirst ? new[] { bandwidthLimit, callLimit } : [callLimit, bandwidthLimit];
        return LimiterTestHarness.Create(context =>
        {
            for (var i = 0; i < repetitions; i++)
            {
                foreach (var policy in policies)
                {
                    context.QuotaByKey(policy);
                }
            }

            context.SetVariable("after", true);
        }, limiter: limiter);
    }
}