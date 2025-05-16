// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Policies;

[Section(nameof(IOutboundContext))]
internal class LlmSemanticCacheStoreHandler : PolicyHandler<uint>
{
    public override string PolicyName => nameof(IOutboundContext.LlmSemanticCacheStore);

    protected override void Handle(GatewayContext context, uint duration)
    {
        throw new NotImplementedException();
    }
}