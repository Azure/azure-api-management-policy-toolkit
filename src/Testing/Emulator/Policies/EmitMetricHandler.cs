// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

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
        if (context.DiagnosticStore.Enabled)
        {
            context.DiagnosticStore.Metrics.Add(new Metric(
                config.Namespace ?? "API Management",
                config.Name,
                config.Value ?? 1.0,
                config.Dimensions
                    .Where(d => d.Value is not null || !context.DiagnosticStore.SkipDimensionOnEmpty)
                    .Select(d => new MetricDimension(d.Name, d.Value ?? "N/A"))
                    .ToArray()
            ));
        }
    }
}