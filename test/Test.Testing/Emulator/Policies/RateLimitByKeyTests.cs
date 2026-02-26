// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Document;

namespace Test.Emulator.Emulator.Policies;

[TestClass]
public class RateLimitByKeyTests
{
    class SimpleRateLimitByKey : IDocument
    {
        public void Inbound(IInboundContext context)
        {
            context.RateLimitByKey(new RateLimitByKeyConfig
            {
                Calls = 3,
                RenewalPeriod = 60,
                CounterKey = "client-ip"
            });
        }
    }

    class RateLimitByKeyWithHeaders : IDocument
    {
        public void Inbound(IInboundContext context)
        {
            context.RateLimitByKey(new RateLimitByKeyConfig
            {
                Calls = 5,
                RenewalPeriod = 30,
                CounterKey = "user-id",
                RemainingCallsHeaderName = "X-RateLimit-Remaining",
                TotalCallsHeaderName = "X-RateLimit-Limit",
                RetryAfterHeaderName = "Retry-After",
                RetryAfterVariableName = "retryAfter",
                RemainingCallsVariableName = "remainingCalls"
            });
        }
    }

    class RateLimitByKeyWithIncrementCount : IDocument
    {
        public void Inbound(IInboundContext context)
        {
            context.RateLimitByKey(new RateLimitByKeyConfig
            {
                Calls = 10,
                RenewalPeriod = 60,
                CounterKey = "heavy-op",
                IncrementCount = 5
            });
        }
    }

    class RateLimitByKeyWithIncrementConditionFalse : IDocument
    {
        public void Inbound(IInboundContext context)
        {
            context.RateLimitByKey(new RateLimitByKeyConfig
            {
                Calls = 3,
                RenewalPeriod = 60,
                CounterKey = "conditional",
                IncrementCondition = false
            });
        }
    }

    class RateLimitByKeyThenSetHeader : IDocument
    {
        public void Inbound(IInboundContext context)
        {
            context.RateLimitByKey(new RateLimitByKeyConfig
            {
                Calls = 1,
                RenewalPeriod = 60,
                CounterKey = "block-me"
            });
            context.SetHeader("X-After-RateLimit", "executed");
        }
    }

    [TestMethod]
    public void RateLimitByKey_UnderLimit()
    {
        var test = new SimpleRateLimitByKey().AsTestDocument();

        test.RunInbound();

        test.Context.Response.StatusCode.Should().NotBe(429);
    }

    [TestMethod]
    public void RateLimitByKey_ExceedsLimit()
    {
        var test = new SimpleRateLimitByKey().AsTestDocument();
        test.SetupRateLimitStore().SetCount("client-ip", 3);

        test.RunInbound();

        test.Context.Response.StatusCode.Should().Be(429);
        test.Context.Response.StatusReason.Should().Be("Too Many Requests");
    }

    [TestMethod]
    public void RateLimitByKey_ResetAndRetry()
    {
        var test = new SimpleRateLimitByKey().AsTestDocument();
        test.SetupRateLimitStore().SetCount("client-ip", 3);

        test.RunInbound();
        test.Context.Response.StatusCode.Should().Be(429);

        test.SetupRateLimitStore().Reset();

        test.RunInbound();
        test.Context.Response.StatusCode.Should().NotBe(429);
    }

    [TestMethod]
    public void RateLimitByKey_DifferentKeysAreIndependent()
    {
        var test = new SimpleRateLimitByKey().AsTestDocument();
        test.SetupRateLimitStore().SetCount("other-key", 100);

        test.RunInbound();

        test.Context.Response.StatusCode.Should().NotBe(429);
    }

    [TestMethod]
    public void RateLimitByKey_SetsHeaders()
    {
        var test = new RateLimitByKeyWithHeaders().AsTestDocument();

        test.RunInbound();

        test.Context.Response.Headers.Should().ContainKey("X-RateLimit-Remaining");
        test.Context.Response.Headers.Should().ContainKey("X-RateLimit-Limit");
        test.Context.Variables.Should().ContainKey("remainingCalls");
    }

    [TestMethod]
    public void RateLimitByKey_SetsRetryAfterOnExceeded()
    {
        var test = new RateLimitByKeyWithHeaders().AsTestDocument();
        test.SetupRateLimitStore().SetCount("user-id", 5);

        test.RunInbound();

        test.Context.Response.StatusCode.Should().Be(429);
        test.Context.Response.Headers.Should().ContainKey("Retry-After");
        test.Context.Variables.Should().ContainKey("retryAfter");
    }

    [TestMethod]
    public void RateLimitByKey_IncrementCount()
    {
        var test = new RateLimitByKeyWithIncrementCount().AsTestDocument();

        test.RunInbound();

        test.SetupRateLimitStore().GetCount("heavy-op").Should().Be(5);
    }

    [TestMethod]
    public void RateLimitByKey_IncrementConditionFalse_DoesNotIncrement()
    {
        var test = new RateLimitByKeyWithIncrementConditionFalse().AsTestDocument();

        test.RunInbound();

        test.SetupRateLimitStore().GetCount("conditional").Should().Be(0);
    }

    [TestMethod]
    public void RateLimitByKey_TerminatesSectionOnExceeded()
    {
        var test = new RateLimitByKeyThenSetHeader().AsTestDocument();
        test.SetupRateLimitStore().SetCount("block-me", 1);
        var headerExecuted = false;
        test.SetupInbound().SetHeader().WithCallback((_, _, _) => headerExecuted = true);

        test.RunInbound();

        headerExecuted.Should().BeFalse();
        test.Context.Response.StatusCode.Should().Be(429);
    }

    [TestMethod]
    public void RateLimitByKey_CounterNotIncrementedOnExceeded()
    {
        var test = new SimpleRateLimitByKey().AsTestDocument();
        test.SetupRateLimitStore().SetCount("client-ip", 3);

        test.RunInbound();

        test.Context.Response.StatusCode.Should().Be(429);
        test.SetupRateLimitStore().GetCount("client-ip").Should().Be(3);
    }

    [TestMethod]
    public void RateLimitByKey_Callback()
    {
        var test = new SimpleRateLimitByKey().AsTestDocument();
        var callbackExecuted = false;
        test.SetupInbound().RateLimitByKey().WithCallback((_, config) =>
        {
            callbackExecuted = true;
            config.CounterKey.Should().Be("client-ip");
        });

        test.RunInbound();

        callbackExecuted.Should().BeTrue();
    }
}
