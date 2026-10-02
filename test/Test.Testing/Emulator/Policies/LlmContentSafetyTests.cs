// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Document;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Services;

using Newtonsoft.Json.Linq;

namespace Test.Emulator.Emulator.Policies;

[TestClass]
public class LlmContentSafetyTests
{
    [TestMethod]
    [DataRow("FourSeverityLevels", 4, 4, true)]
    [DataRow("FourSeverityLevels", 4, 2, false)]
    [DataRow("FourSeverityLevels", 3, 4, true)]
    [DataRow("FourSeverityLevels", 3, 2, false)]
    [DataRow("FourSeverityLevels", 7, 6, false)]
    [DataRow("FourSeverityLevels", 0, 0, true)]
    [DataRow(null, 4, 4, true)]
    [DataRow(null, 4, 2, false)]
    [DataRow("EightSeverityLevels", 4, 4, true)]
    [DataRow("EightSeverityLevels", 4, 3, false)]
    [DataRow("EightSeverityLevels", 2, 1, false)]
    [DataRow("EightSeverityLevels", 5, 5, true)]
    [DataRow("EightSeverityLevels", 0, 0, true)]
    [DataRow("EightSeverityLevels", 7, 7, true)]
    [DataRow("EightSeverityLevels", 7, 6, false)]
    public void EnforcesThresholdIncludingEquality(string? outputType, int threshold, int severity, bool blocked)
    {
        var config = CategoryConfig("Hate", threshold, outputType);
        var evaluator = Evaluator(Evaluation(("Hate", severity)));
        var test = CreateTest(config, evaluator);

        test.RunInbound();

        evaluator.Requests.Should().ContainSingle();
        evaluator.Requests[0].OutputType.Should().Be(outputType ?? "FourSeverityLevels");
        AssertOutcome(test.Context, blocked);
    }

    [TestMethod]
    [DataRow("Hate")]
    [DataRow("SelfHarm")]
    [DataRow("Sexual")]
    [DataRow("Violence")]
    public void SupportsEveryDocumentedCategory(string category)
    {
        var test = CreateTest(CategoryConfig(category), Evaluator(Evaluation((category, 4))));

        test.RunInbound();

        AssertBlocked(test.Context);
    }

    [TestMethod]
    [DataRow(2, 3, false)]
    [DataRow(2, 4, true)]
    [DataRow(4, 0, true)]
    [DataRow(6, 5, true)]
    public void EvaluatesAllConfiguredCategories(int hateSeverity, int violenceSeverity, bool blocked)
    {
        var config = CategoryConfig() with
        {
            Categories = new ContentSafetyCategories
            {
                OutputType = "EightSeverityLevels",
                Categories =
                [
                    new ContentSafetyCategory { Name = "Hate", Threshold = 4 },
                    new ContentSafetyCategory { Name = "Violence", Threshold = 4 }
                ]
            }
        };
        var test = CreateTest(config, Evaluator(Evaluation(("Hate", hateSeverity), ("Violence", violenceSeverity))));

        test.RunInbound();

        AssertOutcome(test.Context, blocked);
    }

    [TestMethod]
    public void DoesNotEnforceUnconfiguredCategories()
    {
        var test = CreateTest(CategoryConfig(), Evaluator(Evaluation(("Hate", 2), ("Sexual", 7))));

        test.RunInbound();

        AssertOutcome(test.Context, false);
    }

