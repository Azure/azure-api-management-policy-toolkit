// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Policies;

[Section(nameof(IInboundContext))]
internal class CrossDomainHandler : PolicyHandler<string>
{
    public override string PolicyName => nameof(IInboundContext.CrossDomain);

    protected override void Handle(GatewayContext context, string policy)
    {
        // No-op - cross-domain policy doesn't affect request processing in emulator
    }
}
