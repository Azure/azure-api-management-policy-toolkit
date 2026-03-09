// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Data;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Expressions;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Services;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Policies;

[Section(nameof(IInboundContext))]
internal class RateLimitHandler : PolicyHandler<RateLimitConfig>
{
    public override string PolicyName => nameof(IInboundContext.RateLimit);

    protected override void Handle(GatewayContext context, RateLimitConfig config)
    {
        var limiter = context.Services.Resolve<IRateLimiter>();
        if (limiter is not null)
        {
            var key = $"rate-limit:{context.Subscription?.Id ?? "anonymous"}";
            var allowed = limiter.TryConsumeAsync(key, 1).GetAwaiter().GetResult();
            if (!allowed)
            {
                DenyRequest(context, config);
            }

            return;
        }

        var limitsToCheck = GetLimitsToCheck(context, config);
        var (exceeded, remainingCalls) = CheckLimits(context.RateLimitStore, limitsToCheck);

        if (exceeded)
        {
            DenyRequest(context, config);
        }

        // All checks passed — increment all counters
        foreach (var (key, _) in limitsToCheck)
        {
            context.RateLimitStore.Increment(key);
        }

        // Set headers/variables on success path only (like gateway)
        if (config.RemainingCallsHeaderName is not null)
        {
            context.Response.Headers[config.RemainingCallsHeaderName] = [Math.Max(0, remainingCalls).ToString()];
        }

        if (config.TotalCallsHeaderName is not null)
        {
            context.Response.Headers[config.TotalCallsHeaderName] = [config.Calls.ToString()];
        }

        if (config.RemainingCallsVariableName is not null)
        {
            context.Variables[config.RemainingCallsVariableName] = Math.Max(0, remainingCalls);
        }

        // Reset response if it was previously set to 429 by a prior rate-limit check
        if (context.Response.StatusCode == 429)
        {
            var existingHeaders = new Dictionary<string, string[]>(context.Response.Headers);
            ResponseUtilities.Overwrite(context.Response, 200, "OK");
            foreach (var header in existingHeaders)
            {
                context.Response.Headers[header.Key] = header.Value;
            }
        }
    }

    private static (bool Exceeded, int RemainingCalls) CheckLimits(
        RateLimitStore store,
        List<(string Key, int Calls)> limits)
    {
        var remainingCalls = int.MaxValue;
        var exceeded = false;

        foreach (var (key, calls) in limits)
        {
            var currentCount = store.GetCount(key);
            remainingCalls = Math.Min(remainingCalls, calls - currentCount - 1);

            if (currentCount >= calls)
            {
                exceeded = true;
            }
        }

        return (exceeded, remainingCalls);
    }

    private static List<(string Key, int Calls)> GetLimitsToCheck(GatewayContext context, RateLimitConfig config)
    {
        var subscriptionKey = $"sub:{context.Subscription.Id}";
        var limits = new List<(string Key, int Calls)> { (subscriptionKey, config.Calls) };

        if (config.Apis is null)
        {
            return limits;
        }

        foreach (var api in config.Apis)
        {
            if (!MatchesEntity(api, context.Api.Id, context.Api.Name))
                continue;

            var apiIdentifier = api.Id ?? api.Name!;
            var apiKey = $"{subscriptionKey}:api:{apiIdentifier}";
            limits.Add((apiKey, api.Calls));

            if (api.Operations is null) continue;

            foreach (var op in api.Operations)
            {
                if (!MatchesEntity(op, context.Operation.Id, context.Operation.Name))
                    continue;

                var opIdentifier = op.Id ?? op.Name!;
                limits.Add(($"{apiKey}:op:{opIdentifier}", op.Calls));
            }
        }

        return limits;
    }

    private static bool MatchesEntity(EntityLimitConfig entity, string contextId, string contextName)
    {
        if (entity.Id is not null)
        {
            return string.Equals(entity.Id, contextId, StringComparison.OrdinalIgnoreCase);
        }

        return entity.Name is not null &&
               string.Equals(entity.Name, contextName, StringComparison.OrdinalIgnoreCase);
    }

    private static void DenyRequest(GatewayContext context, RateLimitConfig config)
    {
        var retryAfter = config.RenewalPeriod;

        if (config.RetryAfterVariableName is not null)
        {
            context.Variables[config.RetryAfterVariableName] = retryAfter;
        }

        ResponseUtilities.Overwrite(context.Response, 429, "Too Many Requests");

        if (config.RetryAfterHeaderName is not null)
        {
            context.Response.Headers[config.RetryAfterHeaderName] = [retryAfter.ToString()];
        }

        if (config.TotalCallsHeaderName is not null)
        {
            context.Response.Headers[config.TotalCallsHeaderName] = [config.Calls.ToString()];
        }

        throw new FinishSectionProcessingException();
    }
}
