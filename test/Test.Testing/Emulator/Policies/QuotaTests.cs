// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Document;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Data;

namespace Test.Emulator.Emulator.Policies;

[TestClass]
public class QuotaTests
{
    [TestMethod]
    public void Quota_EnforcesCallsAndStopsFollowingPolicy()
    {
        var test = LimiterTestHarness.Create(context =>
        {
            context.Quota(new QuotaConfig { Calls = 2, RenewalPeriod = 300 });
            context.SetVariable("after", true);
        });
        var key = $"quota:sub:{test.Context.Subscription.Id}";

        test.RunInbound();
        LimiterTestHarness.RunRequest(test);
        test.SetupRateLimitStore().GetCount(key).Should().Be(2);
        test.Context.Variables.Remove("after");
        LimiterTestHarness.RunRequest(test);

        test.Context.Response.StatusCode.Should().Be(403);
        test.Context.Response.StatusReason.Should().Be("Quota Exceeded");
        test.Context.Response.Headers["Retry-After"].Should().Equal("300");
        test.Context.ResponseTerminated.Should().BeTrue();
        test.Context.Variables.Should().NotContainKey("after");
        test.SetupRateLimitStore().GetCount(key).Should().Be(2);
    }

    [TestMethod]
    public void Quota_AlignsRenewalToSubscriptionStartAndRoundsRetryUp()
    {
        var clock = new LimiterTestTimeProvider();
        var test = LimiterTestHarness.Create(context =>
            context.Quota(new QuotaConfig { Calls = 1, RenewalPeriod = 300 }), clock: clock);
        test.Context.Subscription.StartDate = clock.GetUtcNow().UtcDateTime.AddSeconds(-50);

        test.RunInbound();
        clock.Advance(TimeSpan.FromSeconds(249.1));
        LimiterTestHarness.RunRequest(test);
        test.Context.Response.StatusCode.Should().Be(403);
        test.Context.Response.Headers["Retry-After"].Should().Equal("1");

        clock.Advance(TimeSpan.FromSeconds(0.9));
        LimiterTestHarness.RunRequest(test);

        test.Context.Response.StatusCode.Should().Be(200);
        test.SetupRateLimitStore().GetCount($"quota:sub:{test.Context.Subscription.Id}").Should().Be(1);
    }

    [TestMethod]
    public void Quota_ScopesAreIndependentAndRejectionDoesNotPartiallyIncrement()
    {
        var test = LimiterTestHarness.Create(context => context.Quota(new QuotaConfig
        {
            Calls = 10,
            RenewalPeriod = 300,
            Apis =
            [
                new ApiQuota
                {
                    Name = "orders",
                    Calls = 4,
                    Operations = [new OperationQuota { Name = "create", Calls = 1 }]
                }
            ]
        }));
        test.Context.Api.Name = "orders";
        test.Context.Operation.Name = "create";
        var subscription = $"quota:sub:{test.Context.Subscription.Id}";

        test.RunInbound();
        test.Context.Operation.Name = "read";
        LimiterTestHarness.RunRequest(test);
        test.Context.Operation.Name = "create";
        LimiterTestHarness.RunRequest(test);

        test.Context.Response.StatusCode.Should().Be(403);
        test.SetupRateLimitStore().GetCount(subscription).Should().Be(2);
        test.SetupRateLimitStore().GetCount($"{subscription}:api:orders").Should().Be(2);
        test.SetupRateLimitStore().GetCount($"{subscription}:api:orders:op:create").Should().Be(1);
    }

    [TestMethod]
    public void Quota_MatchesIdsCaseInsensitivelyAndPrioritizesIdOverName()
    {
        var test = LimiterTestHarness.Create(context => context.Quota(new QuotaConfig
        {
            Calls = 10,
            RenewalPeriod = 300,
            Apis =
            [
                new ApiQuota
                {
                    Id = "ORDERS",
                    Name = "ignored",
                    Calls = 2,
                    Operations = [new OperationQuota { Id = "CREATE", Name = "ignored", Calls = 1 }]
                },
                new ApiQuota { Id = "other", Name = "orders", Calls = 0 }
            ]
        }));
        test.Context.Api.Id = "orders";
        test.Context.Api.Name = "orders";
        test.Context.Operation.Id = "create";

        test.RunInbound();

        var key = $"quota:sub:{test.Context.Subscription.Id}";
        test.Context.Response.StatusCode.Should().Be(200);
        test.SetupRateLimitStore().GetCount($"{key}:api:ORDERS:op:CREATE").Should().Be(1);
        test.SetupRateLimitStore().GetCount($"{key}:api:other").Should().Be(0);
    }

