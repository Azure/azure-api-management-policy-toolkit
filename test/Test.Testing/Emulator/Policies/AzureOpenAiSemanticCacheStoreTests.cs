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
public class AzureOpenAiSemanticCacheStoreTests
{
    [TestMethod]
    [DataRow(false, false)]
    [DataRow(false, true)]
    [DataRow(true, false)]
    [DataRow(true, true)]
    public void AzureStoreWritesResponsesReadableByEitherLookupAlias(bool azureLookup, bool azureReader)
    {
        var clock = new CacheTestClock();
        var cache = SemanticCacheTest.SharedCache(clock);
        var seed = SemanticCacheTest.Create(cache: cache, clock: clock, azureLookup: azureLookup, azureStore: true);
        seed.Context.Response.Body.Content = "azure stored answer";
        seed.RunInbound();
        seed.RunOutbound();
        var test = SemanticCacheTest.Create(cache: cache, clock: clock, azureLookup: azureReader);

        test.RunInbound();

        SemanticCacheTest.AssertHit(test, "azure stored answer");
    }

    [TestMethod]
    [DataRow(-1, true)]
    [DataRow(0, false)]
    [DataRow(1, false)]
    public void AzureStoreUsesSecondsAndExpirationBoundary(int ticks, bool hit)
    {
        var clock = new CacheTestClock();
        var cache = SemanticCacheTest.SharedCache(clock);
        var seed = SemanticCacheTest.Create(cache: cache, clock: clock, azureLookup: true, azureStore: true, duration: 3);
        seed.Context.Response.Body.Content = "azure TTL";
        seed.RunInbound();
        seed.RunOutbound();
        clock.Advance(TimeSpan.FromSeconds(3) + TimeSpan.FromTicks(ticks));
        var test = SemanticCacheTest.Create(cache: cache, clock: clock, azureLookup: true);

        test.RunInbound();

        SemanticCacheTest.AssertOutcome(test, hit, "azure TTL");
    }

    [TestMethod]
    public void AzureStoreSnapshotsBodyReasonAndHeadersWithoutAliasing()
    {
        var cache = SemanticCacheTest.SharedCache();
        var seed = SemanticCacheTest.Create(cache: cache, azureLookup: true, azureStore: true);
        var headers = new[] { "original" };
        seed.Context.Response.StatusReason = "Azure OK";
        seed.Context.Response.Body.Content = "original azure body";
        seed.Context.Response.Headers["X-Azure"] = headers;
        seed.RunInbound();
        seed.RunOutbound();
        headers[0] = "mutation";
        seed.Context.Response = new MockResponse();
        var first = SemanticCacheTest.Create(cache: cache, azureLookup: true);
        first.RunInbound();
        first.Context.Response.Headers["X-Azure"][0] = "consumer mutation";
        var second = SemanticCacheTest.Create(cache: cache, azureLookup: true);

        second.RunInbound();

        SemanticCacheTest.AssertHit(second, "original azure body");
        second.Context.Response.StatusReason.Should().Be("Azure OK");
        second.Context.Response.Headers["X-Azure"].Should().Equal("original");
    }

    [TestMethod]
    [DataRow(200, true)]
    [DataRow(201, false)]
    [DataRow(500, false)]
    public void AzureStoreSharesDefault200OnlyEligibility(int status, bool stored)
    {
        var test = SemanticCacheTest.Create(azureLookup: true, azureStore: true);
        test.Context.Response.StatusCode = status;

        test.RunInbound();
        test.RunOutbound();

        test.SetupCacheStore().ExternalCache.Count.Should().Be(stored ? 1 : 0);
    }

    [TestMethod]
    public void AzureStoreRequiresLookupAndReportsAliasInPolicyException()
    {
        var test = SemanticCacheTest.Create(azureStore: true);

        var exception = Assert.ThrowsExactly<PolicyException>(() => test.RunOutbound());

        exception.Policy.Should().Be(nameof(IOutboundContext.AzureOpenAiSemanticCacheStore));
        exception.Section.Should().Be(nameof(IOutboundContext));
        exception.InnerException.Should().BeOfType<InvalidOperationException>();
        exception.Message.Should().Contain("lookup");
    }

