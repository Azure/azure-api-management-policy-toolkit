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
internal class InvokeRequestHandler : IPolicyHandler
{
    public List<Tuple<
        Func<GatewayContext, InvokeRequestConfig, bool>,
        Action<GatewayContext, InvokeRequestConfig>
    >> CallbackHooks
    { get; } = new();

    public string PolicyName => nameof(IInboundContext.InvokeRequest);

    public object? Handle(GatewayContext context, object?[]? args)
    {
        var config = args.ExtractArgument<InvokeRequestConfig>();
        var callbackHook = CallbackHooks.Find(hook => hook.Item1(context, config));
        if (callbackHook is not null)
        {
            callbackHook.Item2(context, config);
        }
        else
        {
            Handle(context, config);
        }

        if (config.ResponseVariableName is null)
        {
            throw new FinishSectionProcessingException { TerminatesPipeline = false };
        }

        return null;
    }

    private static void Handle(GatewayContext context, InvokeRequestConfig config)
    {
        if (config.ResponseVariableName is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(config.ResponseVariableName);
        }

        var client = HttpPolicyTransport.GetClient(context);
        var request = HttpTransportRequestBuilder.Create(context, BackendResolver.InvokeUri(context, config),
            config.Method ?? context.Request.Method, copyHeaders: true, copyBody: true,
            headers: config.Headers, body: config.Body);
        var response = HttpPolicyTransport.Send(context, client, request);
        if (config.ResponseVariableName is not null)
        {
            context.Variables[config.ResponseVariableName] = response;
        }
        else
        {
            ResponseUtilities.Copy(response, context.Response);
        }
    }
}