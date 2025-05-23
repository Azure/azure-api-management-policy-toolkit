// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;

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
}

internal abstract class SetHeaderHandler : PolicyHandler<string, string[]>
{
    public override string PolicyName => nameof(IInboundContext.SetHeader);

    protected override void Handle(GatewayContext context, string name, string[] values) =>
        GetHeaders(context)[name] = values;

    protected abstract Dictionary<string, string[]> GetHeaders(GatewayContext context);
}