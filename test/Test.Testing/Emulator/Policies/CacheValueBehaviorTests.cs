// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Document;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Services;

namespace Test.Emulator.Emulator.Policies;

[TestClass]
public class CacheValueBehaviorTests
{
    [TestMethod]
    [DataRow("inbound", false)]
    [DataRow("backend", false)]
    [DataRow("outbound", false)]
    [DataRow("on-error", false)]
    [DataRow("inbound", true)]
    [DataRow("backend", true)]
    [DataRow("outbound", true)]
    [DataRow("on-error", true)]
    public void CacheValue_UsesTheSameCacheAsStoreLookupAndRemoveInEverySection(string section, bool injected)
    {
        var blockCalls = 0;
        var test = CacheTest.Value(context =>
        {
            var key = context.ExpressionContext.Request.Url.Path;
            context.Store(new CacheStoreValueConfig { Key = key, Value = "stored", Duration = 10 });
            context.Value(new CacheValueConfig { Key = key, VariableName = "first" }, () => blockCalls++);
            context.Remove(new CacheRemoveValueConfig { Key = key });
            context.Value(new CacheValueConfig { Key = key, VariableName = "second", ExpiresAfter = 10 }, () =>
            {
                blockCalls++;
                context.SetVariable("second", "computed");
            });
            context.Lookup(new CacheLookupValueConfig { Key = key, VariableName = "last" });
        }, new CacheTestClock(), injected ? CacheTest.SharedCache() : null);

        ExecutionTest.RunSection(test, section);

        test.Context.Variables["first"].Should().Be("stored");
        test.Context.Variables["second"].Should().Be("computed");
        test.Context.Variables["last"].Should().Be("computed");
        blockCalls.Should().Be(1);
    }

    [TestMethod]
    [DataRow(false, -1, 1)]
    [DataRow(false, 0, 2)]
    [DataRow(false, 1, 2)]
    [DataRow(true, -1, 1)]
    [DataRow(true, 0, 2)]
    [DataRow(true, 1, 2)]
    public void CacheValue_RefreshStartsAtTheExactRefreshBoundary(bool injected, int ticks, int expectedCalls)
    {
        var clock = new CacheTestClock();
        var calls = 0;
        var test = CacheTest.Value(context => context.Value(new CacheValueConfig
        {
            Key = "key",
            VariableName = "result",
            ExpiresAfter = 10,
            RefreshAfter = 4
        }, () => context.SetVariable("result", $"value-{++calls}")), clock,
            injected ? CacheTest.SharedCache() : null);
        test.RunInbound();
        clock.Advance(TimeSpan.FromSeconds(4) + TimeSpan.FromTicks(ticks));

        test.RunInbound();

        calls.Should().Be(expectedCalls);
        test.Context.Variables["result"].Should().Be($"value-{expectedCalls}");
    }

    [TestMethod]
    [DataRow(false, -1, false)]
    [DataRow(false, 0, true)]
    [DataRow(false, 1, true)]
    [DataRow(true, -1, false)]
    [DataRow(true, 0, true)]
    [DataRow(true, 1, true)]
    public void CacheValue_ExpiredEntriesAreNotReturnedWhenTheFactoryProducesNoValue(
        bool injected, int ticks, bool expired)
    {
        var clock = new CacheTestClock();
        var calls = 0;
        var test = CacheTest.Value(context => context.Value(new CacheValueConfig
        {
            Key = "key",
            VariableName = "result",
            ExpiresAfter = 10,
            DefaultValue = "default"
        }, () =>
        {
            if (++calls == 1)
            {
                context.SetVariable("result", "cached");
            }
        }), clock, injected ? CacheTest.SharedCache() : null);
        test.RunInbound();
        clock.Advance(TimeSpan.FromSeconds(10) + TimeSpan.FromTicks(ticks));

        test.RunInbound();

        calls.Should().Be(expired ? 2 : 1);
        test.Context.Variables["result"].Should().Be(expired ? "default" : "cached");
    }

