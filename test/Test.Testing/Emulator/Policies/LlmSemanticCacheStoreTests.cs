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
public class LlmSemanticCacheStoreTests
{
    [TestMethod]
    public void SnapshotsResponseAndCopiesHeaderArraysAtBothStoreAndLookup()
    {
        var clock = new CacheTestClock();
        var cache = SemanticCacheTest.SharedCache(clock);
        var seed = SemanticCacheTest.Create(cache: cache, clock: clock);
        var original = new[] { "original" };
        seed.Context.Response.StatusReason = "Custom OK";
        seed.Context.Response.Body.Content = "unicode \u00e9 response";
        seed.Context.Response.Headers["X-Snapshot"] = original;
        seed.Context.Response.Headers["Content-Type"] = ["text/plain"];
        seed.RunInbound();
        seed.RunOutbound();
        original[0] = "source mutation";
        seed.Context.Response.Headers.Clear();
        seed.Context.Response.Body.Content = "mutated";
        seed.Context.Response.StatusCode = 500;
        seed.Context.Response.StatusReason = "Error";
        var first = SemanticCacheTest.Create(cache: cache, clock: clock);
        first.Context.Response.Headers["X-Stale"] = ["remove me"];

        first.RunInbound();
        first.Context.Response.StatusCode.Should().Be(200);
        first.Context.Response.StatusReason.Should().Be("Custom OK");
        first.Context.Response.Headers.Should().NotContainKey("X-Stale");
        first.Context.Response.Headers["x-snapshot"].Should().Equal("original");
        SemanticCacheTest.AssertHit(first, "unicode \u00e9 response");
        first.Context.Response.Headers["X-Snapshot"][0] = "consumer mutation";
        first.Context.Response.Body.Content = "consumer body";
        var second = SemanticCacheTest.Create(cache: cache, clock: clock);
        second.RunInbound();

        SemanticCacheTest.AssertHit(second, "unicode \u00e9 response");
        second.Context.Response.Headers["X-Snapshot"].Should().Equal("original");
    }

    [TestMethod]
    [DataRow(-1, true)]
    [DataRow(0, false)]
    [DataRow(1, false)]
    public void DurationIsSecondsAndExactExpirationIsAMiss(int offsetTicks, bool hit)
    {
        var clock = new CacheTestClock();
        var cache = new SemanticCacheTestCache(clock);
        var seed = SemanticCacheTest.Create(cache: cache, clock: clock, duration: 17);
        seed.Context.Response.Body.Content = "TTL response";
        seed.RunInbound();
        seed.RunOutbound();
        cache.LastTtl.Should().Be(TimeSpan.FromSeconds(17));
        clock.Advance(TimeSpan.FromSeconds(17) + TimeSpan.FromTicks(offsetTicks));
        var test = SemanticCacheTest.Create(cache: cache, clock: clock);

        test.RunInbound();

        SemanticCacheTest.AssertOutcome(test, hit, "TTL response");
    }

    [TestMethod]
    public void SnapshotExpirationIsEnforcedEvenIfInjectedCacheIgnoresTtl()
    {
        var clock = new CacheTestClock();
        var cache = new SemanticCacheTestCache(clock);
        var seed = SemanticCacheTest.Create(cache: cache, clock: clock, duration: 1);
        seed.Context.Response.Body.Content = "expired response";
        seed.RunInbound();
        seed.RunOutbound();
        var retained = cache.LastValue;
        retained.Should().NotBeNull();
        cache.ReadOverride = _ => Task.FromResult(retained);
        clock.Advance(TimeSpan.FromSeconds(1));
        var test = SemanticCacheTest.Create(cache: cache, clock: clock);

        test.RunInbound();

        SemanticCacheTest.AssertMiss(test);
        test.Context.Response.Body.Content.Should().BeNull();
    }

    [TestMethod]
    public void AResponseThatExpiresDuringSimilarityEvaluationCannotBeReturned()
    {
        var clock = new CacheTestClock();
        var cache = SemanticCacheTest.SharedCache(clock);
        var seed = SemanticCacheTest.Create(cache: cache, clock: clock, duration: 1);
        seed.Context.Response.Body.Content = "expires while scoring";
        seed.RunInbound();
        seed.RunOutbound();
        var test = SemanticCacheTest.Create(cache: cache, clock: clock);
        test.Context.Services.Register<ISemanticCacheSimilarity>(new SemanticCacheTestSimilarity((_, _) =>
        {
            clock.Advance(TimeSpan.FromSeconds(1));
            return 0;
        }));

        test.RunInbound();

        SemanticCacheTest.AssertMiss(test);
        test.Context.Response.Body.Content.Should().BeNull();
    }

