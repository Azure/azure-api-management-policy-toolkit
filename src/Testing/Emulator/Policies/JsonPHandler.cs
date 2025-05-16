// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Policies;

[Section(nameof(IOutboundContext))]
internal class JsonPHandler : PolicyHandler<string>
{
    public override string PolicyName => nameof(IOutboundContext.JsonP);

    protected override void Handle(GatewayContext context, string config)
    {
        throw new NotImplementedException();
    }
}