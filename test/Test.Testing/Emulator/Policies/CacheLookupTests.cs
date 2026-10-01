// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net;

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Document;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Expressions;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Services;

namespace Test.Emulator.Emulator.Policies;

[TestClass]
public class CacheLookupTests
{
    [TestMethod]
    [DataRow(false, -1, true)]
    [DataRow(false, 0, false)]
    [DataRow(false, 1, false)]
    [DataRow(true, -1, true)]
    [DataRow(true, 0, false)]
    [DataRow(true, 1, false)]
    public void CacheLookup_HitMissAndExactExpirationBoundary(bool injected, int ticks, bool hit)
    {
        var clock = new CacheTestClock();
        var cache = injected ? CacheTest.SharedCache() : null;
        var test = CacheTest.Response(CacheTest.LookupConfig, clock, cache);
        test.Context.Response.Body.Content = "cached";
        test.RunInbound();
        test.Context.Variables["__cache_hit"].Should().Be(false);
        test.Context.Variables["after-lookup"].Should().Be(true);
        test.RunOutbound();
        clock.Advance(TimeSpan.FromSeconds(10) + TimeSpan.FromTicks(ticks));
        test.Context.Response = new MockResponse();
        test.Context.Variables.Remove("after-lookup");

        test.RunInbound();

        test.Context.Variables["__cache_hit"].Should().Be(hit);
        test.Context.ResponseTerminated.Should().Be(hit);
        test.Context.Variables.ContainsKey("after-lookup").Should().Be(!hit);
        test.Context.Response.Body.Content.Should().Be(hit ? "cached" : null);
    }

    [TestMethod]
    [DataRow(false, false)]
    [DataRow(false, true)]
    [DataRow(true, false)]
    [DataRow(true, true)]
    public void CacheLookup_HitStopsCoordinatedScopesSectionsAndNestedBase(bool injected, bool nested)
    {
        var clock = new CacheTestClock();
        var cache = injected ? CacheTest.SharedCache() : null;
        var calls = new List<string>();
        var pipeline = PolicyPipelineBuilder.Create()
            .AddPolicy(PolicyScope.Global, new ExecutionTestDocument
            {
                InboundAction = context =>
                {
                    calls.Add("global-before");
                    if (nested)
                    {
                        context.Base();
                    }
                    calls.Add("global-after");
                }
            })
            .AddPolicy(PolicyScope.Product, new ExecutionTestDocument
            {
                InboundAction = context =>
                {
                    calls.Add("lookup-before");
                    context.CacheLookup(CacheTest.LookupConfig);
                    calls.Add("lookup-after");
                    if (nested)
                    {
                        context.Base();
                    }
                }
            })
            .AddPolicy(PolicyScope.Operation, new ExecutionTestDocument
            {
                InboundAction = _ => calls.Add("operation-inbound"),
                BackendAction = _ => calls.Add("backend"),
                OutboundAction = _ => calls.Add("outbound"),
                OnErrorAction = _ => calls.Add("on-error")
            })
            .Build();
        var seed = new TestDocument(new ExecutionTestDocument
        {
            InboundAction = context => context.CacheLookup(CacheTest.LookupConfig),
            OutboundAction = context => context.CacheStore(10, null)
        })
        { Context = pipeline.Context };
        CacheTest.Configure(seed, clock, cache);
        seed.Context.Response.Body.Content = "cached";
        seed.RunInbound();
        seed.RunOutbound();
        pipeline.Context.Response = new MockResponse();
        pipeline.Context.Variables.Clear();

        if (nested)
        {
            pipeline.RunAllNested();
        }
        else
        {
            pipeline.RunAll();
        }
        pipeline.RunAll();
        pipeline.RunAllNested();
        pipeline.RunOnError();
        pipeline.RunOnErrorNested();

        calls.Should().Equal(nested
            ? ["global-before", "lookup-before"]
            : ["global-before", "global-after", "lookup-before"]);
        pipeline.Context.Response.Body.Content.Should().Be("cached");
        pipeline.Context.ResponseTerminated.Should().BeTrue();
    }

