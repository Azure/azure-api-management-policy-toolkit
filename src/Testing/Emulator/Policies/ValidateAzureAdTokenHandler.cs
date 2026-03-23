// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Policies;

[Section(nameof(IInboundContext))]
internal class ValidateAzureAdTokenHandler : PolicyHandler<ValidateAzureAdTokenConfig>
{
    public override string PolicyName => nameof(IInboundContext.ValidateAzureAdToken);

    protected override void Handle(GatewayContext context, ValidateAzureAdTokenConfig config)
    {
        // No-op by default in emulator.
        // Azure AD token validation is not simulated in tests.
    }
}