    [TestMethod]
    [DataRow("summarize a public report")]
    [DataRow("{\"prompt\":\"summarize a public report\"}")]
    [DataRow("{\"messages\":[{\"role\":\"system\",\"content\":\"be concise\"},{\"role\":\"user\",\"content\":\"summarize a public report\"}]}")]
    [DataRow("")]
    public void PassesPreservedInboundContentAndConfigurationToEvaluator(string content)
    {
        var config = CategoryConfig() with
        {
            ShieldPrompt = true,
            BlockLists = new ContentSafetyBlockLists { Ids = ["tenant-list", "shared-list"] }
        };
        var evaluator = Evaluator(Evaluation(("Hate", 0)) with { PromptAttackDetected = false });
        var test = CreateTest(config, evaluator);
        test.Context.Request.Body.Content = content;

        test.RunInbound();

        var request = evaluator.Requests.Should().ContainSingle().Which;
        request.BackendId.Should().Be("safety-backend");
        request.Content.Should().Be(content);
        request.OutputType.Should().Be("EightSeverityLevels");
        request.CategoryThresholds.Should().BeEquivalentTo(new Dictionary<string, int> { ["Hate"] = 4 });
        request.BlockListIds.Should().Equal("tenant-list", "shared-list");
        request.ShieldPrompt.Should().BeTrue();
        test.Context.Request.Body.Content.Should().Be(content);
        test.Context.Request.Body.Consumed.Should().BeFalse();
        AssertOutcome(test.Context, false);
    }

    [TestMethod]
    public void EvaluatesCurrentRequestOnEveryInvocation()
    {
        var evaluator = new RecordingEvaluator(request => Task.FromResult(
            Evaluation(("Hate", request.Content == "flagged prompt" ? 4 : 0))));
        var test = CreateTest(CategoryConfig(), evaluator);
        test.Context.Request.Body.Content = "ordinary prompt";

        test.RunInbound();
        AssertOutcome(test.Context, false);
        test.Context.Variables.Remove("after-safety");
        test.Context.Request.Body.Content = "flagged prompt";

        test.RunInbound();

        evaluator.Requests.Select(request => request.Content).Should().Equal("ordinary prompt", "flagged prompt");
        AssertBlocked(test.Context);
        test.Context.Response.Body.Content.Should().NotContain("flagged prompt");
    }

    [TestMethod]
    [DataRow("tenant-list", true)]
    [DataRow("shared-list", true)]
    [DataRow("another-list", false)]
    [DataRow("TENANT-LIST", false)]
    [DataRow(null, false)]
    public void BlocksOnlyMatchesForConfiguredBlockLists(string? matchedId, bool blocked)
    {
        var config = new LlmContentSafetyConfig
        {
            BackendId = "safety-backend",
            BlockLists = new ContentSafetyBlockLists { Ids = ["tenant-list", "shared-list"] }
        };
        var result = Evaluation() with { MatchedBlockListIds = matchedId is null ? [] : [matchedId] };
        var evaluator = Evaluator(result);
        var test = CreateTest(config, evaluator);

        test.RunInbound();

        evaluator.Requests.Should().ContainSingle();
        evaluator.Requests[0].CategoryThresholds.Should().BeEmpty();
        AssertOutcome(test.Context, blocked);
    }

