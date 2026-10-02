// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring.Expressions;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Document;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Data;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Services;

namespace Test.Emulator.Emulator.Policies;

[TestClass]
public class CacheFamilyTests
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
    public void CacheFamily_StoreLookupAndRemoveShareExpressionsAndObjectValues(string section, bool injected)
    {
        var clock = new CacheTestClock();
        var cache = injected ? CacheTest.SharedCache() : null;
        var value = new Dictionary<string, int> { ["answer"] = 42 };
        var test = CacheTest.Value(context =>
        {
            var key = context.ExpressionContext.Request.Url.Path;
            var duration = (int)context.ExpressionContext.Variables["duration"];
            context.Store(new CacheStoreValueConfig { Key = key, Value = value, Duration = duration });
            context.Lookup(new CacheLookupValueConfig { Key = key, VariableName = "hit" });
            context.Remove(new CacheRemoveValueConfig { Key = key });
            context.Lookup(new CacheLookupValueConfig
            {
                Key = key,
                VariableName = "miss",
                DefaultValue = context.ExpressionContext.Variables["default"]
            });
        }, clock, cache);
        test.Context.Variables["duration"] = 10;
        test.Context.Variables["default"] = "fallback";

        ExecutionTest.RunSection(test, section);

        test.Context.Variables["hit"].Should().BeSameAs(value);
        test.Context.Variables["miss"].Should().Be("fallback");
        test.SetupCacheStore().InternalCache.Should().BeEmpty();
        cache?.GetAsync(test.Context.Request.Url.Path).GetAwaiter().GetResult().Should().BeNull();
    }

    [TestMethod]
    [DataRow(false, -1, false)]
    [DataRow(false, 0, true)]
    [DataRow(false, 1, true)]
    [DataRow(true, -1, false)]
    [DataRow(true, 0, true)]
    [DataRow(true, 1, true)]
    public void CacheFamily_LookupHonorsTheExactExpirationBoundary(bool injected, int ticks, bool expired)
    {
        var clock = new CacheTestClock();
        var cache = injected ? CacheTest.SharedCache() : null;
        var store = CacheTest.Value(context => context.Store(new CacheStoreValueConfig
        {
            Key = "key",
            Value = "cached",
            Duration = 10
        }), clock, cache);
        store.RunInbound();
        if (!injected)
        {
            store.SetupCacheStore().InternalCache["key"].StoredAt.Should().Be(clock.GetUtcNow().UtcDateTime);
        }
        clock.Advance(TimeSpan.FromSeconds(10) + TimeSpan.FromTicks(ticks));
        var lookup = CacheTest.Value(context => context.Lookup(new CacheLookupValueConfig
        {
            Key = "key",
            VariableName = "result",
            DefaultValue = "expired"
        }), clock, cache, injected ? null : store.Context);

        lookup.RunInbound();

        lookup.Context.Variables["result"].Should().Be(expired ? "expired" : "cached");
        if (!injected)
        {
            store.SetupCacheStore().InternalCache.ContainsKey("key").Should().Be(!expired);
        }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void CacheFamily_ZeroDurationIsImmediatelyExpired(bool injected)
    {
        var clock = new CacheTestClock();
        var test = CacheTest.Value(context =>
        {
            context.Store(new CacheStoreValueConfig { Key = "key", Value = "value", Duration = 0 });
            context.Lookup(new CacheLookupValueConfig { Key = "key", VariableName = "result", DefaultValue = "miss" });
        }, clock, injected ? CacheTest.SharedCache() : null);

        test.RunInbound();

        test.Context.Variables["result"].Should().Be("miss");
    }

    [TestMethod]
    [DataRow(false, false)]
    [DataRow(false, true)]
    [DataRow(true, false)]
    [DataRow(true, true)]
    public void CacheFamily_MissDoesNotLeaveAnUnrelatedPreviousVariable(bool injected, bool withDefault)
    {
        var clock = new CacheTestClock();
        var test = CacheTest.Value(context => context.Lookup(new CacheLookupValueConfig
        {
            Key = "missing",
            VariableName = "result",
            DefaultValue = withDefault ? 42 : null
        }), clock, injected ? CacheTest.SharedCache() : null);
        test.Context.Variables["result"] = "previous";

        test.RunInbound();

        if (withDefault)
        {
            test.Context.Variables["result"].Should().Be(42);
        }
        else
        {
            test.Context.Variables.Should().NotContainKey("result");
        }
    }

    [TestMethod]
    [DataRow(null, false, false)]
    [DataRow(null, true, true)]
    [DataRow("prefer-external", false, false)]
    [DataRow("prefer-external", true, true)]
    [DataRow("internal", true, false)]
    [DataRow("external", true, true)]
    public void CacheFamily_FallbackCacheTypesAreIsolated(string? cachingType, bool externalSetup, bool external)
    {
        var clock = new CacheTestClock();
        var test = CacheTest.Value(context =>
        {
            context.Store(new CacheStoreValueConfig
            {
                Key = "key",
                Value = "selected",
                Duration = 10,
                CachingType = cachingType
            });
            context.Lookup(new CacheLookupValueConfig
            {
                Key = "key",
                VariableName = "result",
                CachingType = cachingType
            });
        }, clock);
        test.SetupCacheStore().WithExternalCacheSetup(externalSetup);

        test.RunInbound();

        test.Context.Variables["result"].Should().Be("selected");
        var selected = external ? test.SetupCacheStore().ExternalCache : test.SetupCacheStore().InternalCache;
        var other = external ? test.SetupCacheStore().InternalCache : test.SetupCacheStore().ExternalCache;
        selected["key"].Value.Should().Be("selected");
        other.Should().BeEmpty();
    }

    [TestMethod]
    public void CacheFamily_KeyedServicesHonorInternalExternalAndPreferExternal()
    {
        var clock = new CacheTestClock();
        var internalCache = CacheTest.SharedCache();
        var externalCache = new CacheStore(clock).WithExternalCacheSetup();
        var test = CacheTest.Value(context =>
        {
            context.Store(new CacheStoreValueConfig
            {
                Key = "key",
                Value = "internal",
                Duration = 10,
                CachingType = "internal"
            });
            context.Store(new CacheStoreValueConfig
            {
                Key = "key",
                Value = "external",
                Duration = 10,
                CachingType = "external"
            });
            context.Lookup(new CacheLookupValueConfig
            {
                Key = "key",
                VariableName = "internal",
                CachingType = "internal"
            });
            context.Lookup(new CacheLookupValueConfig
            {
                Key = "key",
                VariableName = "external",
                CachingType = "external"
            });
            context.Lookup(new CacheLookupValueConfig { Key = "key", VariableName = "preferred" });
            context.Remove(new CacheRemoveValueConfig { Key = "key", CachingType = "internal" });
        }, clock);
        test.Context.Services.Register<ICache>("internal", internalCache).Register<ICache>("external", externalCache);

        test.RunInbound();

        test.Context.Variables["internal"].Should().Be("internal");
        test.Context.Variables["external"].Should().Be("external");
        test.Context.Variables["preferred"].Should().Be("external");
        internalCache.GetAsync("key").GetAwaiter().GetResult().Should().BeNull();
        externalCache.GetAsync("key").GetAwaiter().GetResult().Should().Be("external");
        test.SetupCacheStore().InternalCache.Should().BeEmpty();
    }

    [TestMethod]
    public void CacheFamily_SharedInjectedCacheWorksAcrossIndependentRequestsAndRemoval()
    {
        var clock = new CacheTestClock();
        var cache = CacheTest.SharedCache();
        var store = CacheTest.Value(context => context.Store(new CacheStoreValueConfig
        {
            Key = "shared",
            Value = 42,
            Duration = 10
        }), clock, cache);
        var lookup = CacheTest.Value(context => context.Lookup(new CacheLookupValueConfig
        {
            Key = "shared",
            VariableName = "result",
            DefaultValue = "miss"
        }), clock, cache);
        var remove = CacheTest.Value(context => context.Remove(new CacheRemoveValueConfig { Key = "shared" }),
            clock, cache);

        store.RunInbound();
        lookup.RunBackend();
        lookup.Context.Variables["result"].Should().Be(42);
        remove.RunOutbound();
        lookup.RunOnError();

        lookup.Context.Should().NotBeSameAs(store.Context);
        lookup.Context.Variables["result"].Should().Be("miss");
    }

    [TestMethod]
    [DataRow("inbound")]
    [DataRow("backend")]
    [DataRow("outbound")]
    [DataRow("on-error")]
    public void CacheFamily_CallbacksOverrideAllServiceOperationsInEverySection(string section)
    {
        var calls = new List<string>();
        var blockCalls = 0;
        var test = CacheTest.Value(context =>
        {
            context.Store(new CacheStoreValueConfig { Key = "key", Value = "value", Duration = 10 });
            context.Lookup(new CacheLookupValueConfig { Key = "key", VariableName = "lookup" });
            context.Remove(new CacheRemoveValueConfig { Key = "key" });
            context.Value(new CacheValueConfig { Key = "key", VariableName = "value" }, () => blockCalls++);
        }, new CacheTestClock(), new FailingCache());
        switch (section)
        {
            case "inbound": SetupCallbacks(test.SetupInbound(), calls); break;
            case "backend": SetupCallbacks(test.SetupBackend(), calls); break;
            case "outbound": SetupCallbacks(test.SetupOutbound(), calls); break;
            case "on-error": SetupCallbacks(test.SetupOnError(), calls); break;
        }

        ExecutionTest.RunSection(test, section);

        calls.Should().Equal("store", "lookup", "remove", "value");
        test.Context.Variables["lookup"].Should().Be("lookup-override");
        test.Context.Variables["value"].Should().Be("value-override");
        blockCalls.Should().Be(0);
    }

    [TestMethod]
    public void CacheFamily_WithValueOverrideTakesPrecedenceOverInjectedService()
    {
        var test = CacheTest.Value(context => context.Lookup(new CacheLookupValueConfig
        {
            Key = "key",
            VariableName = "result"
        }), new CacheTestClock(), new FailingCache());
        test.SetupInbound().CacheLookupValue((_, config) => config.Key == "other").WithValue("wrong");
        test.SetupInbound().CacheLookupValue((_, config) => config.Key == "key").WithValue(42);

        test.RunInbound();

        test.Context.Variables["result"].Should().Be(42);
    }

    [TestMethod]
    [DataRow("lookup", false)]
    [DataRow("store", false)]
    [DataRow("remove", false)]
    [DataRow("value", false)]
    [DataRow("lookup", true)]
    [DataRow("store", true)]
    [DataRow("remove", true)]
    [DataRow("value", true)]
    public void CacheFamily_InvalidCachingTypeIsNotHiddenByAnInjectedService(string policy, bool injected)
    {
        var test = CacheTest.Value(context => Invoke(context, policy, "invalid"), new CacheTestClock(),
            injected ? new FailingCache() : null);

        var error = Assert.ThrowsExactly<PolicyException>(test.RunInbound);

        error.InnerException.Should().BeOfType<ArgumentException>();
        test.SetupCacheStore().InternalCache.Should().BeEmpty();
    }

    [TestMethod]
    [DataRow("store")]
    [DataRow("remove")]
    [DataRow("value")]
    public void CacheFamily_MissingRequiredExternalCacheIsAnExplicitError(string policy)
    {
        var test = CacheTest.Value(context => Invoke(context, policy, "external"), new CacheTestClock());

        var error = Assert.ThrowsExactly<PolicyException>(test.RunInbound);

        error.InnerException.Should().BeOfType<InvalidOperationException>()
            .Which.Message.Should().Contain("external");
        test.Context.Variables.Should().NotContainKey("result");
        test.SetupCacheStore().InternalCache.Should().BeEmpty();
    }

    [TestMethod]
    public void CacheFamily_UnconfiguredExternalLookupStillAppliesItsDefault()
    {
        var test = CacheTest.Value(context => context.Lookup(new CacheLookupValueConfig
        {
            Key = "key",
            VariableName = "result",
            CachingType = "external",
            DefaultValue = "miss"
        }), new CacheTestClock());

        test.RunInbound();

        test.Context.Variables["result"].Should().Be("miss");
    }

    [TestMethod]
    [DataRow("lookup")]
    [DataRow("store")]
    [DataRow("remove")]
    [DataRow("value")]
    public void CacheFamily_ServiceFailuresAreSurfacedWithPolicyAndSection(string policy)
    {
        var test = CacheTest.Value(context => Invoke(context, policy, null), new CacheTestClock(), new FailingCache());

        var error = Assert.ThrowsExactly<PolicyException>(test.RunBackend);

        error.Policy.Should().Be(policy switch
        {
            "lookup" => nameof(IBackendContext.CacheLookupValue),
            "store" => nameof(IBackendContext.CacheStoreValue),
            "remove" => nameof(IBackendContext.CacheRemoveValue),
            _ => nameof(IBackendContext.CacheValue)
        });
        error.Section.Should().Be(nameof(IBackendContext));
        error.InnerException.Should().BeOfType<InvalidOperationException>()
            .Which.Message.Should().Be("cache service failure");
    }

    [TestMethod]
    [DataRow("lookup")]
    [DataRow("store")]
    [DataRow("remove")]
    [DataRow("value")]
    public void CacheFamily_EmptyRequiredKeysAreRejected(string policy)
    {
        var test = CacheTest.Value(context => Invoke(context, policy, null, ""), new CacheTestClock());

        var error = Assert.ThrowsExactly<PolicyException>(test.RunInbound);

        error.InnerException.Should().BeAssignableTo<ArgumentException>();
        test.SetupCacheStore().InternalCache.Should().BeEmpty();
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void CacheFamily_NegativeDurationIsRejectedWithoutWriting(bool injected)
    {
        var test = CacheTest.Value(context => context.Store(new CacheStoreValueConfig
        {
            Key = "key",
            Value = "value",
            Duration = -1
        }), new CacheTestClock(), injected ? CacheTest.SharedCache() : null);

        var error = Assert.ThrowsExactly<PolicyException>(test.RunInbound);

        error.InnerException.Should().BeOfType<ArgumentOutOfRangeException>();
        test.SetupCacheStore().InternalCache.Should().BeEmpty();
    }

    [TestMethod]
    public void CacheFamily_RequiredVariableAndValueAreNotSilentlyIgnored()
    {
        var clock = new CacheTestClock();
        var lookup = CacheTest.Value(context => context.Lookup(new CacheLookupValueConfig
        {
            Key = "key",
            VariableName = ""
        }), clock);
        var store = CacheTest.Value(context => context.Store(new CacheStoreValueConfig
        {
            Key = "key",
            Value = null!,
            Duration = 10
        }), clock);

        Assert.ThrowsExactly<PolicyException>(lookup.RunInbound).InnerException.Should()
            .BeAssignableTo<ArgumentException>();
        Assert.ThrowsExactly<PolicyException>(store.RunInbound).InnerException.Should()
            .BeOfType<ArgumentNullException>();
        store.SetupCacheStore().InternalCache.Should().BeEmpty();
    }

    private static void Invoke(CacheTestSection context, string policy, string? cachingType, string key = "key")
    {
        switch (policy)
        {
            case "lookup":
                context.Lookup(new CacheLookupValueConfig
                {
                    Key = key,
                    VariableName = "result",
                    CachingType = cachingType
                });
                break;
            case "store":
                context.Store(new CacheStoreValueConfig
                {
                    Key = key,
                    Value = "value",
                    Duration = 10,
                    CachingType = cachingType
                });
                break;
            case "remove":
                context.Remove(new CacheRemoveValueConfig { Key = key, CachingType = cachingType });
                break;
            case "value":
                context.Value(new CacheValueConfig
                {
                    Key = key,
                    VariableName = "result",
                    CachingType = cachingType
                }, () => context.SetVariable("result", "computed"));
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(policy));
        }
    }

    private static void SetupCallbacks<T>(MockPoliciesProvider<T> mock, List<string> calls) where T : class
    {
        mock.CacheStoreValue().WithCallback((_, _) => calls.Add("store"));
        mock.CacheLookupValue().WithCallback((context, config) =>
        {
            calls.Add("lookup");
            context.Variables[config.VariableName] = "lookup-override";
        });
        mock.CacheRemoveValue().WithCallback((_, _) => calls.Add("remove"));
        mock.CacheValue().WithCallback((context, config) =>
        {
            calls.Add("value");
            context.Variables[config.VariableName] = "value-override";
        });
    }
}

