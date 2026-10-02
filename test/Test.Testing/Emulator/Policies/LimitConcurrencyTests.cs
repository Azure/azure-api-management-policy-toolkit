// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Document;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Services;

namespace Test.Emulator.Emulator.Policies;

[TestClass]
public class LimitConcurrencyTests
{
    private sealed class Document(string section, LimitConcurrencyConfig config, Action action) : IDocument
    {
        public void Inbound(IInboundContext context)
        {
            if (section != "Inbound") return;
            context.LimitConcurrency(config, action);
            context.SetVariable("after", true);
        }

        public void Backend(IBackendContext context)
        {
            if (section != "Backend") return;
            context.LimitConcurrency(config, action);
            context.SetVariable("after", true);
        }

        public void Outbound(IOutboundContext context)
        {
            if (section != "Outbound") return;
            context.LimitConcurrency(config, action);
            context.SetVariable("after", true);
        }

        public void OnError(IOnErrorContext context)
        {
            if (section != "OnError") return;
            context.LimitConcurrency(config, action);
            context.SetVariable("after", true);
        }
    }

    private sealed class ExpressionDocument : IDocument
    {
        public void Inbound(IInboundContext context)
        {
            context.LimitConcurrency(new LimitConcurrencyConfig
            {
                Key = (string)context.ExpressionContext.Variables["key"],
                MaxCount = 1
            }, () => context.SetVariable("executed-key", context.ExpressionContext.Variables["key"]));
        }
    }

    private sealed class EarlyTerminationDocument : IDocument
    {
        public void Inbound(IInboundContext context)
        {
            context.LimitConcurrency(new LimitConcurrencyConfig { Key = "shared", MaxCount = 1 }, () =>
                context.ReturnResponse(new ReturnResponseConfig { Status = new StatusConfig { Code = 204, Reason = "No Content" } }));
            context.SetVariable("after", true);
        }
    }

    private static TestDocument Create(string section, string key, int max, Action action, IConcurrencyLimiter? limiter = null)
    {
        var test = new Document(section, new LimitConcurrencyConfig { Key = key, MaxCount = max }, action).AsTestDocument();
        if (limiter is not null)
        {
            test.Context.Services.Register(limiter);
        }

        return test;
    }

    private static void Run(TestDocument test, string section)
    {
        switch (section)
        {
            case "Inbound": test.RunInbound(); break;
            case "Backend": test.RunBackend(); break;
            case "Outbound": test.RunOutbound(); break;
            case "OnError": test.RunOnError(); break;
            default: throw new ArgumentException(section);
        }
    }

    private static void Setup(
        TestDocument test,
        string section,
        Func<GatewayContext, LimitConcurrencyConfig, Action, bool> predicate,
        Action<GatewayContext, LimitConcurrencyConfig, Action> callback)
    {
        switch (section)
        {
            case "Inbound": test.SetupInbound().LimitConcurrency(predicate).WithCallback(callback); break;
            case "Backend": test.SetupBackend().LimitConcurrency(predicate).WithCallback(callback); break;
            case "Outbound": test.SetupOutbound().LimitConcurrency(predicate).WithCallback(callback); break;
            case "OnError": test.SetupOnError().LimitConcurrency(predicate).WithCallback(callback); break;
            default: throw new ArgumentException(section);
        }
    }

    [TestMethod]
    [DataRow("Inbound")]
    [DataRow("Backend")]
    [DataRow("Outbound")]
    [DataRow("OnError")]
    public void LimitConcurrency_ExecutesNestedActionAndReleasesInEverySection(string section)
    {
        var limiter = new KeyedConcurrencyLimiter();
        var inside = new List<int>();
        var test = Create(section, "shared", 1, () => inside.Add(limiter.GetCount("shared")), limiter);

        Run(test, section);

        inside.Should().Equal(1);
        limiter.GetCount("shared").Should().Be(0);
        test.Context.Variables["after"].Should().Be(true);
        test.Context.Response.StatusCode.Should().Be(200);
    }