    [TestMethod]
    [DataRow(false, false)]
    [DataRow(false, true)]
    [DataRow(true, false)]
    [DataRow(true, true)]
    public void CacheValue_ForceRefreshExecutesDespiteAFreshEntryAndDoesNotCacheNull(
        bool injected, bool nullResult)
    {
        var clock = new CacheTestClock();
        var calls = 0;
        var test = CacheTest.Value(context => context.Value(new CacheValueConfig
        {
            Key = "key",
            VariableName = "result",
            ExpiresAfter = 10,
            RefreshAfter = 0
        }, () =>
        {
            calls++;
            if (calls == 1 || !nullResult)
            {
                context.SetVariable("result", $"value-{calls}");
            }
            else
            {
                context.SetVariable("result", null!);
            }
        }), clock, injected ? CacheTest.SharedCache() : null);

        test.RunInbound();
        test.RunInbound();

        calls.Should().Be(2);
        test.Context.Variables["result"].Should().Be(nullResult ? "value-1" : "value-2");
        var lookup = CacheTest.Value(context => context.Lookup(new CacheLookupValueConfig
        {
            Key = "key",
            VariableName = "lookup"
        }), clock, context: test.Context);
        lookup.RunInbound();
        test.Context.Variables["lookup"].Should().Be(nullResult ? "value-1" : "value-2");
    }

    [TestMethod]
    [DataRow(false, false)]
    [DataRow(false, true)]
    [DataRow(true, false)]
    [DataRow(true, true)]
    public void CacheValue_RefreshWithoutAValueKeepsTheUnexpiredEntryButDoesNotExtendItsTtl(
        bool injected, bool nullResult)
    {
        var clock = new CacheTestClock();
        var calls = 0;
        var test = CacheTest.Value(context => context.Value(new CacheValueConfig
        {
            Key = "key",
            VariableName = "result",
            ExpiresAfter = 10,
            RefreshAfter = 4,
            DefaultValue = "default"
        }, () =>
        {
            if (++calls == 1)
            {
                context.SetVariable("result", "cached");
            }
            else if (nullResult)
            {
                context.SetVariable("result", null!);
            }
        }), clock, injected ? CacheTest.SharedCache() : null);
        test.RunInbound();
        clock.Advance(TimeSpan.FromSeconds(4));
        test.RunInbound();
        test.Context.Variables["result"].Should().Be("cached");
        clock.Advance(TimeSpan.FromSeconds(6));

        test.RunInbound();

        calls.Should().Be(3);
        test.Context.Variables["result"].Should().Be("default");
    }

