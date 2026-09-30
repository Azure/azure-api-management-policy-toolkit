// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Globalization;
using System.Text.Json;

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Document;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Data;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Expressions;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Services;

namespace Test.Emulator.Emulator.Policies;

[TestClass]
public class LlmSemanticCacheLookupTests
{
    [TestMethod]
    public void MissPreservesRequestAndResponseAndContinuesInbound()
    {
        var embeddings = new SemanticCacheTestEmbeddings();
        var test = SemanticCacheTest.Create(embeddings: embeddings);
        var requestBody = test.Context.Request.Body.Content;
        test.Context.Response.StatusCode = 202;
        test.Context.Response.StatusReason = "Accepted";
        test.Context.Response.Body.Content = "not replaced";
        test.Context.Response.Headers["X-Existing"] = ["retained"];

        test.RunInbound();

        SemanticCacheTest.AssertMiss(test);
        test.Context.Request.Body.Content.Should().Be(requestBody);
        test.Context.Request.Body.Consumed.Should().BeFalse();
        test.Context.Response.StatusCode.Should().Be(202);
        test.Context.Response.StatusReason.Should().Be("Accepted");
        test.Context.Response.Body.Content.Should().Be("not replaced");
        test.Context.Response.Headers["X-Existing"].Should().Equal("retained");
        var request = embeddings.Requests.Should().ContainSingle().Which;
        request.BackendId.Should().Be("embeddings");
        request.Authentication.Should().Be("system-assigned");
        request.Prompt.Should().Contain("\"role\":\"user\"").And.Contain("question");
    }

    [TestMethod]
    public void DefaultExternalCacheRoundTripsWithoutAnInjectedCache()
    {
        var test = SemanticCacheTest.Create();
        test.Context.Response.Body.Content = "cached locally";
        test.RunInbound();
        test.RunOutbound();
        test.Context.Response = new MockResponse();
        test.Context.Variables.Remove("after-semantic-lookup");

        test.RunInbound();

        SemanticCacheTest.AssertHit(test, "cached locally");
        test.SetupCacheStore().InternalCache.Should().BeEmpty();
        test.SetupCacheStore().ExternalCache.Should().ContainSingle();
    }

    [TestMethod]
    [DataRow("0", 0.0, true)]
    [DataRow("0", 0.000001, false)]
    [DataRow("0.05", 0.049999, true)]
    [DataRow("0.05", 0.05, true)]
    [DataRow("0.05", 0.050001, false)]
    [DataRow("1", 1.0, true)]
    [DataRow("1", 1.000001, false)]
    [DataRow("1", 2.0, false)]
    public void UsesInclusiveMaximumDistanceAndLowerThresholdIsStricter(string threshold, double distance, bool hit)
    {
        var clock = new CacheTestClock();
        var cache = SemanticCacheTest.SharedCache(clock);
        var seed = SemanticCacheTest.Create(cache: cache, clock: clock);
        seed.Context.Response.Body.Content = "semantic match";
        seed.RunInbound();
        seed.RunOutbound();
        var config = SemanticCacheTest.Config with { ScoreThreshold = decimal.Parse(threshold, CultureInfo.InvariantCulture) };
        var test = SemanticCacheTest.Create(config, cache, clock,
            new SemanticCacheTestEmbeddings { Vector = [0, 1] }, body: SemanticCacheTest.Prompt("different wording"));
        var similarity = new SemanticCacheTestSimilarity((_, _) => distance);
        test.Context.Services.Register<ISemanticCacheSimilarity>(similarity);

        test.RunInbound();

        similarity.Calls.Should().Be(1);
        SemanticCacheTest.AssertOutcome(test, hit, "semantic match");
    }

    [TestMethod]
    [DataRow("0", 1.0, 0.0, true)]
    [DataRow("0.19", 0.8, 0.6, false)]
    [DataRow("0.2", 0.8, 0.6, true)]
    [DataRow("0.999", 0.0, 1.0, false)]
    [DataRow("1", 0.0, 1.0, true)]
    [DataRow("1", -1.0, 0.0, false)]
    [DataRow("0", double.MaxValue, 0.0, true)]
    [DataRow("0", double.Epsilon, 0.0, true)]
    public void DefaultCosineDistanceHandlesThresholdsOppositeAndScaledVectors(
        string threshold, double first, double second, bool hit)
    {
        var clock = new CacheTestClock();
        var cache = SemanticCacheTest.SharedCache(clock);
        var seed = SemanticCacheTest.Create(cache: cache, clock: clock);
        seed.Context.Response.Body.Content = "cosine match";
        seed.RunInbound();
        seed.RunOutbound();
        var test = SemanticCacheTest.Create(
            SemanticCacheTest.Config with { ScoreThreshold = decimal.Parse(threshold, CultureInfo.InvariantCulture) },
            cache, clock, new SemanticCacheTestEmbeddings { Vector = [first, second] },
            body: SemanticCacheTest.Prompt("another prompt"));

        test.RunInbound();

        SemanticCacheTest.AssertOutcome(test, hit, "cosine match");
    }

    [TestMethod]
    public void ReturnsNearestCandidateRatherThanTheFirstWithinThreshold()
    {
        var clock = new CacheTestClock();
        var cache = SemanticCacheTest.SharedCache(clock);
        var first = SemanticCacheTest.Create(cache: cache, clock: clock);
        first.Context.Response.Body.Content = "first";
        first.RunInbound();
        first.RunOutbound();
        var second = SemanticCacheTest.Create(SemanticCacheTest.Config with { ScoreThreshold = 0 },
            cache, clock, new SemanticCacheTestEmbeddings { Vector = [0.8, 0.6] },
            body: SemanticCacheTest.Prompt("second"));
        second.Context.Response.Body.Content = "nearest";
        second.RunInbound();
        SemanticCacheTest.AssertMiss(second);
        second.RunOutbound();
        var test = SemanticCacheTest.Create(SemanticCacheTest.Config with { ScoreThreshold = 0.5M },
            cache, clock, new SemanticCacheTestEmbeddings { Vector = [0.8, 0.6] },
            body: SemanticCacheTest.Prompt("query"));

        test.RunInbound();

        SemanticCacheTest.AssertHit(test, "nearest");
    }

    [TestMethod]
    [DataRow(false, false)]
    [DataRow(true, true)]
    [DataRow(null, false)]
    public void IgnoreSystemMessagesRemovesOnlySystemTurns(bool? ignore, bool hit)
    {
        var clock = new CacheTestClock();
        var cache = SemanticCacheTest.SharedCache(clock);
        var config = SemanticCacheTest.Config with { IgnoreSystemMessages = ignore, ScoreThreshold = 0 };
        var embeddings = new SemanticCacheTestEmbeddings(request =>
            request.Prompt.Contains("changed system", StringComparison.Ordinal) ? [0, 1] : [1, 0]);
        var seed = SemanticCacheTest.Create(config, cache, clock, embeddings,
            body: """{"messages":[{"role":"system","content":"original system"},{"role":"user","content":"question"},{"role":"assistant","content":"previous answer"},{"role":"developer","content":"retain this instruction"}]}""");
        seed.Context.Response.Body.Content = "answer";
        seed.RunInbound();
        seed.RunOutbound();
        var test = SemanticCacheTest.Create(config, cache, clock, embeddings,
            body: """{"messages":[{"role":"system","content":"changed system"},{"role":"user","content":"question"},{"role":"assistant","content":"previous answer"},{"role":"developer","content":"retain this instruction"}]}""");

        test.RunInbound();

        SemanticCacheTest.AssertOutcome(test, hit, "answer");
        embeddings.Requests.Last().Prompt.Should().Contain("previous answer").And.Contain("retain this instruction");
        embeddings.Requests.Last().Prompt.Contains("changed system", StringComparison.Ordinal).Should().Be(ignore != true);
    }

    [TestMethod]
    [DataRow(0, false, false)]
    [DataRow(1, false, true)]
    [DataRow(2, false, false)]
    [DataRow(1, true, false)]
    public void MaxMessageCountSkipsCachingOnlyAboveTheRemainingDialogLimit(int max, bool ignore, bool skipped)
    {
        var embeddings = new SemanticCacheTestEmbeddings();
        var config = SemanticCacheTest.Config with { MaxMessageCount = (uint)max, IgnoreSystemMessages = ignore };
        var test = SemanticCacheTest.Create(config, embeddings: embeddings,
            body: """{"messages":[{"role":"system","content":"instruction"},{"role":"user","content":"question"}]}""");
        test.Context.Response.Body.Content = "answer";

        test.RunInbound();
        test.RunOutbound();

        SemanticCacheTest.AssertMiss(test);
        embeddings.Requests.Count.Should().Be(skipped ? 0 : 1);
        test.SetupCacheStore().ExternalCache.Count.Should().Be(skipped ? 0 : 1);
        test.Context.Variables["after-semantic-store"].Should().Be(true);
    }

    [TestMethod]
    public void OmittedMaxMessageCountDoesNotInventTheAuthoringCommentsFourMessageDefault()
    {
        var test = SemanticCacheTest.Create(body:
            """{"messages":[{"role":"user","content":"one"},{"role":"assistant","content":"two"},{"role":"user","content":"three"},{"role":"assistant","content":"four"},{"role":"user","content":"five"}]}""");
        test.Context.Response.Body.Content = "five turns";

        test.RunInbound();
        test.RunOutbound();
        test.Context.Response = new MockResponse();
        test.Context.Variables.Remove("after-semantic-lookup");
        test.RunInbound();

        SemanticCacheTest.AssertHit(test, "five turns");
    }

