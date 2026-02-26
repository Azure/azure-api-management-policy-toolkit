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
        var metric = new EmittedMetric(
            config.Name,
            config.Value ?? 1,
            config.Namespace,
            config.Dimensions);

        context.MetricStore.MetricsInternal.Add(metric);
    }
}