// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Services;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Policies;

[
    Section(nameof(IInboundContext)),
    Section(nameof(IBackendContext)),
    Section(nameof(IOutboundContext)),
    Section(nameof(IOnErrorContext))
]
internal class SendOneWayRequestHandler : PolicyHandler<SendOneWayRequestConfig>
{
    public override string PolicyName => nameof(IInboundContext.SendOneWayRequest);

    protected override void Handle(GatewayContext context, SendOneWayRequestConfig config)
    {
        var client = HttpPolicyTransport.GetClient(context);
        var copy = HttpTransportRequestBuilder.CopyMode(config.Mode);
        var options = new HttpTransportOptions { Timeout = HttpPolicyTransport.Timeout(config.Timeout) };
        var request = HttpTransportRequestBuilder.Create(context,
            HttpTransportRequestBuilder.ParseHttpUri(config.Url ?? context.Request.Url.ToString(), nameof(config.Url)),
            config.Method ?? (copy ? context.Request.Method : "GET"),
            copyHeaders: copy,
            copyBody: copy && context.CurrentSectionName != nameof(IOutboundContext),
            headers: config.Headers,
            body: config.Body,
            authentication: config.Authentication,
            proxy: config.Proxy,
            options: options);
        HttpPolicyTransport.SendOneWay(context, client, request);
    }
}