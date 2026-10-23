// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Policies;

[Section(nameof(IInboundContext))]
internal class PublishToDaprHandler : PolicyHandler<PublishToDaprConfig>
{
    public override string PolicyName => nameof(IInboundContext.PublishToDapr);

    protected override void Handle(GatewayContext context, PublishToDaprConfig config)
    {
        // No-op by default in emulator.
        // Dapr publish is not simulated in tests.
    }
}
