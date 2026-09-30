// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Immutable;
using System.Text.Json;

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Data;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Services;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Policies;

/// <summary>
/// Records usage available at the inbound invocation, without deferring execution or estimating token counts.
/// </summary>
[Section(nameof(IInboundContext))]
internal class LlmEmitTokenMetricHandler : PolicyHandler<EmitTokenMetricConfig>
{
    private const long MaximumTokenCount = 9_007_199_254_740_992;

    public override string PolicyName => nameof(IInboundContext.LlmEmitTokenMetric);

    protected override void Handle(GatewayContext context, EmitTokenMetricConfig config)
    {
        var usage = ReadResponseUsage(context) ?? context.Services.Resolve<ILlmTokenUsageProvider>()?.GetUsage(context)
            ?? throw new InvalidOperationException(
                "Token usage is unavailable. Supply a JSON response usage section or register ILlmTokenUsageProvider.");
        var metrics = GetMetrics(usage);
        var emitter = new EmitMetricHandler();
        foreach (var (name, value) in metrics)
        {
            emitter.Handle(context,
            [
                new EmitMetricConfig
                {
                    Name = name,
                    Value = value,
                    Namespace = config.Namespace ?? "API Management",
                    Dimensions = config.Dimensions
                }
            ]);
        }
    }

    private static LlmTokenUsage? ReadResponseUsage(GatewayContext context)
    {
        var body = context.Response.Body?.As<string>(preserveContent: true);
        if (string.IsNullOrWhiteSpace(body))
        {
            return null;
        }

        using var document = JsonDocument.Parse(body);
        if (document.RootElement.ValueKind != JsonValueKind.Object)
        {
            throw new ArgumentException("The LLM response body must be a JSON object.", nameof(body));
        }

        if (!document.RootElement.TryGetProperty("usage", out var usage) || usage.ValueKind == JsonValueKind.Null)
        {
            if (!document.RootElement.TryGetProperty("usageMetadata", out usage) || usage.ValueKind == JsonValueKind.Null)
            {
                return null;
            }
        }

        if (usage.ValueKind != JsonValueKind.Object)
        {
            throw new ArgumentException("The LLM response usage section must be a JSON object.", nameof(usage));
        }

        var counts = new Dictionary<string, long>(StringComparer.Ordinal);
        ReadResponseCounts(usage, "", counts);
        if (counts.Count == 0)
        {
            return null;
        }

        return new LlmTokenUsage
        {
            TotalTokens = counts.TryGetValue("Total Tokens", out var total) ? total : null,
            PromptTokens = counts.TryGetValue("Prompt Tokens", out var prompt) ? prompt : null,
            CompletionTokens = counts.TryGetValue("Completion Tokens", out var completion) ? completion : null,
            AdditionalTokens = counts.Where(count => !IsPrimaryMetric(count.Key))
                .ToImmutableDictionary(StringComparer.Ordinal)
        };
    }

    private static void ReadResponseCounts(JsonElement usage, string parentPath, Dictionary<string, long> counts)
    {
        foreach (var property in usage.EnumerateObject())
        {
            var path = parentPath.Length == 0 ? property.Name : $"{parentPath}.{property.Name}";
            var name = ResolveMetricName(path);
            if (name is not null)
            {
                if (property.Value.ValueKind != JsonValueKind.Number || !property.Value.TryGetInt64(out var value))
                {
                    throw new ArgumentException($"Token count '{path}' must be an integer.", nameof(usage));
                }

                ValidateCount(value);
                if (!counts.TryAdd(name, value))
                {
                    throw new ArgumentException($"Token metric '{name}' must be reported only once.", nameof(usage));
                }
            }
            else if (property.Value.ValueKind == JsonValueKind.Object)
            {
                ReadResponseCounts(property.Value, path, counts);
            }
            else if (property.Name.EndsWith("_tokens_details", StringComparison.Ordinal)
                     && property.Value.ValueKind != JsonValueKind.Null)
            {
                throw new ArgumentException($"Token details '{path}' must be a JSON object.", nameof(usage));
            }
        }
    }

    private static string? ResolveMetricName(string path) => path switch
    {
        "total_tokens" or "totalTokenCount" => "Total Tokens",
        "prompt_tokens" or "input_tokens" or "promptTokenCount" => "Prompt Tokens",
        "completion_tokens" or "output_tokens" or "candidatesTokenCount" => "Completion Tokens",
        "cached_tokens" or "prompt_tokens_details.cached_tokens" or "input_tokens_details.cached_tokens"
            or "cachedContentTokenCount" => "Cached Tokens",
        "reasoning_tokens" or "completion_tokens_details.reasoning_tokens" or "output_tokens_details.reasoning_tokens"
            => "Reasoning Tokens",
        "thinking_tokens" or "thoughtsTokenCount" => "Thinking Tokens",
        "prompt_tokens_details.audio_tokens" or "input_tokens_details.audio_tokens" => "Prompt Audio Tokens",
        "completion_tokens_details.audio_tokens" or "output_tokens_details.audio_tokens" => "Completion Audio Tokens",
        "completion_tokens_details.accepted_prediction_tokens" => "Accepted Prediction Tokens",
        "completion_tokens_details.rejected_prediction_tokens" => "Rejected Prediction Tokens",
        "cache_creation_input_tokens" => "Cache Creation Prompt Tokens",
        "cache_read_input_tokens" => "Cache Read Prompt Tokens",
        "toolUsePromptTokenCount" => "Tool Use Prompt Tokens",
        _ when path.EndsWith("_tokens", StringComparison.Ordinal) || path.EndsWith("TokenCount", StringComparison.Ordinal)
            => path,
        _ => null
    };

    private static List<(string Name, long Value)> GetMetrics(LlmTokenUsage usage)
    {
        ArgumentNullException.ThrowIfNull(usage.AdditionalTokens, nameof(usage));
        var metrics = new List<(string Name, long Value)>();
        AddMetric(metrics, "Total Tokens", usage.TotalTokens);
        AddMetric(metrics, "Prompt Tokens", usage.PromptTokens);
        AddMetric(metrics, "Completion Tokens", usage.CompletionTokens);
        foreach (var (name, value) in usage.AdditionalTokens.OrderBy(count => count.Key, StringComparer.Ordinal))
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(name, nameof(usage));
            if (IsPrimaryMetric(name))
            {
                throw new ArgumentException($"Additional token category '{name}' cannot reuse a primary metric name.", nameof(usage));
            }

            AddMetric(metrics, name, value);
        }

        if (metrics.Count == 0)
        {
            throw new ArgumentException("Token usage must contain at least one observed count.", nameof(usage));
        }

        return metrics;
    }

    private static bool IsPrimaryMetric(string name) => name is "Total Tokens" or "Prompt Tokens" or "Completion Tokens";

    private static void AddMetric(List<(string Name, long Value)> metrics, string name, long? value)
    {
        if (value is not { } count)
        {
            return;
        }

        ValidateCount(count);
        metrics.Add((name, count));
    }

    private static void ValidateCount(long count)
    {
        if (count is < 0 or > MaximumTokenCount)
        {
            throw new ArgumentOutOfRangeException(
                "usage", count, "Token counts must be non-negative and at most 2^53 to retain exact metric values.");
        }
    }
}