    [TestMethod]
    public void CacheLookup_SharedCacheRoundTripsIndependentRequestsAndVaryKeys()
    {
        var clock = new CacheTestClock();
        var cache = CacheTest.SharedCache();
        var config = CacheTest.LookupConfig with
        {
            VaryByHeaders = ["Accept-Language"],
            VaryByQueryParameters = ["version"]
        };
        var english = CacheTest.Response(config, clock, cache);
        english.Context.Request.Headers["Accept-Language"] = ["en"];
        english.Context.Request.Url.Query["version"] = ["1"];
        english.Context.Response.Body.Content = "english-v1";
        english.RunInbound();
        english.RunOutbound();
        var french = CacheTest.Response(config, clock, cache);
        french.Context.Request.Headers["Accept-Language"] = ["fr"];
        french.Context.Request.Url.Query["version"] = ["1"];
        french.Context.Response.Body.Content = "french-v1";
        french.RunInbound();
        french.Context.Variables["__cache_hit"].Should().Be(false);
        french.RunOutbound();
        var nextVersion = CacheTest.Response(config, clock, cache);
        nextVersion.Context.Request.Headers["Accept-Language"] = ["en"];
        nextVersion.Context.Request.Url.Query["version"] = ["2"];
        nextVersion.RunInbound();
        nextVersion.Context.Variables["__cache_hit"].Should().Be(false);
        var englishHit = CacheTest.Response(config, clock, cache);
        englishHit.Context.Request.Headers["accept-language"] = ["en"];
        englishHit.Context.Request.Url.Query["version"] = ["1"];
        var frenchHit = CacheTest.Response(config, clock, cache);
        frenchHit.Context.Request.Headers["Accept-Language"] = ["fr"];
        frenchHit.Context.Request.Url.Query["version"] = ["1"];

        englishHit.RunInbound();
        frenchHit.RunInbound();

        englishHit.Context.Response.Body.Content.Should().Be("english-v1");
        frenchHit.Context.Response.Body.Content.Should().Be("french-v1");
        englishHit.Context.ResponseTerminated.Should().BeTrue();
        frenchHit.Context.ResponseTerminated.Should().BeTrue();
        englishHit.Context.Should().NotBeSameAs(english.Context);
    }

    [TestMethod]
    public void CacheLookup_VaryKeysAreCanonicalAndIgnoreUnselectedQueryParameters()
    {
        var clock = new CacheTestClock();
        var cache = CacheTest.SharedCache();
        var config = CacheTest.LookupConfig with
        {
            VaryByHeaders = ["Accept-Language", "X-Tenant"],
            VaryByQueryParameters = ["version", "id"]
        };
        var seed = CacheTest.Response(config, clock, cache);
        seed.Context.Request.Headers["Accept-Language"] = ["en"];
        seed.Context.Request.Headers["X-Tenant"] = ["tenant"];
        seed.Context.Request.Url.Query["version"] = ["1"];
        seed.Context.Request.Url.Query["id"] = ["42"];
        seed.Context.Request.Url.Query["trace"] = ["first"];
        seed.Context.Response.Body.Content = "canonical";
        seed.RunInbound();
        seed.RunOutbound();
        var hit = CacheTest.Response(config with
        {
            VaryByHeaders = ["x-tenant", "accept-language", "X-TENANT"],
            VaryByQueryParameters = ["id", "version", "id"]
        }, clock, cache);
        hit.Context.Request.Headers = new Dictionary<string, string[]>
        {
            ["x-tenant"] = ["tenant"],
            ["accept-language"] = ["en"]
        };
        hit.Context.Request.Url.Query["trace"] = ["second"];
        hit.Context.Request.Url.Query["id"] = ["42"];
        hit.Context.Request.Url.Query["version"] = ["1"];

        hit.RunInbound();

        hit.Context.Variables["__cache_lookup_key"].Should().Be(seed.Context.Variables["__cache_lookup_key"]);
        hit.Context.Response.Body.Content.Should().Be("canonical");
    }

