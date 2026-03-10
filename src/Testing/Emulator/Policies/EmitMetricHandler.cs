// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;

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
        // No-op by default in emulator.
        // Metric emission is not simulated in tests.
        // Test authors use CallbackSetup to inspect emitted metrics.
    }
}