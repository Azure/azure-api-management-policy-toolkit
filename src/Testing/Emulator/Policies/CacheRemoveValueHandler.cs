// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Services;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Policies;

[
    Section(nameof(IInboundContext)),
    Section(nameof(IBackendContext)),
    Section(nameof(IOutboundContext)),
    Section(nameof(IOnErrorContext))
]
internal class CacheRemoveValueHandler : PolicyHandler<CacheRemoveValueConfig>
{
    public override string PolicyName => nameof(IInboundContext.CacheRemoveValue);

    protected override void Handle(GatewayContext context, CacheRemoveValueConfig config)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(config.Key);
        CachePolicyServices.ResolveRequired(context, config.CachingType)
            .RemoveAsync(config.Key).GetAwaiter().GetResult();
    }
}