    [TestMethod]
    [DataRow("header-values")]
    [DataRow("missing-header")]
    [DataRow("query-values")]
    public void CacheLookup_VaryKeysDistinguishMissingEmptyAndAmbiguousMultiValues(string variation)
    {
        var clock = new CacheTestClock();
        var cache = CacheTest.SharedCache();
        var config = CacheTest.LookupConfig with
        {
            VaryByHeaders = ["X-Vary"],
            VaryByQueryParameters = ["vary"]
        };
        var first = CacheTest.Response(config, clock, cache);
        var second = CacheTest.Response(config, clock, cache);
        switch (variation)
        {
            case "header-values":
                first.Context.Request.Headers["X-Vary"] = ["a,b"];
                second.Context.Request.Headers["X-Vary"] = ["a", "b"];
                break;
            case "missing-header":
                first.Context.Request.Headers["X-Vary"] = [""];
                break;
            case "query-values":
                first.Context.Request.Url.Query["vary"] = ["a,b"];
                second.Context.Request.Url.Query["vary"] = ["a", "b"];
                break;
        }
        first.Context.Response.Body.Content = "first";
        first.RunInbound();
        first.RunOutbound();

        second.RunInbound();

        second.Context.Variables["__cache_lookup_key"].Should().NotBe(first.Context.Variables["__cache_lookup_key"]);
        second.Context.Variables["__cache_hit"].Should().Be(false);
        second.Context.ResponseTerminated.Should().BeFalse();
    }

    [TestMethod]
    [DataRow("developer", false)]
    [DataRow("groups", false)]
    [DataRow("none", true)]
    public void CacheLookup_DeveloperAndGroupVaryConditionsAreHonored(string variation, bool hit)
    {
        var clock = new CacheTestClock();
        var cache = CacheTest.SharedCache();
        var config = CacheTest.LookupConfig with
        {
            VaryByDeveloper = variation == "developer",
            VaryByDeveloperGroups = variation == "groups"
        };
        var first = CacheTest.Response(config, clock, cache);
        first.Context.User.Id = "alice";
        first.Context.User.MockGroups = [new MockGroup { Id = "group-a" }];
        first.Context.Response.Body.Content = "first";
        first.RunInbound();
        first.RunOutbound();
        var second = CacheTest.Response(config, clock, cache);
        second.Context.User.Id = "bob";
        second.Context.User.MockGroups = [new MockGroup { Id = "group-b" }];

        second.RunInbound();

        second.Context.Variables["__cache_hit"].Should().Be(hit);
        second.Context.ResponseTerminated.Should().Be(hit);
    }

    [TestMethod]
    public void CacheLookup_GroupOrderDoesNotChangeTheVaryKey()
    {
        var clock = new CacheTestClock();
        var cache = CacheTest.SharedCache();
        var config = CacheTest.LookupConfig with { VaryByDeveloperGroups = true };
        var first = CacheTest.Response(config, clock, cache);
        first.Context.User.MockGroups = [new MockGroup { Id = "a" }, new MockGroup { Id = "b" }];
        first.Context.Response.Body.Content = "groups";
        first.RunInbound();
        first.RunOutbound();
        var second = CacheTest.Response(config, clock, cache);
        second.Context.User.MockGroups = [new MockGroup { Id = "b" }, new MockGroup { Id = "a" }];

        second.RunInbound();

        second.Context.Response.Body.Content.Should().Be("groups");
    }

