// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Policies;

[Section(nameof(IInboundContext))]
internal class RewriteUriHandler : PolicyHandler<string, bool>
{
    public override string PolicyName => nameof(IInboundContext.RewriteUri);

    protected override void Handle(GatewayContext context, string tempalte, bool copyUnmatchedParams)
    {
        throw new NotImplementedException();
    }
}