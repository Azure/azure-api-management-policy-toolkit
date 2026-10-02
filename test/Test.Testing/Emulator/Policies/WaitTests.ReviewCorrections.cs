// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Document;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Data;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Expressions;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Services;

namespace Test.Emulator.Emulator.Policies;

public partial class WaitTests
{
    [TestMethod]
    [DataRow("all", false, false)]
    [DataRow("all", false, true)]
    [DataRow("all", true, false)]
    [DataRow("all", true, true)]
    [DataRow("any", false, false)]
    [DataRow("any", false, true)]
    [DataRow("any", true, false)]
    [DataRow("any", true, true)]
    public void WaitCorrection_ChildTokenRejectionPreservesParentAdmissionThroughSuccessfulBackend(
        string mode, bool quota, bool azure)
    {
        var store = new TokenLimitCounterStore();
        var clock = new ReviewWaitClock();
        var parent = ReviewParentTokenConfig();
        var rejected = ReviewRejectedTokenConfig(quota);
        var test = ReviewTokenDocument(context =>
        {
            context.LlmTokenLimit(parent);
            context.Wait(mode, branch =>
            {
                if (true)
                {
                    if (azure) branch.AzureOpenAiTokenLimit(rejected);
                    else branch.LlmTokenLimit(rejected);
                    Assert.Fail("A rejected child must terminate its own pipeline.");
                }
            });
            context.ExpressionContext.Variables["parent-consumed"].Should().Be(5L);
        }, store, clock);

        test.RunAll();

        AssertReviewParentTokenSettlement(test, store, clock);
        store.GetRateTokens("denied", clock.GetUtcNow()).Should().Be(0);
        store.GetQuotaTokens("denied", "Daily", clock.GetUtcNow()).Should().Be(0);
        var probe = ReviewTokenDocument(context => context.LlmTokenLimit(parent with
        {
            TokensPerMinute = 7,
            EstimatePromptToken = false
        }), store, clock, actualTokens: 0);
        probe.RunAll();
        probe.Context.Response.StatusCode.Should().Be(200);
        probe.Context.Variables["parent-remaining"].Should().Be(1L);
        store.GetRateTokens("parent", clock.GetUtcNow()).Should().Be(6);
    }

    [TestMethod]
    public void WaitCorrection_ParentTokenAdmissionControlChargesSixActualTokens()
    {
        var store = new TokenLimitCounterStore();
        var clock = new ReviewWaitClock();
        var test = ReviewTokenDocument(context => context.LlmTokenLimit(ReviewParentTokenConfig()), store, clock);

        test.RunAll();

        AssertReviewParentTokenSettlement(test, store, clock);
    }

    [TestMethod]
    [DataRow(false, false)]
    [DataRow(false, true)]
    [DataRow(true, false)]
    [DataRow(true, true)]
    public void WaitCorrection_ChildTokenRejectionPreservesPreviouslyAdmittedSibling(
        bool quota, bool sharedKey)
    {
        var store = new TokenLimitCounterStore();
        var clock = new ReviewWaitClock();
        var admitted = WaitSignal<bool>();
        var parent = ReviewParentTokenConfig();
        var sibling = parent with
        {
            CounterKey = sharedKey ? "parent" : "sibling",
            TokensConsumedVariableName = "sibling-consumed",
            RemainingTokensVariableName = "sibling-remaining"
        };
        var test = ReviewTokenDocument(context =>
        {
            context.LlmTokenLimit(parent);
            context.Wait("all",
                branch =>
                {
                    if (true)
                    {
                        branch.AzureOpenAiTokenLimit(sibling);
                        admitted.SetResult(true);
                    }
                },
                branch =>
                {
                    if (true)
                    {
                        admitted.Task.WaitAsync(s_waitTimeout).GetAwaiter().GetResult();
                        branch.LlmTokenLimit(ReviewRejectedTokenConfig(quota));
                    }
                });
        }, store, clock);

        test.RunAll();

        AssertReviewParentTokenSettlement(test, store, clock);
        store.GetRateTokens(sibling.CounterKey, clock.GetUtcNow()).Should().Be(6);
        test.Context.Variables["sibling-consumed"].Should().Be(5L,
            "only the branch's Wait-completion variables, not its later settlement outputs, are back-propagated");
        store.GetRateTokens("denied", clock.GetUtcNow()).Should().Be(0);
    }

