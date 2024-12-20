// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.ApiManagement.PolicyToolkit.Authoring;

namespace Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Policies;

[Section(nameof(IInboundContext)), Section(nameof(IBackendContext))]
internal class SetBackendServiceHandler : PolicyHandler<SetBackendServiceConfig>
{
    public override string PolicyName => nameof(IInboundContext.SetBackendService);

    protected override void Handle(GatewayContext context, SetBackendServiceConfig config)
    {
        throw new NotImplementedException();
    }
}