    [TestMethod]
    [DataRow(true, true, true)]
    [DataRow(true, false, false)]
    [DataRow(false, true, false)]
    [DataRow(null, true, false)]
    public void EnforcesPromptShieldOnlyWhenEnabled(bool? shieldPrompt, bool attackDetected, bool blocked)
    {
        var config = CategoryConfig() with { ShieldPrompt = shieldPrompt };
        var evaluator = Evaluator(Evaluation(("Hate", 0)) with { PromptAttackDetected = attackDetected });
        var test = CreateTest(config, evaluator);

        test.RunInbound();

        evaluator.Requests.Should().ContainSingle();
        evaluator.Requests[0].ShieldPrompt.Should().Be(shieldPrompt == true);
        AssertOutcome(test.Context, blocked);
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void SupportsPromptShieldWithoutCategoryOrBlockListRules(bool attackDetected)
    {
        var config = new LlmContentSafetyConfig { BackendId = "safety-backend", ShieldPrompt = true };
        var test = CreateTest(config, Evaluator(Evaluation() with { PromptAttackDetected = attackDetected }));

        test.RunInbound();

        AssertOutcome(test.Context, attackDetected);
    }

    [TestMethod]
    public void MissingEvaluatorFailsExplicitlyWithoutChangingResponse()
    {
        var test = CreateTest(CategoryConfig());

        var exception = Assert.ThrowsExactly<PolicyException>(() => test.RunInbound());

        exception.Policy.Should().Be(nameof(IInboundContext.LlmContentSafety));
        exception.Section.Should().Be(nameof(IInboundContext));
        exception.InnerException.Should().BeOfType<InvalidOperationException>()
            .Which.Message.Should().Contain("Services.Register<ILlmContentSafetyEvaluator>");
        AssertFailureDidNotReturnResponse(test.Context);
    }

    [TestMethod]
    public void BackendKeyedEvaluatorTakesPrecedenceOverUnkeyedEvaluator()
    {
        var unkeyed = Evaluator(Evaluation(("Hate", 7)));
        var keyed = Evaluator(Evaluation(("Hate", 0)));
        var test = CreateTest(CategoryConfig(), unkeyed);
        test.Context.Services.Register<ILlmContentSafetyEvaluator>("safety-backend", keyed);

        test.RunInbound();

        keyed.Requests.Should().ContainSingle();
        unkeyed.Requests.Should().BeEmpty();
        AssertOutcome(test.Context, false);
    }

    [TestMethod]
    public void DoesNotUseEvaluatorRegisteredForAnotherBackend()
    {
        var evaluator = Evaluator(Evaluation(("Hate", 0)));
        var test = CreateTest(CategoryConfig());
        test.Context.Services.Register<ILlmContentSafetyEvaluator>("another-backend", evaluator);

        Assert.ThrowsExactly<PolicyException>(() => test.RunInbound())
            .InnerException.Should().BeOfType<InvalidOperationException>();

        evaluator.Requests.Should().BeEmpty();
        AssertFailureDidNotReturnResponse(test.Context);
    }

    [TestMethod]
    [DataRow("synchronous")]
    [DataRow("faulted-task")]
    [DataRow("cancellation")]
    public void EvaluatorFailuresPropagateWithOriginalCause(string failureMode)
    {
        Exception failure = failureMode == "cancellation"
            ? new OperationCanceledException("Evaluation cancelled")
            : new HttpRequestException("Content safety dependency unavailable");
        var evaluator = new RecordingEvaluator(_ => failureMode == "synchronous"
            ? throw failure
            : Task.FromException<LlmContentSafetyEvaluationResult>(failure));
        var test = CreateTest(CategoryConfig(), evaluator);

        var exception = Assert.ThrowsExactly<PolicyException>(() => test.RunInbound());

        exception.Policy.Should().Be(nameof(IInboundContext.LlmContentSafety));
        exception.Section.Should().Be(nameof(IInboundContext));
        exception.InnerException.Should().BeSameAs(failure);
        evaluator.Requests.Should().ContainSingle();
        AssertFailureDidNotReturnResponse(test.Context);
    }

    [TestMethod]
    public void CallbackOverridesConfigurationValidationAndMissingEvaluator()
    {
        var test = CreateTest(new LlmContentSafetyConfig { BackendId = "" });
        var called = false;
        test.SetupInbound().LlmContentSafety().WithCallback((context, config) =>
        {
            called = true;
            config.BackendId.Should().BeEmpty();
            context.Variables["callback"] = true;
        });

        test.RunInbound();

        called.Should().BeTrue();
        test.Context.Variables["callback"].Should().Be(true);
        AssertOutcome(test.Context, false);
    }

    [TestMethod]
    public void MatchingCallbackOverridesEvaluator()
    {
        var evaluator = Evaluator(Evaluation(("Hate", 7)));
        var test = CreateTest(CategoryConfig(), evaluator);
        test.SetupInbound().LlmContentSafety((_, config) => config.BackendId == "safety-backend")
            .WithCallback((context, _) => context.Variables["callback"] = true);

        test.RunInbound();

        evaluator.Requests.Should().BeEmpty();
        test.Context.Variables["callback"].Should().Be(true);
        AssertOutcome(test.Context, false);
    }

    [TestMethod]
    public void NonMatchingCallbackDoesNotBypassEvaluation()
    {
        var evaluator = Evaluator(Evaluation(("Hate", 4)));
        var test = CreateTest(CategoryConfig(), evaluator);
        test.SetupInbound().LlmContentSafety((_, config) => config.BackendId == "another-backend")
            .WithCallback((context, _) => context.Variables["callback"] = true);

        test.RunInbound();

        evaluator.Requests.Should().ContainSingle();
        test.Context.Variables.Should().NotContainKey("callback");
        AssertBlocked(test.Context);
    }

    [TestMethod]
    public void CallbackFailuresUsePolicyErrorSemantics()
    {
        var evaluator = Evaluator(Evaluation(("Hate", 0)));
        var test = CreateTest(CategoryConfig(), evaluator);
        var failure = new InvalidOperationException("Simulated content safety failure");
        test.SetupInbound().LlmContentSafety().WithCallback((_, _) => throw failure);

        var exception = Assert.ThrowsExactly<PolicyException>(() => test.RunInbound());

        exception.InnerException.Should().BeSameAs(failure);
        evaluator.Requests.Should().BeEmpty();
        AssertFailureDidNotReturnResponse(test.Context);
    }

    [TestMethod]
    [DataRow(4, false, false, true)]
    [DataRow(5, false, false, false)]
    [DataRow(5, true, true, true)]
    [DataRow(5, false, true, false)]
    public void UsesEvaluatedPolicyExpressions(int threshold, bool shieldPrompt, bool attackDetected, bool blocked)
    {
        var document = new SafetyDocument(context =>
        {
            var variables = context.ExpressionContext.Variables;
            return new LlmContentSafetyConfig
            {
                BackendId = (string)variables["backend"],
                ShieldPrompt = (bool)variables["shield"],
                Categories = new ContentSafetyCategories
                {
                    OutputType = (string)variables["output-type"],
                    Categories =
                    [
                        new ContentSafetyCategory
                        {
                            Name = (string)variables["category"],
                            Threshold = (int)variables["threshold"]
                        }
                    ]
                },
                BlockLists = new ContentSafetyBlockLists { Ids = (string[])variables["blocklists"] }
            };
        });
        var evaluator = Evaluator(Evaluation(("Violence", 4)) with { PromptAttackDetected = attackDetected });
        var test = document.AsTestDocument();
        InitializeContext(test.Context);
        test.Context.Variables["backend"] = "expression-backend";
        test.Context.Variables["shield"] = shieldPrompt;
        test.Context.Variables["output-type"] = "EightSeverityLevels";
        test.Context.Variables["category"] = "Violence";
        test.Context.Variables["threshold"] = threshold;
        test.Context.Variables["blocklists"] = new[] { "expression-list" };
        test.Context.Services.Register<ILlmContentSafetyEvaluator>(evaluator);

        test.RunInbound();

        var request = evaluator.Requests.Should().ContainSingle().Which;
        request.BackendId.Should().Be("expression-backend");
        request.OutputType.Should().Be("EightSeverityLevels");
        request.CategoryThresholds.Should().BeEquivalentTo(new Dictionary<string, int> { ["Violence"] = threshold });
        request.BlockListIds.Should().Equal("expression-list");
        request.ShieldPrompt.Should().Be(shieldPrompt);
        AssertOutcome(test.Context, blocked);
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow(" \t")]
    public void RejectsMissingOrEmptyBackend(string? backendId)
    {
        var evaluator = Evaluator(Evaluation(("Hate", 0)));
        var test = CreateTest(CategoryConfig() with { BackendId = backendId! }, evaluator);

        Assert.ThrowsExactly<PolicyException>(() => test.RunInbound())
            .InnerException.Should().BeAssignableTo<ArgumentException>();

        evaluator.Requests.Should().BeEmpty();
        AssertFailureDidNotReturnResponse(test.Context);
    }

    [TestMethod]
    public void RejectsNullConfiguration()
    {
        var evaluator = Evaluator(Evaluation(("Hate", 0)));
        var test = CreateTest(null!, evaluator);

        Assert.ThrowsExactly<PolicyException>(() => test.RunInbound())
            .InnerException.Should().BeOfType<ArgumentException>();

        evaluator.Requests.Should().BeEmpty();
        AssertFailureDidNotReturnResponse(test.Context);
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow(false)]
    public void RejectsConfigurationWithoutEnabledSafetyRules(bool? shieldPrompt)
    {
        var evaluator = Evaluator(Evaluation());
        var test = CreateTest(new LlmContentSafetyConfig
        {
            BackendId = "safety-backend",
            ShieldPrompt = shieldPrompt
        }, evaluator);

        Assert.ThrowsExactly<PolicyException>(() => test.RunInbound())
            .InnerException.Should().BeOfType<ArgumentException>();

        evaluator.Requests.Should().BeEmpty();
        AssertFailureDidNotReturnResponse(test.Context);
    }

    [TestMethod]
    [DataRow("missing-categories")]
    [DataRow("empty-categories")]
    [DataRow("null-category")]
    [DataRow("duplicate-category")]
    [DataRow("missing-blocklist-ids")]
    [DataRow("empty-blocklist-ids")]
    [DataRow("null-blocklist-id")]
    [DataRow("empty-blocklist-id")]
    [DataRow("blank-blocklist-id")]
    [DataRow("duplicate-blocklist-id")]
    public void RejectsMalformedCollectionsBeforeEvaluation(string scenario)
    {
        var categories = new ContentSafetyCategories
        {
            Categories = [new ContentSafetyCategory { Name = "Hate", Threshold = 4 }]
        };
        var blockLists = new ContentSafetyBlockLists { Ids = ["tenant-list"] };
        switch (scenario)
        {
            case "missing-categories":
                categories = categories with { Categories = null };
                break;
            case "empty-categories":
                categories = categories with { Categories = [] };
                break;
            case "null-category":
                categories = categories with { Categories = [null!] };
                break;
            case "duplicate-category":
                categories = categories with { Categories = [categories.Categories![0], categories.Categories[0]] };
                break;
            case "missing-blocklist-ids":
                blockLists = blockLists with { Ids = null! };
                break;
            case "empty-blocklist-ids":
                blockLists = blockLists with { Ids = [] };
                break;
            case "null-blocklist-id":
                blockLists = blockLists with { Ids = [null!] };
                break;
            case "empty-blocklist-id":
                blockLists = blockLists with { Ids = [""] };
                break;
            case "blank-blocklist-id":
                blockLists = blockLists with { Ids = [" \t"] };
                break;
            case "duplicate-blocklist-id":
                blockLists = blockLists with { Ids = ["tenant-list", "tenant-list"] };
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(scenario));
        }

        var config = new LlmContentSafetyConfig
        {
            BackendId = "safety-backend",
            ShieldPrompt = true,
            Categories = categories,
            BlockLists = blockLists
        };
        var evaluator = Evaluator(Evaluation(("Hate", 0)) with { PromptAttackDetected = false });
        var test = CreateTest(config, evaluator);

        Assert.ThrowsExactly<PolicyException>(() => test.RunInbound())
            .InnerException.Should().BeAssignableTo<ArgumentException>();

        evaluator.Requests.Should().BeEmpty();
        AssertFailureDidNotReturnResponse(test.Context);
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("hate")]
    [DataRow("HATE")]
    [DataRow("Unknown")]
    public void RejectsInvalidCategoryNames(string? category)
    {
        var evaluator = Evaluator(Evaluation());
        var test = CreateTest(CategoryConfig(category!), evaluator);

        Assert.ThrowsExactly<PolicyException>(() => test.RunInbound())
            .InnerException.Should().BeAssignableTo<ArgumentException>();

        evaluator.Requests.Should().BeEmpty();
        AssertFailureDidNotReturnResponse(test.Context);
    }