internal sealed class CacheTestClock : TimeProvider
{
    private DateTimeOffset _utcNow = new(2030, 1, 1, 0, 0, 0, TimeSpan.Zero);

    public override DateTimeOffset GetUtcNow() => _utcNow;

    public void Advance(TimeSpan time) => _utcNow += time;
}

internal static class CacheTest
{
    public static CacheLookupConfig LookupConfig => new()
    {
        VaryByDeveloper = false,
        VaryByDeveloperGroups = false
    };

    public static ICache SharedCache() => (ICache)new CacheStore();

    public static TestDocument Value(
        Action<CacheTestSection> action, CacheTestClock clock, ICache? cache = null, GatewayContext? context = null)
    {
        var test = new TestDocument(new CacheTestDocument(action)) { Context = context ?? new GatewayContext() };
        Configure(test, clock, cache);
        return test;
    }

    public static TestDocument Response(
        CacheLookupConfig config, CacheTestClock clock, ICache? cache = null, int duration = 10, bool? cacheResponse = null)
    {
        var test = new ExecutionTestDocument
        {
            InboundAction = context =>
            {
                context.CacheLookup(config);
                context.SetVariable("after-lookup", true);
            },
            OutboundAction = context => context.CacheStore(duration, cacheResponse)
        }.AsTestDocument();
        Configure(test, clock, cache);
        return test;
    }

