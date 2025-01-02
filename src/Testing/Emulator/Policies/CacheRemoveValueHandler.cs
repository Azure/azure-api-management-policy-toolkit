// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.ApiManagement.PolicyToolkit.Authoring;

namespace Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Policies;

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
        var store = context.CacheStore.GetCache(config.CachingType ?? "prefer-external");
        store?.Remove(config.Key);
    }
}