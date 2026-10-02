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
internal class SendRequestHandler : PolicyHandler<SendRequestConfig>
{
    public override string PolicyName => nameof(IInboundContext.SendRequest);

    protected override void Handle(GatewayContext context, SendRequestConfig config)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(config.ResponseVariableName);
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
        try
        {
            context.Variables[config.ResponseVariableName] = HttpPolicyTransport.Send(context, client, request);
        }
        catch (Exception error) when (config.IgnoreError == true
            && error is HttpRequestException or OperationCanceledException or IOException)
        {
            context.Trace($"SendRequest transport failed and was explicitly ignored: {error.Message}");
            context.Variables[config.ResponseVariableName] = null!;
        }
    }
}