    [TestMethod]
    public void AzureAndLlmStoreCallbacksUseTheirOwnPredicatesAndUIntArguments()
    {
        var calls = new List<string>();
        var test = new ExecutionTestDocument
        {
            OutboundAction = context =>
            {
                context.AzureOpenAiSemanticCacheStore(17);
                context.AzureOpenAiSemanticCacheStore(23);
                context.LlmSemanticCacheStore(31);
            }
        }.AsTestDocument();
        test.SetupOutbound().AzureOpenAiSemanticCacheStore((_, duration) => duration == 0)
            .WithCallback((_, _) => Assert.Fail("Wrong predicate."));
        test.SetupOutbound().AzureOpenAiSemanticCacheStore((_, duration) => duration == 17)
            .WithCallback((_, duration) => calls.Add($"azure-{duration}"));
        test.SetupOutbound().AzureOpenAiSemanticCacheStore((_, duration) => duration == 23)
            .WithCallback((_, duration) => calls.Add($"azure-{duration}"));
        test.SetupOutbound().LlmSemanticCacheStore().WithCallback((_, duration) => calls.Add($"llm-{duration}"));

        test.RunOutbound();

        calls.Should().Equal("azure-17", "azure-23", "llm-31");
    }

    [TestMethod]
    public void AzureStoreSupportsUIntDurationExpressionAndUnmatchedCallback()
    {
        var clock = new CacheTestClock();
        var cache = new SemanticCacheTestCache(clock);
        var test = new ExecutionTestDocument
        {
            InboundAction = context => context.AzureOpenAiSemanticCacheLookup(SemanticCacheTest.Config),
            OutboundAction = context => context.AzureOpenAiSemanticCacheStore((uint)context.ExpressionContext.Variables["ttl"])
        }.AsTestDocument();
        SemanticCacheTest.Configure(test, cache, clock);
        test.Context.Variables["ttl"] = 19U;
        test.SetupOutbound().AzureOpenAiSemanticCacheStore((_, _) => false)
            .WithCallback((_, _) => Assert.Fail("Wrong predicate."));

        test.RunInbound();
        test.RunOutbound();

        cache.LastTtl.Should().Be(TimeSpan.FromSeconds(19));
        cache.Updates.Should().ContainSingle();
    }

    [TestMethod]
    public void AzureStoreCallbackCanOverrideMissingDependenciesAndSimulateFailure()
    {
        var test = SemanticCacheTest.Create(azureStore: true, configureCache: false, configureEmbeddings: false);
        test.SetupOutbound().AzureOpenAiSemanticCacheStore().WithCallback((_, duration) =>
        {
            duration.Should().Be(10);
            throw new InvalidOperationException("azure store callback failure");
        });

        var exception = Assert.ThrowsExactly<PolicyException>(() => test.RunOutbound());

        exception.Policy.Should().Be(nameof(IOutboundContext.AzureOpenAiSemanticCacheStore));
        exception.Message.Should().Be("azure store callback failure");
    }

    [TestMethod]
    public void SemanticAndOrdinaryResponseCachesDoNotCollideInOneExternalBackend()
    {
        var clock = new CacheTestClock();
        var shared = SemanticCacheTest.SharedCache(clock);
        var semantic = SemanticCacheTest.Create(cache: shared, clock: clock, azureLookup: true, azureStore: true);
        semantic.Context.Response.Body.Content = "semantic response";
        semantic.RunInbound();
        semantic.RunOutbound();
        var ordinary = CacheTest.Response(CacheTest.LookupConfig with { CachingType = "external" }, clock, shared);
        ordinary.Context.Response.Body.Content = "ordinary response";
        ordinary.RunInbound();
        ordinary.RunOutbound();
        var semanticHit = SemanticCacheTest.Create(cache: shared, clock: clock);
        var ordinaryHit = CacheTest.Response(CacheTest.LookupConfig with { CachingType = "external" }, clock, shared);

        semanticHit.RunInbound();
        ordinaryHit.RunInbound();

        SemanticCacheTest.AssertHit(semanticHit, "semantic response");
        ordinaryHit.Context.Response.Body.Content.Should().Be("ordinary response");
        ordinaryHit.Context.ResponseTerminated.Should().BeTrue();
    }

