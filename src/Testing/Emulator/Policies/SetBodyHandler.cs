// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Globalization;
using System.Text;

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Expressions;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Policies;

[Section(nameof(IInboundContext)), Section(nameof(IBackendContext))]
internal class SetBodyRequestHandler : SetBodyHandler
{
    protected override MockMessage GetMessage(GatewayContext context) => context.Request;
}

[Section(nameof(IOutboundContext)), Section(nameof(IOnErrorContext))]
internal class SetBodyResponseHandler : SetBodyHandler
{
    protected override MockMessage GetMessage(GatewayContext context) => context.Response;
}

internal abstract class SetBodyHandler : IPolicyHandler
{
    public List<Tuple<
        Func<GatewayContext, string, SetBodyConfig?, bool>,
        Action<GatewayContext, string, SetBodyConfig?>
    >> CallbackHooks { get; } = new();

    public string PolicyName => nameof(IInboundContext.SetBody);

    public object? Handle(GatewayContext context, object?[]? args)
    {
        var (body, config) = args.ExtractArguments<string, SetBodyConfig>();
        var callbackHook = CallbackHooks.Find(hook => hook.Item1(context, body, config));

        if (callbackHook is not null)
        {
            callbackHook.Item2(context, body, config);
            return null;
        }

        ValidateRenderingConfiguration(config);
        var message = GetMessage(context);
        var contentLengthHeaders = message.Headers.Keys
            .Where(name => name.Equals("Content-Length", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        var contentLength = Encoding.UTF8.GetByteCount(body).ToString(CultureInfo.InvariantCulture);

        message.Body.Content = body;
        foreach (var header in contentLengthHeaders)
        {
            message.Headers[header] = [contentLength];
        }

        return null;
    }

    private static void ValidateRenderingConfiguration(SetBodyConfig? config)
    {
        if (config is null)
        {
            return;
        }

        if (config.Template is not null && config.Template != "liquid")
        {
            throw new ArgumentException("Template must be 'liquid' when specified.", nameof(config.Template));
        }

        if (config.XsiNil is not null && config.XsiNil is not ("blank" or "null"))
        {
            throw new ArgumentException("XsiNil must be 'blank' or 'null' when specified.", nameof(config.XsiNil));
        }

        if (config.Template is not null || config.XsiNil is not null || config.ParseDate is not null)
        {
            throw new NotSupportedException(
                "SetBody liquid rendering, including XsiNil and ParseDate settings, is not supported by the emulator. Use SetBody().WithCallback(...) to supply a rendered body.");
        }

        // UseValueElement controls compiled policy XML, not runtime body rendering.
    }

    protected abstract MockMessage GetMessage(GatewayContext context);
}