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
internal class CacheStoreValueHandler : PolicyHandler<CacheStoreValueConfig>
{
    public override string PolicyName => nameof(IInboundContext.CacheStoreValue);

    protected override void Handle(GatewayContext context, CacheStoreValueConfig config)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(config.Key);
        ArgumentNullException.ThrowIfNull(config.Value);
        ArgumentOutOfRangeException.ThrowIfNegative(config.Duration);

        CachePolicyServices.ResolveRequired(context, config.CachingType)
            .SetAsync(config.Key, config.Value, TimeSpan.FromSeconds(config.Duration)).GetAwaiter().GetResult();
    }
}