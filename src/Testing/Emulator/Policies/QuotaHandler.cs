// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Data;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Services;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Policies;

[Section(nameof(IInboundContext))]
internal class QuotaHandler : PolicyHandler<QuotaConfig>
{
    public override string PolicyName => nameof(IInboundContext.Quota);

    protected override void Handle(GatewayContext context, QuotaConfig config)
    {
        if (context.Subscription is null)
        {
            return;
        }

        var limits = GetLimitsToCheck(context, config);
        var bandwidth = limits.Any(limit => limit.Bandwidth is not null)
            ? PolicyCounterService.GetMessageLength(context.Request)
            : 0;
        var counters = PolicyCounterService.For(context);
        var result = counters.Consume(limits, 1, bandwidth);
        counters.ApplyQuota(result);
        counters.DeferQuotaResponseBandwidth(limits);
    }

    private static List<PolicyCounterLimit> GetLimitsToCheck(GatewayContext context, QuotaConfig config)
    {
        var key = $"quota:sub:{context.Subscription.Id}";
        var limiterKey = $"quota:{context.Subscription.Id}";
        var start = PolicyCounterService.SubscriptionStart(context);
        var limits = new List<PolicyCounterLimit>
        {
            new(key, limiterKey, config.Calls, (long?)config.Bandwidth * 1024, config.RenewalPeriod, false, start,
                SubscriptionScoped: true)
        };
        if (config.Apis is null)
        {
            return limits;
        }

        foreach (var api in config.Apis)
        {
            if (!PolicyCounterService.MatchesEntity(api.Id, api.Name, context.Api.Id, context.Api.Name))
            {
                continue;
            }

            var identifier = api.Id ?? api.Name!;
            var apiKey = $"{key}:api:{identifier}";
            var apiLimiterKey = $"{limiterKey}:api:{identifier}";
            limits.Add(new PolicyCounterLimit(
                apiKey, apiLimiterKey, api.Calls, (long?)api.Bandwidth * 1024, config.RenewalPeriod, false, start,
                SubscriptionScoped: true));
            foreach (var operation in api.Operations ?? [])
            {
                if (!PolicyCounterService.MatchesEntity(operation.Id, operation.Name, context.Operation.Id, context.Operation.Name))
                {
                    continue;
                }

                var operationIdentifier = operation.Id ?? operation.Name!;
                limits.Add(new PolicyCounterLimit(
                    $"{apiKey}:op:{operationIdentifier}", $"{apiLimiterKey}:op:{operationIdentifier}",
                    operation.Calls, (long?)operation.Bandwidth * 1024, config.RenewalPeriod, false, start,
                    SubscriptionScoped: true));
            }
        }

        return limits;
    }
}