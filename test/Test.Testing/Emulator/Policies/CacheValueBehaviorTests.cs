// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Document;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Data;
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

    [TestMethod]
    [DataRow("inbound", "standalone")]
    [DataRow("backend", "standalone")]
    [DataRow("outbound", "standalone")]
    [DataRow("on-error", "standalone")]
    [DataRow("inbound", "request")]
    [DataRow("backend", "request")]
    [DataRow("outbound", "request")]
    [DataRow("on-error", "request")]
    [DataRow("inbound", "all")]
    public async Task CacheValue_WaitingForACanceledSharedFactoryRunsItsChildrenOnTheRequestThread(
        string section, string mode)
    {
        var clock = new CacheTestClock();
        var shared = new CacheStore(clock);
        var cache = new CacheRefreshTestCache(shared);
        using var cancellation = new CancellationTokenSource();
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var first = shared.GetOrCreateWithDynamicTtlAsync("key", async (_, ct) =>
        {
            await release.Task.WaitAsync(ct);
            return new CacheValueFactoryResult("other", TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(4));
        }, ct: cancellation.Token);
        first.IsCompleted.Should().BeFalse();
        var ownerThread = 0;
        var factoryThread = 0;
        var factoryCalls = 0;
        var test = CacheTest.Value(context => context.Value(new CacheValueConfig
        {
            Key = "key", VariableName = "result", ExpiresAfter = 10
        }, () =>
        {
            factoryThread = Environment.CurrentManagedThreadId;
            factoryCalls++;
            context.SetVariable("result", "computed");
        }), clock, cache);
        var requestId = test.Context.RequestId;
        var request = CacheRefreshTestCache.OnOwnerThread(() =>
        {
            ownerThread = Environment.CurrentManagedThreadId;
            switch (mode)
            {
                case "standalone": ExecutionTest.RunSection(test, section); break;
                case "request": test.RunRequest(inner => ExecutionTest.RunSection(inner, section)); break;
                case "all": test.RunAll(); break;
                default: throw new ArgumentOutOfRangeException(nameof(mode));
            }
        });
        try
        {
            await cache.Pending.Task.WaitAsync(TimeSpan.FromSeconds(5));
            cancellation.Cancel();
            Func<Task> canceled = () => first;
            await canceled.Should().ThrowAsync<OperationCanceledException>();
            await request.WaitAsync(TimeSpan.FromSeconds(5));
        }
        finally
        {
            cancellation.Cancel();
            release.TrySetResult();
        }

        factoryCalls.Should().Be(1);
        factoryThread.Should().Be(ownerThread);
        test.Context.RequestId.Should().Be(requestId);
        test.Context.Variables["result"].Should().Be("computed");
        (await shared.GetAsync("key")).Should().Be("computed");
    }

    [TestMethod]
    [DataRow("inbound")]
    [DataRow("backend")]
    [DataRow("outbound")]
    [DataRow("on-error")]
    public async Task CacheValue_CanceledAsyncFactoryRestoresVariablesWithoutExecutingChildren(string section)
    {
        var clock = new CacheTestClock();
        var shared = new CacheStore(clock);
        await shared.SetAsync("key", "cached", TimeSpan.FromSeconds(10));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var cache = new CacheRefreshTestCache(shared)
        {
            DynamicGet = async (_, factory, _, _) =>
            {
                await Task.Yield();
                var value = await factory("cached", cancellation.Token);
                return new CacheValueResult(value.Value, true, false);
            }
        };
        var calls = 0;
        var test = CacheTest.Value(context => context.Value(new CacheValueConfig
        {
            Key = "key", VariableName = "result", RefreshAfter = 0
        }, () =>
        {
            calls++;
            context.SetVariable("result", "replacement");
        }), clock, cache);
        test.Context.Variables["result"] = "original";

        var error = await CacheRefreshTestCache.OnOwnerThread(() =>
            Assert.ThrowsExactly<PolicyException>(() => ExecutionTest.RunSection(test, section)))
            .WaitAsync(TimeSpan.FromSeconds(5));

        error.Policy.Should().Be(nameof(IInboundContext.CacheValue));
        error.Section.Should().Be(ExecutionTest.SectionName(section));
        error.InnerException.Should().BeAssignableTo<OperationCanceledException>();
        calls.Should().Be(0);
        test.Context.Variables["result"].Should().Be("original");
        (await shared.GetAsync("key")).Should().Be("cached");
        var recovery = new TestDocument(new ExecutionTestDocument
        {
            OnErrorAction = context => context.SetVariable("recovered", true)
        }) { Context = test.Context };
        recovery.RunOnError();
        test.Context.Variables["recovered"].Should().Be(true);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task CacheValue_AsyncFactoryAndEvaluatorFailuresRestoreStateAndAllowRetry(bool evaluator)
    {
        var clock = new CacheTestClock();
        var shared = new CacheStore(clock);
        await shared.SetAsync("key", "cached", TimeSpan.FromSeconds(10));
        var cache = CacheRefreshTestCache.Asynchronous(shared);
        var expected = new InvalidOperationException("async factory failure");
        var fail = true;
        var test = CacheTest.Value(context => context.Value(new CacheValueConfig
        {
            Key = "key", VariableName = "result", RefreshAfter = 0,
            ExpiresAfterEvaluator = evaluator ? () => fail ? throw expected : 10 : null
        }, () =>
        {
            context.SetVariable("result", "replacement");
            if (fail && !evaluator)
            {
                throw expected;
            }
        }), clock, cache);
        test.Context.Variables["result"] = "original";

        var error = await CacheRefreshTestCache.OnOwnerThread(() =>
            Assert.ThrowsExactly<PolicyException>(test.RunInbound)).WaitAsync(TimeSpan.FromSeconds(5));

        error.Policy.Should().Be(nameof(IInboundContext.CacheValue));
        error.InnerException.Should().BeSameAs(expected);
        test.Context.Variables["result"].Should().Be("original");
        (await shared.GetAsync("key")).Should().Be("cached");
        fail = false;
        await CacheRefreshTestCache.OnOwnerThread(test.RunInbound).WaitAsync(TimeSpan.FromSeconds(5));
        test.Context.Variables["result"].Should().Be("replacement");
        (await shared.GetAsync("key")).Should().Be("replacement");
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task CacheValue_RealConcurrentCallerIsRejectedWhileTheRequestWaits(bool retainedProxy)
    {
        var clock = new CacheTestClock();
        var shared = new CacheStore(clock);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var cache = new CacheRefreshTestCache(shared)
        {
            DynamicGet = async (key, factory, force, ct) =>
            {
                entered.TrySetResult();
                await release.Task.WaitAsync(ct);
                return await shared.GetOrCreateWithDynamicTtlAsync(key, factory, force, ct);
            }
        };
        Action? retained = null;
        var test = CacheTest.Value(context =>
        {
            retained = () => context.SetVariable("escaped", true);
            context.Value(new CacheValueConfig { Key = "key", VariableName = "result" },
                () => context.SetVariable("result", "computed"));
        }, clock, cache);
        var request = CacheRefreshTestCache.OnOwnerThread(() => test.RunRequest(inner => inner.RunInbound()));
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            var denied = await Task.Run(() => Assert.ThrowsExactly<InvalidOperationException>(() =>
            {
                if (retainedProxy) retained!();
                else test.RunInbound();
            })).WaitAsync(TimeSpan.FromSeconds(5));
            denied.Message.Should().Contain("Concurrent");
            test.Context.Variables.Should().NotContainKey("escaped").And.NotContainKey("result");
        }
        finally
        {
            release.TrySetResult();
            await request.WaitAsync(TimeSpan.FromSeconds(5));
        }
        test.Context.Variables["result"].Should().Be("computed");
        (await shared.GetAsync("key")).Should().Be("computed");
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task CacheValue_SpawnedTasksDoNotInheritPermissionToExecutePolicies(bool startNew)
    {
        var clock = new CacheTestClock();
        var shared = new CacheStore(clock);
        var cache = CacheRefreshTestCache.Asynchronous(shared);
        var test = CacheTest.Value(context => context.Value(new CacheValueConfig
        {
            Key = "key", VariableName = "result"
        }, () =>
        {
            InvalidOperationException Attempt() => Assert.ThrowsExactly<InvalidOperationException>(
                () => context.SetVariable("escaped", true));
            var unrelated = startNew
                ? Task.Factory.StartNew(Attempt, CancellationToken.None, TaskCreationOptions.None, TaskScheduler.Default)
                : Task.Run(Attempt);
            unrelated.WaitAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult().Message.Should().Contain("Concurrent");
            context.SetVariable("result", "computed");
        }), clock, cache);

        await CacheRefreshTestCache.OnOwnerThread(test.RunAll).WaitAsync(TimeSpan.FromSeconds(5));

        test.Context.Variables.Should().NotContainKey("escaped");
        test.Context.Variables["result"].Should().Be("computed");
    }

    [TestMethod]
    public async Task CacheValue_AsyncNestedFactoriesPreserveOwnerAndServiceRecursionContext()
    {
        var clock = new CacheTestClock();
        var shared = new CacheStore(clock);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var cache = CacheRefreshTestCache.Asynchronous(shared, cancellation.Token);
        var recurse = true;
        var test = CacheTest.Value(context => context.Value(new CacheValueConfig
        {
            Key = "outer", VariableName = "outer", RefreshAfter = 0
        }, () =>
        {
            context.Value(new CacheValueConfig
            {
                Key = recurse ? "outer" : "inner", VariableName = "inner", RefreshAfter = 0
            }, () => context.SetVariable("inner", "inner-value"));
            context.SetVariable("outer", "outer-value");
        }), clock, cache);

        var error = await CacheRefreshTestCache.OnOwnerThread(() =>
            Assert.ThrowsExactly<PolicyException>(test.RunInbound)).WaitAsync(TimeSpan.FromSeconds(5));

        error.GetBaseException().Message.Should().Contain("recursively refresh its own key");
        shared.InternalCache.Should().BeEmpty();
        test.Context.Variables.Should().NotContainKey("outer").And.NotContainKey("inner");
        recurse = false;
        await CacheRefreshTestCache.OnOwnerThread(test.RunInbound).WaitAsync(TimeSpan.FromSeconds(5));
        test.Context.Variables["outer"].Should().Be("outer-value");
        test.Context.Variables["inner"].Should().Be("inner-value");
        (await shared.GetAsync("outer")).Should().Be("outer-value");
        (await shared.GetAsync("inner")).Should().Be("inner-value");
    }

    [TestMethod]
    public async Task CacheValue_AsyncFactoryCannotAdvanceRequestIdBeforeItsChildPolicies()
    {
        var clock = new CacheTestClock();
        var shared = new CacheStore(clock);
        var cache = CacheRefreshTestCache.Asynchronous(shared);
        TestDocument? test = null;
        var changeId = true;
        test = CacheTest.Value(context => context.Value(new CacheValueConfig
        {
            Key = "key", VariableName = "result"
        }, () =>
        {
            if (changeId) test!.Context.RequestId = Guid.NewGuid();
            context.SetVariable("result", "computed");
        }), clock, cache);
        var requestId = test.Context.RequestId;
        test.Context.Variables["result"] = "original";

        var error = await CacheRefreshTestCache.OnOwnerThread(() =>
            Assert.ThrowsExactly<PolicyException>(() => test.RunRequest(inner => inner.RunInbound())))
            .WaitAsync(TimeSpan.FromSeconds(5));

        error.GetBaseException().Message.Should().Contain("RequestId must remain unchanged");
        test.Context.Variables["result"].Should().Be("original");
        shared.InternalCache.Should().BeEmpty();
        test.Context.RequestId = requestId;
        changeId = false;
        await CacheRefreshTestCache.OnOwnerThread(() => test.RunRequest(inner => inner.RunInbound()))
            .WaitAsync(TimeSpan.FromSeconds(5));
        test.Context.Variables["result"].Should().Be("computed");
    }
}

internal sealed class CacheRefreshTestCache(ICache inner) : ICache
{
    public TaskCompletionSource Pending { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public Func<string, Func<object?, CancellationToken, Task<CacheValueFactoryResult>>, bool,
        CancellationToken, Task<CacheValueResult>>? DynamicGet { get; init; }

    public static CacheRefreshTestCache Asynchronous(ICache inner, CancellationToken cancellationToken = default) =>
        new(inner)
        {
            DynamicGet = async (key, factory, force, ct) =>
            {
                await Task.Yield();
                return await inner.GetOrCreateWithDynamicTtlAsync(
                    key, factory, force, cancellationToken.CanBeCanceled ? cancellationToken : ct).ConfigureAwait(false);
            }
        };

    public static Task OnOwnerThread(Action action) =>
        Task.Factory.StartNew(action, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);

    public static Task<T> OnOwnerThread<T>(Func<T> action) =>
        Task.Factory.StartNew(action, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);

    public Task<object?> GetAsync(string key, CancellationToken ct = default) => inner.GetAsync(key, ct);

    public Task SetAsync(string key, object value, TimeSpan ttl, CancellationToken ct = default) =>
        inner.SetAsync(key, value, ttl, ct);

    public Task RemoveAsync(string key, CancellationToken ct = default) => inner.RemoveAsync(key, ct);

    public Task<CacheValueResult> GetOrCreateAsync(
        string key, TimeSpan expiresAfter, TimeSpan? refreshAfter,
        Func<object?, CancellationToken, Task<object?>> valueFactory, CancellationToken ct = default) =>
        inner.GetOrCreateAsync(key, expiresAfter, refreshAfter, valueFactory, ct);

    public Task<CacheValueResult> GetOrCreateWithDynamicTtlAsync(
        string key, Func<object?, CancellationToken, Task<CacheValueFactoryResult>> valueFactory,
        bool forceRefresh = false, CancellationToken ct = default)
    {
        var result = DynamicGet is { } get
            ? get(key, valueFactory, forceRefresh, ct)
            : inner.GetOrCreateWithDynamicTtlAsync(key, valueFactory, forceRefresh, ct);
        if (!result.IsCompleted) Pending.TrySetResult();
        return result;
    }
}