    [TestMethod]
    [DataRow("Inbound")]
    [DataRow("Backend")]
    [DataRow("Outbound")]
    [DataRow("OnError")]
    public void LimitConcurrency_RejectsImmediatelyWhenSharedKeyIsFull(string section)
    {
        var limiter = new KeyedConcurrencyLimiter();
        var rejectedAction = false;
        var rejected = Create(section, "shared", 1, () => rejectedAction = true, limiter);
        var admitted = Create(section, "shared", 1, () =>
        {
            limiter.GetCount("shared").Should().Be(1);
            Run(rejected, section);
            limiter.GetCount("shared").Should().Be(1);
        }, limiter);
        rejected.Context.Response.Headers["stale"] = ["value"];
        rejected.Context.Response.Body.Content = "stale";

        Run(admitted, section);

        rejectedAction.Should().BeFalse();
        rejected.Context.Response.StatusCode.Should().Be(429);
        rejected.Context.Response.StatusReason.Should().Be("Too Many Requests");
        rejected.Context.ResponseTerminated.Should().BeTrue();
        rejected.Context.Response.Headers.Should().BeEmpty();
        rejected.Context.Response.Body.Content.Should().BeEmpty();
        rejected.Context.Variables.Should().NotContainKey("after");
        admitted.Context.Variables["after"].Should().Be(true);
        limiter.GetCount("shared").Should().Be(0);
    }

    [TestMethod]
    public void LimitConcurrency_DifferentKeysRemainIndependent()
    {
        var limiter = new KeyedConcurrencyLimiter();
        var inside = new List<int>();
        var second = Create("Inbound", "second", 1, () =>
        {
            inside.Add(limiter.GetCount("first"));
            inside.Add(limiter.GetCount("second"));
        }, limiter);
        var first = Create("Inbound", "first", 1, second.RunInbound, limiter);

        first.RunInbound();

        inside.Should().Equal(1, 1);
        first.Context.Response.StatusCode.Should().Be(200);
        second.Context.Response.StatusCode.Should().Be(200);
        limiter.GetCount("first").Should().Be(0);
        limiter.GetCount("second").Should().Be(0);
    }

    [TestMethod]
    public void LimitConcurrency_AllowsUpToConfiguredCountWithoutQueueing()
    {
        var limiter = new KeyedConcurrencyLimiter();
        var rejected = Create("Inbound", "shared", 2, () => Assert.Fail("The third action must not execute"), limiter);
        var second = Create("Inbound", "shared", 2, () =>
        {
            limiter.GetCount("shared").Should().Be(2);
            rejected.RunInbound();
        }, limiter);
        var first = Create("Inbound", "shared", 2, second.RunInbound, limiter);

        first.RunInbound();

        rejected.Context.Response.StatusCode.Should().Be(429);
        first.Context.Response.StatusCode.Should().Be(200);
        second.Context.Response.StatusCode.Should().Be(200);
        limiter.GetCount("shared").Should().Be(0);
    }

    [TestMethod]
    public void LimitConcurrency_DefaultServicesAreIsolatedAcrossGatewayContexts()
    {
        var executions = 0;
        var second = Create("Inbound", "shared", 1, () => executions++);
        var first = Create("Inbound", "shared", 1, () =>
        {
            executions++;
            second.RunInbound();
        });

        first.RunInbound();

        executions.Should().Be(2);
        first.Context.Response.StatusCode.Should().Be(200);
        second.Context.Response.StatusCode.Should().Be(200);
    }

    [TestMethod]
    public void LimitConcurrency_DefaultServiceSharesKeyWithinGatewayContext()
    {
        var test = new NestedDocument().AsTestDocument();

        test.RunInbound();

        test.Context.Variables.Should().NotContainKey("inner");
        test.Context.Variables.Should().NotContainKey("after");
        test.Context.Response.StatusCode.Should().Be(429);
    }

    private sealed class NestedDocument : IDocument
    {
        public void Inbound(IInboundContext context)
        {
            context.LimitConcurrency(new LimitConcurrencyConfig { Key = "shared", MaxCount = 1 }, () =>
                context.LimitConcurrency(new LimitConcurrencyConfig { Key = "shared", MaxCount = 1 }, () =>
                    context.SetVariable("inner", true)));
            context.SetVariable("after", true);
        }
    }

    [TestMethod]
    public void LimitConcurrency_ExpressionKeyIsUsed()
    {
        var limiter = new KeyedConcurrencyLimiter();
        using var held = limiter.TryAcquire("blocked", 1);
        var test = new ExpressionDocument().AsTestDocument();
        test.Context.Services.Register<IConcurrencyLimiter>(limiter);
        test.Context.Variables["key"] = "blocked";

        test.RunInbound();
        test.Context.Response.StatusCode.Should().Be(429);
        test.Context.Variables.Should().NotContainKey("executed-key");
        test.Context.Variables["key"] = "independent";
        LimiterTestHarness.RunRequest(test);

        test.Context.Variables["executed-key"].Should().Be("independent");
        limiter.GetCount("blocked").Should().Be(1);
        limiter.GetCount("independent").Should().Be(0);
    }

