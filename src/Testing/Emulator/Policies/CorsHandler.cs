// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Policies;

[Section(nameof(IInboundContext))]
internal class CorsHandler : PolicyHandler<CorsConfig>
{
    public override string PolicyName => nameof(IInboundContext.Cors);

    protected override void Handle(GatewayContext context, CorsConfig config)
    {
        throw new NotImplementedException();
    }
}