    [TestMethod]
    [DataRow("tenant")]
    [DataRow("ordering")]
    [DataRow("concatenation")]
    [DataRow("empty")]
    public void VaryByUsesCollisionSafeOrderedRuntimePartitions(string variation)
    {
        var clock = new CacheTestClock();
        var cache = SemanticCacheTest.SharedCache(clock);
        string[] first = ["a", "bc"];
        var second = variation switch
        {
            "tenant" => ["other", "bc"],
            "ordering" => new[] { "bc", "a" },
            "concatenation" => ["ab", "c"],
            "empty" => new[] { "" },
            _ => throw new ArgumentOutOfRangeException(nameof(variation))
        };
        var seed = SemanticCacheTest.Create(SemanticCacheTest.Config with { VaryBy = first }, cache, clock);
        seed.Context.Response.Body.Content = "private answer";
        seed.RunInbound();
        seed.RunOutbound();
        var miss = SemanticCacheTest.Create(SemanticCacheTest.Config with { VaryBy = second }, cache, clock);
        var hit = SemanticCacheTest.Create(SemanticCacheTest.Config with { VaryBy = first.ToArray() }, cache, clock);

        miss.RunInbound();
        hit.RunInbound();

        SemanticCacheTest.AssertMiss(miss);
        SemanticCacheTest.AssertHit(hit, "private answer");
    }

    [TestMethod]
    [DataRow("backend")]
    [DataRow("api")]
    [DataRow("operation")]
    [DataRow("endpoint")]
    [DataRow("model")]
    [DataRow("options")]
    [DataRow("system-filter")]
    public void DoesNotMixIncompatibleBackendApiEndpointModelOrPromptContracts(string partition)
    {
        var clock = new CacheTestClock();
        var cache = SemanticCacheTest.SharedCache(clock);
        var seed = SemanticCacheTest.Create(cache: cache, clock: clock,
            body: """{"model":"model-a","temperature":0,"messages":[{"role":"user","content":"question"}]}""");
        seed.Context.Response.Body.Content = "isolated answer";
        seed.RunInbound();
        seed.RunOutbound();
        var config = partition switch
        {
            "backend" => SemanticCacheTest.Config with { EmbeddingsBackendId = "other-backend" },
            "system-filter" => SemanticCacheTest.Config with { IgnoreSystemMessages = true },
            _ => SemanticCacheTest.Config
        };
        var body = partition switch
        {
            "model" => """{"model":"model-b","temperature":0,"messages":[{"role":"user","content":"question"}]}""",
            "options" => """{"model":"model-a","temperature":1,"messages":[{"role":"user","content":"question"}]}""",
            _ => seed.Context.Request.Body.Content
        };
        var test = SemanticCacheTest.Create(config, cache, clock, body: body);
        switch (partition)
        {
            case "api": test.Context.Api.Id = "another-api"; break;
            case "operation": test.Context.Operation.Id = "another-operation"; break;
            case "endpoint": test.Context.Request.OriginalUrl.Path = "/other-model/chat/completions"; break;
        }

        test.RunInbound();

        SemanticCacheTest.AssertMiss(test);
    }

    [TestMethod]
    public void CanonicalizesObjectPropertyOrderWithoutReorderingDialogOrVaryByValues()
    {
        var clock = new CacheTestClock();
        var cache = SemanticCacheTest.SharedCache(clock);
        var embeddings = new SemanticCacheTestEmbeddings();
        var seed = SemanticCacheTest.Create(cache: cache, clock: clock, embeddings: embeddings,
            body: """{"model":"test","options":{"b":2,"a":1},"messages":[{"role":"user","content":"question"}]}""");
        seed.Context.Response.Body.Content = "canonical";
        seed.RunInbound();
        seed.RunOutbound();
        var test = SemanticCacheTest.Create(cache: cache, clock: clock, embeddings: embeddings,
            body: """{"messages":[{"content":"question","role":"user"}],"options":{"a":1,"b":2},"model":"test"}""");

        test.RunInbound();

        SemanticCacheTest.AssertHit(test, "canonical");
        embeddings.Requests[0].Prompt.Should().Be(embeddings.Requests[1].Prompt);
    }

    [TestMethod]
    [DataRow("""{"messages":[{"role":"user","content":[{"type":"text","text":"question"}]}]}""")]
    [DataRow("""{"input":"question","instructions":"be helpful"}""")]
    [DataRow("""{"input":[{"type":"message","role":"user","content":[{"type":"input_text","text":"question"}]}]}""")]
    [DataRow("""{"system":"be helpful","messages":[{"role":"user","content":[{"type":"text","text":"question"}]}]}""")]
    [DataRow("""{"systemInstruction":{"parts":[{"text":"be helpful"}]},"contents":[{"role":"user","parts":[{"text":"question"}]}]}""")]
    public void SupportsDocumentedTextOnlyModelApiEnvelopes(string body)
    {
        var embeddings = new SemanticCacheTestEmbeddings();
        var test = SemanticCacheTest.Create(embeddings: embeddings, body: body);
        test.Context.Response.Body.Content = "text envelope";

        test.RunInbound();
        test.RunOutbound();
        test.Context.Response = new MockResponse();
        test.Context.Variables.Remove("after-semantic-lookup");
        test.RunInbound();

        SemanticCacheTest.AssertHit(test, "text envelope");
        embeddings.Requests[0].Prompt.Should().Contain("question");
        test.Context.Request.Body.Content.Should().Be(body);
        test.Context.Request.Body.Consumed.Should().BeFalse();
    }

    [TestMethod]
    [DataRow("""{"input":"question","instructions":"system instruction"}""")]
    [DataRow("""{"system":[{"type":"text","text":"system instruction"}],"messages":[{"role":"user","content":"question"},{"role":"assistant","content":"previous"}]}""")]
    [DataRow("""{"systemInstruction":{"parts":[{"text":"system instruction"}]},"contents":[{"role":"user","parts":[{"text":"question"}]},{"role":"model","parts":[{"text":"previous"}]}]}""")]
    public void SystemFilteringAndMessageLimitsApplyToEachTextEnvelope(string body)
    {
        var embeddings = new SemanticCacheTestEmbeddings();
        var test = SemanticCacheTest.Create(
            SemanticCacheTest.Config with { IgnoreSystemMessages = true, MaxMessageCount = 2 },
            embeddings: embeddings, body: body);

        test.RunInbound();

        embeddings.Requests.Should().ContainSingle().Which.Prompt.Should().NotContain("system instruction");
        SemanticCacheTest.AssertMiss(test);
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("not json")]
    [DataRow("[]")]
    [DataRow("{}")]
    [DataRow("""{"messages":null}""")]
    [DataRow("""{"messages":[]}""")]
    [DataRow("""{"messages":[null]}""")]
    [DataRow("""{"messages":[{"content":"question"}]}""")]
    [DataRow("""{"messages":[{"role":"user"}]}""")]
    [DataRow("""{"messages":[{"role":"user","content":42}]}""")]
    [DataRow("""{"messages":[{"role":"","content":"question"}]}""")]
    [DataRow("""{"messages":[{"role":42,"content":"question"}]}""")]
    [DataRow("""{"messages":[{"role":"user","content":""}]}""")]
    [DataRow("""{"messages":[{"role":"user","content":[]}]}""")]
    [DataRow("""{"messages":[{"role":"user","content":"question"}],"messages":[]}""")]
    [DataRow("""{"messages":[{"role":"user","role":"assistant","content":"question"}]}""")]
    [DataRow("""{"messages":[{"role":"user","content":"question"}],"options":{"temperature":0,"temperature":1}}""")]
    [DataRow("""{"messages":[{"role":"user","content":"question"}],"stream":"true"}""")]
    public void RejectsInvalidOrMissingPromptsWithoutEmbeddingOrCacheWrites(string body)
    {
        var embeddings = new SemanticCacheTestEmbeddings();
        var test = SemanticCacheTest.Create(embeddings: embeddings, body: body);

        var exception = Assert.ThrowsExactly<PolicyException>(() => test.RunInbound());

        exception.Policy.Should().Be(nameof(IInboundContext.LlmSemanticCacheLookup));
        exception.Section.Should().Be(nameof(IInboundContext));
        exception.InnerException.Should().NotBeNull();
        embeddings.Requests.Should().BeEmpty();
        test.SetupCacheStore().ExternalCache.Should().BeEmpty();
        test.Context.ResponseTerminated.Should().BeFalse();
    }

    [TestMethod]
    public void RejectsNullConfigWithTheActualLookupPolicyAndSection()
    {
        var test = new ExecutionTestDocument
        {
            InboundAction = context => context.LlmSemanticCacheLookup(null!)
        }.AsTestDocument();

        var exception = Assert.ThrowsExactly<PolicyException>(() => test.RunInbound());

        exception.Policy.Should().Be(nameof(IInboundContext.LlmSemanticCacheLookup));
        exception.Section.Should().Be(nameof(IInboundContext));
        exception.InnerException.Should().BeOfType<ArgumentException>();
    }

    [TestMethod]
    public void AMessageLimitSkipReplacesAnyEarlierPendingLookupWithoutStoringIt()
    {
        var test = SemanticCacheTest.Create(SemanticCacheTest.Config with { MaxMessageCount = 1 });
        test.Context.Response.Body.Content = "must not leak from earlier lookup";
        test.RunInbound();
        test.Context.Request.Body.Content =
            """{"messages":[{"role":"user","content":"one"},{"role":"assistant","content":"two"}]}""";

        test.RunInbound();
        test.RunOutbound();

        test.SetupCacheStore().ExternalCache.Should().BeEmpty();
    }

    [TestMethod]
    public void RejectsNoRemainingPromptAfterIgnoringSystemMessages()
    {
        var test = SemanticCacheTest.Create(SemanticCacheTest.Config with { IgnoreSystemMessages = true },
            body: """{"messages":[{"role":"system","content":"only system"}]}""");

        var exception = Assert.ThrowsExactly<PolicyException>(() => test.RunInbound());

        exception.InnerException.Should().BeOfType<ArgumentException>();
        exception.Message.Should().Contain("message");
    }