    [TestMethod]
    [DynamicData(nameof(SemanticCacheCompatibilityTest.UnsupportedResponses), typeof(SemanticCacheCompatibilityTest))]
    public void AzureIncompatibleResponsesFailBeforeCacheWritesAndCannotBeReplayed(string scenario, string body, string contentType, string reason)
    {
        SemanticCacheCompatibilityTest.AssertRejectedResponse(scenario, body, contentType, reason, true);
    }

    [TestMethod]
    [DynamicData(nameof(SemanticCacheCompatibilityTest.AcceptedResponses), typeof(SemanticCacheCompatibilityTest))]
    public void AzureTextOnlyResponsesIncludingVertexMetadataStillRoundTrip(string scenario, string body)
    {
        SemanticCacheCompatibilityTest.AssertAcceptedResponse(scenario, body, true);
    }

    [TestMethod]
    [DataRow("original")]
    [DataRow("current")]
    [DataRow("backend")]
    public void AzureStreamingEndpointIntroducedAfterLookupCannotBeStoredAsJson(string location)
    {
        SemanticCacheCompatibilityTest.AssertRejectedStreamingStore(location, true);
    }

    [TestMethod]
    [DataRow("""{"messages":[{"role":"user","content":"question"}],"tools":[{"type":"function","function":{"name":"lookup"}}]}""")]
    [DataRow("""{"messages":[{"role":"user","content":"question"}],"tool_choice":"required"}""")]
    [DataRow("""{"messages":[{"role":"assistant","content":"transcript","audio":{"id":"audio-1"}}]}""")]
    public void AzureToolsOrAudioIntroducedAfterLookupCannotReuseItsSafePendingState(string body)
    {
        SemanticCacheCompatibilityTest.AssertRejectedRewrittenRequest(body, true);
    }

    [TestMethod]
    [DynamicData(nameof(SemanticCacheCompatibilityTest.ResponseMetadataAndText), typeof(SemanticCacheCompatibilityTest))]
    public void AzureSetBodyTextAndInertResponseMetadataRoundTripAcrossAliases(string scenario, string body, string? contentType)
    {
        SemanticCacheCompatibilityTest.AssertResponseBodyRoundTrip(scenario, body, contentType, true);
    }

    [TestMethod]
    [DynamicData(nameof(SemanticCacheCompatibilityTest.ActiveResponseTools), typeof(SemanticCacheCompatibilityTest))]
    public void AzureResponseMetadataDoesNotAllowEnabledOrForcedTools(string scenario, string body)
    {
        SemanticCacheCompatibilityTest.AssertRejectedResponse(scenario, body, "application/json", "tool", true);
    }

    [TestMethod]
    [DynamicData(nameof(SemanticCacheCompatibilityTest.CompletionChunks), typeof(SemanticCacheCompatibilityTest))]
    public void AzureUsageOnlyCompletionChunksCannotBeStored(string scenario, string body, string? contentType)
    {
        SemanticCacheCompatibilityTest.AssertRejectedResponse(scenario, body, contentType, "stream", true);
    }

    [TestMethod]
    [DynamicData(nameof(SemanticCacheCompatibilityTest.MalformedDeclaredJson), typeof(SemanticCacheCompatibilityTest))]
    public void AzureDeclaredJsonIsParsedStrictlyBeforeAnyStore(string scenario, string body, string contentType)
    {
        SemanticCacheCompatibilityTest.AssertMalformedJson(scenario, body, contentType, true, cached: false);
    }
}