    [TestMethod]
    [DataRow(-1)]
    [DataRow(8)]
    [DataRow(int.MinValue)]
    [DataRow(int.MaxValue)]
    public void RejectsThresholdOutsideDocumentedRange(int threshold)
    {
        var evaluator = Evaluator(Evaluation(("Hate", 0)));
        var test = CreateTest(CategoryConfig(threshold: threshold), evaluator);

        Assert.ThrowsExactly<PolicyException>(() => test.RunInbound())
            .InnerException.Should().BeOfType<ArgumentOutOfRangeException>();

        evaluator.Requests.Should().BeEmpty();
        AssertFailureDidNotReturnResponse(test.Context);
    }

    [TestMethod]
    [DataRow("")]
    [DataRow(" ")]
    [DataRow("fourseveritylevels")]
    [DataRow("SixSeverityLevels")]
    public void RejectsInvalidOutputTypes(string outputType)
    {
        var evaluator = Evaluator(Evaluation(("Hate", 0)));
        var test = CreateTest(CategoryConfig(outputType: outputType), evaluator);

        Assert.ThrowsExactly<PolicyException>(() => test.RunInbound())
            .InnerException.Should().BeOfType<ArgumentException>();

        evaluator.Requests.Should().BeEmpty();
        AssertFailureDidNotReturnResponse(test.Context);
    }

