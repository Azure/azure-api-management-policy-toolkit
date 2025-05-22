// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Expressions;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Policies;

[Section(nameof(IInboundContext))]
internal class SetBodyRequestHandler : SetBodyHandler
{
    protected override MockBody GetBody(GatewayContext context) => context.Request.Body;
}

[Section(nameof(IOutboundContext)), Section(nameof(IOnErrorContext))]
internal class SetBodyResponseHandler : SetBodyHandler
{
    protected override MockBody GetBody(GatewayContext context) => context.Response.Body;
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
        if (args is not { Length: 2 })
        {
            throw new ArgumentException("Expected 2 arguments", nameof(args));
        }

        if (args[0] is not string body)
        {
            throw new ArgumentException("SetBodyHandler requires a string argument.");
        }

        SetBodyConfig? config = null;

        if (args[1] is not null)
        {
            if (args[1] is not SetBodyConfig c)
            {
                throw new ArgumentException("SetBodyHandler requires a string argument for the content type.");
            }

            config = c;
        }

        var callbackHook = CallbackHooks.Find(hook => hook.Item1(context, body, config));

        if (callbackHook is not null)
        {
            callbackHook.Item2(context, body, config);
            return null;
        }

        var contextBody = GetBody(context);
        contextBody.Content = body;

        return null;
    }

    protected abstract MockBody GetBody(GatewayContext context);
}