// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Services;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Policies;

[Section(nameof(IOutboundContext))]
internal class CacheStoreHandler : IPolicyHandler
{
    public List<Tuple<
        Func<GatewayContext, uint, bool, bool>,
        Action<GatewayContext, uint, bool>
    >> CallbackHooks { get; } = new();

    public string PolicyName => nameof(IOutboundContext.CacheStore);

    public object? Handle(GatewayContext context, object?[]? args)
    {
        var (duration, cacheResponse) = ExtractParameters(args);

        var callbackHook = CallbackHooks.Find(hook => hook.Item1(context, duration, cacheResponse));
        if (callbackHook is not null)
        {
            callbackHook.Item2(context, duration, cacheResponse);
        }
        else
        {
            Handle(context, duration, cacheResponse);
        }

        return null;
    }

    protected void Handle(GatewayContext context, uint duration, bool cacheResponse)
    {
        if (!cacheResponse)
        {
            return;
        }

        var cache = context.Services.Resolve<ICache>();
        if (cache is not null)
        {
            var cacheKey = context.Request.Url.ToString();
            cache.SetAsync(
                cacheKey,
                new CachedResponse
                {
                    StatusCode = context.Response.StatusCode,
                    StatusReason = context.Response.StatusReason,
                    Body = context.Response.Body.Content,
                    Headers = new Dictionary<string, string[]>(context.Response.Headers)
                },
                TimeSpan.FromSeconds(duration)).GetAwaiter().GetResult();
            return;
        }

        var store = context.CacheStore.GetCache("prefer-external");
        if (store is null)
        {
            return;
        }

        var cacheStoreKey = context.Request.Url.ToString();
        store[cacheStoreKey] = new Data.CacheValue(context.Response, (int)duration);
    }

    private static (uint, bool) ExtractParameters(object?[]? args)
    {
        if (args is not { Length: 1 or 2 })
        {
            throw new ArgumentException("Expected 1 or 2 arguments", nameof(args));
        }

        if (args[0] is not uint duration)
        {
            throw new ArgumentException($"Expected {typeof(uint).Name} as first argument", nameof(args));
        }

        if (args.Length != 2)
        {
            return (duration, true);
        }

        if (args[0] is not bool cacheValue)
        {
            throw new ArgumentException($"Expected {typeof(bool).Name} as second argument", nameof(args));
        }

        return (duration, cacheValue);
    }
}