    [TestMethod]
    [DataRow("FourSeverityLevels", -1)]
    [DataRow("FourSeverityLevels", 1)]
    [DataRow("FourSeverityLevels", 3)]
    [DataRow("FourSeverityLevels", 5)]
    [DataRow("FourSeverityLevels", 7)]
    [DataRow("FourSeverityLevels", 8)]
    [DataRow("EightSeverityLevels", -1)]
    [DataRow("EightSeverityLevels", 8)]
    public void RejectsInvalidEvaluatorSeverities(string outputType, int severity)
    {
        var evaluator = Evaluator(Evaluation(("Hate", severity)));
        var test = CreateTest(CategoryConfig(outputType: outputType), evaluator);

        AssertInvalidEvaluation(test);
    }

    [TestMethod]
    [DataRow("null-result")]
    [DataRow("null-task")]
    [DataRow("missing-category")]
    [DataRow("null-severities")]
    [DataRow("null-blocklist-matches")]
    [DataRow("invalid-result-category")]
    [DataRow("null-blocklist-match")]
    [DataRow("blank-blocklist-match")]
    [DataRow("missing-shield-result")]
    public void RejectsMalformedOrIncompleteEvaluationResults(string scenario)
    {
        LlmContentSafetyEvaluationResult? result = Evaluation(("Hate", 0)) with { PromptAttackDetected = false };
        result = scenario switch
        {
            "null-result" or "null-task" => null,
            "missing-category" => result with { CategorySeverities = new Dictionary<string, int>() },
            "null-severities" => result with { CategorySeverities = null! },
            "null-blocklist-matches" => result with { MatchedBlockListIds = null! },
            "invalid-result-category" => result with
            {
                CategorySeverities = new Dictionary<string, int> { ["hate"] = 0 }
            },
            "null-blocklist-match" => result with { MatchedBlockListIds = [null!] },
            "blank-blocklist-match" => result with { MatchedBlockListIds = [" "] },
            "missing-shield-result" => result with { PromptAttackDetected = null },
            _ => throw new ArgumentOutOfRangeException(nameof(scenario))
        };
        var evaluator = new RecordingEvaluator(_ => scenario == "null-task" ? null! : Task.FromResult(result!));
        var test = CreateTest(CategoryConfig() with { ShieldPrompt = true }, evaluator);

        AssertInvalidEvaluation(test);
    }

