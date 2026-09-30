// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Data;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Services;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Policies;

[Section(nameof(IInboundContext))]
internal class QuotaByKeyHandler : PolicyHandler<QuotaByKeyConfig>
{
    public override string PolicyName => nameof(IInboundContext.QuotaByKey);

    protected override void Handle(GatewayContext context, QuotaByKeyConfig config)
    {
        ArgumentException.ThrowIfNullOrEmpty(config.CounterKey);
        var incrementCount = config.IncrementCount ?? 1;
        ArgumentOutOfRangeException.ThrowIfNegative(incrementCount);
        var incrementCondition = config.IncrementCondition ?? true;
        var start = PolicyCounterService.ParseFirstPeriodStart(config.FirstPeriodStart);
        PolicyCounterLimit[] limits =
        [
            new($"quota-by-key:{config.CounterKey}", config.CounterKey, config.Calls,
                (long?)config.Bandwidth * 1024, config.RenewalPeriod, false, start)
        ];
        var bandwidth = incrementCondition && config.Bandwidth is not null
            ? PolicyCounterService.GetMessageLength(context.Request)
            : 0;
        var counters = PolicyCounterService.For(context);
        var result = counters.Consume(
            limits, incrementCondition ? incrementCount : 0, bandwidth,
            oncePerRequest: true, countRequest: incrementCondition);
        counters.ApplyQuota(result);
        if (incrementCondition)
        {
            counters.DeferQuotaResponseBandwidth(limits, oncePerRequest: true);
        }
    }
}