    [TestMethod]
    [DataRow("""{"messages":[{"role":"user","content":"question"}],"stream":true}""", "stream")]
    [DataRow("""{"messages":[{"role":"user","content":[{"type":"image_url","image_url":{"url":"https://example.test/image"}}]}]}""", "text")]
    [DataRow("""{"messages":[{"role":"assistant","content":null,"tool_calls":[{"id":"call","type":"function"}]}]}""", "tool")]
    [DataRow("""{"input":[{"type":"function_call","name":"tool","arguments":"{}"}]}""", "tool")]
    [DataRow("""{"contents":[{"role":"user","parts":[{"inlineData":{"mimeType":"image/png","data":"AA=="}}]}]}""", "text")]
    [DataRow("""{"prompt":"legacy completion"}""", "schema")]
    public void UnsupportedBufferedEmulatorFeaturesFailExplicitly(string body, string reason)
    {
        var embeddings = new SemanticCacheTestEmbeddings();
        var test = SemanticCacheTest.Create(embeddings: embeddings, body: body);

        var exception = Assert.ThrowsExactly<PolicyException>(() => test.RunInbound());

        exception.InnerException.Should().BeOfType<NotSupportedException>();
        exception.Message.Should().Contain(reason);
        embeddings.Requests.Should().BeEmpty();
    }

    [TestMethod]
    [DataRow("-0.0001")]
    [DataRow("1.0001")]
    public void RejectsOutOfRangeScoreThresholds(string threshold)
    {
        var test = SemanticCacheTest.Create(SemanticCacheTest.Config with
        {
            ScoreThreshold = decimal.Parse(threshold, CultureInfo.InvariantCulture)
        });

        var exception = Assert.ThrowsExactly<PolicyException>(() => test.RunInbound());

        exception.InnerException.Should().BeOfType<ArgumentOutOfRangeException>();
        exception.Message.Should().Contain("ScoreThreshold");
    }

