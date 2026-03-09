// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Policies;

[Section(nameof(IOutboundContext))]
internal class RedirectContentUrlsHandler : IPolicyHandler
{
    public List<Tuple<
        Func<GatewayContext, bool>,
        Action<GatewayContext>
    >> CallbackHooks { get; } = new();

    public string PolicyName => nameof(IOutboundContext.RedirectContentUrls);

    public object? Handle(GatewayContext context, object?[]? args)
    {
        var callbackHook = CallbackHooks.Find(hook => hook.Item1(context));
        callbackHook?.Item2(context);
        return null;
    }
}
