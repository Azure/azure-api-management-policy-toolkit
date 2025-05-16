// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Data;

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
        var store = context.CacheStore.GetCache(config.CachingType ?? "prefer-external");

        if (store is not null)
        {
            store[config.Key] = new CacheValue(config.Value) { Duration = config.Duration };
        }
    }
}