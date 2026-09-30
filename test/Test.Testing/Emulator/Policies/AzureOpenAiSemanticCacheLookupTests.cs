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
public class AzureOpenAiSemanticCacheLookupTests
{
    class SimpleAzureOpenAiSemanticCacheLookup : IDocument
    {
        public void Inbound(IInboundContext context)
        {
            context.AzureOpenAiSemanticCacheLookup(new SemanticCacheLookupConfig()
            {
                ScoreThreshold = 0.05M,
                EmbeddingsBackendId = "backend-id",
                EmbeddingsBackendAuth = "token"
            });
        }
    }

    [TestMethod]
    public void AzureOpenAiSemanticCacheLookup_Callback()
    {
        var test = new SimpleAzureOpenAiSemanticCacheLookup().AsTestDocument();
        var executedCallback = false;
        test.SetupInbound().AzureOpenAiSemanticCacheLookup().WithCallback((_, _) =>
        {
            executedCallback = true;
        });

        test.RunInbound();

        executedCallback.Should().BeTrue();
    }

    [TestMethod]
    [DataRow(false, false)]
    [DataRow(false, true)]
    [DataRow(true, false)]
    [DataRow(true, true)]
    public void AzureLookupReadsEntriesFromEitherLookupAndStoreAlias(bool azureLookup, bool azureStore)
    {
        var clock = new CacheTestClock();
        var cache = SemanticCacheTest.SharedCache(clock);
        var seed = SemanticCacheTest.Create(cache: cache, clock: clock,
            azureLookup: azureLookup, azureStore: azureStore);
        seed.Context.Response.Body.Content = "alias answer";
        seed.RunInbound();
        seed.RunOutbound();
        var test = SemanticCacheTest.Create(cache: cache, clock: clock, azureLookup: true);

        test.RunInbound();

        SemanticCacheTest.AssertHit(test, "alias answer");
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void NamedExternalCachesStayIsolatedEvenWhenNamesResolveToOneBackend(bool azureStore)
    {
        var clock = new CacheTestClock();
        var shared = SemanticCacheTest.SharedCache(clock);
        var config = SemanticCacheTest.Config with { CacheId = "tenant-a" };
        var seed = SemanticCacheTest.Create(config, clock: clock, azureLookup: true, azureStore: azureStore);
        seed.Context.Services.Register<ICache>("tenant-a", shared).Register<ICache>("tenant-b", shared);
        seed.Context.Response.Body.Content = "tenant-a answer";
        seed.RunInbound();
        seed.RunOutbound();
        var miss = SemanticCacheTest.Create(config with { CacheId = "tenant-b" }, clock: clock);
        miss.Context.Services.Register<ICache>("tenant-a", shared).Register<ICache>("tenant-b", shared);
        var hit = SemanticCacheTest.Create(config, clock: clock);
        hit.Context.Services.Register<ICache>("tenant-a", shared);

        miss.RunInbound();
        hit.RunInbound();

        SemanticCacheTest.AssertMiss(miss);
        SemanticCacheTest.AssertHit(hit, "tenant-a answer");
    }

    [TestMethod]
    public void AzureLookupSupportsVaryBySystemFilteringMessageLimitAndExpressions()
    {
        var clock = new CacheTestClock();
        var shared = SemanticCacheTest.SharedCache(clock);
        var embeddings = new SemanticCacheTestEmbeddings();
        var test = new ExecutionTestDocument
        {
            InboundAction = context =>
            {
                var expression = context.ExpressionContext;
                context.AzureOpenAiSemanticCacheLookup(new SemanticCacheLookupConfig
                {
                    ScoreThreshold = (decimal)expression.Variables["threshold"],
                    EmbeddingsBackendId = (string)expression.Variables["backend"],
                    EmbeddingsBackendAuth = (string)expression.Variables["auth"],
                    IgnoreSystemMessages = (bool)expression.Variables["ignore"],
                    MaxMessageCount = (uint)expression.Variables["max"],
                    VaryBy = [(string)expression.Variables["tenant"]]
                });
                context.SetVariable("after-semantic-lookup", true);
            },
            OutboundAction = context => context.AzureOpenAiSemanticCacheStore(10)
        }.AsTestDocument();
        SemanticCacheTest.Configure(test, shared, clock, embeddings);
        test.Context.Request.Body.Content =
            """{"messages":[{"role":"system","content":"remove this system"},{"role":"user","content":"question"}]}""";
        test.Context.Variables["threshold"] = 0M;
        test.Context.Variables["backend"] = "azure-embeddings";
        test.Context.Variables["auth"] = "system-assigned";
        test.Context.Variables["ignore"] = true;
        test.Context.Variables["max"] = 1U;
        test.Context.Variables["tenant"] = "private";
        test.Context.Response.Body.Content = "azure expression";

        test.RunInbound();
        test.RunOutbound();
        test.Context.Response = new MockResponse();
        test.Context.Variables.Remove("after-semantic-lookup");
        test.RunInbound();

        SemanticCacheTest.AssertHit(test, "azure expression");
        embeddings.Requests.Should().HaveCount(2);
        embeddings.Requests.Should().OnlyContain(request => request.BackendId == "azure-embeddings"
            && request.Authentication == "system-assigned" && !request.Prompt.Contains("remove this system"));
    }

    [TestMethod]
    [DataRow("threshold")]
    [DataRow("auth")]
    [DataRow("backend")]
    [DataRow("cache")]
    [DataRow("named-cache")]
    public void AzureLookupReportsItsOwnPolicyForInvalidOrMissingDependencies(string missing)
    {
        var config = missing switch
        {
            "threshold" => SemanticCacheTest.Config with { ScoreThreshold = 2 },
            "auth" => SemanticCacheTest.Config with { EmbeddingsBackendAuth = "token" },
            "named-cache" => SemanticCacheTest.Config with { CacheId = "not-registered" },
            _ => SemanticCacheTest.Config
        };
        var test = SemanticCacheTest.Create(config, azureLookup: true,
            configureCache: missing != "cache", configureEmbeddings: missing != "backend");

        var exception = Assert.ThrowsExactly<PolicyException>(() => test.RunInbound());

        exception.Policy.Should().Be(nameof(IInboundContext.AzureOpenAiSemanticCacheLookup));
        exception.Section.Should().Be(nameof(IInboundContext));
        test.Context.ResponseTerminated.Should().BeFalse();
    }

    [TestMethod]
    public void AzureLookupPredicatesAndFailureCallbacksDoNotAffectLlmHandler()
    {
        var test = new ExecutionTestDocument
        {
            InboundAction = context =>
            {
                context.LlmSemanticCacheLookup(SemanticCacheTest.Config);
                context.AzureOpenAiSemanticCacheLookup(SemanticCacheTest.Config);
            }
        }.AsTestDocument();
        var llmCalled = false;
        test.SetupInbound().LlmSemanticCacheLookup().WithCallback((_, _) => llmCalled = true);
        test.SetupInbound().AzureOpenAiSemanticCacheLookup((_, config) => config.EmbeddingsBackendId == "never")
            .WithCallback((_, _) => Assert.Fail("Wrong predicate."));
        test.SetupInbound().AzureOpenAiSemanticCacheLookup((_, config) => config.ScoreThreshold == 0.05M)
            .WithCallback((_, _) => throw new InvalidOperationException("azure callback failure"));

        var exception = Assert.ThrowsExactly<PolicyException>(() => test.RunInbound());

        llmCalled.Should().BeTrue();
        exception.Policy.Should().Be(nameof(IInboundContext.AzureOpenAiSemanticCacheLookup));
        exception.Message.Should().Be("azure callback failure");
    }

    [TestMethod]
    public void AzureLookupUnmatchedCallbackUsesRealDefaultAndPreservesPrompt()
    {
        var embeddings = new SemanticCacheTestEmbeddings();
        var test = SemanticCacheTest.Create(embeddings: embeddings, azureLookup: true);
        var body = test.Context.Request.Body.Content;
        test.SetupInbound().AzureOpenAiSemanticCacheLookup((_, _) => false)
            .WithCallback((_, _) => Assert.Fail("Wrong predicate."));

        test.RunInbound();

        SemanticCacheTest.AssertMiss(test);
        embeddings.Requests.Should().ContainSingle();
        test.Context.Request.Body.Content.Should().Be(body);
        test.Context.Request.Body.Consumed.Should().BeFalse();
    }

    [TestMethod]
    [DynamicData(nameof(SemanticCacheCompatibilityTest.UnsupportedRequests), typeof(SemanticCacheCompatibilityTest))]
    public void AzureRejectsToolAndAudioFeaturesBeforeEmbeddingOrCacheAccess(string scenario, string body, string reason)
    {
        SemanticCacheCompatibilityTest.AssertRejectedRequest(scenario, body, reason, true);
    }

    [TestMethod]
    [DynamicData(nameof(SemanticCacheCompatibilityTest.AcceptedRequests), typeof(SemanticCacheCompatibilityTest))]
    public void AzureAcceptsDisabledToolsAndGenuinelyTextOnlyRequests(string scenario, string body)
    {
        SemanticCacheCompatibilityTest.AssertAcceptedRequest(scenario, body, true);
    }

    [TestMethod]
    [DynamicData(nameof(SemanticCacheCompatibilityTest.VertexTextMetadata), typeof(SemanticCacheCompatibilityTest))]
    public void AzureAcceptsAndPreservesDocumentedNonModalVertexPartMetadata(string scenario, string body, string expected)
    {
        SemanticCacheCompatibilityTest.AssertVertexMetadata(scenario, body, expected, true);
    }

    [TestMethod]
    [DynamicData(nameof(SemanticCacheCompatibilityTest.InvalidVertexMetadata), typeof(SemanticCacheCompatibilityTest))]
    public void AzureRejectsInvalidVertexMetadataBeforeEmbedding(string scenario, string body)
    {
        SemanticCacheCompatibilityTest.AssertInvalidVertexMetadata(scenario, body, true);
    }

    [TestMethod]
    [DataRow("original", ":streamGenerateContent")]
    [DataRow("current", ":streamGenerateContent")]
    [DataRow("backend", ":streamGenerateContent")]
    [DataRow("current", "%3AstreamGenerateContent")]
    [DataRow("original", ":streamRawPredict")]
    [DataRow("current", ":serverStreamingPredict")]
    public void AzureRejectsStreamingRpcUrlsWithoutABodyStreamFlagOrSseHeader(string location, string method)
    {
        SemanticCacheCompatibilityTest.AssertStreamingLookup(location, method, true);
    }

    [TestMethod]
    public void AzureNonstreamingVertexEndpointAndModelNamesContainingStreamStillRoundTrip()
    {
        SemanticCacheCompatibilityTest.AssertNonstreamingVertex(true);
    }

    [TestMethod]
    [DynamicData(nameof(SemanticCacheCompatibilityTest.UnsupportedResponses), typeof(SemanticCacheCompatibilityTest))]
    public void AzureDoesNotReplayAnIncompatibleSnapshotFromAnOlderCache(string scenario, string body, string contentType, string reason)
    {
        SemanticCacheCompatibilityTest.AssertRejectedCachedResponse(scenario, body, contentType, reason, true);
    }

    [TestMethod]
    [DynamicData(nameof(SemanticCacheCompatibilityTest.ResponseMetadataAndText), typeof(SemanticCacheCompatibilityTest))]
    public void AzureCachedResponsesAcceptInertMetadataAndHonorTextContentTypes(string scenario, string body, string? contentType)
    {
        SemanticCacheCompatibilityTest.AssertAcceptedSnapshot(scenario, body, contentType, true);
    }

    [TestMethod]
    [DynamicData(nameof(SemanticCacheCompatibilityTest.ActiveResponseTools), typeof(SemanticCacheCompatibilityTest))]
    public void AzureCachedResponsesStillRejectForcedToolsAndActualCalls(string scenario, string body)
    {
        SemanticCacheCompatibilityTest.AssertRejectedCachedResponse(scenario, body, "application/json", "tool", true);
    }

    [TestMethod]
    [DynamicData(nameof(SemanticCacheCompatibilityTest.CompletionChunks), typeof(SemanticCacheCompatibilityTest))]
    public void AzureCachedUsageOnlyChunksRejectTheStreamingDiscriminator(string scenario, string body, string? contentType)
    {
        SemanticCacheCompatibilityTest.AssertRejectedCachedResponse(scenario, body, contentType, "stream", true);
    }

    [TestMethod]
    [DynamicData(nameof(SemanticCacheCompatibilityTest.MalformedDeclaredJson), typeof(SemanticCacheCompatibilityTest))]
    public void AzureMalformedDeclaredJsonSnapshotsFailInsteadOfReplayingText(string scenario, string body, string contentType)
    {
        SemanticCacheCompatibilityTest.AssertMalformedJson(scenario, body, contentType, true, cached: true);
    }
}