    [TestMethod]
    public void DifferentEntriesInOnePartitionRetainIndependentExpirations()
    {
        var clock = new CacheTestClock();
        var cache = SemanticCacheTest.SharedCache(clock);
        var config = SemanticCacheTest.Config with { ScoreThreshold = 0 };
        var longLived = SemanticCacheTest.Create(config, cache, clock, duration: 10,
            body: SemanticCacheTest.Prompt("long"));
        longLived.Context.Response.Body.Content = "long-lived";
        longLived.RunInbound();
        longLived.RunOutbound();
        var shortLived = SemanticCacheTest.Create(config, cache, clock,
            new SemanticCacheTestEmbeddings { Vector = [0, 1] }, duration: 1, body: SemanticCacheTest.Prompt("short"));
        shortLived.Context.Response.Body.Content = "short-lived";
        shortLived.RunInbound();
        shortLived.RunOutbound();
        clock.Advance(TimeSpan.FromSeconds(2));
        var longHit = SemanticCacheTest.Create(config, cache, clock, body: SemanticCacheTest.Prompt("long"));
        var shortMiss = SemanticCacheTest.Create(config, cache, clock,
            new SemanticCacheTestEmbeddings { Vector = [0, 1] }, body: SemanticCacheTest.Prompt("short"));

        longHit.RunInbound();
        shortMiss.RunInbound();

        SemanticCacheTest.AssertHit(longHit, "long-lived");
        SemanticCacheTest.AssertMiss(shortMiss);
    }

    [TestMethod]
    public void ZeroDurationExpiresImmediately()
    {
        var clock = new CacheTestClock();
        var cache = SemanticCacheTest.SharedCache(clock);
        var seed = SemanticCacheTest.Create(cache: cache, clock: clock, duration: 0);
        seed.Context.Response.Body.Content = "do not retain";
        seed.RunInbound();
        seed.RunOutbound();
        var test = SemanticCacheTest.Create(cache: cache, clock: clock);

        test.RunInbound();

        SemanticCacheTest.AssertMiss(test);
    }

    [TestMethod]
    public void UIntMaxDurationDoesNotOverflowAndCanUseTheFullAuthoredRange()
    {
        var clock = new CacheTestClock();
        var cache = new SemanticCacheTestCache(clock);
        var seed = SemanticCacheTest.Create(cache: cache, clock: clock, duration: uint.MaxValue);
        seed.Context.Response.Body.Content = "long duration";

        seed.RunInbound();
        seed.RunOutbound();
        clock.Advance(TimeSpan.FromSeconds(uint.MaxValue) - TimeSpan.FromTicks(1));
        var test = SemanticCacheTest.Create(cache: cache, clock: clock);
        test.RunInbound();

        cache.LastTtl.Should().Be(TimeSpan.FromSeconds(uint.MaxValue));
        SemanticCacheTest.AssertHit(test, "long duration");
    }

    [TestMethod]
    public void UnrepresentableExpirationFailsExplicitly()
    {
        var clock = new CacheTestClock();
        clock.Advance(DateTimeOffset.MaxValue - clock.GetUtcNow() - TimeSpan.FromSeconds(1));
        var seed = SemanticCacheTest.Create(cache: SemanticCacheTest.SharedCache(clock), clock: clock);
        seed.RunInbound();

        var exception = Assert.ThrowsExactly<PolicyException>(() => seed.RunOutbound());

        exception.Policy.Should().Be(nameof(IOutboundContext.LlmSemanticCacheStore));
        exception.InnerException.Should().BeOfType<ArgumentOutOfRangeException>();
    }

    [TestMethod]
    [DataRow(200, true)]
    [DataRow(201, false)]
    [DataRow(204, false)]
    [DataRow(400, false)]
    [DataRow(500, false)]
    public void AuthoredDurationOnlyStoreCachesOnly200Responses(int statusCode, bool stored)
    {
        var test = SemanticCacheTest.Create();
        test.Context.Response.StatusCode = statusCode;
        test.Context.Response.Body.Content = "backend response";

        test.RunInbound();
        test.RunOutbound();

        test.SetupCacheStore().ExternalCache.Count.Should().Be(stored ? 1 : 0);
        test.Context.Response.StatusCode.Should().Be(statusCode);
        test.Context.Response.Body.Content.Should().Be("backend response");
        test.Context.Variables["after-semantic-store"].Should().Be(true);
    }

