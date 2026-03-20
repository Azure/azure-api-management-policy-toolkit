// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Policies;

[
    Section(nameof(IOutboundContext)),
    Section(nameof(IOnErrorContext))
]
internal class ValidateHeadersHandler : PolicyHandler<ValidateHeadersConfig>
{
    public override string PolicyName => nameof(IOutboundContext.ValidateHeaders);

    protected override void Handle(GatewayContext context, ValidateHeadersConfig config)
    {
        // No-op by default in emulator.
        // Header validation against API schemas is not simulated in tests.
    }
}
