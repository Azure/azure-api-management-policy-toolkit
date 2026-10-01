// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Services;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Policies;

[Section(nameof(IOutboundContext))]
internal class CacheStoreHandler : IPolicyHandler
{
    public List<Tuple<
        Func<GatewayContext, int, bool?, bool>,
        Action<GatewayContext, int, bool?>
    >> CallbackHooks
    { get; } = new();

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

    protected void Handle(GatewayContext context, int duration, bool? cacheResponse)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(duration);
        if (cacheResponse == false
            || (cacheResponse is null && context.Response.StatusCode != 200)
            || !string.Equals(context.Request.Method, "GET", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (!context.Variables.TryGetValue(CacheLookupHandler.LookupStateVariable, out var lookup)
            || lookup is not CacheLookupHandler.LookupState state)
        {
            throw new InvalidOperationException("CacheStore requires a corresponding CacheLookup in the inbound section.");
        }
        if (!state.Cacheable)
        {
            return;
        }

        var cache = CachePolicyServices.ResolveRequired(context, state.CachingType);
        var ttl = TimeSpan.FromSeconds(duration);
        var cacheControl = CacheLookupHandler.GetCacheControl(state.DownstreamCachingType, state.MustRevalidate, ttl);
        var snapshot = new CachedResponse
        {
            StatusCode = context.Response.StatusCode,
            StatusReason = context.Response.StatusReason,
            Body = context.Response.Body.Content,
            Headers = context.Response.Headers.ToDictionary(
                header => header.Key, header => header.Value.ToArray(), context.Response.Headers.Comparer),
            ExpiresAt = CachePolicyServices.GetTimeProvider(context, cache).GetUtcNow() + ttl
        };
        CachePolicyServices.SetCacheControl(snapshot.Headers, cacheControl);
        PolicyServiceAwaiter.Wait(context, cache.SetAsync(state.Key, snapshot, ttl,
            HttpPolicyTransport.GetCancellationToken(context)));
        CachePolicyServices.SetCacheControl(context.Response.Headers, cacheControl);
    }

    private static (int, bool?) ExtractParameters(object?[]? args)
    {
        if (args is not { Length: 1 or 2 })
        {
            throw new ArgumentException("Expected 1 or 2 arguments", nameof(args));
        }

        if (args[0] is not int duration)
        {
            throw new ArgumentException($"Expected {typeof(int).Name} as first argument", nameof(args));
        }

        if (args.Length != 2)
        {
            return (duration, null);
        }

        if (args[1] is null)
        {
            return (duration, null);
        }
        if (args[1] is not bool cacheValue)
        {
            throw new ArgumentException($"Expected {typeof(bool).Name} or null as second argument", nameof(args));
        }

        return (duration, cacheValue);
    }
}