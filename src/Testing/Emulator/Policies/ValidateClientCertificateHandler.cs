// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Policies;

[Section(nameof(IInboundContext))]
internal class ValidateClientCertificateHandler : PolicyHandler<ValidateClientCertificateConfig>
{
    public override string PolicyName => nameof(IInboundContext.ValidateClientCertificate);

    protected override void Handle(GatewayContext context, ValidateClientCertificateConfig config)
    {
        // No-op by default in emulator.
        // Client certificate validation is not simulated in tests.
    }
}
