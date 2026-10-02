// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Data;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Services;

using static Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Services.LlmTokenUsageReader;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Policies;

/// <summary>
/// Records usage available at the inbound invocation, without deferring execution or estimating token counts.
/// </summary>
[Section(nameof(IInboundContext))]
internal class LlmEmitTokenMetricHandler : PolicyHandler<EmitTokenMetricConfig>
{
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

    private static void AddMetric(List<(string Name, long Value)> metrics, string name, long? value)
    {
        if (value is not { } count)
        {
            return;
        }

        ValidateCount(count);
        metrics.Add((name, count));
    }
}