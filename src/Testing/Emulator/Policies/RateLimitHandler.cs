// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Data;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Services;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Policies;

[Section(nameof(IInboundContext))]
internal class RateLimitHandler : PolicyHandler<RateLimitConfig>
{
    public override string PolicyName => nameof(IInboundContext.RateLimit);

    protected override void Handle(GatewayContext context, RateLimitConfig config)
    {
        if (context.Subscription is null)
        {
            return;
        }

        var counters = PolicyCounterService.For(context);
        var limitsToCheck = GetLimitsToCheck(context, config);
        var result = counters.Consume(limitsToCheck, 1);
        counters.ApplyRateLimit(result, RateLimitOutput.From(config), restoreSuccessfulResponse: true);
    }

    private static List<PolicyCounterLimit> GetLimitsToCheck(GatewayContext context, RateLimitConfig config)
    {
        var subscriptionKey = $"sub:{context.Subscription.Id}";
        var limiterKey = $"rate-limit:{context.Subscription.Id}";
        var limits = new List<PolicyCounterLimit>
        {
            new(subscriptionKey, limiterKey, config.Calls, null, config.RenewalPeriod, true)
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

            var apiIdentifier = api.Id ?? api.Name!;
            var apiKey = $"{subscriptionKey}:api:{apiIdentifier}";
            var apiLimiterKey = $"{limiterKey}:api:{apiIdentifier}";
            limits.Add(new PolicyCounterLimit(apiKey, apiLimiterKey, api.Calls, null, api.RenewalPeriod, true));

            if (api.Operations is null)
            {
                continue;
            }

            foreach (var op in api.Operations)
            {
                if (!PolicyCounterService.MatchesEntity(op.Id, op.Name, context.Operation.Id, context.Operation.Name))
                {
                    continue;
                }

                var opIdentifier = op.Id ?? op.Name!;
                limits.Add(new PolicyCounterLimit(
                    $"{apiKey}:op:{opIdentifier}", $"{apiLimiterKey}:op:{opIdentifier}",
                    op.Calls, null, op.RenewalPeriod, true));
            }
        }

        return limits;
    }
}