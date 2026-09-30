// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Data;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Services;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Policies;

[Section(nameof(IInboundContext))]
internal class RateLimitByKeyHandler : PolicyHandler<RateLimitByKeyConfig>
{
    public override string PolicyName => nameof(IInboundContext.RateLimitByKey);

    protected override void Handle(GatewayContext context, RateLimitByKeyConfig config)
    {
        ArgumentException.ThrowIfNullOrEmpty(config.CounterKey);
        var incrementCount = config.IncrementCount ?? 1;
        ArgumentOutOfRangeException.ThrowIfNegative(incrementCount);
        var increment = (config.IncrementCondition ?? true) ? incrementCount : 0;
        var deferred = config.IncrementAfterResponse ?? false;
        PolicyCounterLimit[] limits =
        [
            new($"rate-limit-by-key:{config.CounterKey}", config.CounterKey, config.Calls, null, config.RenewalPeriod, true)
        ];
        var counters = PolicyCounterService.For(context);
        var output = RateLimitOutput.From(config);
        var result = counters.Consume(limits, deferred ? 0 : increment);
        counters.ApplyRateLimit(result, output);
        if (deferred)
        {
            counters.DeferRateIncrement(limits, increment, output);
        }
    }
}