    [TestMethod]
    [DataRow(200)]
    [DataRow(500)]
    public void RequiresCorrespondingInboundLookupEvenForNoncacheableResponse(int statusCode)
    {
        var test = SemanticCacheTest.Create();
        test.Context.Response.StatusCode = statusCode;

        var exception = Assert.ThrowsExactly<PolicyException>(() => test.RunOutbound());

        exception.Policy.Should().Be(nameof(IOutboundContext.LlmSemanticCacheStore));
        exception.Section.Should().Be(nameof(IOutboundContext));
        exception.InnerException.Should().BeOfType<InvalidOperationException>();
        exception.Message.Should().Contain("lookup");
    }

    [TestMethod]
    [DataRow("request-id")]
    [DataRow("request-instance")]
    public void LookupStateCannotLeakToADifferentRequest(string change)
    {
        var test = SemanticCacheTest.Create();
        test.RunInbound();
        if (change == "request-id") test.Context.RequestId = Guid.NewGuid();
        else test.Context.Request = new MockRequest();

        var exception = Assert.ThrowsExactly<PolicyException>(() => test.RunOutbound());

        exception.InnerException.Should().BeOfType<InvalidOperationException>();
        exception.Message.Should().Contain("request");
        test.SetupCacheStore().ExternalCache.Should().BeEmpty();
    }

    [TestMethod]
    public void StoreConsumesPendingLookupAndCannotStoreTwiceWithoutANewLookup()
    {
        var test = SemanticCacheTest.Create();
        test.RunInbound();
        test.RunOutbound();

        var exception = Assert.ThrowsExactly<PolicyException>(() => test.RunOutbound());

        exception.InnerException.Should().BeOfType<InvalidOperationException>();
        test.SetupCacheStore().ExternalCache.Should().ContainSingle();
    }

    [TestMethod]
    public void ARejectedNon200ResponseAlsoConsumesItsPendingLookup()
    {
        var test = SemanticCacheTest.Create();
        test.RunInbound();
        test.Context.Response.StatusCode = 500;
        test.RunOutbound();
        test.Context.Response.StatusCode = 200;

        var exception = Assert.ThrowsExactly<PolicyException>(() => test.RunOutbound());

        exception.InnerException.Should().BeOfType<InvalidOperationException>();
        test.SetupCacheStore().ExternalCache.Should().BeEmpty();
    }

    [TestMethod]
    public void LaterStoreForTheSamePromptReplacesItsCandidateInsteadOfAppendingDuplicates()
    {
        var clock = new CacheTestClock();
        var cache = SemanticCacheTest.SharedCache(clock);
        var first = SemanticCacheTest.Create(cache: cache, clock: clock);
        var second = SemanticCacheTest.Create(cache: cache, clock: clock);
        first.RunInbound();
        second.RunInbound();
        first.Context.Response.Body.Content = "old answer";
        second.Context.Response.Body.Content = "new answer";
        first.RunOutbound();
        second.RunOutbound();
        var hit = SemanticCacheTest.Create(cache: cache, clock: clock);
        var scorer = new SemanticCacheTestSimilarity((_, _) => 0);
        hit.Context.Services.Register<ISemanticCacheSimilarity>(scorer);

        hit.RunInbound();

        SemanticCacheTest.AssertHit(hit, "new answer");
        scorer.Calls.Should().Be(1);
    }

    [TestMethod]
    public void HitDoesNotLeaveAPendingOutboundStore()
    {
        var test = SemanticCacheTest.Create();
        test.RunInbound();
        test.RunOutbound();
        test.Context.Variables.Remove("after-semantic-lookup");
        test.RunInbound();
        test.Context.ResponseTerminated.Should().BeTrue();

        var exception = Assert.ThrowsExactly<PolicyException>(() => test.RunOutbound());

        exception.InnerException.Should().BeOfType<InvalidOperationException>();
        exception.Message.Should().Contain("lookup");
    }

