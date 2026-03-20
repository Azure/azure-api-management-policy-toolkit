// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Policies;

[
    Section(nameof(IInboundContext)),
    Section(nameof(IOutboundContext)),
    Section(nameof(IOnErrorContext))
]
internal class SendServiceBusMessageHandler : PolicyHandler<SendServiceBusMessageConfig>
{
    public override string PolicyName => nameof(IInboundContext.SendServiceBusMessage);

    protected override void Handle(GatewayContext context, SendServiceBusMessageConfig config)
    {
        // No-op by default in emulator.
        // Service Bus message sending is not simulated in tests.
    }
}
