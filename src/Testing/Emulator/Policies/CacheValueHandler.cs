// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Data;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Services;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Policies;

[
    Section(nameof(IInboundContext)),
    Section(nameof(IBackendContext)),
    Section(nameof(IOutboundContext)),
    Section(nameof(IOnErrorContext))
]
internal class CacheValueHandler : IPolicyHandler
{
    public List<Tuple<
        Func<GatewayContext, CacheValueConfig, bool>,
        Action<GatewayContext, CacheValueConfig>
    >> CallbackHooks { get; } = new();

    public string PolicyName => nameof(IInboundContext.CacheValue);

    public object? Handle(GatewayContext context, object?[]? args)
    {
        var (config, section) = args.ExtractArguments<CacheValueConfig, Action>();

        var callbackHook = CallbackHooks.Find(hook => hook.Item1(context, config));
        if (callbackHook is not null)
        {
            callbackHook.Item2(context, config);
            return null;
        }

        Handle(context, config, section);
        return null;
    }

    private void Handle(GatewayContext context, CacheValueConfig config, Action section)
    {
        var cache = context.Services.Resolve<ICache>();
        if (cache is not null)
        {
            HandleWithExternalCache(context, config, section, cache);
            return;
        }

        var cachingType = config.CachingType ?? "prefer-external";
        var store = context.CacheStore.GetCache(cachingType);

        // Evaluate refresh-after before value block to detect force-refresh (0 = always execute)
        var refreshAfterSeconds = config.RefreshAfter ?? config.ExpiresAfter ?? 28800;
        var forceRefresh = refreshAfterSeconds == 0;

        // Check cache
        if (store is not null && store.TryGetValue(config.Key, out var cached))
        {
            if (!cached.IsExpired && !forceRefresh)
            {
                if (!cached.NeedsRefresh)
                {
                    // Fresh entry — return from cache, skip value block
                    context.Variables[config.VariableName] = cached.Value;
                    return;
                }

                // Stale but not expired — execute value block to refresh, but return stale if block fails
                var staleValue = cached.Value;
                ExecuteValueBlock(context, config, section, store, refreshAfterSeconds);

                // If value block didn't set the variable, use stale value
                if (!context.Variables.ContainsKey(config.VariableName))
                {
                    context.Variables[config.VariableName] = staleValue;
                }
                return;
            }

            if (!forceRefresh && !cached.IsExpired)
            {
                // Not expired, not force refresh — use cached value
                context.Variables[config.VariableName] = cached.Value;
                return;
            }
        }

        // Cache miss or force-refresh: execute value block
        ExecuteValueBlock(context, config, section, store, refreshAfterSeconds);

        // If variable still not set, use default value
        if (!context.Variables.ContainsKey(config.VariableName) && config.DefaultValue is not null)
        {
            context.Variables[config.VariableName] = config.DefaultValue;
        }
    }

    private static void HandleWithExternalCache(GatewayContext context, CacheValueConfig config, Action section, ICache cache)
    {
        var preRefreshAfterSeconds = config.RefreshAfter ?? config.ExpiresAfter ?? 28800;
        var forceRefresh = preRefreshAfterSeconds == 0;
        var variableExisted = context.Variables.TryGetValue(config.VariableName, out var originalValue);

        var result = cache.GetOrCreateWithDynamicTtlAsync(
            config.Key,
            (_, _) =>
            {
                if (variableExisted)
                {
                    context.Variables.Remove(config.VariableName);
                }

                section();

                if (!context.Variables.TryGetValue(config.VariableName, out var newValue) || newValue is null)
                {
                    if (variableExisted)
                    {
                        context.Variables[config.VariableName] = originalValue;
                    }

                    return Task.FromResult(CacheValueFactoryResult.DoNotUpdate());
                }

                var expiresAfterSeconds = config.ExpiresAfterEvaluator?.Invoke() ?? config.ExpiresAfter ?? 28800;
                var refreshAfterSeconds = config.RefreshAfterEvaluator?.Invoke() ?? config.RefreshAfter ?? expiresAfterSeconds;
                return Task.FromResult(new CacheValueFactoryResult(
                    newValue,
                    TimeSpan.FromSeconds(expiresAfterSeconds),
                    TimeSpan.FromSeconds(refreshAfterSeconds)));
            },
            forceRefresh).GetAwaiter().GetResult();

        if (result.Value is not null)
        {
            context.Variables[config.VariableName] = result.Value;
            return;
        }

        if (config.DefaultValue is not null)
        {
            context.Variables[config.VariableName] = config.DefaultValue;
        }
    }

    private static void ExecuteValueBlock(
        GatewayContext context,
        CacheValueConfig config,
        Action section,
        Dictionary<string, CacheValue>? store,
        int preRefreshAfterSeconds)
    {
        // Execute the nested value block
        section();

        // Check if the value block set the variable
        if (!context.Variables.TryGetValue(config.VariableName, out var value))
        {
            return; // Variable not set — don't update cache
        }

        if (value is null)
        {
            return; // Null value — don't update cache (preserve existing unexpired entry)
        }

        // Re-evaluate expires-after and refresh-after AFTER value block execution
        // Use evaluators when available (they re-read context variables set within the value block)
        var expiresAfterSeconds = config.ExpiresAfterEvaluator?.Invoke() ?? config.ExpiresAfter ?? 28800;
        var refreshAfterSeconds = config.RefreshAfterEvaluator?.Invoke() ?? config.RefreshAfter ?? expiresAfterSeconds;

        // Store in cache with full TTL metadata
        if (store is not null)
        {
            store[config.Key] = new CacheValue(
                value,
                ttl: TimeSpan.FromSeconds(expiresAfterSeconds),
                refreshAfter: TimeSpan.FromSeconds(refreshAfterSeconds));
        }
    }
}
