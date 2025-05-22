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
internal class SetVariableHandler : PolicyHandler<string, object>
{
    public override string PolicyName => nameof(IInboundContext.SetVariable);

    protected override void Handle(GatewayContext context, string name, object value) =>
        context.Variables[name] = value;
}