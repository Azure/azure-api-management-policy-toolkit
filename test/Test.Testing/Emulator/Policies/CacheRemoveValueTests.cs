// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Document;

namespace Test.Emulator.Emulator.Policies;

[TestClass]
public class CacheRemoveValueTests
{
    class SimpleCacheRemoveValue : IDocument
    {
        public void Inbound(IInboundContext context)
        {
            context.CacheRemoveValue(new CacheRemoveValueConfig() { Key = "key" });
        }
    }

    [TestMethod]
    public void CacheRemoveValue_Callback()
    {
        var test = new SimpleCacheRemoveValue().AsTestDocument();
        var executedCallback = false;
        test.SetupInbound().CacheRemoveValue().WithCallback((_, _) =>
        {
            executedCallback = true;
        });

        test.RunInbound();

        executedCallback.Should().BeTrue();
    }

    [TestMethod]
    public void CacheRemoveValue_SetupCacheRemove()
    {
        var test = new SimpleCacheRemoveValue().AsTestDocument();
        var cacheStore = test.SetupCacheStore().WithInternalCacheValue("key", "value");

        test.RunInbound();

        cacheStore.InternalCache.Should().NotContainKey("key");
    }

    [TestMethod]
    public void CacheRemoveValue_SetupCacheRemove_WithExternalCacheSetup()
    {
        var test = new SimpleCacheRemoveValue().AsTestDocument();
        var cacheStore = test.SetupCacheStore().WithExternalCacheSetup().WithExternalCacheValue("key", "value");

        test.RunInbound();

        cacheStore.ExternalCache.Should().NotContainKey("key");
    }
}