    [TestMethod]
    public void StoreUsesInboundPromptPartitionVectorCacheAndClockSnapshotsAfterMutation()
    {
        var clock = new CacheTestClock();
        var cache = SemanticCacheTest.SharedCache(clock);
        var values = new[] { "tenant" };
        var vector = new[] { 1.0, 0.0 };
        var embeddings = new SemanticCacheTestEmbeddings { Vector = vector };
        var config = SemanticCacheTest.Config with { VaryBy = values };
        var seed = SemanticCacheTest.Create(config, cache, clock, embeddings);
        seed.RunInbound();
        values[0] = "mutated tenant";
        vector[0] = 0;
        vector[1] = 1;
        seed.Context.Request.Body.Content = SemanticCacheTest.Prompt("rewritten prompt");
        seed.Context.Request.Url.Path = "/rewritten";
        seed.Context.Request.OriginalUrl.Path = "/rewritten-original";
        seed.Context.Variables.Clear();
        seed.Context.Services.Register<ICache>(new FailingCache());
        seed.Context.Services.Register<TimeProvider>(TimeProvider.System);
        seed.Context.Response.Body.Content = "original inbound identity";

        seed.RunOutbound();
        var hit = SemanticCacheTest.Create(
            SemanticCacheTest.Config with { VaryBy = ["tenant"] }, cache, clock);
        hit.RunInbound();
        var miss = SemanticCacheTest.Create(
            SemanticCacheTest.Config with { VaryBy = ["mutated tenant"] }, cache, clock);
        miss.RunInbound();

        SemanticCacheTest.AssertHit(hit, "original inbound identity");
        SemanticCacheTest.AssertMiss(miss);
        embeddings.Requests.Should().ContainSingle();
    }

    [TestMethod]
    [DataRow("callback")]
    [DataRow("invalid-config")]
    [DataRow("embedding-error")]
    public void ANewLookupClearsOldPendingStateEvenWhenItIsOverriddenOrFails(string scenario)
    {
        var config = SemanticCacheTest.Config;
        var test = new ExecutionTestDocument
        {
            InboundAction = context => context.LlmSemanticCacheLookup(config),
            OutboundAction = context => context.LlmSemanticCacheStore(10)
        }.AsTestDocument();
        SemanticCacheTest.Configure(test);
        test.SetupInbound().LlmSemanticCacheLookup((_, value) => value.EmbeddingsBackendId == "callback")
            .WithCallback((_, _) => { });
        test.RunInbound();
        if (scenario == "callback")
        {
            config = config with { EmbeddingsBackendId = "callback" };
            test.RunInbound();
        }
        else
        {
            if (scenario == "invalid-config") config = config with { ScoreThreshold = -1 };
            else test.Context.Services.Register<ISemanticCacheEmbeddingProvider>(new SemanticCacheTestEmbeddings
            {
                TaskFactory = _ => throw new InvalidOperationException("new lookup failed")
            });
            Assert.ThrowsExactly<PolicyException>(() => test.RunInbound());
        }

        var exception = Assert.ThrowsExactly<PolicyException>(() => test.RunOutbound());

        exception.InnerException.Should().BeOfType<InvalidOperationException>();
        test.SetupCacheStore().ExternalCache.Should().BeEmpty();
    }

    [TestMethod]
    public void CallbackReceivesEvaluatedUIntDurationAndDiscardsPendingDefaultStore()
    {
        var test = new ExecutionTestDocument
        {
            InboundAction = context => context.LlmSemanticCacheLookup(SemanticCacheTest.Config),
            OutboundAction = context => context.LlmSemanticCacheStore((uint)context.ExpressionContext.Variables["ttl"])
        }.AsTestDocument();
        SemanticCacheTest.Configure(test);
        test.Context.Variables["ttl"] = 17U;
        object? seen = null;
        test.SetupOutbound().LlmSemanticCacheStore((_, duration) => duration == 0)
            .WithCallback((_, _) => Assert.Fail("Wrong predicate."));
        test.SetupOutbound().LlmSemanticCacheStore((_, duration) => duration == 17)
            .WithCallback((_, duration) => seen = duration);
        test.RunInbound();
        test.RunOutbound();
        test.Context.Variables["ttl"] = 10U;

        var exception = Assert.ThrowsExactly<PolicyException>(() => test.RunOutbound());

        seen.Should().BeOfType<uint>().Which.Should().Be(17);
        exception.InnerException.Should().BeOfType<InvalidOperationException>();
        test.SetupCacheStore().ExternalCache.Should().BeEmpty();
    }

