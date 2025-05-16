// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Policies;

[Section(nameof(IInboundContext))]
internal class RateLimitByKeyHandler : PolicyHandler<RateLimitByKeyConfig>
{
    public override string PolicyName => nameof(IInboundContext.RateLimitByKey);

    protected override void Handle(GatewayContext context, RateLimitByKeyConfig config)
    {
        throw new NotImplementedException();
    }
}