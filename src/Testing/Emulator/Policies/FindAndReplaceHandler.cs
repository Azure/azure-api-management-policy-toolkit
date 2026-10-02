// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Globalization;
using System.Text;

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Expressions;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Policies;

[Section(nameof(IInboundContext)), Section(nameof(IBackendContext))]
internal class FindAndReplaceRequestHandler : FindAndReplaceHandler
{
    protected override MockMessage GetMessage(GatewayContext context) => context.Request;
}

[Section(nameof(IOutboundContext)), Section(nameof(IOnErrorContext))]
internal class FindAndReplaceResponseHandler : FindAndReplaceHandler
{
    protected override MockMessage GetMessage(GatewayContext context) => context.Response;
}

internal abstract class FindAndReplaceHandler : PolicyHandler<string, string>
{
    public override string PolicyName => nameof(IInboundContext.FindAndReplace);

    protected override void Handle(GatewayContext context, string from, string to)
    {
        ArgumentException.ThrowIfNullOrEmpty(from);
        ArgumentNullException.ThrowIfNull(to);

        var message = GetMessage(context);
        var body = message.Body.Content;
        if (body is null)
        {
            return;
        }

        var replacement = body.Replace(from, to, StringComparison.Ordinal);
        if (replacement == body)
        {
            return;
        }

        var contentLengthHeaders = message.Headers.Keys
            .Where(name => name.Equals("Content-Length", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        var contentLength = Encoding.UTF8.GetByteCount(replacement).ToString(CultureInfo.InvariantCulture);

        message.Body.Content = replacement;
        foreach (var header in contentLengthHeaders)
        {
            message.Headers[header] = [contentLength];
        }
    }

    protected abstract MockMessage GetMessage(GatewayContext context);
}