    [TestMethod]
    public void ValidatesEntireResultBeforeReturningForbidden()
    {
        var config = CategoryConfig() with
        {
            Categories = new ContentSafetyCategories
            {
                OutputType = "EightSeverityLevels",
                Categories =
                [
                    new ContentSafetyCategory { Name = "Hate", Threshold = 4 },
                    new ContentSafetyCategory { Name = "Violence", Threshold = 4 }
                ]
            }
        };
        var test = CreateTest(config, Evaluator(Evaluation(("Hate", 7))));

        AssertInvalidEvaluation(test);
    }

    [TestMethod]
    public void UsesConfigurationSnapshotEvenIfEvaluatorChangesOriginalArrays()
    {
        var config = CategoryConfig() with
        {
            BlockLists = new ContentSafetyBlockLists { Ids = ["tenant-list"] }
        };
        var evaluator = new RecordingEvaluator(_ =>
        {
            config.Categories!.Categories![0] = new ContentSafetyCategory { Name = "Hate", Threshold = 7 };
            config.BlockLists.Ids[0] = "changed-list";
            return Task.FromResult(Evaluation(("Hate", 0)) with { MatchedBlockListIds = ["tenant-list"] });
        });
        var test = CreateTest(config, evaluator);

        test.RunInbound();

        var request = evaluator.Requests.Should().ContainSingle().Which;
        request.CategoryThresholds["Hate"].Should().Be(4);
        request.BlockListIds.Should().Equal("tenant-list");
        AssertBlocked(test.Context);
    }

