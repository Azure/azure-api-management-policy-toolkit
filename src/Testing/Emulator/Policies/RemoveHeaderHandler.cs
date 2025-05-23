// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;

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
}

internal abstract class RemoveHeaderHandler : PolicyHandler<string>
{
    public override string PolicyName => nameof(IInboundContext.RemoveHeader);

    protected override void Handle(GatewayContext context, string name) => GetHeaders(context).Remove(name);

    protected abstract Dictionary<string, string[]> GetHeaders(GatewayContext context);
}