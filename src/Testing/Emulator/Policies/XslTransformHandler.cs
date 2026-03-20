// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Policies;

[
    Section(nameof(IInboundContext)),
    Section(nameof(IOutboundContext)),
    Section(nameof(IOnErrorContext))
]
internal class XslTransformHandler : PolicyHandler<XslTransformConfig>
{
    public override string PolicyName => nameof(IInboundContext.XslTransform);

    protected override void Handle(GatewayContext context, XslTransformConfig config)
    {
        // No-op - XSL transformation is not simulated in the emulator
    }
}
