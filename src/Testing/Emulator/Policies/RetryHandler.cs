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
internal class RetryHandler : IPolicyHandler
{
    private static readonly AsyncLocal<RetryScope?> s_currentScope = new();

    public List<Tuple<
        Func<GatewayContext, RetryConfig, Action, bool>,
        Action<GatewayContext, RetryConfig, Action>
    >> CallbackHooks { get; } = [];

    public string PolicyName => nameof(IInboundContext.Retry);

    public object? Handle(GatewayContext context, object?[]? args)
    {
        var (config, section) = args.ExtractArguments<RetryConfig, Action>();
        ArgumentNullException.ThrowIfNull(section);
        var interval = ValidateConfig(config);

        var previousScope = s_currentScope.Value;
        s_currentScope.Value = new RetryScope(context, previousScope);
        try
        {
            var callback = CallbackHooks.Find(hook => hook.Item1(context, config, section));
            if (callback is not null)
            {
                callback.Item2(context, config, section);
            }
            else
            {
                Execute(context, config, section, interval);
            }

            return null;
        }
        finally
        {
            s_currentScope.Value = previousScope;
        }
    }

    internal static bool IsExecuting(GatewayContext context)
    {
        for (var scope = s_currentScope.Value; scope is not null; scope = scope.Parent)
        {
            if (ReferenceEquals(scope.Context, context))
            {
                return true;
            }
        }

        return false;
    }

    private static int ValidateConfig(RetryConfig config)
    {
        if (config.Count is < 1 or > 50)
        {
            throw new ArgumentOutOfRangeException(nameof(config.Count), config.Count,
                "Retry count must be between 1 and 50 additional attempts.");
        }

        if (config.Interval is not { } interval || interval <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(config.Interval), config.Interval,
                "Retry interval is required and must be a positive number of seconds.");
        }

        if (config.Delta is <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(config.Delta), config.Delta,
                "Retry delta must be a positive number of seconds.");
        }

        if (config.MaxInterval is <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(config.MaxInterval), config.MaxInterval,
                "Retry maximum interval must be a positive number of seconds.");
        }

        if (config.MaxInterval.HasValue && !config.Delta.HasValue)
        {
            throw new ArgumentException("Retry maximum interval requires a delta for exponential backoff.",
                nameof(config.MaxInterval));
        }

        return interval;
    }

    private static void Execute(GatewayContext context, RetryConfig config, Action section, int interval)
    {
        section();

        IRetryScheduler? scheduler = null;
        for (var retry = 1; retry <= config.Count; retry++)
        {
            if (!(config.ConditionEvaluator?.Invoke() ?? config.Condition))
            {
                return;
            }

            if (retry != 1 || config.FirstFastRetry != true)
            {
                scheduler ??= GetScheduler(context);
                scheduler.Delay(GetDelay(config, interval, retry, scheduler));
            }

            section();
        }
    }

    private static IRetryScheduler GetScheduler(GatewayContext context)
    {
        var scheduler = context.Services.Resolve<IRetryScheduler>();
        if (scheduler is null)
        {
            scheduler = new VirtualRetryScheduler();
            context.Services.Register<IRetryScheduler>(scheduler);
        }

        return scheduler;
    }

    private static TimeSpan GetDelay(RetryConfig config, int interval, int retry, IRetryScheduler scheduler)
    {
        var seconds = (double)interval;
        if (config.Delta is { } delta)
        {
            if (config.MaxInterval is { } maximum)
            {
                var jitter = scheduler.NextJitterFactor();
                if (!double.IsFinite(jitter) || jitter is < 0.8 or > 1.2)
                {
                    throw new InvalidOperationException(
                        "The retry scheduler must return a finite jitter factor between 0.8 and 1.2.");
                }

                seconds = Math.Min(interval + Math.Pow(2, retry - 1) * delta * jitter, maximum);
            }
            else
            {
                seconds += (retry - 1d) * delta;
            }
        }

        return TimeSpan.FromSeconds(seconds);
    }

    private sealed record RetryScope(GatewayContext Context, RetryScope? Parent);
}