    [TestMethod]
    [DataRow("backend", null)]
    [DataRow("backend", "")]
    [DataRow("backend", " ")]
    [DataRow("auth", null)]
    [DataRow("auth", "")]
    [DataRow("auth", "token")]
    [DataRow("auth", "user-assigned")]
    [DataRow("auth", "SYSTEM-ASSIGNED")]
    [DataRow("cache", "")]
    [DataRow("cache", " ")]
    [DataRow("vary", null)]
    public void RejectsInvalidRequiredBackendAuthenticationCacheAndVaryByInputs(string field, string? value)
    {
        var config = field switch
        {
            "backend" => SemanticCacheTest.Config with { EmbeddingsBackendId = value! },
            "auth" => SemanticCacheTest.Config with { EmbeddingsBackendAuth = value! },
            "cache" => SemanticCacheTest.Config with { CacheId = value },
            "vary" => SemanticCacheTest.Config with { VaryBy = [value!] },
            _ => throw new ArgumentOutOfRangeException(nameof(field))
        };
        var embeddings = new SemanticCacheTestEmbeddings();
        var test = SemanticCacheTest.Create(config, embeddings: embeddings);

        var exception = Assert.ThrowsExactly<PolicyException>(() => test.RunInbound());

        exception.InnerException.Should().BeAssignableTo<ArgumentException>();
        embeddings.Requests.Should().BeEmpty();
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void MissingEmbeddingsProviderFailsEvenWhenMessageLimitWouldSkip(bool limited)
    {
        var config = SemanticCacheTest.Config with { MaxMessageCount = limited ? 1U : 0 };
        var test = SemanticCacheTest.Create(config, configureEmbeddings: false,
            body: """{"messages":[{"role":"user","content":"one"},{"role":"assistant","content":"two"}]}""");

        var exception = Assert.ThrowsExactly<PolicyException>(() => test.RunInbound());

        exception.InnerException.Should().BeOfType<InvalidOperationException>();
        exception.Message.Should().Contain("ISemanticCacheEmbeddingProvider").And.Contain("embeddings");
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void MissingExternalCacheFailsInsteadOfFallingBackToInternal(bool limited)
    {
        var test = SemanticCacheTest.Create(
            SemanticCacheTest.Config with { MaxMessageCount = limited ? 1U : 0 },
            configureCache: false,
            body: """{"messages":[{"role":"user","content":"one"},{"role":"assistant","content":"two"}]}""");
        test.SetupCacheStore().WithInternalCacheValue("unrelated", "not semantic");

        var exception = Assert.ThrowsExactly<PolicyException>(() => test.RunInbound());

        exception.InnerException.Should().BeOfType<InvalidOperationException>();
        exception.Message.Should().Contain("external cache");
        test.SetupCacheStore().InternalCache.Should().ContainSingle();
    }

    [TestMethod]
    public void AnInjectedInternalOnlyCacheStoreDoesNotSatisfyAnExternalCacheRequirement()
    {
        var test = SemanticCacheTest.Create(cache: new CacheStore());

        var exception = Assert.ThrowsExactly<PolicyException>(() => test.RunInbound());

        exception.InnerException.Should().BeOfType<InvalidOperationException>();
        exception.Message.Should().Contain("external cache");
    }

    [TestMethod]
    public void NamedCacheRequiresItsExactRegistrationWithoutDefaultFallback()
    {
        var test = SemanticCacheTest.Create(SemanticCacheTest.Config with { CacheId = "missing-cache" },
            cache: SemanticCacheTest.SharedCache());

        var exception = Assert.ThrowsExactly<PolicyException>(() => test.RunInbound());

        exception.InnerException.Should().BeOfType<InvalidOperationException>();
        exception.Message.Should().Contain("missing-cache");
    }

    [TestMethod]
    public void KeyedEmbeddingAndExternalCacheRegistrationsTakePrecedence()
    {
        var clock = new CacheTestClock();
        var cache = SemanticCacheTest.SharedCache(clock);
        var keyed = new SemanticCacheTestEmbeddings();
        var unkeyed = new SemanticCacheTestEmbeddings
        {
            TaskFactory = _ => throw new InvalidOperationException("unkeyed embeddings must not execute")
        };
        var test = SemanticCacheTest.Create(cache: new FailingCache(), clock: clock, embeddings: unkeyed);
        test.Context.Services.Register<ICache>("external", cache)
            .Register<ISemanticCacheEmbeddingProvider>("embeddings", keyed);
        test.Context.Response.Body.Content = "keyed";

        test.RunInbound();
        test.RunOutbound();
        var hit = SemanticCacheTest.Create(cache: cache, clock: clock);
        hit.RunInbound();

        keyed.Requests.Should().ContainSingle();
        unkeyed.Requests.Should().BeEmpty();
        SemanticCacheTest.AssertHit(hit, "keyed");
    }

    [TestMethod]
    [DataRow("empty")]
    [DataRow("zero")]
    [DataRow("nan")]
    [DataRow("infinity")]
    [DataRow("null-result")]
    [DataRow("null-task")]
    [DataRow("failure")]
    public void InvalidOrFailedEmbeddingsAreNotCacheMisses(string scenario)
    {
        var embeddings = new SemanticCacheTestEmbeddings();
        switch (scenario)
        {
            case "empty": embeddings.Vector = []; break;
            case "zero": embeddings.Vector = [0, 0]; break;
            case "nan": embeddings.Vector = [double.NaN, 1]; break;
            case "infinity": embeddings.Vector = [double.PositiveInfinity, 1]; break;
            case "null-result": embeddings.TaskFactory = _ => Task.FromResult<IReadOnlyList<double>>(null!); break;
            case "null-task": embeddings.TaskFactory = _ => null!; break;
            case "failure": embeddings.TaskFactory = _ => throw new InvalidOperationException("embedding backend failed"); break;
        }
        var test = SemanticCacheTest.Create(embeddings: embeddings);

        var exception = Assert.ThrowsExactly<PolicyException>(() => test.RunInbound());

        exception.InnerException.Should().BeOfType<InvalidOperationException>();
        test.Context.Variables.Should().NotContainKey("after-semantic-lookup");
        test.SetupCacheStore().ExternalCache.Should().BeEmpty();
    }

    [TestMethod]
    public void ChangedEmbeddingDimensionsFailRatherThanMatchAnIncompatibleIndex()
    {
        var cache = SemanticCacheTest.SharedCache();
        var seed = SemanticCacheTest.Create(cache: cache);
        seed.RunInbound();
        seed.RunOutbound();
        var test = SemanticCacheTest.Create(cache: cache, embeddings:
            new SemanticCacheTestEmbeddings { Vector = [1, 0, 0] });

        var exception = Assert.ThrowsExactly<PolicyException>(() => test.RunInbound());

        exception.InnerException.Should().BeOfType<InvalidOperationException>();
        exception.Message.Should().Contain("dimension");
    }

    [TestMethod]
    [DataRow(-0.01)]
    [DataRow(2.01)]
    [DataRow(double.NaN)]
    [DataRow(double.PositiveInfinity)]
    public void InvalidSimilarityScoresFailExplicitly(double score)
    {
        var cache = SemanticCacheTest.SharedCache();
        var seed = SemanticCacheTest.Create(cache: cache);
        seed.RunInbound();
        seed.RunOutbound();
        var test = SemanticCacheTest.Create(cache: cache);
        test.Context.Services.Register<ISemanticCacheSimilarity>(new SemanticCacheTestSimilarity((_, _) => score));

        var exception = Assert.ThrowsExactly<PolicyException>(() => test.RunInbound());

        exception.InnerException.Should().BeOfType<InvalidOperationException>();
        exception.Message.Should().Contain("distance");
    }

    [TestMethod]
    [DataRow("cache-failure")]
    [DataRow("cache-shape")]
    [DataRow("cache-null-task")]
    [DataRow("similarity-failure")]
    public void DependencyFailuresPreservePolicyErrorContext(string scenario)
    {
        var cache = new SemanticCacheTestCache();
        var seed = SemanticCacheTest.Create(cache: cache);
        seed.RunInbound();
        seed.RunOutbound();
        switch (scenario)
        {
            case "cache-failure": cache.ReadOverride = _ => throw new InvalidOperationException("cache read failed"); break;
            case "cache-shape": cache.ReadOverride = _ => Task.FromResult<object?>("invalid semantic index"); break;
            case "cache-null-task": cache.ReadOverride = _ => null!; break;
        }
        var test = SemanticCacheTest.Create(cache: cache);
        if (scenario == "similarity-failure")
        {
            test.Context.Services.Register<ISemanticCacheSimilarity>(
                new SemanticCacheTestSimilarity((_, _) => throw new InvalidOperationException("similarity failed")));
        }

        var exception = Assert.ThrowsExactly<PolicyException>(() => test.RunInbound());

        exception.Policy.Should().Be(nameof(IInboundContext.LlmSemanticCacheLookup));
        exception.Section.Should().Be(nameof(IInboundContext));
        exception.InnerException.Should().BeOfType<InvalidOperationException>();
        exception.PolicyArgs.Should().ContainSingle().Which.Should().BeOfType<SemanticCacheLookupConfig>();
        test.Context.ResponseTerminated.Should().BeFalse();
    }

    [TestMethod]
    public void EvaluatesEveryExpressionAllowedLookupPropertyAtRuntime()
    {
        var clock = new CacheTestClock();
        var cache = SemanticCacheTest.SharedCache(clock);
        var embeddings = new SemanticCacheTestEmbeddings();
        var test = new ExecutionTestDocument
        {
            InboundAction = context =>
            {
                var expression = context.ExpressionContext;
                context.LlmSemanticCacheLookup(new SemanticCacheLookupConfig
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
            OutboundAction = context => context.LlmSemanticCacheStore(10)
        }.AsTestDocument();
        SemanticCacheTest.Configure(test, cache, clock, embeddings);
        test.Context.Request.Body.Content =
            """{"messages":[{"role":"system","content":"system text"},{"role":"user","content":"question"}]}""";
        test.Context.Variables["threshold"] = 0.05M;
        test.Context.Variables["backend"] = "expression-backend";
        test.Context.Variables["auth"] = "system-assigned";
        test.Context.Variables["ignore"] = true;
        test.Context.Variables["max"] = 1U;
        test.Context.Variables["tenant"] = "first";
        test.Context.Response.Body.Content = "expression answer";

        test.RunInbound();
        test.RunOutbound();
        test.Context.Variables["tenant"] = "second";
        test.Context.Response = new MockResponse();
        test.RunInbound();
        SemanticCacheTest.AssertMiss(test);
        test.Context.Variables["tenant"] = "first";
        test.Context.Variables.Remove("after-semantic-lookup");
        test.RunInbound();

        SemanticCacheTest.AssertHit(test, "expression answer");
        embeddings.Requests.Should().HaveCount(3);
        embeddings.Requests.Should().OnlyContain(request => request.BackendId == "expression-backend"
            && request.Authentication == "system-assigned" && !request.Prompt.Contains("system text"));
    }

    [TestMethod]
    public void CallbackAndPredicateOverridesRemainIndependentFromAzureAlias()
    {
        var test = new ExecutionTestDocument
        {
            InboundAction = context =>
            {
                context.LlmSemanticCacheLookup(SemanticCacheTest.Config with { EmbeddingsBackendId = "first" });
                context.LlmSemanticCacheLookup(SemanticCacheTest.Config with { EmbeddingsBackendId = "second" });
                context.AzureOpenAiSemanticCacheLookup(SemanticCacheTest.Config);
            }
        }.AsTestDocument();
        var callbacks = new List<string>();
        test.SetupInbound().LlmSemanticCacheLookup((_, config) => config.EmbeddingsBackendId == "never")
            .WithCallback((_, _) => Assert.Fail("Wrong predicate."));
        test.SetupInbound().LlmSemanticCacheLookup((_, config) => config.EmbeddingsBackendId == "first")
            .WithCallback((_, _) => callbacks.Add("first"));
        test.SetupInbound().LlmSemanticCacheLookup((_, config) => config.EmbeddingsBackendId == "second")
            .WithCallback((_, _) => callbacks.Add("second"));
        test.SetupInbound().AzureOpenAiSemanticCacheLookup().WithCallback((_, _) => callbacks.Add("azure"));

        test.RunInbound();

        callbacks.Should().Equal("first", "second", "azure");
        test.SetupCacheStore().ExternalCache.Should().BeEmpty();
    }

    [TestMethod]
    public void UnmatchedLookupPredicateExecutesRealBehavior()
    {
        var test = SemanticCacheTest.Create();
        test.SetupInbound().LlmSemanticCacheLookup((_, _) => false)
            .WithCallback((_, _) => Assert.Fail("Unmatched callback."));
        test.Context.Response.Body.Content = "default behavior";
        test.RunInbound();
        test.RunOutbound();
        test.Context.Response = new MockResponse();
        test.Context.Variables.Remove("after-semantic-lookup");

        test.RunInbound();

        SemanticCacheTest.AssertHit(test, "default behavior");
    }

    [TestMethod]
    public void CallbackCanSimulateAHitAndTerminateCoordinatedExecutionWithoutDependencies()
    {
        var calls = new List<string>();
        var pipeline = PolicyPipelineBuilder.Create()
            .AddPolicy(PolicyScope.Api, new ExecutionTestDocument
            {
                InboundAction = context =>
                {
                    context.LlmSemanticCacheLookup(SemanticCacheTest.Config);
                    calls.Add("after");
                },
                BackendAction = _ => calls.Add("backend"),
                OutboundAction = _ => calls.Add("outbound")
            }).Build();
        var setup = new TestDocument(new ExecutionTestDocument()) { Context = pipeline.Context };
        setup.SetupInbound().LlmSemanticCacheLookup().WithCallback((context, _) =>
        {
            context.Response.Body.Content = "callback hit";
            throw new FinishSectionProcessingException();
        });

        pipeline.RunAll();

        calls.Should().BeEmpty();
        pipeline.Context.ResponseTerminated.Should().BeTrue();
        pipeline.Context.Response.Body.Content.Should().Be("callback hit");
    }

    [TestMethod]
    [DataRow(false, false)]
    [DataRow(false, true)]
    [DataRow(true, false)]
    [DataRow(true, true)]
    public void RealHitStopsFlatAndNestedScopesBackendOutboundAndOnError(bool azure, bool nested)
    {
        var clock = new CacheTestClock();
        var cache = SemanticCacheTest.SharedCache(clock);
        var seed = SemanticCacheTest.Create(cache: cache, clock: clock, azureLookup: azure, azureStore: azure);
        seed.Context.Response.Body.Content = "terminal hit";
        seed.RunInbound();
        seed.RunOutbound();
        var calls = new List<string>();
        var pipeline = PolicyPipelineBuilder.Create()
            .AddPolicy(PolicyScope.Global, new ExecutionTestDocument
            {
                InboundAction = context =>
                {
                    calls.Add("global-before");
                    if (nested) context.Base();
                    calls.Add("global-after");
                }
            })
            .AddPolicy(PolicyScope.Product, new ExecutionTestDocument
            {
                InboundAction = context =>
                {
                    calls.Add("lookup");
                    SemanticCacheTest.Lookup(context, SemanticCacheTest.Config, azure);
                    calls.Add("after");
                    if (nested) context.Base();
                }
            })
            .AddPolicy(PolicyScope.Operation, new ExecutionTestDocument
            {
                InboundAction = _ => calls.Add("operation"),
                BackendAction = _ => calls.Add("backend"),
                OutboundAction = _ => calls.Add("outbound"),
                OnErrorAction = _ => calls.Add("on-error")
            }).Build();
        var setup = new TestDocument(new ExecutionTestDocument()) { Context = pipeline.Context };
        SemanticCacheTest.Configure(setup, cache, clock);

        if (nested) pipeline.RunAllNested();
        else pipeline.RunAll();
        pipeline.RunAll();
        pipeline.RunAllNested();
        pipeline.RunOnError();
        pipeline.RunOnErrorNested();

        calls.Should().Equal(nested ? ["global-before", "lookup"] : ["global-before", "global-after", "lookup"]);
        pipeline.Context.ResponseTerminated.Should().BeTrue();
        pipeline.Context.Response.Body.Content.Should().Be("terminal hit");
    }

    [TestMethod]
    [DynamicData(nameof(SemanticCacheCompatibilityTest.UnsupportedRequests), typeof(SemanticCacheCompatibilityTest))]
    public void RejectsToolAndAudioFeaturesBeforeEmbeddingOrCacheAccess(string scenario, string body, string reason)
    {
        SemanticCacheCompatibilityTest.AssertRejectedRequest(scenario, body, reason, false);
    }

    [TestMethod]
    [DynamicData(nameof(SemanticCacheCompatibilityTest.AcceptedRequests), typeof(SemanticCacheCompatibilityTest))]
    public void AcceptsDisabledToolsAndGenuinelyTextOnlyRequests(string scenario, string body)
    {
        SemanticCacheCompatibilityTest.AssertAcceptedRequest(scenario, body, false);
    }

    [TestMethod]
    [DynamicData(nameof(SemanticCacheCompatibilityTest.VertexTextMetadata), typeof(SemanticCacheCompatibilityTest))]
    public void AcceptsAndPreservesDocumentedNonModalVertexPartMetadata(string scenario, string body, string expected)
    {
        SemanticCacheCompatibilityTest.AssertVertexMetadata(scenario, body, expected, false);
    }

    [TestMethod]
    [DynamicData(nameof(SemanticCacheCompatibilityTest.InvalidVertexMetadata), typeof(SemanticCacheCompatibilityTest))]
    public void InvalidVertexMetadataFailsBeforeEmbedding(string scenario, string body)
    {
        SemanticCacheCompatibilityTest.AssertInvalidVertexMetadata(scenario, body, false);
    }

    [TestMethod]
    [DataRow("original", ":streamGenerateContent")]
    [DataRow("current", ":streamGenerateContent")]
    [DataRow("backend", ":streamGenerateContent")]
    [DataRow("current", "%3AstreamGenerateContent")]
    [DataRow("original", ":streamRawPredict")]
    [DataRow("current", ":serverStreamingPredict")]
    public void StreamingRpcUrlsFailWithoutABodyStreamFlagOrSseHeader(string location, string method)
    {
        SemanticCacheCompatibilityTest.AssertStreamingLookup(location, method, false);
    }

    [TestMethod]
    public void NonstreamingVertexEndpointAndModelNamesContainingStreamStillRoundTrip()
    {
        SemanticCacheCompatibilityTest.AssertNonstreamingVertex(false);
    }

    [TestMethod]
    [DynamicData(nameof(SemanticCacheCompatibilityTest.UnsupportedResponses), typeof(SemanticCacheCompatibilityTest))]
    public void DoesNotReplayAnIncompatibleSnapshotFromAnOlderCache(string scenario, string body, string contentType, string reason)
    {
        SemanticCacheCompatibilityTest.AssertRejectedCachedResponse(scenario, body, contentType, reason, false);
    }

    [TestMethod]
    [DynamicData(nameof(SemanticCacheCompatibilityTest.ResponseMetadataAndText), typeof(SemanticCacheCompatibilityTest))]
    public void CachedResponsesAcceptInertMetadataAndHonorTextContentTypes(string scenario, string body, string? contentType)
    {
        SemanticCacheCompatibilityTest.AssertAcceptedSnapshot(scenario, body, contentType, false);
    }

    [TestMethod]
    [DynamicData(nameof(SemanticCacheCompatibilityTest.ActiveResponseTools), typeof(SemanticCacheCompatibilityTest))]
    public void CachedResponsesStillRejectForcedToolsAndActualCalls(string scenario, string body)
    {
        SemanticCacheCompatibilityTest.AssertRejectedCachedResponse(scenario, body, "application/json", "tool", false);
    }

    [TestMethod]
    [DynamicData(nameof(SemanticCacheCompatibilityTest.CompletionChunks), typeof(SemanticCacheCompatibilityTest))]
    public void CachedUsageOnlyChunksRejectTheStreamingDiscriminator(string scenario, string body, string? contentType)
    {
        SemanticCacheCompatibilityTest.AssertRejectedCachedResponse(scenario, body, contentType, "stream", false);
    }

    [TestMethod]
    [DynamicData(nameof(SemanticCacheCompatibilityTest.MalformedDeclaredJson), typeof(SemanticCacheCompatibilityTest))]
    public void MalformedDeclaredJsonSnapshotsFailInsteadOfReplayingText(string scenario, string body, string contentType)
    {
        SemanticCacheCompatibilityTest.AssertMalformedJson(scenario, body, contentType, false, cached: true);
    }
}

internal static class SemanticCacheTest
{
    public static SemanticCacheLookupConfig Config => new()
    {
        ScoreThreshold = 0.05M,
        EmbeddingsBackendId = "embeddings",
        EmbeddingsBackendAuth = "system-assigned"
    };

    public static string Prompt(string content) =>
        JsonSerializer.Serialize(new { messages = new[] { new { role = "user", content } } });

    public static CacheStore SharedCache(TimeProvider? clock = null) =>
        new CacheStore(clock ?? TimeProvider.System).WithExternalCacheSetup();

    public static TestDocument Create(
        SemanticCacheLookupConfig? config = null,
        ICache? cache = null,
        CacheTestClock? clock = null,
        SemanticCacheTestEmbeddings? embeddings = null,
        bool azureLookup = false,
        bool azureStore = false,
        uint duration = 10,
        bool configureCache = true,
        bool configureEmbeddings = true,
        string? body = null)
    {
        var test = new ExecutionTestDocument
        {
            InboundAction = context =>
            {
                Lookup(context, config ?? Config, azureLookup);
                context.SetVariable("after-semantic-lookup", true);
            },
            OutboundAction = context =>
            {
                Store(context, duration, azureStore);
                context.SetVariable("after-semantic-store", true);
            }
        }.AsTestDocument();
        Configure(test, cache, clock, embeddings, configureCache, configureEmbeddings);
        test.Context.Request.Body.Content = body ?? Prompt("question");
        return test;
    }

    public static void Configure(
        TestDocument test, ICache? cache = null, TimeProvider? clock = null,
        ISemanticCacheEmbeddingProvider? embeddings = null, bool configureCache = true, bool configureEmbeddings = true)
    {
        test.Context.Request = new MockRequest(new Uri("https://example.test/models/chat/completions?api-version=1"))
        {
            Method = "POST"
        };
        test.Context.Request.Body.Content = Prompt("question");
        if (clock is not null) test.Context.Services.Register<TimeProvider>(clock);
        if (cache is not null) test.Context.Services.Register<ICache>(cache);
        else if (configureCache) test.SetupCacheStore().WithExternalCacheSetup();
        if (configureEmbeddings)
        {
            test.Context.Services.Register<ISemanticCacheEmbeddingProvider>(embeddings ?? new SemanticCacheTestEmbeddings());
        }
    }

    public static void Lookup(IInboundContext context, SemanticCacheLookupConfig config, bool azure)
    {
        if (azure) context.AzureOpenAiSemanticCacheLookup(config);
        else context.LlmSemanticCacheLookup(config);
    }

    public static void Store(IOutboundContext context, uint duration, bool azure)
    {
        if (azure) context.AzureOpenAiSemanticCacheStore(duration);
        else context.LlmSemanticCacheStore(duration);
    }

    public static void AssertHit(TestDocument test, string? body)
    {
        test.Context.ResponseTerminated.Should().BeTrue();
        test.Context.Response.Body.Content.Should().Be(body);
        test.Context.Variables.Should().NotContainKey("after-semantic-lookup");
    }

    public static void AssertMiss(TestDocument test)
    {
        test.Context.ResponseTerminated.Should().BeFalse();
        test.Context.Variables["after-semantic-lookup"].Should().Be(true);
    }

    public static void AssertOutcome(TestDocument test, bool hit, string body)
    {
        if (hit) AssertHit(test, body);
        else
        {
            AssertMiss(test);
            test.Context.Response.Body.Content.Should().BeNull();
        }
    }
}

internal sealed class SemanticCacheTestEmbeddings(
    Func<SemanticCacheEmbeddingRequest, IReadOnlyList<double>>? vectorFactory = null) : ISemanticCacheEmbeddingProvider
{
    public List<SemanticCacheEmbeddingRequest> Requests { get; } = [];
    public IReadOnlyList<double> Vector { get; set; } = new double[] { 1, 0 };
    public Func<SemanticCacheEmbeddingRequest, Task<IReadOnlyList<double>>>? TaskFactory { get; set; }

    public Task<IReadOnlyList<double>> GenerateAsync(
        SemanticCacheEmbeddingRequest request, CancellationToken cancellationToken = default)
    {
        Requests.Add(request);
        return TaskFactory is not null ? TaskFactory(request) : Task.FromResult(vectorFactory?.Invoke(request) ?? Vector);
    }
}

internal sealed class SemanticCacheTestSimilarity(
    Func<IReadOnlyList<double>, IReadOnlyList<double>, double> measure) : ISemanticCacheSimilarity
{
    public int Calls { get; private set; }

    public double GetDistance(IReadOnlyList<double> first, IReadOnlyList<double> second)
    {
        Calls++;
        return measure(first, second);
    }
}

internal sealed class SemanticCacheTestCache(TimeProvider? clock = null) : ICache
{
    private readonly ICache _inner = SemanticCacheTest.SharedCache(clock);
    public Func<string, Task<object?>>? ReadOverride { get; set; }
    public bool FailUpdates { get; set; }
    public bool NullUpdateTask { get; set; }
    public bool NullUpdateResult { get; set; }
    public bool InvalidUpdateValue { get; set; }
    public object? LastValue { get; private set; }
    public TimeSpan LastTtl { get; private set; }
    public List<string> Reads { get; } = [];
    public List<string> Updates { get; } = [];

    public Task<object?> GetAsync(string key, CancellationToken ct = default)
    {
        Reads.Add(key);
        return ReadOverride is null ? _inner.GetAsync(key, ct) : ReadOverride(key);
    }

    public Task SetAsync(string key, object value, TimeSpan ttl, CancellationToken ct = default) =>
        _inner.SetAsync(key, value, ttl, ct);

    public Task RemoveAsync(string key, CancellationToken ct = default) => _inner.RemoveAsync(key, ct);

    public Task<CacheValueResult> GetOrCreateAsync(
        string key, TimeSpan expiresAfter, TimeSpan? refreshAfter,
        Func<object?, CancellationToken, Task<object?>> valueFactory, CancellationToken ct = default) =>
        _inner.GetOrCreateAsync(key, expiresAfter, refreshAfter, valueFactory, ct);

    public Task<CacheValueResult> GetOrCreateWithDynamicTtlAsync(
        string key, Func<object?, CancellationToken, Task<CacheValueFactoryResult>> valueFactory,
        bool forceRefresh = false, CancellationToken ct = default)
    {
        Updates.Add(key);
        if (FailUpdates) throw new InvalidOperationException("semantic cache write failed");
        if (NullUpdateTask) return null!;
        if (NullUpdateResult) return Task.FromResult<CacheValueResult>(null!);
        if (InvalidUpdateValue) return Task.FromResult(new CacheValueResult("invalid index", false, true));
        return _inner.GetOrCreateWithDynamicTtlAsync(key, async (previous, cancellation) =>
        {
            var result = await valueFactory(previous, cancellation);
            LastValue = result.Value;
            LastTtl = result.ExpiresAfter;
            return result;
        }, forceRefresh, ct);
    }
}

internal static class SemanticCacheCompatibilityTest
{
    private const string VertexRequest = """{"contents":[{"role":"user","parts":[{"text":"question"}]}]}""";
    private const string TextResponse = """{"choices":[{"message":{"role":"assistant","content":"text answer","audio":null,"tool_calls":[]},"finish_reason":"stop"}]}""";

    public static IEnumerable<object[]> UnsupportedRequests()
    {
        yield return ["function declaration", ChatOptions("""{"tools":[{"type":"function","function":{"name":"lookup"}}]}"""), "tool"];
        yield return ["declarations even with choice none", ChatOptions("""{"tools":[{"type":"function","function":{"name":"lookup"}}],"tool_choice":"none"}"""), "tool"];
        yield return ["forced choice", ChatOptions("""{"tool_choice":"required"}"""), "tool"];
        yield return ["automatic choice", ChatOptions("""{"tool_choice":"auto"}"""), "tool"];
        yield return ["named choice", ChatOptions("""{"tool_choice":{"type":"function","function":{"name":"lookup"}}}"""), "tool"];
        yield return ["legacy functions", ChatOptions("""{"functions":[{"name":"lookup"}]}"""), "tool"];
        yield return ["legacy function choice", ChatOptions("""{"function_call":{"name":"lookup"}}"""), "tool"];
        yield return ["anthropic tools", """{"messages":[{"role":"user","content":"question"}],"tools":[{"name":"lookup","input_schema":{"type":"object"}}]}""", "tool"];
        yield return ["anthropic forced choice", ChatOptions("""{"tool_choice":{"type":"any"}}"""), "tool"];
        yield return ["responses built-in tool", """{"input":"question","tools":[{"type":"web_search"}]}""", "tool"];
        yield return ["responses forced tool", """{"input":"question","tool_choice":{"type":"function","name":"lookup"}}""", "tool"];
        yield return ["vertex declaration", VertexOptions("""{"tools":[{"functionDeclarations":[{"name":"lookup"}]}]}"""), "tool"];
        yield return ["vertex forced choice", VertexOptions("""{"toolConfig":{"functionCallingConfig":{"mode":"ANY"}}}"""), "tool"];
        yield return ["vertex automatic choice", VertexOptions("""{"toolConfig":{"functionCallingConfig":{"mode":"AUTO"}}}"""), "tool"];
        yield return ["text-bearing tool history", """{"messages":[{"role":"assistant","content":"text explanation","tool_calls":[{"type":"function","function":{"name":"lookup","arguments":"{}"}}]}]}""", "tool"];
        yield return ["text-bearing legacy history", """{"messages":[{"role":"assistant","content":"text explanation","function_call":{"name":"lookup","arguments":"{}"}}]}""", "tool"];
        yield return ["tool response turn", """{"messages":[{"role":"tool","content":"text result","tool_call_id":"call-1"}]}""", "tool"];
        yield return ["tool history reference", """{"messages":[{"role":"assistant","content":"text result","tool_call_id":"call-1"}]}""", "tool"];
        yield return ["text-bearing assistant audio history", """{"messages":[{"role":"user","content":"question"},{"role":"assistant","content":"transcript","audio":{"id":"audio-1"}}]}""", "audio"];
        yield return ["responses text-bearing audio history", """{"input":[{"type":"message","role":"assistant","content":[{"type":"input_text","text":"transcript"}],"audio":{"id":"audio-1"}}]}""", "audio"];
        yield return ["audio output modalities", ChatOptions("""{"modalities":["text","audio"]}"""), "audio"];
        yield return ["audio output config", ChatOptions("""{"audio":{"voice":"alloy","format":"wav"}}"""), "audio"];
        yield return ["audio on a text content block", """{"messages":[{"role":"user","content":[{"type":"text","text":"question","audio":{"id":"audio-1"}}]}]}""", "audio"];
        yield return ["image on a text content block", """{"messages":[{"role":"user","content":[{"type":"text","text":"question","image_url":{"url":"https://example.test/image"}}]}]}""", "text"];
        yield return ["vertex audio output", VertexOptions("""{"generationConfig":{"responseModalities":["TEXT","AUDIO"]}}"""), "audio"];
        yield return ["vertex speech config", VertexOptions("""{"generationConfig":{"speechConfig":{"voiceConfig":{"prebuiltVoiceConfig":{"voiceName":"voice"}}}}}"""), "audio"];
        yield return ["vertex media on a text part", VertexPart("""{"text":"Hello","thought":false,"inlineData":{"mimeType":"audio/wav","data":"AA=="}}"""), "text"];
        yield return ["vertex tool on a text part", VertexPart("""{"text":"Hello","thought":false,"functionCall":{"name":"lookup","args":{}}}"""), "tool"];
        yield return ["vertex video metadata is not text metadata", VertexPart("""{"text":"Hello","thought":false,"videoMetadata":{"fps":1}}"""), "text"];
        yield return ["vertex media resolution is not text metadata", VertexPart("""{"text":"Hello","thought":false,"mediaResolution":{"level":"MEDIA_RESOLUTION_LOW"}}"""), "text"];
    }

    public static IEnumerable<object[]> AcceptedRequests()
    {
        yield return ["ordinary text", SemanticCacheTest.Prompt("question")];
        yield return ["empty tools", ChatOptions("""{"tools":[],"tool_choice":"none"}""")];
        yield return ["unset tools", ChatOptions("""{"tools":null,"tool_choice":null}""")];
        yield return ["empty legacy functions", ChatOptions("""{"functions":[],"function_call":"none"}""")];
        yield return ["text-only modality", ChatOptions("""{"modalities":["text"],"audio":null}""")];
        yield return ["text-bearing assistant without tool or audio state", """{"messages":[{"role":"user","content":"question"},{"role":"assistant","content":"text answer","name":"assistant-name","audio":null,"tool_calls":[],"function_call":null}]}"""];
        yield return ["topic words are not active features", SemanticCacheTest.Prompt("Explain tools, tool_choice, audio and thoughtSignature as text.")];
        yield return ["ordinary metadata is not tool configuration", ChatOptions("""{"metadata":{"tool_choice":"required","audio":"label"}}""")];
        yield return ["responses tool choice disabled", """{"input":"question","tools":[],"tool_choice":"none"}"""];
        yield return ["vertex tool choice disabled", VertexOptions("""{"tools":[],"toolConfig":{"functionCallingConfig":{"mode":"NONE"}}}""")];
        yield return ["vertex text output", VertexOptions("""{"generationConfig":{"responseModalities":["TEXT"]}}""")];
    }

    public static IEnumerable<object[]> VertexTextMetadata()
    {
        yield return ["thought false", VertexPart("""{"text":"Hello","thought":false}"""), "\"thought\":false"];
        yield return ["thought true", VertexPart("""{"text":"Hello","thought":true}"""), "\"thought\":true"];
        yield return ["thought signature", VertexPart("""{"text":"Hello","thoughtSignature":"c2lnbmF0dXJl"}"""), "\"thoughtSignature\":\"c2lnbmF0dXJl\""];
        yield return ["thought flag and signature", VertexPart("""{"thoughtSignature":"c2ln","thought":false,"text":"Hello"}"""), "\"thoughtSignature\":\"c2ln\""];
        yield return ["system instruction metadata", """{"systemInstruction":{"parts":[{"text":"instruction","thought":false}]},"contents":[{"role":"user","parts":[{"text":"Hello"}]}]}""", "\"thought\":false"];
    }

    public static IEnumerable<object[]> InvalidVertexMetadata()
    {
        yield return ["thought must be boolean", VertexPart("""{"text":"Hello","thought":"false"}""")];
        yield return ["thought must not be numeric", VertexPart("""{"text":"Hello","thought":0}""")];
        yield return ["signature must be string", VertexPart("""{"text":"Hello","thoughtSignature":42}""")];
        yield return ["signature must not be object", VertexPart("""{"text":"Hello","thoughtSignature":{"data":"c2ln"}}""")];
    }

    public static IEnumerable<object[]> UnsupportedResponses()
    {
        yield return ["text-bearing OpenAI tool call", """{"choices":[{"message":{"role":"assistant","content":"text explanation","tool_calls":[{"type":"function","function":{"name":"lookup","arguments":"{}"}}]},"finish_reason":"tool_calls"}]}""", "application/json", "tool"];
        yield return ["OpenAI tool finish reason", """{"choices":[{"message":{"role":"assistant","content":"text explanation"},"finish_reason":"tool_calls"}]}""", "application/json", "tool"];
        yield return ["legacy function result", """{"choices":[{"message":{"role":"assistant","content":"text explanation","function_call":{"name":"lookup","arguments":"{}"}},"finish_reason":"function_call"}]}""", "application/json", "tool"];
        yield return ["OpenAI audio with transcript", """{"choices":[{"message":{"role":"assistant","content":"transcript","audio":{"id":"audio-1","data":"AA=="}},"finish_reason":"stop"}]}""", "application/json", "audio"];
        yield return ["Responses function call", """{"output":[{"type":"function_call","name":"lookup","arguments":"{}"},{"type":"message","role":"assistant","content":[{"type":"output_text","text":"explanation"}]}]}""", "application/json", "tool"];
        yield return ["Responses audio output", """{"output":[{"type":"message","role":"assistant","content":[{"type":"output_text","text":"transcript"},{"type":"output_audio","data":"AA=="}]}]}""", "application/json", "audio"];
        yield return ["Anthropic tool use", """{"type":"message","role":"assistant","content":[{"type":"text","text":"explanation"},{"type":"tool_use","id":"tool-1","name":"lookup","input":{}}],"stop_reason":"tool_use"}""", "application/json", "tool"];
        yield return ["Vertex function call", """{"candidates":[{"content":{"role":"model","parts":[{"text":"explanation","functionCall":{"name":"lookup","args":{}}}]}}]}""", "application/json", "tool"];
        yield return ["Vertex audio part with text", """{"candidates":[{"content":{"role":"model","parts":[{"text":"transcript","inlineData":{"mimeType":"audio/wav","data":"AA=="}}]}}]}""", "application/json", "text"];
        yield return ["JSON stream chunk array", """[{"candidates":[{"content":{"parts":[{"text":"first"}]}}]},{"candidates":[{"content":{"parts":[{"text":"second"}]}}]}]""", "application/json", "stream"];
        yield return ["audio content type", "opaque waveform", "AUDIO/WAV; charset=binary", "audio"];
        yield return ["image content type", "opaque image", "image/png", "text"];
    }

    public static IEnumerable<object[]> AcceptedResponses()
    {
        yield return ["ordinary buffered text", "text answer"];
        yield return ["OpenAI empty tool state and no audio", TextResponse];
        yield return ["Responses text", """{"output":[{"type":"message","role":"assistant","content":[{"type":"output_text","text":"text answer"}]}]}"""];
        yield return ["Anthropic text", """{"type":"message","role":"assistant","content":[{"type":"text","text":"text answer"}],"stop_reason":"end_turn"}"""];
        yield return ["Vertex text metadata", """{"candidates":[{"content":{"role":"model","parts":[{"text":"text answer","thought":false,"thoughtSignature":"c2ln"}]}}]}"""];
        yield return ["feature names mentioned as text", """{"choices":[{"message":{"role":"assistant","content":"Tool calls and audio are topics, not active payloads."},"finish_reason":"stop"}]}"""];
    }

    public static void AssertRejectedRequest(string scenario, string body, string reason, bool azure)
    {
        var cache = new SemanticCacheTestCache();
        var embeddings = new SemanticCacheTestEmbeddings
        {
            TaskFactory = _ => throw new InvalidOperationException("Unsupported requests must not reach embeddings.")
        };
        var test = SemanticCacheTest.Create(
            SemanticCacheTest.Config with { MaxMessageCount = 1, IgnoreSystemMessages = true },
            cache: cache, embeddings: embeddings, azureLookup: azure, body: body);
        test.Context.Response.Body.Content = "untouched response";
        test.Context.Response.Headers["X-Caller"] = ["untouched"];

        var exception = Assert.ThrowsExactly<PolicyException>(() => test.RunInbound(), scenario);

        exception.Policy.Should().Be(azure
            ? nameof(IInboundContext.AzureOpenAiSemanticCacheLookup)
            : nameof(IInboundContext.LlmSemanticCacheLookup));
        exception.Section.Should().Be(nameof(IInboundContext));
        exception.InnerException.Should().BeOfType<NotSupportedException>(scenario);
        exception.Message.Should().Contain(reason, scenario);
        embeddings.Requests.Should().BeEmpty(scenario);
        cache.Reads.Should().BeEmpty(scenario);
        cache.Updates.Should().BeEmpty(scenario);
        test.Context.Response.Body.Content.Should().Be("untouched response");
        test.Context.Response.Headers["X-Caller"].Should().Equal("untouched");
        test.Context.ResponseTerminated.Should().BeFalse();
        test.Context.Variables.Should().NotContainKey("after-semantic-lookup");
    }

    public static void AssertAcceptedRequest(string scenario, string body, bool azure)
    {
        var cache = new SemanticCacheTestCache();
        var embeddings = new SemanticCacheTestEmbeddings();
        var seed = SemanticCacheTest.Create(cache: cache, embeddings: embeddings,
            azureLookup: azure, azureStore: azure, body: body);
        seed.Context.Response.Body.Content = TextResponse;
        seed.Context.Response.Headers["Content-Type"] = ["application/json"];
        seed.RunInbound();
        seed.RunOutbound();
        var hit = SemanticCacheTest.Create(cache: cache, embeddings: embeddings, azureLookup: !azure, body: body);

        hit.RunInbound();

        SemanticCacheTest.AssertHit(hit, TextResponse);
        embeddings.Requests.Should().HaveCount(2, scenario);
        cache.Updates.Should().ContainSingle(scenario);
    }

    public static void AssertVertexMetadata(string scenario, string body, string expected, bool azure)
    {
        var cache = new SemanticCacheTestCache();
        var embeddings = new SemanticCacheTestEmbeddings();
        var seed = SemanticCacheTest.Create(cache: cache, embeddings: embeddings,
            azureLookup: azure, azureStore: azure, body: body);
        seed.Context.Response.Body.Content = "vertex text answer";
        seed.RunInbound();
        seed.RunOutbound();
        var hit = SemanticCacheTest.Create(cache: cache, embeddings: embeddings, azureLookup: !azure, body: body);

        hit.RunInbound();

        SemanticCacheTest.AssertHit(hit, "vertex text answer");
        embeddings.Requests.Should().HaveCount(2, scenario);
        embeddings.Requests.Should().OnlyContain(request => request.Prompt.Contains(expected), scenario);
        seed.Context.Request.Body.Content.Should().Be(body);
        seed.Context.Request.Body.Consumed.Should().BeFalse();
    }

    public static void AssertInvalidVertexMetadata(string scenario, string body, bool azure)
    {
        var cache = new SemanticCacheTestCache();
        var embeddings = new SemanticCacheTestEmbeddings();
        var test = SemanticCacheTest.Create(cache: cache, embeddings: embeddings, azureLookup: azure, body: body);

        var exception = Assert.ThrowsExactly<PolicyException>(() => test.RunInbound(), scenario);

        exception.InnerException.Should().BeAssignableTo<ArgumentException>(scenario);
        embeddings.Requests.Should().BeEmpty(scenario);
        cache.Reads.Should().BeEmpty(scenario);
        cache.Updates.Should().BeEmpty(scenario);
    }

    public static void AssertStreamingLookup(string location, string method, bool azure)
    {
        var cache = new SemanticCacheTestCache();
        var embeddings = new SemanticCacheTestEmbeddings
        {
            TaskFactory = _ => throw new InvalidOperationException("Streaming endpoints must not reach embeddings.")
        };
        var test = SemanticCacheTest.Create(cache: cache, embeddings: embeddings,
            azureLookup: azure, azureStore: azure, body: VertexRequest);
        SetStreamingEndpoint(test.Context, location, method);
        test.Context.Response.StatusCode = 200;
        test.Context.Response.Headers["Content-Type"] = ["application/json"];
        test.Context.Response.Body.Content = """[{"candidates":[{"content":{"parts":[{"text":"stream chunk"}]}}]}]""";

        var exception = Assert.ThrowsExactly<PolicyException>(() => test.RunInbound());

        exception.InnerException.Should().BeOfType<NotSupportedException>();
        exception.Message.Should().Contain("stream");
        embeddings.Requests.Should().BeEmpty();
        cache.Reads.Should().BeEmpty();
        cache.Updates.Should().BeEmpty();
        test.Context.ResponseTerminated.Should().BeFalse();
    }

    public static void AssertNonstreamingVertex(bool azure)
    {
        var cache = new SemanticCacheTestCache();
        var seed = SemanticCacheTest.Create(cache: cache, azureLookup: azure, azureStore: azure, body: VertexRequest);
        seed.Context.Request.Url.Path = "/models/gemini-stream-name:generateContent";
        seed.Context.Request.OriginalUrl.Path = seed.Context.Request.Url.Path;
        seed.Context.Response.Headers["Content-Type"] = ["application/json"];
        seed.Context.Response.Body.Content = """{"candidates":[{"content":{"parts":[{"text":"buffered answer"}]}}]}""";
        seed.RunInbound();
        seed.RunOutbound();
        var hit = SemanticCacheTest.Create(cache: cache, azureLookup: !azure, body: VertexRequest);
        hit.Context.Request.Url.Path = seed.Context.Request.Url.Path;
        hit.Context.Request.OriginalUrl.Path = seed.Context.Request.Url.Path;

        hit.RunInbound();

        SemanticCacheTest.AssertHit(hit, seed.Context.Response.Body.Content);
    }

    public static void AssertRejectedResponse(string scenario, string body, string? contentType, string reason, bool azure)
    {
        var cache = new SemanticCacheTestCache();
        var test = SemanticCacheTest.Create(cache: cache, azureLookup: azure, azureStore: azure);
        test.RunInbound();
        test.Context.Response.Body.Content = body;
        if (contentType is not null)
        {
            test.Context.Response.Headers["Content-Type"] = [contentType];
        }

        var exception = Assert.ThrowsExactly<PolicyException>(() => test.RunOutbound(), scenario);

        exception.Policy.Should().Be(azure
            ? nameof(IOutboundContext.AzureOpenAiSemanticCacheStore)
            : nameof(IOutboundContext.LlmSemanticCacheStore));
        exception.Section.Should().Be(nameof(IOutboundContext));
        exception.InnerException.Should().BeOfType<NotSupportedException>(scenario);
        exception.Message.Should().Contain(reason, scenario);
        cache.Updates.Should().BeEmpty(scenario);
        test.Context.Response.Body.Content.Should().Be(body);
        test.Context.ResponseTerminated.Should().BeFalse();
        Assert.ThrowsExactly<PolicyException>(() => test.RunOutbound()).Message.Should().Contain("lookup");
        var fresh = SemanticCacheTest.Create(cache: cache, azureLookup: !azure);
        fresh.RunInbound();
        SemanticCacheTest.AssertMiss(fresh);
        fresh.Context.Response.Body.Content.Should().BeNull();
    }

    public static void AssertRejectedCachedResponse(string scenario, string body, string? contentType, string reason, bool azure)
    {
        var cache = new SemanticCacheTestCache();
        var seed = SemanticCacheTest.Create(cache: cache, azureLookup: !azure, azureStore: !azure);
        seed.Context.Response.Body.Content = "old text-only response";
        seed.RunInbound();
        seed.RunOutbound();
        var snapshot = GetStoredSnapshot(cache);
        snapshot.Body = body;
        if (contentType is not null)
        {
            snapshot.Headers["Content-Type"] = [contentType];
        }
        var hit = SemanticCacheTest.Create(cache: cache, azureLookup: azure);
        hit.Context.Response.Body.Content = "caller response";
        hit.Context.Response.Headers["X-Caller"] = ["caller header"];
        var writeCount = cache.Updates.Count;

        var exception = Assert.ThrowsExactly<PolicyException>(() => hit.RunInbound(), scenario);

        exception.Policy.Should().Be(azure
            ? nameof(IInboundContext.AzureOpenAiSemanticCacheLookup)
            : nameof(IInboundContext.LlmSemanticCacheLookup));
        exception.InnerException.Should().BeOfType<NotSupportedException>(scenario);
        exception.Message.Should().Contain(reason, scenario);
        hit.Context.Response.Body.Content.Should().Be("caller response");
        hit.Context.Response.Headers["X-Caller"].Should().Equal("caller header");
        hit.Context.ResponseTerminated.Should().BeFalse();
        hit.Context.Variables.Should().NotContainKey("after-semantic-lookup");
        cache.Updates.Should().HaveCount(writeCount);
    }

    public static void AssertRejectedStreamingStore(string location, bool azure)
    {
        var cache = new SemanticCacheTestCache();
        var test = SemanticCacheTest.Create(cache: cache, azureLookup: azure, azureStore: azure, body: VertexRequest);
        test.RunInbound();
        SetStreamingEndpoint(test.Context, location, ":streamGenerateContent");
        test.Context.Response.Headers["Content-Type"] = ["application/json"];
        test.Context.Response.Body.Content = """[{"candidates":[{"content":{"parts":[{"text":"stream chunk"}]}}]}]""";

        var exception = Assert.ThrowsExactly<PolicyException>(() => test.RunOutbound());

        exception.InnerException.Should().BeOfType<NotSupportedException>();
        exception.Message.Should().Contain("stream");
        cache.Updates.Should().BeEmpty();
    }

    public static void AssertRejectedRewrittenRequest(string body, bool azure)
    {
        var cache = new SemanticCacheTestCache();
        var test = SemanticCacheTest.Create(cache: cache, azureLookup: azure, azureStore: azure);
        test.RunInbound();
        test.Context.Request.Body.Content = body;
        test.Context.Response.Body.Content = "text response after incompatible backend request";

        var exception = Assert.ThrowsExactly<PolicyException>(() => test.RunOutbound());

        exception.InnerException.Should().BeOfType<NotSupportedException>();
        cache.Updates.Should().BeEmpty();
    }

    public static void AssertAcceptedResponse(string scenario, string body, bool azure)
    {
        var cache = new SemanticCacheTestCache();
        var seed = SemanticCacheTest.Create(cache: cache, azureLookup: azure, azureStore: azure);
        seed.RunInbound();
        seed.Context.Response.Headers["Content-Type"] = [scenario == "ordinary buffered text" ? "text/plain" : "application/json"];
        seed.Context.Response.Body.Content = body;
        seed.RunOutbound();
        var hit = SemanticCacheTest.Create(cache: cache, azureLookup: !azure);

        hit.RunInbound();

        SemanticCacheTest.AssertHit(hit, body);
        cache.Updates.Should().ContainSingle(scenario);
    }

    public static IEnumerable<object[]> ResponseMetadataAndText()
    {
        var response = """{"id":"resp-text","object":"response","status":"completed","tool_choice":"auto","tools":[],"parallel_tool_calls":true,"output":[{"type":"message","role":"assistant","content":[{"type":"output_text","text":"answer"}]}]}""";
        yield return ["Responses default metadata", response, "application/json"];
        yield return ["Responses vendor JSON", response, "application/vnd.openai+json; charset=utf-8"];
        yield return ["Responses metadata without content type", response, null!];
        yield return ["Responses explicit no tools", """{"object":"response","tool_choice":"none","tools":[],"output":[{"type":"message","role":"assistant","content":[{"type":"output_text","text":"answer"}]}]}""", "application/json"];
        yield return ["normal completion usage is not a chunk", """{"object":"chat.completion","choices":[],"usage":{"total_tokens":3}}""", "application/json"];
        yield return ["SetBody bracket text", "[answer]", "text/plain"];
        yield return ["SetBody brace text", "{answer}", "text/plain"];
        yield return ["leading whitespace text", " \r\n[answer]", "TEXT/PLAIN; charset=utf-8"];
        yield return ["JSON array represented as text", """["answer"]""", "text/plain"];
        yield return ["tool names represented as text", """{"tool_choice":"required","tools":[{"name":"example"}]}""", "text/plain"];
    }

    public static IEnumerable<object[]> ActiveResponseTools()
    {
        yield return ["auto with enabled tools", """{"object":"response","tool_choice":"auto","tools":[{"type":"function","name":"lookup"}],"output":[{"type":"message","role":"assistant","content":[{"type":"output_text","text":"answer"}]}]}"""];
        yield return ["forced mode even without declarations", """{"object":"response","tool_choice":"required","tools":[],"output":[{"type":"message","role":"assistant","content":[{"type":"output_text","text":"answer"}]}]}"""];
        yield return ["forced named tool", """{"object":"response","tool_choice":{"type":"function","name":"lookup"},"tools":[],"output":[]}"""];
        yield return ["actual call with inert metadata", """{"object":"response","tool_choice":"auto","tools":[],"output":[{"type":"function_call","name":"lookup","arguments":"{}"}]}"""];
    }

    public static IEnumerable<object[]> CompletionChunks()
    {
        var chunk = """{"id":"chatcmpl-final","object":"chat.completion.chunk","choices":[],"usage":{"prompt_tokens":1,"completion_tokens":2,"total_tokens":3}}""";
        yield return ["final usage-only chunk", chunk, "application/json"];
        yield return ["known chunk contract without header", chunk, null!];
        yield return ["streaming chunk with no choices field", """{"object":"chat.completion.chunk","usage":{"total_tokens":3}}""", "application/json"];
        yield return ["chunk remains streaming without usage", """{"object":"chat.completion.chunk","choices":[]}""", "application/json"];
    }

    public static IEnumerable<object[]> MalformedDeclaredJson()
    {
        yield return ["declared JSON bracket prose", "[answer]", "application/json"];
        yield return ["declared JSON brace prose", "{answer}", "application/json"];
        yield return ["declared JSON ordinary prose", "not json", "application/json"];
        yield return ["declared JSON empty body", "", "application/json"];
        yield return ["declared JSON whitespace", " \r\n", "application/json"];
        yield return ["declared JSON incomplete envelope", """{"object":"response","output":""", "application/json"];
        yield return ["JSON suffix media type is also strict", "not json", "application/vnd.openai+json; charset=utf-8"];
    }

    public static void AssertAcceptedSnapshot(string scenario, string body, string? contentType, bool azure)
    {
        var cache = new SemanticCacheTestCache();
        var seed = SemanticCacheTest.Create(cache: cache, azureLookup: !azure, azureStore: !azure);
        seed.Context.Response.Body.Content = "old text snapshot";
        seed.RunInbound();
        seed.RunOutbound();
        var snapshot = GetStoredSnapshot(cache);
        snapshot.Body = body;
        if (contentType is not null)
        {
            snapshot.Headers["Content-Type"] = [contentType];
        }
        var hit = SemanticCacheTest.Create(cache: cache, azureLookup: azure);

        hit.RunInbound();

        SemanticCacheTest.AssertHit(hit, body);
        cache.Updates.Should().ContainSingle(scenario);
        if (contentType is not null)
        {
            hit.Context.Response.Headers["Content-Type"].Should().Equal(contentType);
        }
    }

    public static void AssertResponseBodyRoundTrip(string scenario, string body, string? contentType, bool azure)
    {
        var cache = new SemanticCacheTestCache();
        var seed = new ExecutionTestDocument
        {
            InboundAction = context => SemanticCacheTest.Lookup(context, SemanticCacheTest.Config, azure),
            OutboundAction = context =>
            {
                context.SetBody(body);
                if (contentType is not null)
                {
                    context.SetHeader("Content-Type", contentType);
                }
                SemanticCacheTest.Store(context, 10, azure);
            }
        }.AsTestDocument();
        SemanticCacheTest.Configure(seed, cache);
        seed.RunInbound();
        seed.RunOutbound();
        var hit = SemanticCacheTest.Create(cache: cache, azureLookup: !azure);

        hit.RunInbound();

        SemanticCacheTest.AssertHit(hit, body);
        seed.Context.Response.Body.Content.Should().Be(body);
        cache.Updates.Should().ContainSingle(scenario);
        if (contentType is not null)
        {
            hit.Context.Response.Headers["Content-Type"].Should().Equal(contentType);
        }
    }

    public static void AssertMalformedJson(string scenario, string body, string contentType, bool azure, bool cached)
    {
        var cache = new SemanticCacheTestCache();
        var test = SemanticCacheTest.Create(cache: cache, azureLookup: azure, azureStore: azure);
        test.RunInbound();
        if (cached)
        {
            test.Context.Response.Body.Content = "old text snapshot";
            test.RunOutbound();
            var snapshot = GetStoredSnapshot(cache);
            snapshot.Body = body;
            snapshot.Headers["Content-Type"] = [contentType];
            var hit = SemanticCacheTest.Create(cache: cache, azureLookup: !azure);
            hit.Context.Response.Body.Content = "caller body";

            var exception = Assert.ThrowsExactly<PolicyException>(() => hit.RunInbound(), scenario);

            exception.InnerException.Should().BeAssignableTo<JsonException>(scenario);
            hit.Context.Response.Body.Content.Should().Be("caller body");
            hit.Context.ResponseTerminated.Should().BeFalse();
            cache.Updates.Should().ContainSingle();
        }
        else
        {
            test.Context.Response.Body.Content = body;
            test.Context.Response.Headers["Content-Type"] = [contentType];

            var exception = Assert.ThrowsExactly<PolicyException>(() => test.RunOutbound(), scenario);

            exception.InnerException.Should().BeAssignableTo<JsonException>(scenario);
            test.Context.Response.Body.Content.Should().Be(body);
            test.Context.ResponseTerminated.Should().BeFalse();
            cache.Updates.Should().BeEmpty();
        }
    }

    private static CachedResponse GetStoredSnapshot(SemanticCacheTestCache cache)
    {
        var partition = cache.LastValue ?? throw new InvalidOperationException("Expected a stored semantic cache partition.");
        var entries = partition.GetType().GetProperty("Entries")?.GetValue(partition);
        if (entries is not System.Collections.IEnumerable values)
        {
            throw new InvalidOperationException("Expected semantic cache entries.");
        }
        var entry = values.Cast<object>().Single();
        return entry.GetType().GetProperty("Response")?.GetValue(entry) as CachedResponse
            ?? throw new InvalidOperationException("Expected a cached response snapshot.");
    }

    private static void SetStreamingEndpoint(GatewayContext context, string location, string method)
    {
        var path = "/v1/projects/test/locations/test/publishers/google/models/gemini" + method;
        switch (location)
        {
            case "original": context.Request.OriginalUrl.Path = path; break;
            case "current": context.Request.Url.Path = path; break;
            case "backend": context.BackendUrl = "https://vertex.example.test" + path; break;
            default: throw new ArgumentOutOfRangeException(nameof(location));
        }
    }

    private static string ChatOptions(string options) =>
        """{"messages":[{"role":"user","content":"question"}],""" + options[1..^1] + "}";

    private static string VertexOptions(string options) =>
        """{"contents":[{"role":"user","parts":[{"text":"question"}]}],""" + options[1..^1] + "}";

    private static string VertexPart(string part) =>
        """{"contents":[{"role":"model","parts":[""" + part + "]}]}";
}