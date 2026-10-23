// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Policies;

[
    Section(nameof(IInboundContext)),
    Section(nameof(IOutboundContext)),
    Section(nameof(IOnErrorContext))
]
internal class InvokeDaprBindingHandler : PolicyHandler<InvokeDaprBindingConfig>
{
    public override string PolicyName => nameof(IInboundContext.InvokeDaprBinding);

    protected override void Handle(GatewayContext context, InvokeDaprBindingConfig config)
    {
        // No-op by default in emulator.
        // Dapr binding invocation is not simulated in tests.
    }
}
