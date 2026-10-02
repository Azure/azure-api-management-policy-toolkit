// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring.Expressions;
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
    >> CallbackHooks
    { get; } = new();

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

        context.ResponseTerminated = true;
        throw new FinishSectionProcessingException();
    }

    private static void Handle(GatewayContext context, ReturnResponseConfig config)
    {
        var response = new MockResponse
        {
            Headers = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        };
        if (config.ResponseVariableName is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(config.ResponseVariableName);
            if (!context.Variables.TryGetValue(config.ResponseVariableName, out var variable))
            {
                throw new KeyNotFoundException($"Response variable '{config.ResponseVariableName}' was not found.");
            }
            if (variable is not IResponse selectedResponse)
            {
                throw new ArgumentException($"Variable '{config.ResponseVariableName}' must contain a response.");
            }

            ResponseUtilities.Copy(selectedResponse, response);
        }

        if (config.Status is not null)
        {
            if (config.Status.Code is < 100 or > 599)
            {
                throw new ArgumentOutOfRangeException(nameof(config.Status.Code), config.Status.Code,
                    "HTTP response status codes must be between 100 and 599.");
            }
            ArgumentNullException.ThrowIfNull(config.Status.Reason);
            response.StatusCode = config.Status.Code;
            response.StatusReason = config.Status.Reason;
        }

        foreach (var header in config.Headers ?? [])
        {
            ArgumentNullException.ThrowIfNull(header);
            ArgumentException.ThrowIfNullOrWhiteSpace(header.Name);
            var action = header.ExistsAction ?? "override";
            if (action is not ("override" or "append" or "skip" or "delete"))
            {
                throw new ArgumentException($"Unsupported header exists-action '{action}'.",
                    nameof(header.ExistsAction));
            }
            if (action == "delete")
            {
                response.Headers.Remove(header.Name);
                continue;
            }

            var values = header.Values;
            ArgumentNullException.ThrowIfNull(values);
            switch (action)
            {
                case "skip":
                    if (!response.Headers.ContainsKey(header.Name))
                    {
                        response.Headers[header.Name] = values.ToArray();
                    }

                    break;
                case "append":
                    response.Headers[header.Name] = response.Headers.TryGetValue(header.Name, out var v)
                        ? v.Concat(values).ToArray()
                        : values.ToArray();
                    break;
                case "override":
                    response.Headers[header.Name] = values.ToArray();
                    break;
            }
        }

        if (config.Body is not null)
        {
            if (config.Body.Template is not null)
            {
                throw new NotSupportedException(
                    $"The return-response emulator does not support body template '{config.Body.Template}'.");
            }
            response.Body.Content = config.Body.Content as string ?? config.Body.Content?.ToString();
        }

        context.Response = response;
    }
}