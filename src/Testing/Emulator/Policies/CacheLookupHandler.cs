// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Policies;

[Section(nameof(IInboundContext))]
internal class CacheLookupHandler : PolicyHandler<CacheLookupConfig>
{
    public override string PolicyName => nameof(IInboundContext.CacheLookup);

    protected override void Handle(GatewayContext context, CacheLookupConfig config)
    {
        throw new NotImplementedException();
    }
}