    [TestMethod]
    [DataRow(false, false)]
    [DataRow(false, true)]
    [DataRow(true, false)]
    [DataRow(true, true)]
    public async Task WaitCorrection_IndependentLimiterProvidersOverlapAndAnyCancelsUnsettledLoser(
        bool deferred, bool lateAllowed)
    {
        var limiter = new ReviewAsymmetricLimiter();
        var rates = new RateLimitStore();
        var exited = WaitSignal<bool>();
        var test = new FullRequestDocument(context => context.Wait("any",
            branch =>
            {
                try
                {
                    if (true)
                    {
                        branch.RateLimitByKey(ReviewRateConfig("slow", deferred));
                        branch.SetVariable("loser", true);
                    }
                }
                finally
                {
                    exited.TrySetResult(true);
                }
            },
            branch =>
            {
                if (true)
                {
                    limiter.SlowStarted.Task.WaitAsync(s_waitTimeout).GetAwaiter().GetResult();
                    branch.RateLimitByKey(ReviewRateConfig("fast", deferred));
                    branch.SetVariable("winner", "fast");
                }
            })).AsTestDocument();
        test.Context.Services.Register(rates).Register<IRateLimiter>(limiter);
        var execution = RunWaitAsync(test.RunAll);

        try
        {
            await Task.WhenAll(limiter.SlowStarted.Task, limiter.FastStarted.Task).WaitAsync(s_waitTimeout);
            limiter.SlowResult.Task.IsCompleted.Should().BeFalse();
            execution.IsCompleted.Should().BeFalse();
            limiter.FastResult.SetResult(true);
            await execution.WaitAsync(s_waitTimeout);
            await exited.Task.WaitAsync(s_waitTimeout);

            limiter.SlowToken.IsCancellationRequested.Should().BeTrue();
            test.Context.Variables["winner"].Should().Be("fast");
            test.Context.Variables.Should().NotContainKey("loser").And.NotContainKey("slow-remaining");
            rates.GetCount("rate-limit-by-key:fast").Should().Be(1);
            rates.GetCount("rate-limit-by-key:slow").Should().Be(0);
            limiter.SlowResult.SetResult(lateAllowed);
            await limiter.SlowObserved.Task.WaitAsync(s_waitTimeout);
            test.CompleteLimiterResponse();

            limiter.SlowCalls.Should().Be(1);
            limiter.FastCalls.Should().Be(deferred ? 2 : 1);
            rates.GetCount("rate-limit-by-key:slow").Should().Be(0);
            rates.GetCount("rate-limit-by-key:fast").Should().Be(1);
            test.Context.Response.StatusCode.Should().Be(200);
            test.Context.Variables.Should().NotContainKey("loser").And.NotContainKey("slow-remaining");
            test.Context.Services.Resolve<PolicyCounterService>()!.HasPendingForFullWait().Should().BeFalse();
        }
        finally
        {
            limiter.Release();
            await ObserveWaitExecution(execution);
            await exited.Task.WaitAsync(s_waitTimeout);
        }
    }

    [TestMethod]
    public async Task WaitCorrection_DistinctQuotaProvidersOverlapAndAccountEachKeyOnce()
    {
        var limiter = new ReviewAsymmetricLimiter();
        var rates = new RateLimitStore();
        var fastExited = WaitSignal<bool>();
        var test = new FullRequestDocument(context => context.Wait("all",
            branch => { if (true) branch.QuotaByKey(ReviewQuotaConfig("slow")); },
            branch =>
            {
                if (true)
                {
                    limiter.SlowStarted.Task.WaitAsync(s_waitTimeout).GetAwaiter().GetResult();
                    branch.QuotaByKey(ReviewQuotaConfig("fast"));
                    fastExited.SetResult(true);
                }
            }), backend: context => ((GatewayContext)context.ExpressionContext).Response.Body.Content =
                new string('r', 2048)).AsTestDocument();
        test.Context.Request.Body.Content = new string('q', 1024);
        test.Context.Services.Register(rates).Register<IRateLimiter>(limiter);
        var execution = RunWaitAsync(test.RunAll);

        try
        {
            await Task.WhenAll(limiter.SlowStarted.Task, limiter.FastStarted.Task).WaitAsync(s_waitTimeout);
            limiter.FastResult.SetResult(true);
            await fastExited.Task.WaitAsync(s_waitTimeout);
            execution.IsCompleted.Should().BeFalse();
            limiter.SlowResult.SetResult(true);
            await execution.WaitAsync(s_waitTimeout);

            foreach (var key in new[] { "slow", "fast" })
            {
                rates.GetCount($"quota-by-key:{key}").Should().Be(1);
                rates.GetBandwidth($"quota-by-key:{key}").Should().Be(3072);
            }
            limiter.SlowCalls.Should().Be(1);
            limiter.FastCalls.Should().Be(1);
        }
        finally
        {
            limiter.Release();
            await ObserveWaitExecution(execution);
        }
    }