    [TestMethod]
    [DataRow("category")]
    [DataRow("blocklist")]
    [DataRow("shield")]
    public void ViolationStopsCurrentSectionLaterScopesBackendAndOutbound(string violation)
    {
        var config = CategoryConfig() with
        {
            ShieldPrompt = true,
            BlockLists = new ContentSafetyBlockLists { Ids = ["tenant-list"] }
        };
        var result = Evaluation(("Hate", violation == "category" ? 4 : 0)) with
        {
            MatchedBlockListIds = violation == "blocklist" ? ["tenant-list"] : [],
            PromptAttackDetected = violation == "shield"
        };
        var pipeline = PolicyPipelineBuilder.Create()
            .AddPolicy(PolicyScope.Global, new SafetyDocument(_ => config))
            .AddPolicy(PolicyScope.Api, new ProbeDocument())
            .ConfigureContext(context =>
            {
                InitializeContext(context);
                context.Services.Register<ILlmContentSafetyEvaluator>(Evaluator(result));
            })
            .Build();

        pipeline.RunAll();

        AssertBlocked(pipeline.Context);
        pipeline.Context.Variables.Should().NotContainKey("inner-inbound")
            .And.NotContainKey("backend").And.NotContainKey("outbound");
    }

    [TestMethod]
    public void ViolationInNestedScopeDoesNotResumeOuterSection()
    {
        var pipeline = PolicyPipelineBuilder.Create()
            .AddPolicy(PolicyScope.Global, new ProbeDocument(callBase: true))
            .AddPolicy(PolicyScope.Api, new SafetyDocument(_ => CategoryConfig()))
            .ConfigureContext(context =>
            {
                InitializeContext(context);
                context.Services.Register<ILlmContentSafetyEvaluator>(Evaluator(Evaluation(("Hate", 4))));
            })
            .Build();

        pipeline.RunAllNested();

        AssertBlocked(pipeline.Context);
        pipeline.Context.Variables.Should().ContainKey("inner-inbound")
            .And.NotContainKey("outer-after-base").And.NotContainKey("backend").And.NotContainKey("outbound");
    }

    private static LlmContentSafetyConfig CategoryConfig(
        string category = "Hate", int threshold = 4, string? outputType = "EightSeverityLevels") => new()
    {
        BackendId = "safety-backend",
        Categories = new ContentSafetyCategories
        {
            OutputType = outputType,
            Categories = [new ContentSafetyCategory { Name = category, Threshold = threshold }]
        }
    };

    private static LlmContentSafetyEvaluationResult Evaluation(params (string Category, int Severity)[] severities) => new()
    {
        CategorySeverities = severities.ToDictionary(pair => pair.Category, pair => pair.Severity, StringComparer.Ordinal),
        MatchedBlockListIds = []
    };

    private static RecordingEvaluator Evaluator(LlmContentSafetyEvaluationResult result) =>
        new(_ => Task.FromResult(result));

