// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Policies;

[Section(nameof(IInboundContext))]
internal class ValidateParametersHandler : PolicyHandler<ValidateParametersConfig>
{
    public override string PolicyName => nameof(IInboundContext.ValidateParameters);

    protected override void Handle(GatewayContext context, ValidateParametersConfig config)
    {
        // No-op by default in emulator.
        // Parameter validation against API schemas is not simulated in tests.
    }
}