    [TestMethod]
    public async Task WaitCorrection_SameKeyQuotaAdmissionsRemainAtomicAcrossBranches()
    {
        var limiter = new ReviewAsymmetricLimiter("quota");
        var rates = new RateLimitStore();
        var secondEntered = WaitSignal<bool>();
        var test = new FullRequestDocument(context => context.Wait("all",
            branch => { if (true) branch.QuotaByKey(ReviewQuotaConfig("quota")); },
            branch =>
            {
                if (true)
                {
                    limiter.SlowStarted.Task.WaitAsync(s_waitTimeout).GetAwaiter().GetResult();
                    secondEntered.SetResult(true);
                    branch.QuotaByKey(ReviewQuotaConfig("quota"));
                }
            }), backend: context => ((GatewayContext)context.ExpressionContext).Response.Body.Content =
                new string('r', 1024)).AsTestDocument();
        test.Context.Request.Body.Content = new string('q', 512);
        test.Context.Services.Register(rates).Register<IRateLimiter>(limiter);
        var execution = RunWaitAsync(test.RunAll);

        try
        {
            await secondEntered.Task.WaitAsync(s_waitTimeout);
            limiter.SlowResult.Task.IsCompleted.Should().BeFalse();
            limiter.SlowResult.SetResult(true);
            await execution.WaitAsync(s_waitTimeout);

            rates.GetCount("quota-by-key:quota").Should().Be(1);
            rates.GetBandwidth("quota-by-key:quota").Should().Be(1536);
            limiter.SlowCalls.Should().Be(1);
        }
        finally
        {
            limiter.Release();
            await ObserveWaitExecution(execution);
        }
    }

    [TestMethod]
    public async Task WaitCorrection_DeferredSettlementDoesNotHoldTheSharedMonitorOverProviderAwait()
    {
        var limiter = new ReviewSettlementLimiter();
        var test = new InboundWaitAction(context => context.RateLimitByKey(ReviewRateConfig("settle", true)))
            .AsTestDocument();
        test.Context.Services.Register<IRateLimiter>(limiter);
        test.RunInbound();
        var counters = test.Context.Services.Resolve<PolicyCounterService>()!;
        var completion = RunWaitAsync(test.CompleteLimiterResponse);
        Task<bool>? pendingRead = null;

        try
        {
            await limiter.Started.Task.WaitAsync(s_waitTimeout);
            pendingRead = Task.Factory.StartNew(counters.HasPendingForFullWait, CancellationToken.None,
                TaskCreationOptions.LongRunning, TaskScheduler.Default);
            (await pendingRead.WaitAsync(s_waitTimeout)).Should().BeTrue();
            completion.IsCompleted.Should().BeFalse();
            limiter.Result.SetResult(true);
            await completion.WaitAsync(s_waitTimeout);
            test.SetupRateLimitStore().GetCount("rate-limit-by-key:settle").Should().Be(1);
            counters.HasPendingForFullWait().Should().BeFalse();
        }
        finally
        {
            limiter.Result.TrySetResult(true);
            await ObserveWaitExecution(completion);
            if (pendingRead is not null) await pendingRead.WaitAsync(s_waitTimeout);
        }
    }

