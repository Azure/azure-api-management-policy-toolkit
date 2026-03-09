// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text;

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Expressions;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Services;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Policies;

[Section(nameof(IInboundContext))]
internal class CacheLookupHandler : PolicyHandler<CacheLookupConfig>
{
    public override string PolicyName => nameof(IInboundContext.CacheLookup);

    protected override void Handle(GatewayContext context, CacheLookupConfig config)
    {
        var key = BuildCacheKey(context, config);
        context.Variables["__cache_lookup_key"] = key;

        var cache = context.Services.Resolve<ICache>();
        if (cache is not null)
        {
            var cachedValue = cache.GetAsync(key).GetAwaiter().GetResult();
            if (ResponseUtilities.TryCopyCachedResponse(cachedValue, context.Response))
            {
                context.Variables["__cache_hit"] = true;
                return;
            }

            context.Variables["__cache_hit"] = false;
            return;
        }

        var cachingType = config.CachingType ?? "prefer-external";
        var store = context.CacheStore.GetCache(cachingType);
        if (store is null)
        {
            context.Variables["__cache_hit"] = false;
            return;
        }

        if (store.TryGetValue(key, out var cachedEntry) && cachedEntry.Value is MockResponse cachedResponse)
        {
            ResponseUtilities.Copy(cachedResponse, context.Response);
            context.Variables["__cache_hit"] = true;
            return;
        }

        context.Variables["__cache_hit"] = false;
    }

    internal static string BuildCacheKey(GatewayContext context, CacheLookupConfig config)
    {
        var sb = new StringBuilder();
        sb.Append(context.Request.Url);

        if (config.VaryByHeaders is not null)
        {
            foreach (var header in config.VaryByHeaders)
            {
                if (context.Request.Headers.TryGetValue(header, out var values))
                {
                    sb.Append($";h:{header}={string.Join(",", values)}");
                }
            }
        }

        if (config.VaryByQueryParameters is not null)
        {
            foreach (var param in config.VaryByQueryParameters)
            {
                if (context.Request.Url.Query.TryGetValue(param, out var values))
                {
                    sb.Append($";q:{param}={string.Join(",", values)}");
                }
            }
        }

        return sb.ToString();
    }
}