    [TestMethod]
    public void Quota_BandwidthUsesKilobytesAndRenewsWithClock()
    {
        var clock = new LimiterTestTimeProvider();
        var test = LimiterTestHarness.Create(context =>
            context.Quota(new QuotaConfig { Bandwidth = 1, RenewalPeriod = 300 }), clock: clock);
        test.Context.Request.Body.Content = new string('a', 1024);

        test.RunInbound();
        test.CompleteLimiterResponse();
        test.Context.Response.StatusCode.Should().Be(200);
        LimiterTestHarness.RunRequest(test);
        test.Context.Response.StatusCode.Should().Be(403);
        test.SetupRateLimitStore().GetCount($"quota:sub:{test.Context.Subscription.Id}").Should().Be(1);

        clock.Advance(TimeSpan.FromSeconds(300));
        LimiterTestHarness.RunRequest(test);
        test.CompleteLimiterResponse();
        test.Context.Response.StatusCode.Should().Be(200);
        test.SetupRateLimitStore().GetCount($"quota:sub:{test.Context.Subscription.Id}").Should().Be(1);
    }

    [TestMethod]
    public void Quota_CountsRequestAndFinalResponseUtf8BytesWithoutConsumingBodies()
    {
        var test = LimiterTestHarness.Create(context =>
            context.Quota(new QuotaConfig { Bandwidth = 1, RenewalPeriod = 300 }));
        test.Context.Request.Body.Content = new string('a', 512);

        test.RunInbound();
        test.Context.Response.Body.Content = new string('\u00e9', 256);
        test.CompleteLimiterResponse();
        test.Context.Response.StatusCode.Should().Be(200);
        test.Context.Request.Body.Consumed.Should().BeFalse();
        test.Context.Response.Body.Consumed.Should().BeFalse();

        test.Context.Request.Body.Content = "";
        test.Context.Response.Body.Content = "";
        LimiterTestHarness.RunRequest(test);

        test.Context.Response.StatusCode.Should().Be(403);
        test.SetupRateLimitStore().GetCount($"quota:sub:{test.Context.Subscription.Id}").Should().Be(1);
    }

    [TestMethod]
    public void Quota_RejectsOversizedRequestWithoutChargingAnyScope()
    {
        var test = LimiterTestHarness.Create(context => context.Quota(new QuotaConfig
        {
            Calls = 10,
            RenewalPeriod = 300,
            Apis = [new ApiQuota { Name = "orders", Bandwidth = 1 }]
        }));
        test.Context.Api.Name = "orders";
        test.Context.Request.Body.Content = new string('a', 1025);

        test.RunInbound();

        var key = $"quota:sub:{test.Context.Subscription.Id}";
        test.Context.Response.StatusCode.Should().Be(403);
        test.SetupRateLimitStore().GetCount(key).Should().Be(0);
        test.SetupRateLimitStore().GetCount($"{key}:api:orders").Should().Be(0);
    }

    [TestMethod]
    public void Quota_UsesCaseInsensitiveContentLengthWhenBodyIsNotMaterialized()
    {
        var test = LimiterTestHarness.Create(context =>
            context.Quota(new QuotaConfig { Bandwidth = 1, RenewalPeriod = 300 }));
        test.Context.Request.Headers["content-length"] = ["1024"];

        test.RunInbound();
        test.CompleteLimiterResponse();
        LimiterTestHarness.RunRequest(test);

        test.Context.Response.StatusCode.Should().Be(403);
        test.SetupRateLimitStore().GetCount($"quota:sub:{test.Context.Subscription.Id}").Should().Be(1);
    }

