// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Document;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Data;

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
}
