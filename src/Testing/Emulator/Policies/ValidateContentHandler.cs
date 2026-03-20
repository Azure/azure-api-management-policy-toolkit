// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Policies;

[
    Section(nameof(IInboundContext)),
    Section(nameof(IOutboundContext)),
    Section(nameof(IOnErrorContext))
]
internal class ValidateContentHandler : PolicyHandler<ValidateContentConfig>
{
    public override string PolicyName => nameof(IInboundContext.ValidateContent);

    protected override void Handle(GatewayContext context, ValidateContentConfig config)
    {
        // No-op by default in emulator.
        // Content validation against API schemas is not simulated in tests.
    }
}
