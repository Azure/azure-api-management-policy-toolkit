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
public class CachePartitionTests
{
    [TestMethod]
    [DataRow("inbound", "internal", "internal")]
    [DataRow("backend", "internal", "internal")]
    [DataRow("outbound", "internal", "internal")]
    [DataRow("on-error", "internal", "internal")]
    [DataRow("inbound", "external", "external")]
    [DataRow("inbound", "prefer-external", "external")]
    [DataRow("inbound", null, "external")]
    public void CachePartition_InjectedStoreLookupSelectsTheRequestedPartition(
        string section, string? cachingType, string expected)
    {
        var clock = new CacheTestClock();
        var shared = CreatePartitions(clock);
        var test = CacheTest.Value(context => context.Lookup(new CacheLookupValueConfig
        {
            Key = "key",
            VariableName = "result",
            CachingType = cachingType
        }), clock, shared);

        ExecutionTest.RunSection(test, section);

        test.Context.Variables["result"].Should().Be(expected);
        shared.InternalCache["key"].Value.Should().Be("internal");
        shared.ExternalCache["key"].Value.Should().Be("external");
        test.SetupCacheStore().InternalCache.Should().BeEmpty();
        test.SetupCacheStore().ExternalCache.Should().BeEmpty();
    }

    [TestMethod]
    [DataRow("internal", false)]
    [DataRow("external", true)]
    [DataRow("prefer-external", true)]
    [DataRow(null, true)]
    public void CachePartition_InjectedStoreWriteChangesOnlyTheRequestedPartition(string? cachingType, bool external)
    {
        var clock = new CacheTestClock();
        var shared = CreatePartitions(clock);
        var test = CacheTest.Value(context => context.Store(new CacheStoreValueConfig
        {
            Key = "key",
            Value = "updated",
            Duration = 17,
            CachingType = cachingType
        }), clock, shared);

        test.RunOutbound();

        var selected = external ? shared.ExternalCache : shared.InternalCache;
        var other = external ? shared.InternalCache : shared.ExternalCache;
        selected["key"].Value.Should().Be("updated");
        selected["key"].Ttl.Should().Be(TimeSpan.FromSeconds(17));
        other["key"].Value.Should().Be(external ? "internal" : "external");
        other["key"].Ttl.Should().Be(TimeSpan.FromSeconds(60));
    }

    [TestMethod]
    [DataRow("internal", false)]
    [DataRow("external", true)]
    [DataRow("prefer-external", true)]
    [DataRow(null, true)]
    public void CachePartition_InjectedStoreRemovalChangesOnlyTheRequestedPartition(string? cachingType, bool external)
    {
        var clock = new CacheTestClock();
        var shared = CreatePartitions(clock);
        var test = CacheTest.Value(context => context.Remove(new CacheRemoveValueConfig
        {
            Key = "key",
            CachingType = cachingType
        }), clock, shared);

        test.RunOnError();

        var selected = external ? shared.ExternalCache : shared.InternalCache;
        var other = external ? shared.InternalCache : shared.ExternalCache;
        selected.Should().NotContainKey("key");
        other["key"].Value.Should().Be(external ? "internal" : "external");
    }

    [TestMethod]
    [DataRow("internal", "internal")]
    [DataRow("external", "external")]
    [DataRow("prefer-external", "external")]
    [DataRow(null, "external")]
    public void CachePartition_InjectedStoreFreshCacheValueUsesTheRequestedPartition(
        string? cachingType, string expected)
    {
        var clock = new CacheTestClock();
        var shared = CreatePartitions(clock);
        var calls = 0;
        var test = CacheTest.Value(context => context.Value(new CacheValueConfig
        {
            Key = "key",
            VariableName = "result",
            CachingType = cachingType
        }, () => calls++), clock, shared);

        test.RunInbound();

        test.Context.Variables["result"].Should().Be(expected);
        calls.Should().Be(0);
        shared.InternalCache["key"].Value.Should().Be("internal");
        shared.ExternalCache["key"].Value.Should().Be("external");
    }

