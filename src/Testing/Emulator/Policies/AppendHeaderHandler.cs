// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Policies;

[Section(nameof(IInboundContext))]
internal class AppendHeaderRequestHandler : AppendHeaderHandler
{
    protected override Dictionary<string, string[]> GetHeaders(GatewayContext context) => context.Request.Headers;
}

[Section(nameof(IOutboundContext)), Section(nameof(IOnErrorContext))]
internal class AppendHeaderResponseHandler : AppendHeaderHandler
{
    protected override Dictionary<string, string[]> GetHeaders(GatewayContext context) => context.Response.Headers;
}

internal abstract class AppendHeaderHandler : PolicyHandler<string, string[]>
{
    public override string PolicyName => nameof(IInboundContext.AppendHeader);

    protected override void Handle(GatewayContext context, string name, string[] values)
    {
        var headers = GetHeaders(context);
        if (headers.TryGetValue(name, out var currentValues))
        {
            values = currentValues.Concat(values).ToArray();
        }

        headers[name] = values;
    }

    protected abstract Dictionary<string, string[]> GetHeaders(GatewayContext context);
}