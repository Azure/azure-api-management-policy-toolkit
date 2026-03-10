// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Policies;

[Section(nameof(IInboundContext))]
internal class LlmEmitTokenMetricHandler : PolicyHandler<EmitTokenMetricConfig>
{
    public override string PolicyName => nameof(IInboundContext.LlmEmitTokenMetric);

    protected override void Handle(GatewayContext context, EmitTokenMetricConfig config)
    {
        // No-op by default in emulator.
        // LLM token metric emission is not simulated in tests.
        // Test authors use CallbackSetup to inspect emitted metrics.
    }
}