    [TestMethod]
    [DataRow("-1")]
    [DataRow("invalid")]
    [DataRow("9223372036854775808")]
    public void Quota_InvalidContentLengthIsAnExplicitPolicyError(string value)
    {
        var test = LimiterTestHarness.Create(context =>
            context.Quota(new QuotaConfig { Bandwidth = 1, RenewalPeriod = 300 }));
        test.Context.Request.Headers["Content-Length"] = [value];

        var error = Assert.ThrowsException<PolicyException>(test.RunInbound);

        error.InnerException.Should().BeOfType<FormatException>();
        test.SetupRateLimitStore().GetCount($"quota:sub:{test.Context.Subscription.Id}").Should().Be(0);
    }

    [TestMethod]
    public void Quota_LifetimeQuotaDoesNotRenewOrRecommendImmediateRetry()
    {
        var clock = new LimiterTestTimeProvider();
        var test = LimiterTestHarness.Create(context =>
            context.Quota(new QuotaConfig { Calls = 1, RenewalPeriod = 0 }), clock: clock);
        test.RunInbound();
        clock.Advance(TimeSpan.FromDays(36500));

        LimiterTestHarness.RunRequest(test);

        test.Context.Response.StatusCode.Should().Be(403);
        test.Context.Response.Headers.Should().NotContainKey("Retry-After");
        test.SetupRateLimitStore().GetCount($"quota:sub:{test.Context.Subscription.Id}").Should().Be(1);
    }

    [TestMethod]
    public void Quota_SharedStoreSeparatesSubscriptions()
    {
        var store = new RateLimitStore();
        var first = LimiterTestHarness.Create(context =>
            context.Quota(new QuotaConfig { Calls = 1, RenewalPeriod = 0 }), store);
        var second = LimiterTestHarness.Create(context =>
            context.Quota(new QuotaConfig { Calls = 1, RenewalPeriod = 0 }), store);
        first.Context.Subscription.Id = "first";
        second.Context.Subscription.Id = "second";

        first.RunInbound();
        second.RunInbound();
        LimiterTestHarness.RunRequest(first);

        first.Context.Response.StatusCode.Should().Be(403);
        second.Context.Response.StatusCode.Should().Be(200);
        store.GetCount("quota:sub:first").Should().Be(1);
        store.GetCount("quota:sub:second").Should().Be(1);
    }

    [TestMethod]
    public void Quota_InjectedLimiterCannotBypassLocalQuotaAndKeeps403Parity()
    {
        var limiter = new RecordingRateLimiter();
        var test = LimiterTestHarness.Create(context =>
            context.Quota(new QuotaConfig { Calls = 1, RenewalPeriod = 300 }), limiter: limiter);

        test.RunInbound();
        LimiterTestHarness.RunRequest(test);

        test.Context.Response.StatusCode.Should().Be(403);
        test.Context.Response.Headers["Retry-After"].Should().Equal("300");
        test.SetupRateLimitStore().GetCount($"quota:sub:{test.Context.Subscription.Id}").Should().Be(1);
        limiter.Calls.Should().Equal(($"quota:{test.Context.Subscription.Id}", 1));
    }

    [TestMethod]
    public void Quota_InjectedRejectionDoesNotChargeCounters()
    {
        var test = LimiterTestHarness.Create(context =>
            context.Quota(new QuotaConfig { Calls = 1, RenewalPeriod = 300 }),
            limiter: new RecordingRateLimiter((_, _) => false));

        test.RunInbound();

        test.Context.Response.StatusCode.Should().Be(403);
        test.Context.Response.Headers["Retry-After"].Should().Equal("300");
        test.SetupRateLimitStore().GetCount($"quota:sub:{test.Context.Subscription.Id}").Should().Be(0);
    }

    [TestMethod]
    public void Quota_CallbackPredicateOverridesDefaultAndSupportsErrors()
    {
        var test = LimiterTestHarness.Create(context => context.Quota(new QuotaConfig { Calls = 1, RenewalPeriod = 300 }));
        test.SetupInbound().Quota((_, config) => config.Calls == 1).WithCallback((context, _) =>
        {
            context.Variables["callback"] = true;
            context.Response.StatusCode = 503;
            throw new FinishSectionProcessingException();
        });

        test.RunInbound();

        test.Context.Variables["callback"].Should().Be(true);
        test.Context.Response.StatusCode.Should().Be(503);
        test.SetupRateLimitStore().GetCount($"quota:sub:{test.Context.Subscription.Id}").Should().Be(0);
    }

