// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Services;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Policies;

[Section(nameof(IInboundContext)), Section(nameof(IBackendContext))]
internal class SetHeaderRequestHandler : SetHeaderHandler
{
    protected override Dictionary<string, string[]> GetHeaders(GatewayContext context) => context.Request.Headers;
}

[Section(nameof(IOutboundContext)), Section(nameof(IOnErrorContext))]
internal class SetHeaderResponseHandler : SetHeaderHandler
{
    protected override Dictionary<string, string[]> GetHeaders(GatewayContext context) => context.Response.Headers;

    protected override void Handle(GatewayContext context, string name, string[] values)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(values);

        var overlay = PolicyResponseHeaderOverlay.Existing(context);
        var headers = GetHeaders(context);
        var displayName = headers.Keys.FirstOrDefault(key => key.Equals(name, StringComparison.OrdinalIgnoreCase)) ?? name;
        ResponseHeaderUtilities.RemoveCaseVariants(headers, name);
        headers[displayName] = values;
        overlay?.UpdateRegistered(name, values);
    }
}

internal abstract class SetHeaderHandler : PolicyHandler<string, string[]>
{
    public override string PolicyName => nameof(IInboundContext.SetHeader);

    protected override void Handle(GatewayContext context, string name, string[] values)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(values);

        GetHeaders(context)[name] = values;
    }

    protected abstract Dictionary<string, string[]> GetHeaders(GatewayContext context);
}