    [TestMethod]
    [DataRow("internal", false)]
    [DataRow("external", true)]
    [DataRow("prefer-external", true)]
    [DataRow(null, true)]
    public void CachePartition_InjectedStoreForceRefreshUpdatesOnlyTheRequestedPartition(
        string? cachingType, bool external)
    {
        var clock = new CacheTestClock();
        var shared = CreatePartitions(clock);
        var calls = 0;
        var test = CacheTest.Value(context => context.Value(new CacheValueConfig
        {
            Key = "key",
            VariableName = "result",
            CachingType = cachingType,
            ExpiresAfter = 17,
            RefreshAfter = 0
        }, () =>
        {
            calls++;
            context.SetVariable("result", "refreshed");
        }), clock, shared);

        test.RunBackend();

        calls.Should().Be(1);
        test.Context.Variables["result"].Should().Be("refreshed");
        var selected = external ? shared.ExternalCache : shared.InternalCache;
        var other = external ? shared.InternalCache : shared.ExternalCache;
        selected["key"].Value.Should().Be("refreshed");
        selected["key"].Ttl.Should().Be(TimeSpan.FromSeconds(17));
        selected["key"].RefreshAfter.Should().Be(TimeSpan.Zero);
        other["key"].Value.Should().Be(external ? "internal" : "external");
        other["key"].Ttl.Should().Be(TimeSpan.FromSeconds(60));
    }

    [TestMethod]
    public void CachePartition_InternalMissAndScheduledRefreshDoNotReuseTheFreshExternalEntry()
    {
        var clock = new CacheTestClock();
        var shared = new CacheStore(clock).WithExternalCacheSetup()
            .WithInternalCacheValue("key", "internal", 4)
            .WithExternalCacheValue("key", "external", 60);
        var calls = 0;
        var test = CacheTest.Value(context => context.Value(new CacheValueConfig
        {
            Key = "key",
            VariableName = "result",
            CachingType = "internal",
            ExpiresAfter = 10,
            RefreshAfter = 4
        }, () => context.SetVariable("result", $"computed-{++calls}")), clock, shared);
        clock.Advance(TimeSpan.FromSeconds(4));
        test.RunInbound();
        test.Context.Variables["result"].Should().Be("computed-1");
        clock.Advance(TimeSpan.FromSeconds(4));

        test.RunInbound();

        calls.Should().Be(2);
        test.Context.Variables["result"].Should().Be("computed-2");
        shared.InternalCache["key"].Value.Should().Be("computed-2");
        shared.ExternalCache["key"].Value.Should().Be("external");
        shared.ExternalCache["key"].Ttl.Should().Be(TimeSpan.FromSeconds(60));
    }

    [TestMethod]
    public void CachePartition_InjectedStoreMissingExternalPartitionIsALookupMiss()
    {
        var clock = new CacheTestClock();
        var shared = CreatePartitions(clock, externalSetup: false);
        var test = CacheTest.Value(context => context.Lookup(new CacheLookupValueConfig
        {
            Key = "key",
            VariableName = "result",
            CachingType = "external",
            DefaultValue = "miss"
        }), clock, shared);

        test.RunInbound();

        test.Context.Variables["result"].Should().Be("miss");
        shared.InternalCache["key"].Value.Should().Be("internal");
        shared.ExternalCache["key"].Value.Should().Be("external");
    }

    [TestMethod]
    [DataRow("store")]
    [DataRow("remove")]
    [DataRow("refresh")]
    public void CachePartition_InjectedStoreMissingRequiredExternalPartitionIsAnError(string policy)
    {
        var clock = new CacheTestClock();
        var shared = CreatePartitions(clock, externalSetup: false);
        var calls = 0;
        var test = CacheTest.Value(context =>
        {
            switch (policy)
            {
                case "store":
                    context.Store(new CacheStoreValueConfig
                    {
                        Key = "key",
                        Value = "updated",
                        Duration = 10,
                        CachingType = "external"
                    });
                    break;
                case "remove":
                    context.Remove(new CacheRemoveValueConfig { Key = "key", CachingType = "external" });
                    break;
                case "refresh":
                    context.Value(new CacheValueConfig
                    {
                        Key = "key",
                        VariableName = "result",
                        CachingType = "external",
                        RefreshAfter = 0
                    }, () => calls++);
                    break;
            }
        }, clock, shared);

        var error = Assert.ThrowsExactly<PolicyException>(test.RunInbound);

        error.InnerException.Should().BeOfType<InvalidOperationException>()
            .Which.Message.Should().Contain("external");
        calls.Should().Be(0);
        shared.InternalCache["key"].Value.Should().Be("internal");
        shared.ExternalCache["key"].Value.Should().Be("external");
    }

