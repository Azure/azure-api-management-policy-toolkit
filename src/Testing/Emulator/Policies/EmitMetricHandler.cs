// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.ApiManagement.PolicyToolkit.Authoring;

namespace Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Policies;

[
    Section(nameof(IInboundContext)),
    Section(nameof(IBackendContext)),
    Section(nameof(IOutboundContext)),
    Section(nameof(IOnErrorContext))
]
internal class EmitMetricHandler : PolicyHandler<EmitMetricConfig>
{
    public override string PolicyName => nameof(IInboundContext.EmitMetric);

    protected override void Handle(GatewayContext context, EmitMetricConfig config)
    {
        throw new NotImplementedException();
    }
}