// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Policies;

[Section(nameof(IInboundContext))]
internal class SetHeaderIfNotExistRequestHandler : SetHeaderIfNotExistHandler
{
    protected override Dictionary<string, string[]> GetHeaders(GatewayContext context) => context.Request.Headers;
}

[Section(nameof(IOutboundContext)), Section(nameof(IOnErrorContext))]
internal class SetHeaderIfNotExistResponseHandler : SetHeaderIfNotExistHandler
{
    protected override Dictionary<string, string[]> GetHeaders(GatewayContext context) => context.Response.Headers;
}

internal abstract class SetHeaderIfNotExistHandler : PolicyHandler<string, string[]>
{
    public override string PolicyName => nameof(IInboundContext.SetHeaderIfNotExist);

    protected override void Handle(GatewayContext context, string name, string[] values) =>
        GetHeaders(context).TryAdd(name, values);

    protected abstract Dictionary<string, string[]> GetHeaders(GatewayContext context);
}