    private static TestDocument CreateTest(LlmContentSafetyConfig config, ILlmContentSafetyEvaluator? evaluator = null)
    {
        var test = new SafetyDocument(_ => config).AsTestDocument();
        InitializeContext(test.Context);
        if (evaluator is not null)
        {
            test.Context.Services.Register<ILlmContentSafetyEvaluator>(evaluator);
        }

        return test;
    }

    private static void InitializeContext(GatewayContext context)
    {
        context.Request.Body.Content = "summarize a public report";
        context.Response.StatusCode = 202;
        context.Response.StatusReason = "Accepted";
        context.Response.Headers["X-Existing"] = ["kept"];
        context.Response.Headers["Content-Type"] = ["text/plain"];
        context.Response.Body.Content = "existing response content";
    }

    private static void AssertOutcome(GatewayContext context, bool blocked)
    {
        if (blocked)
        {
            AssertBlocked(context);
        }
        else
        {
            AssertUnchangedResponse(context);
            context.ResponseTerminated.Should().BeFalse();
            context.Variables.Should().ContainKey("after-safety").WhoseValue.Should().Be(true);
        }
    }

    private static void AssertBlocked(GatewayContext context)
    {
        context.Response.StatusCode.Should().Be(403);
        context.Response.StatusReason.Should().Be("Forbidden");
        context.Response.Headers.Should().ContainSingle();
        context.Response.Headers.Should().ContainKey("Content-Type").WhoseValue.Should().Equal("application/json");
        var body = context.Response.Body.As<JObject>(preserveContent: true);
        body.Value<int>("statusCode").Should().Be(403);
        body.Value<string>("message").Should().Be("Request blocked by LLM content safety policy.");
        context.ResponseTerminated.Should().BeTrue();
        context.Variables.Should().NotContainKey("after-safety");
    }

    private static void AssertUnchangedResponse(GatewayContext context)
    {
        context.Response.StatusCode.Should().Be(202);
        context.Response.StatusReason.Should().Be("Accepted");
        context.Response.Headers.Should().BeEquivalentTo(new Dictionary<string, string[]>
        {
            ["X-Existing"] = ["kept"],
            ["Content-Type"] = ["text/plain"]
        });
        context.Response.Body.Content.Should().Be("existing response content");
    }

    private static void AssertFailureDidNotReturnResponse(GatewayContext context)
    {
        AssertUnchangedResponse(context);
        context.ResponseTerminated.Should().BeFalse();
        context.Variables.Should().NotContainKey("after-safety");
    }

    private static void AssertInvalidEvaluation(TestDocument test)
    {
        Assert.ThrowsExactly<PolicyException>(() => test.RunInbound())
            .InnerException.Should().BeOfType<InvalidOperationException>();
        AssertFailureDidNotReturnResponse(test.Context);
    }

    private sealed class RecordingEvaluator(
        Func<LlmContentSafetyEvaluationRequest, Task<LlmContentSafetyEvaluationResult>> evaluate) : ILlmContentSafetyEvaluator
    {
        public List<LlmContentSafetyEvaluationRequest> Requests { get; } = [];

        public Task<LlmContentSafetyEvaluationResult> EvaluateAsync(
            LlmContentSafetyEvaluationRequest request, CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            return evaluate(request);
        }
    }

    private sealed class SafetyDocument(Func<IInboundContext, LlmContentSafetyConfig> configFactory) : IDocument
    {
        public void Inbound(IInboundContext context)
        {
            context.LlmContentSafety(configFactory(context));
            context.SetVariable("after-safety", true);
        }

        public void Backend(IBackendContext context) => context.SetVariable("backend", true);
        public void Outbound(IOutboundContext context) => context.SetVariable("outbound", true);
    }

    private sealed class ProbeDocument(bool callBase = false) : IDocument
    {
        public void Inbound(IInboundContext context)
        {
            context.SetVariable("inner-inbound", true);
            if (callBase)
            {
                context.Base();
                context.SetVariable("outer-after-base", true);
            }
        }

        public void Backend(IBackendContext context) => context.SetVariable("backend", true);
        public void Outbound(IOutboundContext context) => context.SetVariable("outbound", true);
    }
}
