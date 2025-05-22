// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Policies;

[Section(nameof(IInboundContext)), Section(nameof(IOnErrorContext))]
internal class SetMethodHandler : PolicyHandler<string>
{
    public override string PolicyName => nameof(IInboundContext.SetMethod);
    protected override void Handle(GatewayContext context, string method) => context.Request.Method = method;
}