    [TestMethod]
    [DynamicData(nameof(ReviewHeaderCases))]
    public async Task WaitCorrection_OrderedExplicitHeaderMutationsOverridePublishedSiblingState(
        string section, string mutation, bool fragment)
    {
        var barriers = new ReviewHeaderBarriers();
        var test = new ReviewHeaderDocument(mutation, fragment, barriers).AsTestDocument();
        if (fragment) test.RegisterFragment("ordered-headers", new ReviewHeaderFragment(mutation, barriers));
        var message = ReviewMessage(test.Context, section);
        var headers = new Dictionary<string, string[]>(StringComparer.Ordinal);
        if (mutation != "remove-absent") headers["X-State"] = ["original"];
        message.Headers = headers;
        ConfigureReviewHeaderMutation(test, section, mutation, barriers);
        var execution = RunWaitAsync(() => RunSection(test, section));

        try
        {
            await barriers.SiblingPublished.Task.WaitAsync(s_waitTimeout);
            message.Headers["X-State"].Should().Equal("sibling");
            barriers.ReleaseFirst.SetResult(true);
            await barriers.FirstReturned.Task.WaitAsync(s_waitTimeout);
            if (mutation.StartsWith("remove", StringComparison.Ordinal))
                message.Headers.Should().NotContainKey("X-State");
            else
                message.Headers["X-State"].Should().Equal(mutation == "callback-noop" ? "sibling" : "original");
            barriers.ReleaseSibling.SetResult(true);
            await execution.WaitAsync(s_waitTimeout);

            message.Headers.Should().BeSameAs(headers);
            if (mutation.StartsWith("remove", StringComparison.Ordinal))
                message.Headers.Should().NotContainKey("X-State");
            else
                message.Headers["X-State"].Should().Equal(mutation == "callback-noop" ? "sibling" : "original");
        }
        finally
        {
            barriers.ReleaseFirst.TrySetResult(true);
            barriers.ReleaseSibling.TrySetResult(true);
            await ObserveWaitExecution(execution);
        }
    }

    public static IEnumerable<object[]> ReviewHeaderCases()
    {
        foreach (var section in new[]
                 { nameof(IInboundContext), nameof(IBackendContext), nameof(IOutboundContext), nameof(IOnErrorContext) })
        foreach (var mutation in new[] { "callback", "callback-variable", "default", "remove", "remove-absent", "callback-noop" })
        foreach (var fragment in new[] { false, true })
            yield return [section, mutation, fragment];
    }

    private static TokenLimitConfig ReviewParentTokenConfig() => new()
    {
        CounterKey = "parent",
        TokensPerMinute = 100,
        EstimatePromptToken = true,
        TokensConsumedVariableName = "parent-consumed",
        RemainingTokensVariableName = "parent-remaining"
    };

    private static TokenLimitConfig ReviewRejectedTokenConfig(bool quota) => new()
    {
        CounterKey = "denied",
        TokensPerMinute = quota ? null : 1,
        TokenQuota = quota ? 1 : null,
        TokenQuotaPeriod = quota ? "Daily" : null,
        EstimatePromptToken = true
    };

    private static TestDocument ReviewTokenDocument(
        Action<IInboundContext> inbound, TokenLimitCounterStore store, TimeProvider clock, int actualTokens = 6)
    {
        var test = new FullRequestDocument(inbound, context => context.ForwardRequest()).AsTestDocument();
        test.Context.Services.Register(store).Register(clock).Register<ITokenLimitPromptEstimator>(new ReviewPromptEstimator());
        test.Context.Request.Body.Content = """{"messages":[{"role":"user","content":"hello"}]}""";
        test.SetupBackend().ForwardRequest().WithCallback((context, _) => context.Response = new MockResponse
        {
            Body = new MockBody
            {
                Content = $$$"""{"usage":{"prompt_tokens":{{{actualTokens}}},"completion_tokens":0}}"""
            }
        });
        return test;
    }

    private static void AssertReviewParentTokenSettlement(
        TestDocument test, TokenLimitCounterStore store, TimeProvider clock)
    {
        test.Context.Response.StatusCode.Should().Be(200);
        test.Context.ResponseTerminated.Should().BeFalse();
        store.GetRateTokens("parent", clock.GetUtcNow()).Should().Be(6);
        test.Context.Variables["parent-consumed"].Should().Be(6L);
        test.Context.Variables["parent-remaining"].Should().Be(94L);
        test.Context.Services.Resolve<TokenLimitService>()!.HasPendingResponse.Should().BeFalse();
    }

    private static RateLimitByKeyConfig ReviewRateConfig(string key, bool deferred) => new()
    {
        CounterKey = key,
        Calls = 100,
        RenewalPeriod = 60,
        IncrementAfterResponse = deferred,
        RemainingCallsVariableName = $"{key}-remaining"
    };

    private static QuotaByKeyConfig ReviewQuotaConfig(string key) => new()
    {
        CounterKey = key,
        Calls = 100,
        Bandwidth = 100,
        RenewalPeriod = 60
    };

    private static MockMessage ReviewMessage(GatewayContext context, string section) =>
        section is nameof(IInboundContext) or nameof(IBackendContext) ? context.Request : context.Response;

