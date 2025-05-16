// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Policies;

[Section(nameof(IInboundContext))]
internal class ValidateJwtHandler : PolicyHandler<ValidateJwtConfig>
{
    public override string PolicyName => nameof(IInboundContext.ValidateJwt);

    protected override void Handle(GatewayContext context, ValidateJwtConfig config)
    {
        throw new NotImplementedException();
    }
}