    public static void Configure(TestDocument test, CacheTestClock clock, ICache? cache = null)
    {
        test.Context.Services.Register<TimeProvider>(clock);
        if (cache is not null)
        {
            test.Context.Services.Register<ICache>(cache);
        }
    }
}

internal sealed record CacheTestSection(
    IExpressionContext ExpressionContext,
    Action<CacheLookupValueConfig> Lookup,
    Action<CacheStoreValueConfig> Store,
    Action<CacheRemoveValueConfig> Remove,
    Action<CacheValueConfig, Action> Value,
    Action<string, object> SetVariable);

internal sealed class CacheTestDocument(Action<CacheTestSection> action) : IDocument
{
    public void Inbound(IInboundContext context) => action(new CacheTestSection(
        context.ExpressionContext, context.CacheLookupValue, context.CacheStoreValue,
        context.CacheRemoveValue, context.CacheValue, context.SetVariable));

    public void Backend(IBackendContext context) => action(new CacheTestSection(
        context.ExpressionContext, context.CacheLookupValue, context.CacheStoreValue,
        context.CacheRemoveValue, context.CacheValue, context.SetVariable));

    public void Outbound(IOutboundContext context) => action(new CacheTestSection(
        context.ExpressionContext, context.CacheLookupValue, context.CacheStoreValue,
        context.CacheRemoveValue, context.CacheValue, context.SetVariable));

