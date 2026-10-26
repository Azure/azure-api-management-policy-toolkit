// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Policies;

[
    Section(nameof(IInboundContext)),
    Section(nameof(IOutboundContext)),
    Section(nameof(IBackendContext))
]
internal class TraceHandler : PolicyHandler<TraceConfig>
{
    public override string PolicyName => nameof(IInboundContext.Trace);

    protected override void Handle(GatewayContext context, TraceConfig config)
    {
        // No-op by default in emulator.
        // Traces are not simulated in tests.
    }
}