    [TestMethod]
    public void UnmatchedStoreCallbackUsesRealBehaviorAndDurationExpression()
    {
        var clock = new CacheTestClock();
        var cache = new SemanticCacheTestCache(clock);
        var test = new ExecutionTestDocument
        {
            InboundAction = context => context.LlmSemanticCacheLookup(SemanticCacheTest.Config),
            OutboundAction = context => context.LlmSemanticCacheStore((uint)context.ExpressionContext.Variables["ttl"])
        }.AsTestDocument();
        SemanticCacheTest.Configure(test, cache, clock);
        test.Context.Variables["ttl"] = 23U;
        test.Context.Response.Body.Content = "expression TTL";
        test.SetupOutbound().LlmSemanticCacheStore((_, _) => false)
            .WithCallback((_, _) => Assert.Fail("Wrong predicate."));

        test.RunInbound();
        test.RunOutbound();

        cache.Updates.Should().ContainSingle();
        cache.LastTtl.Should().Be(TimeSpan.FromSeconds(23));
    }

    [TestMethod]
    [DataRow("failure")]
    [DataRow("null-task")]
    [DataRow("null-result")]
    [DataRow("invalid-result-value")]
    [DataRow("invalid-shape")]
    public void WriteFailuresAreExplicitAndDiscardPendingState(string scenario)
    {
        var cache = new SemanticCacheTestCache();
        var test = SemanticCacheTest.Create(cache: cache);
        test.RunInbound();
        if (scenario == "failure") cache.FailUpdates = true;
        if (scenario == "null-task") cache.NullUpdateTask = true;
        if (scenario == "null-result") cache.NullUpdateResult = true;
        if (scenario == "invalid-result-value") cache.InvalidUpdateValue = true;
        if (scenario == "invalid-shape")
        {
            cache.SetAsync(cache.Reads.Single(), "invalid semantic index", TimeSpan.FromSeconds(60)).GetAwaiter().GetResult();
        }

        var exception = Assert.ThrowsExactly<PolicyException>(() => test.RunOutbound());
        var repeated = Assert.ThrowsExactly<PolicyException>(() => test.RunOutbound());

        exception.Policy.Should().Be(nameof(IOutboundContext.LlmSemanticCacheStore));
        exception.InnerException.Should().BeOfType<InvalidOperationException>();
        repeated.Message.Should().Contain("lookup");
        cache.Updates.Should().ContainSingle();
    }

    [TestMethod]
    public void CallbackFailureUsesPolicyExceptionAndDoesNotRetainPendingState()
    {
        var duration = 17U;
        var test = new ExecutionTestDocument
        {
            InboundAction = context => context.LlmSemanticCacheLookup(SemanticCacheTest.Config),
            OutboundAction = context => context.LlmSemanticCacheStore(duration)
        }.AsTestDocument();
        SemanticCacheTest.Configure(test);
        test.SetupOutbound().LlmSemanticCacheStore((_, value) => value == 17)
            .WithCallback((_, _) => throw new InvalidOperationException("callback failure"));
        test.RunInbound();
        var exception = Assert.ThrowsExactly<PolicyException>(() => test.RunOutbound());
        duration = 10;

        var repeated = Assert.ThrowsExactly<PolicyException>(() => test.RunOutbound());

        exception.Policy.Should().Be(nameof(IOutboundContext.LlmSemanticCacheStore));
        exception.Message.Should().Be("callback failure");
        repeated.Message.Should().Contain("lookup");
    }

    [TestMethod]
    [DataRow("text/event-stream")]
    [DataRow("TEXT/EVENT-STREAM; charset=utf-8")]
    public void StreamingResponsesCannotBeStoredAsSuccessfulBufferedSnapshots(string contentType)
    {
        var test = SemanticCacheTest.Create();
        test.RunInbound();
        test.Context.Response.Headers = new Dictionary<string, string[]>
        {
            ["content-type"] = [contentType]
        };
        test.Context.Response.Body.Content = "data: partial";

        var exception = Assert.ThrowsExactly<PolicyException>(() => test.RunOutbound());

        exception.InnerException.Should().BeOfType<NotSupportedException>();
        exception.Message.Should().Contain("stream");
        test.SetupCacheStore().ExternalCache.Should().BeEmpty();
    }

