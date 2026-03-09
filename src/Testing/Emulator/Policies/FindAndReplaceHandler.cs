// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Expressions;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Policies;

[Section(nameof(IInboundContext)), Section(nameof(IBackendContext))]
internal class FindAndReplaceRequestHandler : FindAndReplaceHandler
{
    protected override MockBody GetBody(GatewayContext context) => context.Request.Body;
}

[Section(nameof(IOutboundContext)), Section(nameof(IOnErrorContext))]
internal class FindAndReplaceResponseHandler : FindAndReplaceHandler
{
    protected override MockBody GetBody(GatewayContext context) => context.Response.Body;
}

internal abstract class FindAndReplaceHandler : PolicyHandler<string, string>
{
    public override string PolicyName => nameof(IInboundContext.FindAndReplace);

    protected override void Handle(GatewayContext context, string from, string to)
    {
        var body = GetBody(context);
        if (body.Content is not null)
        {
            body.Content = body.Content.Replace(from, to);
        }
    }

    protected abstract MockBody GetBody(GatewayContext context);
}
