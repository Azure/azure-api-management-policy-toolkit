// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Policies;

[
    Section(nameof(IInboundContext)),
    Section(nameof(IBackendContext)),
    Section(nameof(IOutboundContext)),
    Section(nameof(IOnErrorContext))
]
internal class BaseHandler : IPolicyHandler
{
    public List<Tuple<
        Func<GatewayContext, bool>,
        Action<GatewayContext>
    >> CallbackHooks { get; } = new();

    public string PolicyName => nameof(IInboundContext.Base);

    public object? Handle(GatewayContext context, object?[]? args)
    {
        var callbackHook = CallbackHooks.Find(hook => hook.Item1(context));
        callbackHook?.Item2(context);
        return null;
    }
}