    [TestMethod]
    [DataRow(-1, 1, 300)]
    [DataRow(1, -1, 300)]
    [DataRow(1, 1, -1)]
    public void Quota_RejectsInvalidConfiguration(int calls, int bandwidth, int period)
    {
        var limiter = new RecordingRateLimiter();
        var test = LimiterTestHarness.Create(context =>
            context.Quota(new QuotaConfig { Calls = calls, Bandwidth = bandwidth, RenewalPeriod = period }), limiter: limiter);

        Assert.ThrowsException<PolicyException>(test.RunInbound).InnerException.Should().BeOfType<ArgumentOutOfRangeException>();
        limiter.Calls.Should().BeEmpty();
    }

    [TestMethod]
    public void Quota_RequiresCallsOrBandwidth()
    {
        var test = LimiterTestHarness.Create(context => context.Quota(new QuotaConfig { RenewalPeriod = 300 }));

        Assert.ThrowsException<PolicyException>(test.RunInbound).InnerException.Should().BeOfType<ArgumentException>();
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void Quota_ZeroBudgetRejectsWithoutCharging(bool calls)
    {
        var test = LimiterTestHarness.Create(context => context.Quota(new QuotaConfig
        {
            Calls = calls ? 0 : null,
            Bandwidth = calls ? null : 0,
            RenewalPeriod = 300
        }));

        test.RunInbound();

        test.Context.Response.StatusCode.Should().Be(403);
        test.SetupRateLimitStore().GetCount($"quota:sub:{test.Context.Subscription.Id}").Should().Be(0);
    }

    [TestMethod]
    public void Quota_NoSubscriptionDoesNotConsumeOrInvokeInjectedLimiter()
    {
        var limiter = new RecordingRateLimiter();
        var test = LimiterTestHarness.Create(context =>
            context.Quota(new QuotaConfig { Calls = 1, RenewalPeriod = 300 }), limiter: limiter);
        test.Context.Subscription = null!;

        test.RunInbound();

        test.Context.Response.StatusCode.Should().Be(200);
        limiter.Calls.Should().BeEmpty();
    }

    [TestMethod]
    public void Quota_InjectedServiceErrorDoesNotBecomeQuotaSuccess()
    {
        var test = LimiterTestHarness.Create(context =>
            context.Quota(new QuotaConfig { Calls = 1, RenewalPeriod = 300 }),
            limiter: new RecordingRateLimiter((_, _) => throw new InvalidOperationException("service failure")));

        var error = Assert.ThrowsExactly<PolicyException>(test.RunInbound);

        error.Policy.Should().Be(nameof(IInboundContext.Quota));
        error.InnerException.Should().BeOfType<InvalidOperationException>();
        test.SetupRateLimitStore().GetCount($"quota:sub:{test.Context.Subscription.Id}").Should().Be(0);
    }

    [TestMethod]
    public void Quota_AccountsFinalBandwidthEvenWhenReturnResponseTerminates()
    {
        var test = LimiterTestHarness.Create(context =>
        {
            context.Quota(new QuotaConfig { Calls = 10, Bandwidth = 1, RenewalPeriod = 300 });
            context.ReturnResponse(new ReturnResponseConfig
            {
                Status = new StatusConfig { Code = 200, Reason = "OK" },
                Body = new BodyConfig { Content = new string('a', 1000) }
            });
        });
        test.RunInbound();
        test.CompleteLimiterResponse();
        test.Context.ResponseTerminated.Should().BeTrue();
        test.Context.Request.Body.Content = new string('a', 25);
        test.Context.Response.Body.Content = "";

        LimiterTestHarness.RunRequest(test);

        test.Context.Response.StatusCode.Should().Be(403);
        test.SetupRateLimitStore().GetCount($"quota:sub:{test.Context.Subscription.Id}").Should().Be(1);
    }
}