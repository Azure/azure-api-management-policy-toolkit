// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Services;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Policies;

[Section(nameof(IInboundContext)), Section(nameof(IBackendContext))]
internal class RemoveHeaderRequestHandler : RemoveHeaderHandler
{
    protected override Dictionary<string, string[]> GetHeaders(GatewayContext context)
        => context.Request.Headers;
}

[Section(nameof(IOutboundContext)), Section(nameof(IOnErrorContext))]
internal class RemoveHeaderResponseHandler : RemoveHeaderHandler
{
    protected override Dictionary<string, string[]> GetHeaders(GatewayContext context)
        => context.Response.Headers;

    protected override void Handle(GatewayContext context, string name)
    {
        base.Handle(context, name);
        PolicyResponseHeaderOverlay.ForgetHeader(context, name);
    }
}

internal abstract class RemoveHeaderHandler : PolicyHandler<string>
{
    public override string PolicyName => nameof(IInboundContext.RemoveHeader);

    protected override void Handle(GatewayContext context, string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var headers = GetHeaders(context);
        var matchingKeys = headers.Keys
            .Where(key => string.Equals(key, name, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        foreach (var key in matchingKeys)
        {
            headers.Remove(key);
        }
    }

    protected abstract Dictionary<string, string[]> GetHeaders(GatewayContext context);
}