    [TestMethod]
    [DataRow(false, false)]
    [DataRow(false, true)]
    [DataRow(true, false)]
    [DataRow(true, true)]
    public void CacheValue_MissClearsTheTargetBeforeTheBlockAndAppliesDefaultWithoutCachingIt(
        bool injected, bool nullResult)
    {
        var clock = new CacheTestClock();
        var targetWasPresent = true;
        var test = CacheTest.Value(context => context.Value(new CacheValueConfig
        {
            Key = "key",
            VariableName = "result",
            DefaultValue = "default"
        }, () =>
        {
            targetWasPresent = context.ExpressionContext.Variables.ContainsKey("result");
            if (nullResult)
            {
                context.SetVariable("result", null!);
            }
        }), clock, injected ? CacheTest.SharedCache() : null);
        test.Context.Variables["result"] = "unrelated";

        test.RunInbound();

        targetWasPresent.Should().BeFalse();
        test.Context.Variables["result"].Should().Be("default");
        var lookup = CacheTest.Value(context => context.Lookup(new CacheLookupValueConfig
        {
            Key = "key",
            VariableName = "lookup",
            DefaultValue = "not-cached"
        }), clock, context: test.Context);
        lookup.RunInbound();
        test.Context.Variables["lookup"].Should().Be("not-cached");
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void CacheValue_MissWithoutDefaultDoesNotRetainAnUnrelatedOrNullVariable(bool injected)
    {
        var test = CacheTest.Value(context => context.Value(new CacheValueConfig
        {
            Key = "key",
            VariableName = "result"
        }, () => context.SetVariable("result", null!)), new CacheTestClock(),
            injected ? CacheTest.SharedCache() : null);
        test.Context.Variables["result"] = "unrelated";

        test.RunInbound();

        test.Context.Variables.Should().NotContainKey("result");
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void CacheValue_EvaluatorsRunAfterTheBlockAndNotOnAFreshHit(bool injected)
    {
        var clock = new CacheTestClock();
        var calls = new List<string>();
        var expires = 0;
        var refresh = 0;
        var test = CacheTest.Value(context => context.Value(new CacheValueConfig
        {
            Key = "key",
            VariableName = "result",
            ExpiresAfterEvaluator = () =>
            {
                calls.Add("expires");
                return expires;
            },
            RefreshAfterEvaluator = () =>
            {
                calls.Add("refresh");
                return refresh;
            }
        }, () =>
        {
            calls.Add("block");
            expires = 10;
            refresh = 4;
            context.SetVariable("result", "computed");
        }), clock, injected ? CacheTest.SharedCache() : null);

        test.RunInbound();
        test.RunInbound();

        calls.Should().Equal("block", "expires", "refresh");
        test.Context.Variables["result"].Should().Be("computed");
        if (!injected)
        {
            var entry = test.SetupCacheStore().InternalCache["key"];
            entry.Ttl.Should().Be(TimeSpan.FromSeconds(10));
            entry.RefreshAfter.Should().Be(TimeSpan.FromSeconds(4));
        }
    }

    [TestMethod]
    [DataRow(false, false)]
    [DataRow(false, true)]
    [DataRow(true, false)]
    [DataRow(true, true)]
    public void CacheValue_FactoryAndEvaluatorFailuresPropagateWithoutReplacingTheEntry(
        bool injected, bool evaluatorFailure)
    {
        var clock = new CacheTestClock();
        var fail = false;
        var test = CacheTest.Value(context => context.Value(new CacheValueConfig
        {
            Key = "key",
            VariableName = "result",
            ExpiresAfter = 10,
            RefreshAfter = 4,
            ExpiresAfterEvaluator = evaluatorFailure
                ? () => fail ? throw new InvalidOperationException("evaluator failure") : 10
                : null
        }, () =>
        {
            context.SetVariable("result", fail ? "replacement" : "cached");
            if (fail && !evaluatorFailure)
            {
                throw new InvalidOperationException("factory failure");
            }
        }), clock, injected ? CacheTest.SharedCache() : null);
        test.RunInbound();
        clock.Advance(TimeSpan.FromSeconds(4));
        fail = true;

        var error = Assert.ThrowsExactly<PolicyException>(test.RunInbound);

        error.Policy.Should().Be(nameof(IInboundContext.CacheValue));
        error.InnerException.Should().BeOfType<InvalidOperationException>()
            .Which.Message.Should().Be(evaluatorFailure ? "evaluator failure" : "factory failure");
        test.Context.Variables["result"].Should().Be("cached");
        var lookup = CacheTest.Value(context => context.Lookup(new CacheLookupValueConfig
        {
            Key = "key",
            VariableName = "lookup"
        }), clock, context: test.Context);
        lookup.RunInbound();
        test.Context.Variables["lookup"].Should().Be("cached");
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void CacheValue_AnEntryThatExpiresDuringAnEmptyRefreshIsNotReturned(bool injected)
    {
        var clock = new CacheTestClock();
        var calls = 0;
        var test = CacheTest.Value(context => context.Value(new CacheValueConfig
        {
            Key = "key",
            VariableName = "result",
            ExpiresAfter = 10,
            RefreshAfter = 4,
            DefaultValue = "default"
        }, () =>
        {
            if (++calls == 1)
            {
                context.SetVariable("result", "cached");
            }
            else
            {
                clock.Advance(TimeSpan.FromSeconds(6));
            }
        }), clock, injected ? CacheTest.SharedCache() : null);
        test.RunInbound();
        clock.Advance(TimeSpan.FromSeconds(4));

        test.RunInbound();

        test.Context.Variables["result"].Should().Be("default");
    }

    [TestMethod]
    [DataRow(-1, null)]
    [DataRow(10, -1)]
    [DataRow(10, 11)]
    public void CacheValue_InvalidTtlConfigurationIsAnExplicitError(int expires, int? refresh)
    {
        var test = CacheTest.Value(context => context.Value(new CacheValueConfig
        {
            Key = "key",
            VariableName = "result",
            ExpiresAfter = expires,
            RefreshAfter = refresh
        }, () => context.SetVariable("result", "value")), new CacheTestClock());

        var error = Assert.ThrowsExactly<PolicyException>(test.RunInbound);

        error.InnerException.Should().BeAssignableTo<ArgumentException>();
        test.SetupCacheStore().InternalCache.Should().BeEmpty();
    }

    [TestMethod]
    public void CacheValue_InvalidEvaluatedTtlIsNotWrittenAndMissingBlockIsRejected()
    {
        var clock = new CacheTestClock();
        var invalidTtl = CacheTest.Value(context => context.Value(new CacheValueConfig
        {
            Key = "key",
            VariableName = "result",
            ExpiresAfterEvaluator = () => -1
        }, () => context.SetVariable("result", "value")), clock);
        var missingBlock = CacheTest.Value(context => context.Value(new CacheValueConfig
        {
            Key = "key",
            VariableName = "result"
        }, null!), clock);

        Assert.ThrowsExactly<PolicyException>(invalidTtl.RunInbound).InnerException.Should()
            .BeOfType<ArgumentOutOfRangeException>();
        Assert.ThrowsExactly<PolicyException>(missingBlock.RunInbound).InnerException.Should()
            .BeOfType<ArgumentNullException>();
        invalidTtl.SetupCacheStore().InternalCache.Should().BeEmpty();
    }

    [TestMethod]
    public async Task CacheValue_SharedStoreProtectsConcurrentFactoriesAndReportsMissAndRefresh()
    {
        var clock = new CacheTestClock();
        var cache = CacheTest.SharedCache();
        CacheTest.Value(context => context.Remove(new CacheRemoveValueConfig { Key = "initial" }), clock, cache)
            .RunInbound();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        async Task<CacheValueFactoryResult> Factory(object? _, CancellationToken ct)
        {
            Interlocked.Increment(ref calls);
            entered.SetResult();
            await release.Task.WaitAsync(ct);
            return new CacheValueFactoryResult("computed", TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(4));
        }
        var first = cache.GetOrCreateWithDynamicTtlAsync("key", Factory);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var second = cache.GetOrCreateWithDynamicTtlAsync("key", Factory);
        release.SetResult();
        var results = await Task.WhenAll(first, second).WaitAsync(TimeSpan.FromSeconds(5));

        calls.Should().Be(1);
        results.Select(result => result.Value).Should().Equal("computed", "computed");
        results[0].WasCacheMiss.Should().BeTrue();
        results[1].WasCacheMiss.Should().BeFalse();
        clock.Advance(TimeSpan.FromSeconds(4));
        var refreshed = await cache.GetOrCreateAsync("key", TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(4),
            (old, _) =>
            {
                old.Should().Be("computed");
                return Task.FromResult<object?>("refreshed");
            });
        refreshed.WasRefreshed.Should().BeTrue();
        refreshed.WasCacheMiss.Should().BeFalse();
        refreshed.Value.Should().Be("refreshed");
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Func<Task> cancelled = () => cache.GetAsync("key", cancellation.Token);
        await cancelled.Should().ThrowAsync<OperationCanceledException>();
    }
}