    private static void ConfigureReviewHeaderMutation(
        TestDocument test, string section, string mutation, ReviewHeaderBarriers barriers)
    {
        void Pause()
        {
            barriers.FirstEntered.SetResult(true);
            barriers.ReleaseFirst.Task.WaitAsync(s_waitTimeout).GetAwaiter().GetResult();
        }

        if (mutation.StartsWith("remove", StringComparison.Ordinal))
        {
            SetupReviewRemoveHeader(test, section, (_, _) =>
            {
                Pause();
                return false;
            }).WithCallback((_, _) => Assert.Fail("The default remove handler must execute."));
        }
        else if (mutation == "callback-variable")
        {
            void Write(GatewayContext context, string name, object value)
            {
                Pause();
                ReviewMessage(context, section).Headers["X-State"] = ["original"];
                context.Variables[name] = value;
            }
            switch (section)
            {
                case nameof(IInboundContext): test.SetupInbound().SetVariable().WithCallback(Write); break;
                case nameof(IBackendContext): test.SetupBackend().SetVariable().WithCallback(Write); break;
                case nameof(IOutboundContext): test.SetupOutbound().SetVariable().WithCallback(Write); break;
                case nameof(IOnErrorContext): test.SetupOnError().SetVariable().WithCallback(Write); break;
                default: throw new ArgumentException("Unknown section.", nameof(section));
            }
        }
        else if (mutation == "default")
        {
            SetupReviewSetHeader(test, section, (_, _, values) =>
            {
                if (values.SequenceEqual(["original"])) Pause();
                return false;
            }).WithCallback((_, _, _) => Assert.Fail("The default setter must execute."));
        }
        else
        {
            SetupReviewSetHeader(test, section, (_, _, values) => values.SequenceEqual(["original"]))
                .WithCallback((context, name, values) =>
                {
                    Pause();
                    if (mutation != "callback-noop") ReviewMessage(context, section).Headers[name] = values;
                });
        }
    }

    private static MockSetHeaderProvider.Setup SetupReviewSetHeader(
        TestDocument test, string section, Func<GatewayContext, string, string[], bool> predicate) => section switch
    {
        nameof(IInboundContext) => test.SetupInbound().SetHeader(predicate),
        nameof(IBackendContext) => test.SetupBackend().SetHeader(predicate),
        nameof(IOutboundContext) => test.SetupOutbound().SetHeader(predicate),
        nameof(IOnErrorContext) => test.SetupOnError().SetHeader(predicate),
        _ => throw new ArgumentException("Unknown section.", nameof(section))
    };

    private static MockRemoveHeaderProvider.Setup SetupReviewRemoveHeader(
        TestDocument test, string section, Func<GatewayContext, string, bool> predicate) => section switch
    {
        nameof(IInboundContext) => test.SetupInbound().RemoveHeader(predicate),
        nameof(IBackendContext) => test.SetupBackend().RemoveHeader(predicate),
        nameof(IOutboundContext) => test.SetupOutbound().RemoveHeader(predicate),
        nameof(IOnErrorContext) => test.SetupOnError().RemoveHeader(predicate),
        _ => throw new ArgumentException("Unknown section.", nameof(section))
    };

    private static void ReviewFirstHeader(
        string mutation, ReviewHeaderBarriers barriers,
        Action<string, string[]> set, Action<string> remove, Action<string, object> variable)
    {
        if (mutation.StartsWith("remove", StringComparison.Ordinal)) remove("X-State");
        else if (mutation == "callback-variable") variable("write", true);
        else set("X-State", ["original"]);
        barriers.FirstReturned.SetResult(true);
    }

    private static void ReviewSiblingHeader(ReviewHeaderBarriers barriers, Action<string, string[]> set)
    {
        barriers.FirstEntered.Task.WaitAsync(s_waitTimeout).GetAwaiter().GetResult();
        set("X-State", ["sibling"]);
        barriers.SiblingPublished.SetResult(true);
        barriers.ReleaseSibling.Task.WaitAsync(s_waitTimeout).GetAwaiter().GetResult();
    }

