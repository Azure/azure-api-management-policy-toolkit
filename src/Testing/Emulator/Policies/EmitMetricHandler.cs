// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Immutable;

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Data;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Policies;

[
    Section(nameof(IInboundContext)),
    Section(nameof(IOutboundContext)),
    Section(nameof(IOnErrorContext))
]
internal class EmitMetricHandler : PolicyHandler<EmitMetricConfig>
{
    public override string PolicyName => nameof(IInboundContext.EmitMetric);

    protected override void Handle(GatewayContext context, EmitMetricConfig config)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(config.Name, nameof(config.Name));
        var metricNamespace = config.Namespace ?? "apim";
        ArgumentException.ThrowIfNullOrWhiteSpace(metricNamespace, nameof(config.Namespace));

        var value = config.Value ?? 1;
        if (!double.IsFinite(value))
        {
            throw new ArgumentOutOfRangeException(nameof(config.Value), value, "Metric Value must be finite.");
        }

        ArgumentNullException.ThrowIfNull(config.Dimensions, nameof(config.Dimensions));
        if (config.Dimensions.Length is < 1 or > 5)
        {
            throw new ArgumentException("EmitMetric must have between one and five Dimensions.", nameof(config.Dimensions));
        }

        var dimensions = ImmutableDictionary.CreateBuilder<string, string?>(StringComparer.Ordinal);
        foreach (var dimension in config.Dimensions)
        {
            if (dimension is null || string.IsNullOrWhiteSpace(dimension.Name))
            {
                throw new ArgumentException("Metric dimensions must have a non-empty Name.", nameof(config.Dimensions));
            }

            var dimensionValue = dimension.Value ?? ResolveDefaultDimension(context, dimension.Name);
            if (!dimensions.TryAdd(dimension.Name, dimensionValue))
            {
                throw new ArgumentException(
                    $"Metric dimension name '{dimension.Name}' must be unique.", nameof(config.Dimensions));
            }
        }

        context.LoggerStore.MetricsInternal.Add(new MetricEvent(
            metricNamespace, config.Name, value, dimensions.ToImmutable()));
    }

    private static string? ResolveDefaultDimension(GatewayContext context, string name) => name switch
    {
        "API ID" => context.Api?.Id,
        "Operation ID" => context.Operation?.Id,
        "Product ID" => context.Product?.Id,
        "User ID" => context.User?.Id,
        "Subscription ID" => context.Subscription?.Id,
        "Location" => context.Deployment?.Region,
        "Gateway ID" => context.Deployment?.GatewayId,
        "Backend ID" => throw new NotSupportedException(
            "Default dimension 'Backend ID' is not available in the emulator. Set the dimension Value explicitly."),
        _ => throw new ArgumentException(
            $"An explicit Value is required for custom dimension '{name}'.", nameof(EmitMetricConfig.Dimensions))
    };
}