    [TestMethod]
    [DataRow("POST", false)]
    [DataRow("GET", false)]
    [DataRow("GET", true)]
    public void CacheLookup_MethodAndPrivateAuthorizationConditionsApplyToLookupAndStore(string method, bool allow)
    {
        var clock = new CacheTestClock();
        var test = CacheTest.Response(CacheTest.LookupConfig with
        {
            AllowPrivateResponseCaching = allow,
            VaryByHeaders = ["Authorization"]
        }, clock, cacheResponse: true);
        test.Context.Request.Method = method;
        test.Context.Request.Headers["authorization"] = ["Bearer token"];
        test.Context.Response.Body.Content = "private";
        test.RunInbound();
        test.RunOutbound();
        test.Context.Response = new MockResponse();
        test.Context.Variables.Remove("after-lookup");

        test.RunInbound();

        var hit = method == "GET" && allow;
        test.Context.Variables["__cache_hit"].Should().Be(hit);
        test.Context.ResponseTerminated.Should().Be(hit);
        test.SetupCacheStore().InternalCache.Count.Should().Be(hit ? 1 : 0);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void CacheLookup_UnexpectedCachedTypesAreExplicitMissesWithoutChangingTheResponse(bool injected)
    {
        var clock = new CacheTestClock();
        var cache = injected ? CacheTest.SharedCache() : null;
        var test = CacheTest.Response(CacheTest.LookupConfig, clock, cache);
        test.RunInbound();
        var key = (string)test.Context.Variables["__cache_lookup_key"];
        if (cache is not null)
        {
            cache.SetAsync(key, 42, TimeSpan.FromSeconds(10)).GetAwaiter().GetResult();
        }
        else
        {
            test.SetupCacheStore().WithInternalCacheValue(key, 42);
        }
        test.Context.Response.StatusCode = 202;
        test.Context.Response.Body.Content = "unchanged";
        test.Context.Response.Headers["X-Existing"] = ["existing"];

        test.RunInbound();

        test.Context.Variables["__cache_hit"].Should().Be(false);
        test.Context.Response.StatusCode.Should().Be(202);
        test.Context.Response.Body.Content.Should().Be("unchanged");
        test.Context.Response.Headers["X-Existing"].Should().Equal("existing");
        test.Context.ResponseTerminated.Should().BeFalse();
    }

    [TestMethod]
    public void CacheLookup_CallbackOverridesTheServiceAndCanSimulateTermination()
    {
        var test = CacheTest.Response(CacheTest.LookupConfig, new CacheTestClock(), new FailingCache());
        test.SetupInbound().CacheLookup((_, config) => !config.VaryByDeveloper).WithCallback((context, _) =>
        {
            context.Response.Body.Content = "override";
        });

        test.RunInbound();

        test.Context.Response.Body.Content.Should().Be("override");
        test.Context.ResponseTerminated.Should().BeFalse();
        var terminated = CacheTest.Response(CacheTest.LookupConfig, new CacheTestClock(), new FailingCache());
        terminated.SetupInbound().CacheLookup().WithCallback((context, _) =>
        {
            context.Response.Body.Content = "callback hit";
            throw new FinishSectionProcessingException();
        });
        terminated.RunInbound();
        terminated.Context.ResponseTerminated.Should().BeTrue();
        terminated.Context.Variables.Should().NotContainKey("after-lookup");
    }

    [TestMethod]
    public void CacheLookup_MissingExternalCacheIsAMissButInvalidConfigurationAndServiceFailureAreErrors()
    {
        var clock = new CacheTestClock();
        var external = CacheTest.Response(CacheTest.LookupConfig with { CachingType = "external" }, clock);
        external.RunInbound();
        external.Context.Variables["__cache_hit"].Should().Be(false);
        external.Context.ResponseTerminated.Should().BeFalse();
        var invalid = CacheTest.Response(CacheTest.LookupConfig with { CachingType = "invalid" }, clock,
            new FailingCache());
        var downstream = CacheTest.Response(CacheTest.LookupConfig with { DownstreamCachingType = "invalid" }, clock);
        var failure = CacheTest.Response(CacheTest.LookupConfig, clock, new FailingCache());

        Assert.ThrowsExactly<PolicyException>(invalid.RunInbound).InnerException.Should().BeOfType<ArgumentException>();
        Assert.ThrowsExactly<PolicyException>(downstream.RunInbound).InnerException.Should().BeOfType<ArgumentException>();
        var error = Assert.ThrowsExactly<PolicyException>(failure.RunInbound);
        error.Policy.Should().Be(nameof(IInboundContext.CacheLookup));
        error.Section.Should().Be(nameof(IInboundContext));
        error.InnerException.Should().BeOfType<InvalidOperationException>()
            .Which.Message.Should().Be("cache service failure");
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void CacheLookup_ReplacesEveryCopiedCacheControlCaseVariantWithoutReplacingTheDictionary(bool injected)
    {
        var clock = new CacheTestClock();
        var cache = injected ? CacheTest.SharedCache() : null;
        var test = CacheTest.Response(CacheTest.LookupConfig with
        {
            DownstreamCachingType = "private",
            MustRevalidate = true
        }, clock, cache);
        test.RunInbound();
        var key = (string)test.Context.Variables["__cache_lookup_key"];
        var cached = new CachedResponse
        {
            StatusCode = 200,
            StatusReason = "OK",
            Body = "cached",
            ExpiresAt = clock.GetUtcNow() + TimeSpan.FromSeconds(10),
            Headers = new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                ["cache-control"] = ["public, max-age=999"],
                ["CACHE-CONTROL"] = ["no-store"],
                ["Cache-Control"] = ["no-cache"],
                ["X-Cached"] = ["cached-header"]
            }
        };
        if (cache is not null)
        {
            cache.SetAsync(key, cached, TimeSpan.FromSeconds(10)).GetAwaiter().GetResult();
        }
        else
        {
            test.SetupCacheStore().WithInternalCacheValue(key, cached);
        }
        var headers = new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["cache-control"] = ["old response directive"],
            ["Cache-Control"] = ["another old directive"]
        };
        test.Context.Response.Headers = headers;
        clock.Advance(TimeSpan.FromSeconds(2));

        test.RunInbound();

        test.Context.Response.Headers.Should().BeSameAs(headers);
        headers.Comparer.Should().BeSameAs(StringComparer.Ordinal);
        headers.Where(header => string.Equals(header.Key, "Cache-Control", StringComparison.OrdinalIgnoreCase))
            .Should().ContainSingle().Which.Value.Should().Equal("private, max-age=8, must-revalidate");
        headers["Cache-Control"].Should().Equal("private, max-age=8, must-revalidate");
        headers["X-Cached"].Should().Equal("cached-header");
        test.Context.Response.Body.Content.Should().Be("cached");
        test.Context.ResponseTerminated.Should().BeTrue();
        cached.Headers.Should().HaveCount(4);
        cached.Headers["cache-control"].Should().Equal("public, max-age=999");
        cached.Headers["CACHE-CONTROL"].Should().Equal("no-store");
        cached.Headers["Cache-Control"].Should().Equal("no-cache");
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void CacheLookup_ChangingTheLaterHeaderCaseVariantMissesAndRoundTripsTheNewResponse(bool injected)
    {
        var clock = new CacheTestClock();
        var cache = injected ? CacheTest.SharedCache() : null;
        var config = CacheTest.LookupConfig with { VaryByHeaders = ["X-Vary"] };
        var sentValues = new List<string[]>();
        var test = new ExecutionTestDocument
        {
            InboundAction = context =>
            {
                context.CacheLookup(config);
                context.SetVariable("after-lookup", true);
            },
            BackendAction = context => context.ForwardRequest(),
            OutboundAction = context => context.CacheStore(10, null)
        }.AsTestDocument();
        CacheTest.Configure(test, clock, cache);
        test.Context.Services.Register<IHttpClient>(new StubHttpClient(request =>
        {
            sentValues.Add(request.Headers.GetValues("X-Vary").ToArray());
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent($"response-{sentValues.Count}")
            };
        }));
        var headers = new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["X-Vary"] = ["a"],
            ["x-vary"] = ["b"]
        };
        test.Context.Request.Headers = headers;
        test.RunInbound();
        test.RunBackend();
        test.RunOutbound();
        sentValues.Should().ContainSingle().Which.Should().Equal("a", "b");
        var firstKey = test.Context.Variables["__cache_lookup_key"];
        headers["x-vary"][0] = "c";
        test.Context.Response = new MockResponse();
        test.Context.Variables.Remove("after-lookup");