    private sealed class ReviewHeaderDocument(
        string mutation, bool fragment, ReviewHeaderBarriers barriers) : IDocument
    {
        public void Inbound(IInboundContext context)
        {
            if (fragment) context.IncludeFragment("ordered-headers");
            else context.Wait("all",
                branch => ReviewFirstHeader(mutation, barriers, branch.SetHeader, branch.RemoveHeader, branch.SetVariable),
                branch => ReviewSiblingHeader(barriers, branch.SetHeader));
        }
        public void Backend(IBackendContext context)
        {
            if (fragment) context.IncludeFragment("ordered-headers");
            else context.Wait("all",
                branch => ReviewFirstHeader(mutation, barriers, branch.SetHeader, branch.RemoveHeader, branch.SetVariable),
                branch => ReviewSiblingHeader(barriers, branch.SetHeader));
        }
        public void Outbound(IOutboundContext context)
        {
            if (fragment) context.IncludeFragment("ordered-headers");
            else context.Wait("all",
                branch => ReviewFirstHeader(mutation, barriers, branch.SetHeader, branch.RemoveHeader, branch.SetVariable),
                branch => ReviewSiblingHeader(barriers, branch.SetHeader));
        }
        public void OnError(IOnErrorContext context)
        {
            if (fragment) context.IncludeFragment("ordered-headers");
            else context.Wait("all",
                branch => ReviewFirstHeader(mutation, barriers, branch.SetHeader, branch.RemoveHeader, branch.SetVariable),
                branch => ReviewSiblingHeader(barriers, branch.SetHeader));
        }
    }

    private sealed class ReviewHeaderFragment(string mutation, ReviewHeaderBarriers barriers) : IFragment
    {
        public void Fragment(IFragmentContext context) => context.Wait("all",
            branch => ReviewFirstHeader(mutation, barriers, branch.SetHeader, branch.RemoveHeader, branch.SetVariable),
            branch => ReviewSiblingHeader(barriers, branch.SetHeader));
    }

    private sealed class ReviewHeaderBarriers
    {
        internal TaskCompletionSource<bool> FirstEntered { get; } = WaitSignal<bool>();
        internal TaskCompletionSource<bool> ReleaseFirst { get; } = WaitSignal<bool>();
        internal TaskCompletionSource<bool> FirstReturned { get; } = WaitSignal<bool>();
        internal TaskCompletionSource<bool> SiblingPublished { get; } = WaitSignal<bool>();
        internal TaskCompletionSource<bool> ReleaseSibling { get; } = WaitSignal<bool>();
    }

    private sealed class ReviewPromptEstimator : ITokenLimitPromptEstimator
    {
        public long EstimatePromptTokens(GatewayContext context) => 5;
    }

    private sealed class ReviewWaitClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);
    }

    private sealed class ReviewAsymmetricLimiter(string slowKey = "slow") : IRateLimiter
    {
        internal TaskCompletionSource<bool> SlowStarted { get; } = WaitSignal<bool>();
        internal TaskCompletionSource<bool> FastStarted { get; } = WaitSignal<bool>();
        internal TaskCompletionSource<bool> SlowResult { get; } = WaitSignal<bool>();
        internal TaskCompletionSource<bool> FastResult { get; } = WaitSignal<bool>();
        internal TaskCompletionSource<bool> SlowObserved { get; } = WaitSignal<bool>();
        internal CancellationToken SlowToken { get; private set; }
        private int _slowCalls;
        private int _fastCalls;
        internal int SlowCalls => Volatile.Read(ref _slowCalls);
        internal int FastCalls => Volatile.Read(ref _fastCalls);

        public Task<bool> TryConsumeAsync(string key, int permits = 1, CancellationToken cancellationToken = default)
        {
            if (key == slowKey)
            {
                Interlocked.Increment(ref _slowCalls);
                SlowToken = cancellationToken;
                SlowStarted.TrySetResult(true);
                return ObserveSlow();
            }
            if (key != "fast") throw new ArgumentException("Unexpected limiter key.", nameof(key));
            if (Interlocked.Increment(ref _fastCalls) == 1)
            {
                FastStarted.SetResult(true);
                return FastResult.Task;
            }
            return Task.FromResult(true);
        }

        private async Task<bool> ObserveSlow()
        {
            try
            {
                return await SlowResult.Task.ConfigureAwait(false);
            }
            finally
            {
                SlowObserved.TrySetResult(true);
            }
        }

        internal void Release()
        {
            SlowResult.TrySetResult(true);
            FastResult.TrySetResult(true);
        }
    }

    private sealed class ReviewSettlementLimiter : IRateLimiter
    {
        internal TaskCompletionSource<bool> Started { get; } = WaitSignal<bool>();
        internal TaskCompletionSource<bool> Result { get; } = WaitSignal<bool>();

        public Task<bool> TryConsumeAsync(string key, int permits = 1, CancellationToken cancellationToken = default)
        {
            if (permits == 0) return Task.FromResult(true);
            Started.SetResult(true);
            return Result.Task;
        }
    }
}
