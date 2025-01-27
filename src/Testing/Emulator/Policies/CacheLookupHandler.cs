// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Data;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Expressions;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Policies;

[Section(nameof(IInboundContext))]
internal class CacheLookupHandler : PolicyHandler<CacheLookupConfig>
{
    public List<Tuple<
        Func<GatewayContext, CacheLookupConfig, bool>,
        Func<GatewayContext, CacheLookupConfig, string>
    >> CacheKeyProvider { get; } = new();

    public override string PolicyName => nameof(IInboundContext.CacheLookup);

    protected override void Handle(GatewayContext context, CacheLookupConfig config)
    {
        if (context.CacheInfo.CacheSetup)
        {
            return;
        }

        Dictionary<string, CacheValue>? store = context.CacheStore.GetCache(context.CacheInfo.CachingType);
        if (store is null)
        {
            return;
        }

        context.CacheInfo.WithExecutedCacheLookup(config);

        string key = CacheKeyProvider.Find(hook => hook.Item1(context, config))
                         ?.Item2(context, config)
                     ?? CacheInfo.CacheKey(context);
        if (!store.TryGetValue(key, out CacheValue? cacheHit))
        {
            return;
        }

        if (cacheHit.Value is not MockResponse cachedResponse)
        {
            return;
        }

        context.Response = cachedResponse.Clone();
        throw new FinishSectionProcessingException();
    }
}