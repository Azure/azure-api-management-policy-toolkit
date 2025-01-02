// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.ApiManagement.PolicyToolkit.Authoring;
using Azure.ApiManagement.PolicyToolkit.Testing;
using Azure.ApiManagement.PolicyToolkit.Testing.Document;

namespace Test.Emulator.Emulator.Policies;

[TestClass]
public class CacheStoreValueTests
{
    class SimpleCacheStoreValue : IDocument
    {
        public void Inbound(IInboundContext context)
        {
            context.CacheStoreValue(new CacheStoreValueConfig() { Key = "key", Value = "value", Duration = 10 });
        }
    }

    [TestMethod]
    public void CacheStoreValue_Callback()
    {
        var test = new SimpleCacheStoreValue().AsTestDocument();
        var executedCallback = false;
        test.SetupInbound().CacheStoreValue().WithCallback((_, _) =>
        {
            executedCallback = true;
        });

        test.RunInbound();

        executedCallback.Should().BeTrue();
    }

    [TestMethod]
    public void CacheStoreValue_SetupCacheStore()
    {
        var test = new SimpleCacheStoreValue().AsTestDocument();
        var cacheStore = test.SetupCacheStore();

        test.RunInbound();

        test.SetupCacheStore();
        var cacheValue = cacheStore.InternalCache.Should().ContainKey("key").WhoseValue;
        cacheValue.Value.Should().Be("value");
        cacheValue.Duration.Should().Be(10);
        cacheStore.ExternalCache.Should().NotContainKey("key");
    }

    [TestMethod]
    public void CacheStoreValue_SetupCacheStore_WitchExternalCacheSetup()
    {
        var test = new SimpleCacheStoreValue().AsTestDocument();
        var cacheStore = test.SetupCacheStore().WithExternalCacheSetup();

        test.RunInbound();

        var cacheValue = cacheStore.ExternalCache.Should().ContainKey("key").WhoseValue;
        cacheValue.Value.Should().Be("value");
        cacheValue.Duration.Should().Be(10);
        cacheStore.InternalCache.Should().NotContainKey("key");
    }
}