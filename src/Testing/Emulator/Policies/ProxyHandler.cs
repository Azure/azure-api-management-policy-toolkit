// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Services;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Policies;

[Section(nameof(IInboundContext))]
internal class ProxyHandler : PolicyHandler<ProxyConfig>
{
    public override string PolicyName => nameof(IInboundContext.Proxy);

    protected override void Handle(GatewayContext context, ProxyConfig config)
    {
        HttpTransportRequestBuilder.ValidateProxy(config);
        HttpPolicyTransport.GetState(context).Proxy = config;
    }
}