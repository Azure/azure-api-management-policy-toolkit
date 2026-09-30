// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.ObjectModel;

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Services;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Policies;

[Section(nameof(IInboundContext))]
internal class LlmContentSafetyHandler : PolicyHandler<LlmContentSafetyConfig>
{
    public override string PolicyName => nameof(IInboundContext.LlmContentSafety);

    protected override void Handle(GatewayContext context, LlmContentSafetyConfig config)
    {
        var request = CreateEvaluationRequest(context, config);
        var evaluator = context.Services.Resolve<ILlmContentSafetyEvaluator>(request.BackendId)
                        ?? context.Services.Resolve<ILlmContentSafetyEvaluator>()
                        ?? throw new InvalidOperationException(
                            $"No ILlmContentSafetyEvaluator registered for backend '{request.BackendId}'. " +
                            "Register one via test.Context.Services.Register<ILlmContentSafetyEvaluator>(evaluator) " +
                            "or Register<ILlmContentSafetyEvaluator>(backendId, evaluator).");
        var evaluationTask = evaluator.EvaluateAsync(request)
                             ?? throw new InvalidOperationException("The content safety evaluator returned no evaluation task.");
        var result = evaluationTask.GetAwaiter().GetResult()
                     ?? throw new InvalidOperationException("The content safety evaluator returned no evaluation result.");

        ValidateEvaluationResult(request, result);

        var blocked = request.CategoryThresholds.Any(category =>
                          result.CategorySeverities[category.Key] >= category.Value)
                      || result.MatchedBlockListIds.Any(id => request.BlockListIds.Contains(id, StringComparer.Ordinal))
                      || request.ShieldPrompt && result.PromptAttackDetected == true;
        if (!blocked)
        {
            return;
        }

        ResponseUtilities.Overwrite(context.Response, 403, "Forbidden");
        context.Response.Headers["Content-Type"] = ["application/json"];
        context.Response.Body.Content =
            """{"statusCode":403,"message":"Request blocked by LLM content safety policy."}""";
        context.ResponseTerminated = true;
        throw new FinishSectionProcessingException();
    }

    private static LlmContentSafetyEvaluationRequest CreateEvaluationRequest(
        GatewayContext context, LlmContentSafetyConfig config)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(config.BackendId, nameof(LlmContentSafetyConfig.BackendId));

        var outputType = config.Categories?.OutputType ?? "FourSeverityLevels";
        if (outputType is not ("FourSeverityLevels" or "EightSeverityLevels"))
        {
            throw new ArgumentException(
                "Content safety output type must be FourSeverityLevels or EightSeverityLevels.", nameof(config));
        }

        var categoryThresholds = new Dictionary<string, int>(StringComparer.Ordinal);
        if (config.Categories is { } categories)
        {
            if (categories.Categories is not { Length: > 0 })
            {
                throw new ArgumentException("Content safety categories must contain at least one category.", nameof(config));
            }

            foreach (var category in categories.Categories)
            {
                if (category is null || !IsCategoryName(category.Name))
                {
                    throw new ArgumentException(
                        "Content safety category names must be Hate, SelfHarm, Sexual, or Violence.", nameof(config));
                }

                if (category.Threshold is < 0 or > 7)
                {
                    throw new ArgumentOutOfRangeException(nameof(ContentSafetyCategory.Threshold), category.Threshold,
                        "Content safety category thresholds must be between zero and seven.");
                }

                if (!categoryThresholds.TryAdd(category.Name, category.Threshold))
                {
                    throw new ArgumentException(
                        $"Content safety category '{category.Name}' is configured more than once.", nameof(config));
                }
            }
        }

        var blockListIds = Array.Empty<string>();
        if (config.BlockLists is { } blockLists)
        {
            if (blockLists.Ids is not { Length: > 0 })
            {
                throw new ArgumentException("Content safety blocklists must contain at least one ID.", nameof(config));
            }

            var distinctIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var id in blockLists.Ids)
            {
                if (string.IsNullOrWhiteSpace(id))
                {
                    throw new ArgumentException("Content safety blocklist IDs must not be empty.", nameof(config));
                }

                if (!distinctIds.Add(id))
                {
                    throw new ArgumentException(
                        $"Content safety blocklist '{id}' is configured more than once.", nameof(config));
                }
            }

            blockListIds = blockLists.Ids.ToArray();
        }

        if (categoryThresholds.Count == 0 && blockListIds.Length == 0 && config.ShieldPrompt != true)
        {
            throw new ArgumentException(
                "Content safety must configure at least one category, blocklist, or enabled prompt shield rule.", nameof(config));
        }

        return new LlmContentSafetyEvaluationRequest
        {
            BackendId = config.BackendId,
            Content = context.Request.Body.As<string>(preserveContent: true),
            OutputType = outputType,
            CategoryThresholds = new ReadOnlyDictionary<string, int>(categoryThresholds),
            BlockListIds = Array.AsReadOnly(blockListIds),
            ShieldPrompt = config.ShieldPrompt == true
        };
    }

    private static void ValidateEvaluationResult(
        LlmContentSafetyEvaluationRequest request, LlmContentSafetyEvaluationResult result)
    {
        if (result.CategorySeverities is null || result.MatchedBlockListIds is null)
        {
            throw new InvalidOperationException(
                "Content safety evaluation must provide category severities and blocklist matches.");
        }

        foreach (var (category, severity) in result.CategorySeverities)
        {
            if (!IsCategoryName(category))
            {
                throw new InvalidOperationException($"Content safety evaluation returned unknown category '{category}'.");
            }

            var validSeverity = request.OutputType == "FourSeverityLevels"
                ? severity is 0 or 2 or 4 or 6
                : severity is >= 0 and <= 7;
            if (!validSeverity)
            {
                throw new InvalidOperationException(
                    $"Content safety evaluation returned invalid severity {severity} for {request.OutputType}.");
            }
        }

        foreach (var category in request.CategoryThresholds.Keys)
        {
            if (!result.CategorySeverities.ContainsKey(category))
            {
                throw new InvalidOperationException(
                    $"Content safety evaluation did not return severity for configured category '{category}'.");
            }
        }

        if (result.MatchedBlockListIds.Any(string.IsNullOrWhiteSpace))
        {
            throw new InvalidOperationException("Content safety evaluation returned an empty blocklist match ID.");
        }

        if (request.ShieldPrompt && result.PromptAttackDetected is null)
        {
            throw new InvalidOperationException("Content safety evaluation did not return the requested prompt shield result.");
        }
    }

    private static bool IsCategoryName(string? name) => name is "Hate" or "SelfHarm" or "Sexual" or "Violence";
}
