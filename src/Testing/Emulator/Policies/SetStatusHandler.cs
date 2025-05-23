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
internal class SetStatusHandler : PolicyHandler<StatusConfig>
{
    public override string PolicyName => nameof(IInboundContext.SetStatus);

    protected override void Handle(GatewayContext context, StatusConfig config)
    {
        context.Response.StatusCode = config.Code;
        context.Response.StatusReason = config.Reason;
    }
}