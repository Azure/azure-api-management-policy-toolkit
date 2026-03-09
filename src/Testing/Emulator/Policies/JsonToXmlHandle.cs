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
internal class JsonToXmlHandle : PolicyHandler<JsonToXmlConfig>
{
    public override string PolicyName => nameof(IInboundContext.JsonToXml);

    protected override void Handle(GatewayContext context, JsonToXmlConfig config)
    {
        // No-op by default in emulator.
        // JSON to XML conversion is not simulated in tests.
        // Test authors use CallbackSetup to simulate conversion behavior.
    }
}