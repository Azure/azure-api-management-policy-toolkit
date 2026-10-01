// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Services;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Policies;

[Section(nameof(IInboundContext))]
internal class LlmTokenLimitHandler : PolicyHandler<TokenLimitConfig>
{
    public override string PolicyName => nameof(IInboundContext.LlmTokenLimit);

    protected override void Handle(GatewayContext context, TokenLimitConfig config)
    {
        TokenLimitService.ValidateConfiguration(config);
        TokenLimitService.For(context).Register(config, PolicyName);
    }
}
