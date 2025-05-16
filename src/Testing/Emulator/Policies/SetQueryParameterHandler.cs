// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Policies;

[Section(nameof(IInboundContext))]
internal class SetQueryParameterHandler : PolicyHandler<string, string[]>
{
    public override string PolicyName => nameof(IInboundContext.SetQueryParameter);

    protected override void Handle(GatewayContext context, string name, string[] value) =>
        context.Request.Url.Query[name] = value;
}