// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Policies;

[Section(nameof(IInboundContext))]
internal class LlmContentSafetyHandler : PolicyHandler<LlmContentSafetyConfig>
{
    public override string PolicyName => nameof(IInboundContext.LlmContentSafety);

    protected override void Handle(GatewayContext context, LlmContentSafetyConfig config)
    {
        // No-op - LLM content safety evaluation is not simulated in the emulator
    }
}
