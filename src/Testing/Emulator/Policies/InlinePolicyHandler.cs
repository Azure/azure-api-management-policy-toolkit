// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.ApiManagement.PolicyToolkit.Authoring;

namespace Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Policies;

[
    Section(nameof(IInboundContext)),
    Section(nameof(IBackendContext)),
    Section(nameof(IOutboundContext)),
    Section(nameof(IOnErrorContext))
]
internal class InlinePolicyHandler : PolicyHandler<string>
{
    public override string PolicyName => nameof(IInboundContext.InlinePolicy);

    protected override void Handle(GatewayContext context, string config)
    {
        throw new NotImplementedException();
    }
}