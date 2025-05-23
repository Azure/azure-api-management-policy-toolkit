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
internal class RemoveQueryParameterHandler : PolicyHandler<string>
{
    public override string PolicyName => nameof(IInboundContext.RemoveQueryParameter);
    protected override void Handle(GatewayContext context, string name) => context.Request.Url.Query.Remove(name);
}