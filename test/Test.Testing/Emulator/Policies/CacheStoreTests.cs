// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Document;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Expressions;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Services;

namespace Test.Emulator.Emulator.Policies;

[TestClass]
public class CacheStoreTests
{
    [TestMethod]
    [DataRow(null, 200, "GET", true)]
    [DataRow(null, 201, "GET", false)]
    [DataRow(null, 500, "GET", false)]
    [DataRow(true, 201, "GET", true)]
    [DataRow(true, 500, "GET", true)]
    [DataRow(false, 200, "GET", false)]
    [DataRow(true, 200, "POST", false)]
    [DataRow(null, 200, "POST", false)]
    public void CacheStore_ExtractsAuthoredIntAndNullableBoolAndHonorsResponseConditions(
        bool? cacheResponse, int statusCode, string method, bool stored)
    {
        var clock = new CacheTestClock();
        var test = CacheTest.Response(CacheTest.LookupConfig, clock, cacheResponse: cacheResponse);
        test.Context.Request.Method = method;
        test.Context.Response.StatusCode = statusCode;
        test.Context.Response.Body.Content = "response";

        test.RunInbound();
        test.RunOutbound();

        test.SetupCacheStore().InternalCache.Count.Should().Be(stored ? 1 : 0);
        if (stored)
        {
            var entry = test.SetupCacheStore().InternalCache.Single().Value;
            entry.Ttl.Should().Be(TimeSpan.FromSeconds(10));
            entry.StoredAt.Should().Be(clock.GetUtcNow().UtcDateTime);
            entry.Value.Should().BeOfType<CachedResponse>()
                .Which.StatusCode.Should().Be(statusCode);
        }
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow(false)]
    [DataRow(true)]
    public void CacheStore_CallbackUsesAuthoredParameterTypesAndReceivesTheRawNullableFlag(bool? flag)
    {
        object? seenDuration = null;
        object? seenFlag = "not-called";
        var test = new ExecutionTestDocument
        {
            OutboundAction = context => context.CacheStore(
                (int)context.ExpressionContext.Variables["duration"], flag)
        }.AsTestDocument();
        test.Context.Variables["duration"] = 17;
        test.Context.Services.Register<ICache>(new FailingCache());
        test.SetupOutbound().CacheStore((_, duration, _) => duration == 0)
            .WithCallback((_, _, _) => Assert.Fail("Wrong predicate selected."));
        test.SetupOutbound().CacheStore((_, duration, response) => duration == 17 && Equals(response, flag))
            .WithCallback((_, duration, response) =>
            {
                seenDuration = duration;
                seenFlag = response;
            });

        test.RunOutbound();

        seenDuration.Should().BeOfType<int>().Which.Should().Be(17);
        seenFlag.Should().Be(flag);
        typeof(MockCacheStoreProvider.Setup).GetMethod(nameof(MockCacheStoreProvider.Setup.WithCallback))!
            .GetParameters()[0].ParameterType.Should().Be(typeof(Action<GatewayContext, int, bool?>));
        test.SetupCacheStore().InternalCache.Should().BeEmpty();
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void CacheStore_SnapshotsStatusBodyDictionaryAndHeaderArrays(bool injected)
    {
        var clock = new CacheTestClock();
        var cache = injected ? CacheTest.SharedCache() : null;
        var test = CacheTest.Response(CacheTest.LookupConfig, clock, cache, cacheResponse: true);
        var headers = new[] { "original" };
        test.Context.Response.StatusCode = 202;
        test.Context.Response.StatusReason = "Accepted";
        test.Context.Response.Body.Content = "original body";
        test.Context.Response.Headers["X-Snapshot"] = headers;
        test.Context.Response.Headers["Content-Type"] = ["text/plain"];
        test.RunInbound();
        test.RunOutbound();
        var key = (string)test.Context.Variables["__cache_lookup_key"];
        var cached = injected
            ? cache!.GetAsync(key).GetAwaiter().GetResult()
            : test.SetupCacheStore().InternalCache[key].Value;

        headers[0] = "mutated";
        test.Context.Response.Headers.Clear();
        test.Context.Response.Body.Content = "mutated body";
        test.Context.Response.StatusCode = 500;
        test.Context.Response.StatusReason = "Error";

        var snapshot = cached.Should().BeOfType<CachedResponse>().Which;
        snapshot.StatusCode.Should().Be(202);
        snapshot.StatusReason.Should().Be("Accepted");
        snapshot.Body.Should().Be("original body");
        snapshot.Headers["X-Snapshot"].Should().Equal("original");
        test.Context.Response = new MockResponse();
        test.RunInbound();
        test.Context.Response.StatusCode.Should().Be(202);
        test.Context.Response.StatusReason.Should().Be("Accepted");
        test.Context.Response.Body.Content.Should().Be("original body");
        test.Context.Response.Headers["x-snapshot"].Should().Equal("original");
        test.Context.Response.Headers["X-Snapshot"][0] = "client mutation";
        snapshot.Headers["X-Snapshot"].Should().Equal("original");
    }

    [TestMethod]
    public void CacheStore_UsesTheLookupKeyAndCacheTypeEvenWhenTheRequestIsRewritten()
    {
        var clock = new CacheTestClock();
        var config = CacheTest.LookupConfig with
        {
            CachingType = "internal",
            VaryByHeaders = ["Accept-Language"],
            VaryByQueryParameters = ["version"]
        };
        var test = CacheTest.Response(config, clock);
        test.SetupCacheStore().WithExternalCacheSetup();
        test.Context.Request.Headers["Accept-Language"] = ["en"];
        test.Context.Request.Url.Query["version"] = ["1"];
        test.RunInbound();
        var key = (string)test.Context.Variables["__cache_lookup_key"];
        test.Context.Request.Url.Path = "/rewritten";
        test.Context.Request.Headers["Accept-Language"] = ["fr"];
        test.Context.Request.Url.Query["version"] = ["2"];
        test.Context.Response.Body.Content = "original request";

        test.RunOutbound();

        test.SetupCacheStore().InternalCache.Should().ContainKey(key);
        test.SetupCacheStore().ExternalCache.Should().BeEmpty();
        test.Context.Request.Url.Path = "/v2/mock/op";
        test.Context.Request.Headers["Accept-Language"] = ["en"];
        test.Context.Request.Url.Query["version"] = ["1"];
        test.Context.Response.Body.Content = "not cached";
        test.RunInbound();
        test.Context.Response.Body.Content.Should().Be("original request");
        test.Context.ResponseTerminated.Should().BeTrue();
    }

    [TestMethod]
    [DataRow(false, -1, true)]
    [DataRow(false, 0, false)]
    [DataRow(true, -1, true)]
    [DataRow(true, 0, false)]
    public void CacheStore_ResponseDurationAndConditionCanUseExpressions(bool injected, int ticks, bool hit)
    {
        var clock = new CacheTestClock();
        var test = new ExecutionTestDocument
        {
            InboundAction = context => context.CacheLookup(CacheTest.LookupConfig),
            OutboundAction = context => context.CacheStore(
                (int)context.ExpressionContext.Variables["duration"],
                context.ExpressionContext.Response.StatusCode == 202)
        }.AsTestDocument();
        var cache = injected ? CacheTest.SharedCache() : null;
        CacheTest.Configure(test, clock, cache);
        test.Context.Variables["duration"] = 17;
        test.Context.Response.StatusCode = 202;
        test.Context.Response.Body.Content = "accepted";

        test.RunInbound();
        test.RunOutbound();
        clock.Advance(TimeSpan.FromSeconds(17) + TimeSpan.FromTicks(ticks));
        test.Context.Response = new MockResponse();
        test.RunInbound();

        test.Context.Variables["__cache_hit"].Should().Be(hit);
        test.Context.ResponseTerminated.Should().Be(hit);
        test.Context.Response.StatusCode.Should().Be(hit ? 202 : 200);
        test.Context.Response.Body.Content.Should().Be(hit ? "accepted" : null);
    }

    [TestMethod]
    public void CacheStore_MissingLookupAndMissingExternalConfigurationAreExplicitErrors()
    {
        var clock = new CacheTestClock();
        var missingLookup = new ExecutionTestDocument
        {
            OutboundAction = context => context.CacheStore(10, null)
        }.AsTestDocument();
        var external = CacheTest.Response(CacheTest.LookupConfig with { CachingType = "external" }, clock);
        external.RunInbound();

        Assert.ThrowsExactly<PolicyException>(missingLookup.RunOutbound).InnerException.Should()
            .BeOfType<InvalidOperationException>().Which.Message.Should().Contain("CacheLookup");
        Assert.ThrowsExactly<PolicyException>(external.RunOutbound).InnerException.Should()
            .BeOfType<InvalidOperationException>().Which.Message.Should().Contain("external");
        external.SetupCacheStore().InternalCache.Should().BeEmpty();
    }

    [TestMethod]
    public void CacheStore_InvalidDurationAndServiceFailuresAreNotReportedAsSuccess()
    {
        var clock = new CacheTestClock();
        var negative = CacheTest.Response(CacheTest.LookupConfig, clock, duration: -1);
        negative.RunInbound();
        var failure = CacheTest.Response(CacheTest.LookupConfig, clock);
        failure.RunInbound();
        failure.Context.Services.Register<ICache>(new FailingCache());

        Assert.ThrowsExactly<PolicyException>(negative.RunOutbound).InnerException.Should()
            .BeOfType<ArgumentOutOfRangeException>();
        var error = Assert.ThrowsExactly<PolicyException>(failure.RunOutbound);
        error.Policy.Should().Be(nameof(IOutboundContext.CacheStore));
        error.Section.Should().Be(nameof(IOutboundContext));
        error.InnerException.Should().BeOfType<InvalidOperationException>()
            .Which.Message.Should().Be("cache service failure");
        negative.SetupCacheStore().InternalCache.Should().BeEmpty();
        failure.SetupCacheStore().InternalCache.Should().BeEmpty();
    }

    [TestMethod]
    [DataRow("none", true, "no-store")]
    [DataRow("private", true, "private, max-age=10, must-revalidate")]
    [DataRow("private", false, "private, max-age=10")]
    [DataRow("public", true, "public, max-age=10, must-revalidate")]
    [DataRow("public", false, "public, max-age=10")]
    public void CacheStore_HonorsDownstreamCachingAndRevalidation(
        string downstream, bool revalidate, string expected)
    {
        var clock = new CacheTestClock();
        var test = new ExecutionTestDocument
        {
            InboundAction = context => context.CacheLookup(CacheTest.LookupConfig with
            {
                DownstreamCachingType = (string)context.ExpressionContext.Variables["downstream"],
                MustRevalidate = (bool)context.ExpressionContext.Variables["revalidate"]
            }),
            OutboundAction = context => context.CacheStore(10, null)
        }.AsTestDocument();
        CacheTest.Configure(test, clock);
        test.Context.Variables["downstream"] = downstream;
        test.Context.Variables["revalidate"] = revalidate;

        test.RunInbound();
        test.RunOutbound();

        test.Context.Response.Headers["Cache-Control"].Should().Equal(expected);
        clock.Advance(TimeSpan.FromSeconds(9));
        test.RunInbound();
        test.Context.Response.Headers["Cache-Control"].Should().Equal(expected.Replace("max-age=10", "max-age=1"));
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void CacheStore_ReplacesEveryCacheControlCaseVariantInTheResponseAndSnapshot(bool injected)
    {
        var clock = new CacheTestClock();
        var cache = injected ? CacheTest.SharedCache() : null;
        var test = CacheTest.Response(CacheTest.LookupConfig with
        {
            DownstreamCachingType = "public",
            MustRevalidate = false
        }, clock, cache);
        var headers = new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["cache-control"] = ["no-store"],
            ["CACHE-CONTROL"] = ["private, max-age=1"],
            ["Cache-Control"] = ["public, max-age=999"],
            ["X-Keep"] = ["unchanged"]
        };
        test.Context.Response.Headers = headers;
        test.RunInbound();

        test.RunOutbound();

        test.Context.Response.Headers.Should().BeSameAs(headers);
        headers.Comparer.Should().BeSameAs(StringComparer.Ordinal);
        headers.Where(header => string.Equals(header.Key, "Cache-Control", StringComparison.OrdinalIgnoreCase))
            .Should().ContainSingle().Which.Value.Should().Equal("public, max-age=10");
        headers["Cache-Control"].Should().Equal("public, max-age=10");
        headers["X-Keep"].Should().Equal("unchanged");
        var key = (string)test.Context.Variables["__cache_lookup_key"];
        var cached = injected
            ? cache!.GetAsync(key).GetAwaiter().GetResult()
            : test.SetupCacheStore().InternalCache[key].Value;
        var snapshot = cached.Should().BeOfType<CachedResponse>().Which;
        snapshot.Headers.Should().NotBeSameAs(headers);
        snapshot.Headers.Comparer.Should().BeSameAs(StringComparer.Ordinal);
        snapshot.Headers.Where(header => string.Equals(header.Key, "Cache-Control", StringComparison.OrdinalIgnoreCase))
            .Should().ContainSingle().Which.Value.Should().Equal("public, max-age=10");
        snapshot.Headers["Cache-Control"].Should().Equal("public, max-age=10");
        snapshot.Headers["X-Keep"].Should().Equal("unchanged");
    }
}