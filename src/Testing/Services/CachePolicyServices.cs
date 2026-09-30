// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Data;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Services;

internal static class CachePolicyServices
{
    public static string GetCachingType(string? cachingType)
    {
        var type = cachingType ?? "prefer-external";
        if (type is not ("internal" or "external" or "prefer-external"))
        {
            throw new ArgumentException($"Unrecognized caching type '{type}'.", nameof(cachingType));
        }

        return type;
    }

    public static ICache? Resolve(GatewayContext context, string? cachingType)
    {
        var type = GetCachingType(cachingType);
        var clock = context.Services.Resolve<TimeProvider>();
        if (clock is not null)
        {
            context.CacheStore.WithTimeProvider(clock);
        }

        var cache = type switch
        {
            "internal" => context.Services.Resolve<ICache>("internal") ?? context.Services.Resolve<ICache>(),
            "external" => context.Services.Resolve<ICache>("external") ?? context.Services.Resolve<ICache>(),
            _ => context.Services.Resolve<ICache>("prefer-external")
                ?? context.Services.Resolve<ICache>("external")
                ?? context.Services.Resolve<ICache>()
                ?? context.Services.Resolve<ICache>("internal")
        };
        if (cache is CacheStore store)
        {
            if (clock is not null)
            {
                store.WithTimeProvider(clock);
            }

            return store.GetCacheService(type);
        }

        return cache ?? context.CacheStore.GetCacheService(type);
    }

    public static ICache ResolveRequired(GatewayContext context, string? cachingType) =>
        Resolve(context, cachingType) ?? throw new InvalidOperationException(
            "The external cache is not configured. Register an ICache service or use WithExternalCacheSetup().");

    public static TimeProvider GetTimeProvider(GatewayContext context, ICache? cache = null) =>
        context.Services.Resolve<TimeProvider>() ?? (cache switch
        {
            CacheStore store => store.Clock,
            CacheStoreService partition => partition.Clock,
            _ => context.CacheStore.Clock
        });

    public static void SetCacheControl(Dictionary<string, string[]> headers, string value)
    {
        var matchingKeys = headers.Keys
            .Where(key => string.Equals(key, "Cache-Control", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        foreach (var key in matchingKeys)
        {
            headers.Remove(key);
        }

        headers["Cache-Control"] = [value];
    }

    public static void ValidateTtl(TimeSpan expiresAfter, TimeSpan refreshAfter)
    {
        if (expiresAfter < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(expiresAfter), "Cache expiration must not be negative.");
        }
        if (refreshAfter < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(refreshAfter), "Cache refresh duration must not be negative.");
        }
        if (refreshAfter > expiresAfter)
        {
            throw new ArgumentException("Cache refresh duration must not exceed expiration.", nameof(refreshAfter));
        }
    }

    public static void SetVariable(GatewayContext context, string variableName, object? value)
    {
        if (value is not null)
        {
            context.Variables[variableName] = value;
        }
        else
        {
            context.Variables.Remove(variableName);
        }
    }
}