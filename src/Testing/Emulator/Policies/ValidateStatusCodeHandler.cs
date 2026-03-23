// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Policies;

[
    Section(nameof(IOutboundContext)),
    Section(nameof(IOnErrorContext))
]
internal class ValidateStatusCodeHandler : PolicyHandler<ValidateStatusCodeConfig>
{
    public override string PolicyName => nameof(IOutboundContext.ValidateStatusCode);

    protected override void Handle(GatewayContext context, ValidateStatusCodeConfig config)
    {
        // No-op by default in emulator.
        // Status code validation against API schemas is not simulated in tests.
    }
}
