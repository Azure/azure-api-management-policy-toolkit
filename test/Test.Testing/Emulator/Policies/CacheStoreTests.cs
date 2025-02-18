// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.ApiManagement.PolicyToolkit.Authoring;
using Azure.ApiManagement.PolicyToolkit.Authoring.Expressions;
using Azure.ApiManagement.PolicyToolkit.Testing;
using Azure.ApiManagement.PolicyToolkit.Testing.Document;

namespace Test.Emulator.Emulator.Policies;

[TestClass]
public class CacheStoreTests
{
    class SimpleCacheStore : IDocument
    {
        public void Outbound(IOutboundContext context)
        {
            context.CacheStore(10);
        }
    }

    class SimpleCacheStoreStoreResponse : IDocument
    {
        public void Outbound(IOutboundContext context)
        {
            context.CacheStore(10, true);
        }
    }

    [TestMethod]
    public void CacheStore_Callback()
    {
        var test = new SimpleCacheStore().AsTestDocument();
        var executedCallback = false;
        test.SetupOutbound().CacheStore().WithCallback((_, _, _) =>
        {
            executedCallback = true;
        });

        test.RunOutbound();

        executedCallback.Should().BeTrue();
    }

    [TestMethod]
    public void CacheStore_StoreResponseInCache()
    {
        var test = new SimpleCacheStore().AsTestDocument();
        var cache = test.SetupCacheStore();
        test.SetupCacheInfo().WithExecutedCacheLookup();
        test.SetupOutbound().CacheStore().WithCacheKey("key");

        test.RunOutbound();

        var cacheValue = cache.InternalCache.Should().ContainKey("key").WhoseValue;
        cacheValue.Duration.Should().Be(10);
        var response = cacheValue.Value.Should().BeAssignableTo<IResponse>().Which;
        var contextResponse = test.Context.Response;
        response.Should().NotBeSameAs(contextResponse, "Should be a copy of response");
        response.StatusCode.Should().Be(contextResponse.StatusCode);
        response.StatusReason.Should().Be(contextResponse.StatusReason);
        response.Headers.Should().Equal(contextResponse.Headers);
    }

    [TestMethod]
    public void CacheStore_NotStoreIfResponseIsNot200()
    {
        var test = new SimpleCacheStore().AsTestDocument();
        test.Context.Response.StatusCode = 401;
        test.Context.Response.StatusReason = "Unauthorized";
        var cache = test.SetupCacheStore();
        test.SetupCacheInfo().WithExecutedCacheLookup();
        test.SetupOutbound().CacheStore().WithCacheKey("key");

        test.RunOutbound();

        cache.InternalCache.Should().NotContainKey("key");
    }

    [TestMethod]
    public void CacheStore_StoreIfResponseIsNot200_WhenCacheResponseIsSetToTrue()
    {
        var test = new SimpleCacheStoreStoreResponse().AsTestDocument();
        var contextResponse = test.Context.Response;
        contextResponse.StatusCode = 401;
        contextResponse.StatusReason = "Unauthorized";
        var cache = test.SetupCacheStore();
        test.SetupCacheInfo().WithExecutedCacheLookup();
        test.SetupOutbound().CacheStore().WithCacheKey("key");

        test.RunOutbound();

        var cacheValue = cache.InternalCache.Should().ContainKey("key").WhoseValue;
        cacheValue.Duration.Should().Be(10);
        var response = cacheValue.Value.Should().BeAssignableTo<IResponse>().Which;
        response.Should().NotBeSameAs(contextResponse, "Should be a copy of response");
        response.StatusCode.Should().Be(contextResponse.StatusCode);
        response.StatusReason.Should().Be(contextResponse.StatusReason);
        response.Headers.Should().Equal(contextResponse.Headers);
    }
}