    [TestMethod]
    public void CachePartition_ResponseLookupAndStoreUseTheInjectedInternalPartition()
    {
        var clock = new CacheTestClock();
        var shared = new CacheStore(clock).WithExternalCacheSetup();
        var config = CacheTest.LookupConfig with { CachingType = "internal" };
        var store = CacheTest.Response(config, clock, shared);
        store.RunInbound();
        var key = (string)store.Context.Variables["__cache_lookup_key"];
        shared.WithInternalCacheValue(key, "internal", 60).WithExternalCacheValue(key, "external", 60);
        store.Context.Response.Body.Content = "stored response";

        store.RunOutbound();

        shared.InternalCache[key].Value.Should().BeOfType<CachedResponse>()
            .Which.Body.Should().Be("stored response");
        shared.ExternalCache[key].Value.Should().Be("external");
        var lookup = CacheTest.Response(config, clock, shared);
        lookup.RunInbound();
        lookup.Context.Response.Body.Content.Should().Be("stored response");
        lookup.Context.ResponseTerminated.Should().BeTrue();
    }

    [TestMethod]
    public void CachePartition_ResponseSnapshotsKeepTheInjectedStoreClockWithoutARegistryClock()
    {
        var clock = new CacheTestClock();
        var shared = new CacheStore(clock).WithExternalCacheSetup();
        var test = new ExecutionTestDocument
        {
            InboundAction = context => context.CacheLookup(CacheTest.LookupConfig with { CachingType = "internal" }),
            OutboundAction = context => context.CacheStore(10, null)
        }.AsTestDocument();
        test.Context.Services.Register<ICache>(shared);

        test.RunInbound();
        test.RunOutbound();

        var snapshot = shared.InternalCache.Single().Value.Value.Should().BeOfType<CachedResponse>().Which;
        snapshot.ExpiresAt.Should().Be(clock.GetUtcNow() + TimeSpan.FromSeconds(10));
        clock.Advance(TimeSpan.FromSeconds(9));
        test.RunInbound();
        test.Context.Variables["__cache_hit"].Should().Be(true);
    }

    [TestMethod]
    public void CachePartition_OtherInjectedICacheImplementationsRemainOpaqueServices()
    {
        var clock = new CacheTestClock();
        var shared = CreatePartitions(clock);
        var test = CacheTest.Value(context =>
        {
            context.Lookup(new CacheLookupValueConfig
            {
                Key = "key",
                VariableName = "before",
                CachingType = "internal"
            });
            context.Store(new CacheStoreValueConfig
            {
                Key = "key",
                Value = "updated",
                Duration = 10,
                CachingType = "internal"
            });
            context.Lookup(new CacheLookupValueConfig
            {
                Key = "key",
                VariableName = "stored",
                CachingType = "internal"
            });
            context.Value(new CacheValueConfig
            {
                Key = "key",
                VariableName = "refreshed",
                CachingType = "internal",
                RefreshAfter = 0
            }, () => context.SetVariable("refreshed", "computed"));
            context.Remove(new CacheRemoveValueConfig { Key = "key", CachingType = "internal" });
        }, clock, new CacheProxy(shared));

        test.RunInbound();

        test.Context.Variables["before"].Should().Be("external");
        test.Context.Variables["stored"].Should().Be("updated");
        test.Context.Variables["refreshed"].Should().Be("computed");
        shared.InternalCache["key"].Value.Should().Be("internal");
        shared.ExternalCache.Should().NotContainKey("key");
    }

    private static CacheStore CreatePartitions(CacheTestClock clock, bool externalSetup = true) =>
        new CacheStore(clock).WithExternalCacheSetup(externalSetup)
            .WithInternalCacheValue("key", "internal", 60)
            .WithExternalCacheValue("key", "external", 60);

    private sealed class CacheProxy(ICache cache) : ICache
    {
        public Task<object?> GetAsync(string key, CancellationToken ct = default) => cache.GetAsync(key, ct);

        public Task SetAsync(string key, object value, TimeSpan ttl, CancellationToken ct = default) =>
            cache.SetAsync(key, value, ttl, ct);

        public Task RemoveAsync(string key, CancellationToken ct = default) => cache.RemoveAsync(key, ct);

        public Task<CacheValueResult> GetOrCreateAsync(
            string key, TimeSpan expiresAfter, TimeSpan? refreshAfter,
            Func<object?, CancellationToken, Task<object?>> valueFactory, CancellationToken ct = default) =>
            cache.GetOrCreateAsync(key, expiresAfter, refreshAfter, valueFactory, ct);

        public Task<CacheValueResult> GetOrCreateWithDynamicTtlAsync(
            string key, Func<object?, CancellationToken, Task<CacheValueFactoryResult>> valueFactory,
            bool forceRefresh = false, CancellationToken ct = default) =>
            cache.GetOrCreateWithDynamicTtlAsync(key, valueFactory, forceRefresh, ct);
    }
}