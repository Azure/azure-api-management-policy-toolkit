// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Policies;

[Section(nameof(IInboundContext))]
internal class RateLimitHandler : PolicyHandler<RateLimitConfig>
{
    public override string PolicyName => nameof(IInboundContext.RateLimit);

    protected override void Handle(GatewayContext context, RateLimitConfig config)
    {
        throw new NotImplementedException();
    }
}