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
internal class CacheValueHandler : IPolicyHandler
{
    public List<Tuple<
        Func<GatewayContext, CacheValueConfig, bool>,
        Action<GatewayContext, CacheValueConfig>
    >> CallbackHooks
    { get; } = new();

    public string PolicyName => nameof(IInboundContext.CacheValue);

    public object? Handle(GatewayContext context, object?[]? args)
    {
        var (config, section) = args.ExtractArguments<CacheValueConfig, Action>();
        ArgumentNullException.ThrowIfNull(section);

        var callbackHook = CallbackHooks.Find(hook => hook.Item1(context, config));
        if (callbackHook is not null)
        {
            callbackHook.Item2(context, config);
            return null;
        }

        Handle(context, config, section);
        return null;
    }

    private static void Handle(GatewayContext context, CacheValueConfig config, Action section)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(config.Key);
        ArgumentException.ThrowIfNullOrWhiteSpace(config.VariableName);
        if (config.ExpiresAfter is { } expires)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(expires);
        }
        if (config.RefreshAfter is { } refresh)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(refresh);
        }
        if (config.ExpiresAfterEvaluator is null && config.RefreshAfterEvaluator is null)
        {
            var expiresAfter = TimeSpan.FromSeconds(config.ExpiresAfter ?? 28800);
            CachePolicyServices.ValidateTtl(expiresAfter,
                TimeSpan.FromSeconds(config.RefreshAfter ?? config.ExpiresAfter ?? 28800));
        }

        var cache = CachePolicyServices.ResolveRequired(context, config.CachingType);
        var preRefreshAfterSeconds = config.RefreshAfter ?? config.ExpiresAfter ?? 28800;
        var forceRefresh = preRefreshAfterSeconds == 0;
        var variableExisted = context.Variables.TryGetValue(config.VariableName, out var originalValue);
        var completed = false;
        try
        {
            var result = context.ExecuteAsyncService<CacheValueResult, CacheValueFactoryResult>(dispatch =>
                cache.GetOrCreateWithDynamicTtlAsync(
                config.Key,
                (_, cancellation) => dispatch(() =>
                {
                    // A value block must produce its own value, not reuse an unrelated request variable.
                    context.Variables.Remove(config.VariableName);
                    section();
                    if (!context.Variables.TryGetValue(config.VariableName, out var value) || value is null)
                    {
                        return CacheValueFactoryResult.DoNotUpdate();
                    }

                    var expiresAfterSeconds = config.ExpiresAfterEvaluator?.Invoke() ?? config.ExpiresAfter ?? 28800;
                    var refreshAfterSeconds = config.RefreshAfterEvaluator?.Invoke() ?? config.RefreshAfter ?? expiresAfterSeconds;
                    var expiresAfter = TimeSpan.FromSeconds(expiresAfterSeconds);
                    var refreshAfter = TimeSpan.FromSeconds(refreshAfterSeconds);
                    CachePolicyServices.ValidateTtl(expiresAfter, refreshAfter);
                    return new CacheValueFactoryResult(value, expiresAfter, refreshAfter);
                }, cancellation),
                forceRefresh));
            ArgumentNullException.ThrowIfNull(result);
            CachePolicyServices.SetVariable(context, config.VariableName, result.Value ?? config.DefaultValue);
            completed = true;
        }
        finally
        {
            if (!completed)
            {
                CachePolicyServices.SetVariable(context, config.VariableName, variableExisted ? originalValue : null);
            }
        }
    }
}