    [TestMethod]
    public void ConcurrentStoresInOnePartitionDoNotLoseDistinctCandidates()
    {
        var clock = new CacheTestClock();
        var cache = SemanticCacheTest.SharedCache(clock);
        var config = SemanticCacheTest.Config with { ScoreThreshold = 0 };
        var tests = Enumerable.Range(0, 12).Select(index =>
        {
            var vector = new double[12];
            vector[index] = 1;
            var test = SemanticCacheTest.Create(config, cache, clock,
                new SemanticCacheTestEmbeddings { Vector = vector },
                body: SemanticCacheTest.Prompt($"prompt-{index}"));
            test.Context.Response.Body.Content = $"response-{index}";
            test.RunInbound();
            return test;
        }).ToArray();

        Task.WaitAll(tests.Select(test => Task.Run(test.RunOutbound)).ToArray());

        for (var index = 0; index < tests.Length; index++)
        {
            var vector = new double[12];
            vector[index] = 1;
            var hit = SemanticCacheTest.Create(config, cache, clock,
                new SemanticCacheTestEmbeddings { Vector = vector },
                body: SemanticCacheTest.Prompt($"query-{index}"));
            hit.RunInbound();
            SemanticCacheTest.AssertHit(hit, $"response-{index}");
        }
    }

    [TestMethod]
    [DynamicData(nameof(SemanticCacheCompatibilityTest.UnsupportedResponses), typeof(SemanticCacheCompatibilityTest))]
    public void IncompatibleResponsesFailBeforeCacheWritesAndCannotBeReplayed(string scenario, string body, string contentType, string reason)
    {
        SemanticCacheCompatibilityTest.AssertRejectedResponse(scenario, body, contentType, reason, false);
    }

    [TestMethod]
    [DynamicData(nameof(SemanticCacheCompatibilityTest.AcceptedResponses), typeof(SemanticCacheCompatibilityTest))]
    public void TextOnlyResponsesIncludingVertexMetadataStillRoundTrip(string scenario, string body)
    {
        SemanticCacheCompatibilityTest.AssertAcceptedResponse(scenario, body, false);
    }

    [TestMethod]
    [DataRow("original")]
    [DataRow("current")]
    [DataRow("backend")]
    public void AStreamingEndpointIntroducedAfterLookupCannotBeStoredAsJson(string location)
    {
        SemanticCacheCompatibilityTest.AssertRejectedStreamingStore(location, false);
    }

    [TestMethod]
    [DataRow("""{"messages":[{"role":"user","content":"question"}],"tools":[{"type":"function","function":{"name":"lookup"}}]}""")]
    [DataRow("""{"messages":[{"role":"user","content":"question"}],"tool_choice":"required"}""")]
    [DataRow("""{"messages":[{"role":"assistant","content":"transcript","audio":{"id":"audio-1"}}]}""")]
    public void ToolsOrAudioIntroducedAfterLookupCannotReuseItsSafePendingState(string body)
    {
        SemanticCacheCompatibilityTest.AssertRejectedRewrittenRequest(body, false);
    }

    [TestMethod]
    [DynamicData(nameof(SemanticCacheCompatibilityTest.ResponseMetadataAndText), typeof(SemanticCacheCompatibilityTest))]
    public void SetBodyTextAndInertResponseMetadataRoundTripAcrossAliases(string scenario, string body, string? contentType)
    {
        SemanticCacheCompatibilityTest.AssertResponseBodyRoundTrip(scenario, body, contentType, false);
    }

    [TestMethod]
    [DynamicData(nameof(SemanticCacheCompatibilityTest.ActiveResponseTools), typeof(SemanticCacheCompatibilityTest))]
    public void ResponseMetadataDoesNotAllowEnabledOrForcedTools(string scenario, string body)
    {
        SemanticCacheCompatibilityTest.AssertRejectedResponse(scenario, body, "application/json", "tool", false);
    }

    [TestMethod]
    [DynamicData(nameof(SemanticCacheCompatibilityTest.CompletionChunks), typeof(SemanticCacheCompatibilityTest))]
    public void UsageOnlyCompletionChunksCannotBeStored(string scenario, string body, string? contentType)
    {
        SemanticCacheCompatibilityTest.AssertRejectedResponse(scenario, body, contentType, "stream", false);
    }

    [TestMethod]
    [DynamicData(nameof(SemanticCacheCompatibilityTest.MalformedDeclaredJson), typeof(SemanticCacheCompatibilityTest))]
    public void DeclaredJsonIsParsedStrictlyBeforeAnyStore(string scenario, string body, string contentType)
    {
        SemanticCacheCompatibilityTest.AssertMalformedJson(scenario, body, contentType, false, cached: false);
    }
}