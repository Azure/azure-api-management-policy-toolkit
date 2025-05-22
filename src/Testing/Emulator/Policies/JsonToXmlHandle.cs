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
        throw new NotImplementedException();
    }
}