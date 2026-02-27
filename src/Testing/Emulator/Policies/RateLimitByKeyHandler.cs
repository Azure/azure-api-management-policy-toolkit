// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Expressions;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Policies;

[Section(nameof(IInboundContext))]
internal class RateLimitByKeyHandler : PolicyHandler<RateLimitByKeyConfig>
{
    public override string PolicyName => nameof(IInboundContext.RateLimitByKey);

    protected override void Handle(GatewayContext context, RateLimitByKeyConfig config)
    {
        var key = config.CounterKey;
        var currentCount = context.RateLimitStore.GetCount(key);
        var remaining = config.Calls - currentCount - 1;

        if (currentCount >= config.Calls)
        {
            DenyRequest(context, config);
        }

        // Increment counter only if condition is met (default: true)
        if (config.IncrementCondition != false)
        {
            var increment = config.IncrementCount ?? 1;
            context.RateLimitStore.Increment(key, increment);
        }

        // Set headers/variables on success path only
        if (config.RemainingCallsHeaderName is not null)
        {
            context.Response.Headers[config.RemainingCallsHeaderName] = [Math.Max(0, remaining).ToString()];
        }

        if (config.TotalCallsHeaderName is not null)
        {
            context.Response.Headers[config.TotalCallsHeaderName] = [config.Calls.ToString()];
        }

        if (config.RemainingCallsVariableName is not null)
        {
            context.Variables[config.RemainingCallsVariableName] = Math.Max(0, remaining);
        }

        if (context.Response.StatusCode == 429)
        {
            context.Response = new MockResponse { Headers = context.Response.Headers };
        }
    }

    private static void DenyRequest(GatewayContext context, RateLimitByKeyConfig config)
    {
        if (config.RetryAfterVariableName is not null)
        {
            context.Variables[config.RetryAfterVariableName] = config.RenewalPeriod;
        }

        context.Response = new MockResponse
        {
            StatusCode = 429,
            StatusReason = "Too Many Requests",
        };

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
}