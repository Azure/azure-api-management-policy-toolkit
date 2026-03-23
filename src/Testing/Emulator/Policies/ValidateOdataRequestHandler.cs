// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Policies;

[Section(nameof(IInboundContext))]
internal class ValidateOdataRequestHandler : PolicyHandler<ValidateOdataRequestConfig>
{
    public override string PolicyName => nameof(IInboundContext.ValidateOdataRequest);

    protected override void Handle(GatewayContext context, ValidateOdataRequestConfig config)
    {
        // No-op by default in emulator.
        // OData request validation is not simulated in tests.
    }
}
