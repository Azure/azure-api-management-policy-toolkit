// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Policies;

[
    Section(nameof(IInboundContext)),
    Section(nameof(IOutboundContext)),
    Section(nameof(IOnErrorContext))
]
internal class PublishToDarpHandler : PolicyHandler<PublishToDarpConfig>
{
    public override string PolicyName => nameof(IInboundContext.PublishToDarp);

    protected override void Handle(GatewayContext context, PublishToDarpConfig config)
    {
        // No-op by default in emulator.
        // Dapr publish is not simulated in tests.
    }
}
