// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Policies;

[Section(nameof(IInboundContext))]
internal class LlmTokenLimitHandler : PolicyHandler<TokenLimitConfig>
{
    public override string PolicyName => nameof(IInboundContext.LlmTokenLimit);

    protected override void Handle(GatewayContext context, TokenLimitConfig config)
    {
        // No-op by default in emulator.
        // LLM token limiting is not simulated in tests.
    }
}
