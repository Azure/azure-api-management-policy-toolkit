// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Expressions;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Services;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Policies;

[Section(nameof(IInboundContext))]
internal class RateLimitByKeyHandler : PolicyHandler<RateLimitByKeyConfig>
{
    public override string PolicyName => nameof(IInboundContext.RateLimitByKey);

    protected override void Handle(GatewayContext context, RateLimitByKeyConfig config)
    {
        var limiter = context.Services.Resolve<IRateLimiter>();
        if (limiter is not null)
        {
            var allowed = limiter.TryConsumeAsync(config.CounterKey, config.IncrementCount ?? 1).GetAwaiter().GetResult();
            if (!allowed)
            {
                if (config.RetryAfterVariableName is not null)
                {
                    context.Variables[config.RetryAfterVariableName] = config.RenewalPeriod;
                }

                ResponseUtilities.Overwrite(context.Response, 429, "Too Many Requests");
                if (config.RetryAfterHeaderName is not null)
                {
                    context.Response.Headers[config.RetryAfterHeaderName] = [config.RenewalPeriod.ToString()];
                }

                if (config.TotalCallsHeaderName is not null)
                {
                    context.Response.Headers[config.TotalCallsHeaderName] = [config.Calls.ToString()];
                }

                throw new FinishSectionProcessingException();
            }

            return;
        }

        var incrementCondition = config.IncrementCondition ?? true;
        var counterKey = config.CounterKey;
        var currentCount = context.RateLimitStore.GetCount(counterKey);

        if (currentCount >= config.Calls)
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

        if (incrementCondition)
        {
            var incrementCount = config.IncrementCount ?? 1;
            context.RateLimitStore.Increment(counterKey, incrementCount);
        }

        var remaining = Math.Max(0, config.Calls - currentCount - 1);

        if (config.RemainingCallsHeaderName is not null)
        {
            context.Response.Headers[config.RemainingCallsHeaderName] = [remaining.ToString()];
        }

        if (config.RemainingCallsVariableName is not null)
        {
            context.Variables[config.RemainingCallsVariableName] = remaining;
        }

        if (config.TotalCallsHeaderName is not null)
        {
            context.Response.Headers[config.TotalCallsHeaderName] = [config.Calls.ToString()];
        }
    }
}