    public void OnError(IOnErrorContext context) => action(new CacheTestSection(
        context.ExpressionContext, context.CacheLookupValue, context.CacheStoreValue,
        context.CacheRemoveValue, context.CacheValue, context.SetVariable));
}

internal sealed class FailingCache : ICache
{
    public Task<object?> GetAsync(string key, CancellationToken ct = default) =>
        throw new InvalidOperationException("cache service failure");

    public Task SetAsync(string key, object value, TimeSpan ttl, CancellationToken ct = default) =>
        throw new InvalidOperationException("cache service failure");

    public Task RemoveAsync(string key, CancellationToken ct = default) =>
        throw new InvalidOperationException("cache service failure");

    public Task<CacheValueResult> GetOrCreateAsync(
        string key, TimeSpan expiresAfter, TimeSpan? refreshAfter,
        Func<object?, CancellationToken, Task<object?>> valueFactory, CancellationToken ct = default) =>
        throw new InvalidOperationException("cache service failure");

    public Task<CacheValueResult> GetOrCreateWithDynamicTtlAsync(
        string key, Func<object?, CancellationToken, Task<CacheValueFactoryResult>> valueFactory,
        bool forceRefresh = false, CancellationToken ct = default) =>
        throw new InvalidOperationException("cache service failure");
}