    [TestMethod]
    [DataRow("Inbound")]
    [DataRow("Backend")]
    [DataRow("Outbound")]
    [DataRow("OnError")]
    public void LimitConcurrency_CallbackCanExecuteOrSkipNestedAction(string section)
    {
        var limiter = new KeyedConcurrencyLimiter();
        var executions = 0;
        var test = Create(section, "shared", 0, () => executions++, limiter);
        Setup(test, section, (_, config, _) => config.Key == "shared", (context, config, action) =>
        {
            context.Variables["callback"] = config.Key;
            action();
        });

        Run(test, section);

        executions.Should().Be(1);
        test.Context.Variables["callback"].Should().Be("shared");
        test.Context.Variables["after"].Should().Be(true);
        limiter.GetCount("shared").Should().Be(0);

        var skipped = Create(section, "shared", 1, () => executions++, limiter);
        Setup(skipped, section, (_, _, _) => true, (context, _, _) => context.Variables["skipped"] = true);
        Run(skipped, section);
        executions.Should().Be(1);
        skipped.Context.Variables["skipped"].Should().Be(true);
    }

    [TestMethod]
    public void LimitConcurrency_NonmatchingCallbackUsesRealLimiter()
    {
        var limiter = new KeyedConcurrencyLimiter();
        var inside = 0;
        var test = Create("Inbound", "shared", 1, () => inside = limiter.GetCount("shared"), limiter);
        test.SetupInbound().LimitConcurrency((_, config, _) => config.Key == "other")
            .WithCallback((_, _, _) => Assert.Fail("Predicate must not match"));

        test.RunInbound();

        inside.Should().Be(1);
        limiter.GetCount("shared").Should().Be(0);
    }

    [TestMethod]
    [DataRow("Inbound")]
    [DataRow("Backend")]
    [DataRow("Outbound")]
    [DataRow("OnError")]
    public void LimitConcurrency_ReleasesPermitWhenActionThrows(string section)
    {
        var limiter = new KeyedConcurrencyLimiter();
        var test = Create(section, "shared", 1, () => throw new InvalidOperationException("nested failure"), limiter);

        var error = Assert.ThrowsException<PolicyException>(() => Run(test, section));

        error.Policy.Should().Be(nameof(IInboundContext.LimitConcurrency));
        error.Section.Should().Be($"I{section}Context");
        error.InnerException.Should().BeOfType<InvalidOperationException>();
        limiter.GetCount("shared").Should().Be(0);

        var retry = Create(section, "shared", 1, () => { }, limiter);
        Run(retry, section);
        retry.Context.Variables["after"].Should().Be(true);
    }

    [TestMethod]
    public void LimitConcurrency_ReleasesPermitForReturnResponseTermination()
    {
        var limiter = new KeyedConcurrencyLimiter();
        var test = new EarlyTerminationDocument().AsTestDocument();
        test.Context.Services.Register<IConcurrencyLimiter>(limiter);

        test.RunInbound();

        test.Context.Response.StatusCode.Should().Be(204);
        test.Context.ResponseTerminated.Should().BeTrue();
        test.Context.Variables.Should().NotContainKey("after");
        limiter.GetCount("shared").Should().Be(0);
        var next = Create("Inbound", "shared", 1, () => { }, limiter);
        next.RunInbound();
        next.Context.Variables["after"].Should().Be(true);
    }

    [TestMethod]
    public void LimitConcurrency_CallbackErrorIsNotSwallowed()
    {
        var test = Create("Inbound", "shared", 1, () => Assert.Fail("Callback overrides action"));
        test.SetupInbound().LimitConcurrency().WithCallback((_, _, _) => throw new InvalidOperationException("mock error"));

        var error = Assert.ThrowsException<PolicyException>(test.RunInbound);

        error.InnerException.Should().BeOfType<InvalidOperationException>();
        error.Policy.Should().Be(nameof(IInboundContext.LimitConcurrency));
    }