        test.RunInbound();

        test.Context.Variables["__cache_hit"].Should().Be(false);
        test.Context.Variables["__cache_lookup_key"].Should().NotBe(firstKey);
        test.Context.ResponseTerminated.Should().BeFalse();
        test.Context.Variables["after-lookup"].Should().Be(true);
        test.Context.Response.Body.Content.Should().BeNull();
        test.Context.Request.Headers.Should().BeSameAs(headers);
        headers.Comparer.Should().BeSameAs(StringComparer.Ordinal);
        headers["X-Vary"].Should().Equal("a");
        headers["x-vary"].Should().Equal("c");
        var changedKey = test.Context.Variables["__cache_lookup_key"];
        test.RunBackend();
        sentValues.Should().HaveCount(2);
        sentValues[1].Should().Equal("a", "c");
        test.RunOutbound();
        test.Context.Response = new MockResponse();
        test.Context.Variables.Remove("after-lookup");
        test.RunInbound();
        test.Context.Variables["__cache_hit"].Should().Be(true);
        test.Context.Variables["__cache_lookup_key"].Should().Be(changedKey);
        test.Context.Response.Body.Content.Should().Be("response-2");
        test.Context.ResponseTerminated.Should().BeTrue();
        test.Context.Variables.Should().NotContainKey("after-lookup");
    }

    [TestMethod]
    [DataRow("recased")]
    [DataRow("single")]
    [DataRow("fragmented")]
    [DataRow("empty-first")]
    public void CacheLookup_EquivalentOrderedValuesHitRegardlessOfCasingAndHeaderEntryGrouping(string layout)
    {
        var clock = new CacheTestClock();
        var cache = CacheTest.SharedCache();
        var config = CacheTest.LookupConfig with { VaryByHeaders = ["X-Vary"] };
        var seed = CacheTest.Response(config, clock, cache);
        seed.Context.Request.Headers = VaryHeaders("split");
        seed.Context.Response.Body.Content = "all-values";
        seed.RunInbound();
        seed.RunOutbound();
        var lookup = CacheTest.Response(config with { VaryByHeaders = ["x-VARY", "X-vary"] }, clock, cache);
        var headers = VaryHeaders(layout);
        headers["X-Unrelated"] = ["does not vary"];
        lookup.Context.Request.Headers = headers;

        lookup.RunInbound();

        lookup.Context.Variables["__cache_lookup_key"].Should().Be(seed.Context.Variables["__cache_lookup_key"]);
        lookup.Context.Variables["__cache_hit"].Should().Be(true);
        lookup.Context.Response.Body.Content.Should().Be("all-values");
        lookup.Context.Request.Headers.Should().BeSameAs(headers);
        headers.Comparer.Should().BeSameAs(StringComparer.Ordinal);
        headers.Where(header => header.Key.Equals("X-Vary", StringComparison.OrdinalIgnoreCase))
            .SelectMany(header => header.Value).Should().Equal("a", "b", "c", "d");
        headers["X-Unrelated"].Should().Equal("does not vary");
    }

    [TestMethod]
    [DataRow("later-value")]
    [DataRow("later-order")]
    [DataRow("removed-later")]
    [DataRow("comma-value")]
    [DataRow("entry-order")]
    public void CacheLookup_AllHeaderValuesAndTheirOrderParticipateInTheVaryKey(string layout)
    {
        var clock = new CacheTestClock();
        var cache = CacheTest.SharedCache();
        var config = CacheTest.LookupConfig with { VaryByHeaders = ["X-Vary"] };
        var seed = CacheTest.Response(config, clock, cache);
        seed.Context.Request.Headers = VaryHeaders("split");
        seed.Context.Response.Body.Content = "original";
        seed.RunInbound();
        seed.RunOutbound();
        var lookup = CacheTest.Response(config, clock, cache);
        var headers = VaryHeaders(layout);
        var originalKeys = headers.Keys.ToArray();
        var originalValues = headers.Values.SelectMany(values => values).ToArray();
        lookup.Context.Request.Headers = headers;

        lookup.RunInbound();

        lookup.Context.Variables["__cache_lookup_key"].Should().NotBe(seed.Context.Variables["__cache_lookup_key"]);
        lookup.Context.Variables["__cache_hit"].Should().Be(false);
        lookup.Context.Response.Body.Content.Should().BeNull();
        lookup.Context.ResponseTerminated.Should().BeFalse();
        lookup.Context.Request.Headers.Should().BeSameAs(headers);
        headers.Comparer.Should().BeSameAs(StringComparer.Ordinal);
        headers.Keys.Should().Equal(originalKeys);
        headers.Values.SelectMany(values => values).Should().Equal(originalValues);
    }

    [TestMethod]
    [DataRow("absent", "empty", false)]
    [DataRow("empty", "absent", false)]
    [DataRow("empty", "duplicate-empty", true)]
    [DataRow("empty", "empty-string-after-empty", false)]
    [DataRow("empty", "value-after-empty", false)]
    [DataRow("empty-string", "empty", false)]
    public void CacheLookup_AbsentEmptyArraysAndEmptyStringsRemainDistinctAcrossCaseVariants(
        string firstLayout, string secondLayout, bool hit)
    {
        var clock = new CacheTestClock();
        var cache = CacheTest.SharedCache();
        var config = CacheTest.LookupConfig with { VaryByHeaders = ["X-Vary"] };
        var seed = CacheTest.Response(config, clock, cache);
        seed.Context.Request.Headers = VaryHeaders(firstLayout);
        seed.Context.Response.Body.Content = "cached";
        seed.RunInbound();
        seed.RunOutbound();
        var lookup = CacheTest.Response(config, clock, cache);
        var headers = VaryHeaders(secondLayout);
        lookup.Context.Request.Headers = headers;

        lookup.RunInbound();

        lookup.Context.Variables["__cache_hit"].Should().Be(hit);
        lookup.Context.ResponseTerminated.Should().Be(hit);
        lookup.Context.Response.Body.Content.Should().Be(hit ? "cached" : null);
        lookup.Context.Request.Headers.Should().BeSameAs(headers);
        headers.Comparer.Should().BeSameAs(StringComparer.Ordinal);
        if (hit)
        {
            lookup.Context.Variables["__cache_lookup_key"].Should().Be(seed.Context.Variables["__cache_lookup_key"]);
        }
        else
        {
            lookup.Context.Variables["__cache_lookup_key"].Should().NotBe(seed.Context.Variables["__cache_lookup_key"]);
        }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void CacheLookup_InvalidValuesInALaterHeaderCaseVariantAreNotSilentlyIgnored(bool nullArray)
    {
        var test = CacheTest.Response(CacheTest.LookupConfig with { VaryByHeaders = ["X-Vary"] },
            new CacheTestClock());
        var headers = new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["X-Vary"] = ["valid"],
            ["x-vary"] = nullArray ? null! : [null!]
        };
        test.Context.Request.Headers = headers;

        var error = Assert.ThrowsExactly<PolicyException>(test.RunInbound);

        error.InnerException.Should().BeOfType<ArgumentNullException>();
        test.Context.Request.Headers.Should().BeSameAs(headers);
        headers.Comparer.Should().BeSameAs(StringComparer.Ordinal);
    }

    private static Dictionary<string, string[]> VaryHeaders(string layout)
    {
        var headers = new Dictionary<string, string[]>(StringComparer.Ordinal);
        switch (layout)
        {
            case "split": headers["X-Vary"] = ["a", "b"]; headers["x-vary"] = ["c", "d"]; break;
            case "recased": headers["x-vary"] = ["a", "b"]; headers["X-VARY"] = ["c", "d"]; break;
            case "single": headers["x-vary"] = ["a", "b", "c", "d"]; break;
            case "fragmented":
                headers["x-Vary"] = ["a"];
                headers["X-VARY"] = ["b", "c"];
                headers["x-vary"] = ["d"];
                break;
            case "empty-first":
                headers["X-Vary"] = [];
                headers["x-VARY"] = ["a", "b"];
                headers["x-vary"] = ["c", "d"];
                break;
            case "later-value": headers["X-Vary"] = ["a", "b"]; headers["x-vary"] = ["c", "e"]; break;
            case "later-order": headers["X-Vary"] = ["a", "b"]; headers["x-vary"] = ["d", "c"]; break;
            case "removed-later": headers["X-Vary"] = ["a", "b"]; break;
            case "comma-value": headers["X-Vary"] = ["a", "b"]; headers["x-vary"] = ["c,d"]; break;
            case "entry-order": headers["x-vary"] = ["c", "d"]; headers["X-Vary"] = ["a", "b"]; break;
            case "absent": break;
            case "empty": headers["X-Vary"] = []; break;
            case "duplicate-empty": headers["X-Vary"] = []; headers["x-vary"] = []; break;
            case "empty-string-after-empty": headers["X-Vary"] = []; headers["x-vary"] = [""]; break;
            case "value-after-empty": headers["X-Vary"] = []; headers["x-vary"] = ["a"]; break;
            case "empty-string": headers["X-Vary"] = [""]; break;
            default: throw new ArgumentOutOfRangeException(nameof(layout), layout, null);
        }

        return headers;
    }
}