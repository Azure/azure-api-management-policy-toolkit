// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Expressions;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Policies;

[
    Section(nameof(IInboundContext)),
    Section(nameof(IBackendContext)),
    Section(nameof(IOutboundContext)),
    Section(nameof(IOnErrorContext))
]
internal class ReturnResponseHandler : IPolicyHandler
{
    public List<Tuple<
        Func<GatewayContext, ReturnResponseConfig, bool>,
        Action<GatewayContext, ReturnResponseConfig>
    >> CallbackHooks { get; } = new();

    public string PolicyName => nameof(IInboundContext.ReturnResponse);

    public object? Handle(GatewayContext context, object?[]? args)
    {
        var config = args.ExtractArgument<ReturnResponseConfig>();
        var callbackHook = CallbackHooks.Find(hook => hook.Item1(context, config));
        if (callbackHook is not null)
        {
            callbackHook.Item2(context, config);
        }
        else
        {
            Handle(context, config);
        }

        throw new FinishSectionProcessingException();
    }

    private void Handle(GatewayContext context, ReturnResponseConfig config)
    {
        MockResponse response = new();
        if (!string.IsNullOrWhiteSpace(config.ResponseVariableName))
        {
            //copy variable
            response = context.Variables[config.ResponseVariableName] as MockResponse ??
                       throw new ArgumentException($"Variable {config.ResponseVariableName} should be response");
        }

        if (config.Status is not null)
        {
            response.StatusCode = config.Status.Code;
            response.StatusReason = config.Status.Reason;
        }

        foreach (var header in config.Headers ?? [])
        {
            switch (header.ExistsAction)
            {
                case "delete":
                    response.Headers.Remove(header.Name);
                    break;
                case "skip":
                    if (!response.Headers.TryGetValue(header.Name, out _))
                    {
                        ArgumentNullException.ThrowIfNull(header.Values);
                        response.Headers[header.Name] = header.Values;
                    }

                    break;
                case "append":
                    ArgumentNullException.ThrowIfNull(header.Values);
                    response.Headers[header.Name] = response.Headers.TryGetValue(header.Name, out var v)
                        ? v.Concat(header.Values).ToArray()
                        : header.Values;
                    break;
                case "override":
                default:
                    ArgumentNullException.ThrowIfNull(header.Values);
                    response.Headers[header.Name] = header.Values;
                    break;
            }
        }

        if (config.Body is not null)
        {
            response.Body.Content = config.Body.Content as string ?? throw new NotImplementedException();
        }

        context.Response = response;
    }
}