    [TestMethod]
    public async Task LimitConcurrency_SharedServiceRejectsConcurrentRequestsWithoutSleeps()
    {
        var limiter = new KeyedConcurrencyLimiter();
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var first = Create("Inbound", "shared", 1, () =>
        {
            entered.Set();
            release.Wait(TimeSpan.FromSeconds(10)).Should().BeTrue();
        }, limiter);
        var secondExecuted = false;
        var second = Create("Inbound", "shared", 1, () => secondExecuted = true, limiter);
        var running = Task.Run(first.RunInbound);

        try
        {
            entered.Wait(TimeSpan.FromSeconds(10)).Should().BeTrue();
            second.RunInbound();
            secondExecuted.Should().BeFalse();
            second.Context.Response.StatusCode.Should().Be(429);
            limiter.GetCount("shared").Should().Be(1);
        }
        finally
        {
            release.Set();
            await running.WaitAsync(TimeSpan.FromSeconds(10));
        }

        limiter.GetCount("shared").Should().Be(0);
        var retry = Create("Inbound", "shared", 1, () => secondExecuted = true, limiter);
        retry.RunInbound();
        secondExecuted.Should().BeTrue();
    }

    [TestMethod]
    public void LimitConcurrency_LeaseDisposalIsIdempotent()
    {
        var limiter = new KeyedConcurrencyLimiter();
        var permit = limiter.TryAcquire("shared", 1);
        permit.Should().NotBeNull();
        permit!.Dispose();
        permit.Dispose();

        limiter.GetCount("shared").Should().Be(0);
        using var next = limiter.TryAcquire("shared", 1);
        next.Should().NotBeNull();
        limiter.GetCount("shared").Should().Be(1);
    }

    [TestMethod]
    public void LimitConcurrency_ZeroLimitRejectsWithoutExecuting()
    {
        var test = Create("Inbound", "shared", 0, () => Assert.Fail("Zero limit must reject"));

        test.RunInbound();

        test.Context.Response.StatusCode.Should().Be(429);
        test.Context.ResponseTerminated.Should().BeTrue();
        test.Context.Variables.Should().NotContainKey("after");
    }

    [TestMethod]
    public void LimitConcurrency_NegativeLimitAndEmptyKeyArePolicyErrors()
    {
        var negative = Create("Inbound", "shared", -1, () => Assert.Fail("Invalid limit"));
        var empty = Create("Inbound", "", 1, () => Assert.Fail("Invalid key"));

        Assert.ThrowsException<PolicyException>(negative.RunInbound).InnerException.Should().BeOfType<ArgumentOutOfRangeException>();
        Assert.ThrowsException<PolicyException>(empty.RunInbound).InnerException.Should().BeOfType<ArgumentException>();
    }

    [TestMethod]
    public void LimitConcurrency_KeysAreCaseSensitive()
    {
        var limiter = new KeyedConcurrencyLimiter();
        using var held = limiter.TryAcquire("KEY", 1);
        var inside = 0;
        var test = Create("Inbound", "key", 1, () => inside = limiter.GetCount("key"), limiter);

        test.RunInbound();

        inside.Should().Be(1);
        limiter.GetCount("KEY").Should().Be(1);
        limiter.GetCount("key").Should().Be(0);
    }

    [TestMethod]
    public void LimitConcurrency_MaximumLimitDoesNotOverflow()
    {
        var limiter = new KeyedConcurrencyLimiter();
        using var held = limiter.TryAcquire("shared", int.MaxValue);
        var inside = 0;
        var test = Create("Inbound", "shared", int.MaxValue, () => inside = limiter.GetCount("shared"), limiter);

        test.RunInbound();

        inside.Should().Be(2);
        limiter.GetCount("shared").Should().Be(1);
    }

    private sealed class FailingLimiter : IConcurrencyLimiter
    {
        public IDisposable? TryAcquire(string key, int maxCount) => throw new InvalidOperationException("service failure");
    }

    [TestMethod]
    public void LimitConcurrency_InjectedServiceErrorIsExplicitAndDoesNotExecuteAction()
    {
        var executed = false;
        var test = Create("Inbound", "shared", 1, () => executed = true, new FailingLimiter());

        var error = Assert.ThrowsExactly<PolicyException>(test.RunInbound);

        error.Policy.Should().Be(nameof(IInboundContext.LimitConcurrency));
        error.InnerException.Should().BeOfType<InvalidOperationException>();
        executed.Should().BeFalse();
    }
}