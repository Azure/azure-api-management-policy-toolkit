// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Data;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Policies;

[Section(nameof(IOutboundContext))]
internal class CacheStoreHandler : IPolicyHandler
{
    public List<Tuple<
        Func<GatewayContext, uint, bool, bool>,
        Action<GatewayContext, uint, bool>
    >> CallbackHooks { get; } = new();

    public List<Tuple<
        Func<GatewayContext, uint, bool, bool>,
        Func<GatewayContext, uint, bool, string>
    >> CacheKeyProvider { get; } = new();

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

    private void Handle(GatewayContext context, uint duration, bool cacheResponse)
    {
        if (!context.CacheInfo.CacheSetup)
        {
            return;
        }

        var store = context.CacheStore.GetCache(context.CacheInfo.CachingType);
        if (store is null)
        {
            return;
        }

        if (context.Response.StatusCode != 200 && !cacheResponse)
        {
            return;
        }

        var cacheValue = context.Response.Clone();

        var key = CacheKeyProvider.Find(hook => hook.Item1(context, duration, cacheResponse))
                      ?.Item2(context, duration, cacheResponse)
                  ?? CacheInfo.CacheKey(context);

        store[key] = new CacheValue(cacheValue) { Duration = duration };
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

        if (args.Length != 2 || args[1] is null)
        {
            return (duration, false);
        }

        if (args[1] is not bool cacheValue)
        {
            throw new ArgumentException($"Expected {typeof(bool).Name} as second argument", nameof(args));
        }

        return (duration, cacheValue);
    }
}