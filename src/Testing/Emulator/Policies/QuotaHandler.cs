// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Policies;

[Section(nameof(IInboundContext))]
internal class QuotaHandler : PolicyHandler<QuotaConfig>
{
    public override string PolicyName => nameof(IInboundContext.Quota);

    protected override void Handle(GatewayContext context, QuotaConfig config)
    {
        throw new NotImplementedException();
    }
}