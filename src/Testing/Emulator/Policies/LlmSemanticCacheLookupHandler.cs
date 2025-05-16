// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Policies;

[Section(nameof(IInboundContext))]
internal class LlmSemanticCacheLookupHandler : PolicyHandler<SemanticCacheLookupConfig>
{
    public override string PolicyName => nameof(IInboundContext.LlmSemanticCacheLookup);

    protected override void Handle(GatewayContext context, SemanticCacheLookupConfig config)
    {
        throw new NotImplementedException();
    }
}