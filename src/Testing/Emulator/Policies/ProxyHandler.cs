// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Policies;

[Section(nameof(IInboundContext))]
internal class ProxyHandler : PolicyHandler<ProxyConfig>
{
    public override string PolicyName => nameof(IInboundContext.Proxy);

    protected override void Handle(GatewayContext context, ProxyConfig config)
    {
        // No-op - HTTP proxy routing is not simulated in the emulator
    }
}
