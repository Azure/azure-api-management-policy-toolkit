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
        // No-op by default in emulator.
        // JSONP wrapping is not simulated in tests.
        // Test authors use CallbackSetup to simulate JSONP behavior.
    }
}