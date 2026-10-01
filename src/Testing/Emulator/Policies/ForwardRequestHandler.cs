// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Services;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Policies;

[Section(nameof(IBackendContext))]
internal class ForwardRequestHandler : PolicyHandlerOptionalParam<ForwardRequestConfig>, IPolicyHandler
{
    public override string PolicyName => nameof(IBackendContext.ForwardRequest);

    object? IPolicyHandler.Handle(GatewayContext context, object?[]? args)
    {
        var responseVersion = context.BackendResponseVersion;
        var result = base.Handle(context, args);
        if (context.BackendResponseVersion == responseVersion)
        {
            // The callback overrides transport, but its response has the same observation boundary.
            context.ObserveBackendResponse();
        }

        return result;
    }

    protected override void Handle(GatewayContext context, ForwardRequestConfig? config)
    {
        var client = HttpPolicyTransport.GetClient(context);
        var options = HttpPolicyTransport.ForwardOptions(config);
        var (version, versionPolicy) = config?.HttpVersion switch
        {
            null or "1" => (System.Net.HttpVersion.Version11, HttpVersionPolicy.RequestVersionExact),
            "2" => (System.Net.HttpVersion.Version20, HttpVersionPolicy.RequestVersionExact),
            "2or1" => (System.Net.HttpVersion.Version20, HttpVersionPolicy.RequestVersionOrLower),
            _ => throw new ArgumentException($"Unsupported HTTP version '{config.HttpVersion}'.", nameof(config.HttpVersion))
        };
        var request = HttpTransportRequestBuilder.Create(context, BackendResolver.ForwardUri(context),
            context.Request.Method, copyHeaders: true, copyBody: true, options: options);
        request.Version = version;
        request.VersionPolicy = versionPolicy;
        var response = HttpPolicyTransport.Send(context, client, request);
        ResponseUtilities.Copy(response, context.Response);
        context.ObserveBackendResponse();
        if (config?.FailOnErrorStatusCode == true && response.StatusCode is >= 400 and <= 599)
        {
            throw new HttpRequestException($"ForwardRequest backend returned HTTP {response.StatusCode}.",
                null, (System.Net.HttpStatusCode)response.StatusCode);
        }
    }
}