// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Policies;

[
    Section(nameof(IInboundContext)),
    Section(nameof(IBackendContext)),
    Section(nameof(IOutboundContext)),
    Section(nameof(IOnErrorContext))
]
internal class SendRequestHandler : PolicyHandler<SendRequestConfig>
{
    public override string PolicyName => nameof(IInboundContext.SendRequest);

    protected override void Handle(GatewayContext context, SendRequestConfig config)
    {
        throw new NotImplementedException();
    }
}