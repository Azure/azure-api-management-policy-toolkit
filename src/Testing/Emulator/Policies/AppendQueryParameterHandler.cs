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
internal class AppendQueryParameterHandler : PolicyHandler<string, string[]>
{
    public override string PolicyName => nameof(IInboundContext.AppendQueryParameter);

    protected override void Handle(GatewayContext context, string name, string[] values)
    {
        var query = context.Request.Url.Query;
        if (query.TryGetValue(name, out var currentValues))
        {
            values = currentValues.Concat(values).ToArray();
        }

        query[name] = values;
    }
}