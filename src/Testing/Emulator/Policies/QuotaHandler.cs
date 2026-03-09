// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Expressions;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Services;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Policies;

[Section(nameof(IInboundContext))]
internal class QuotaHandler : PolicyHandler<QuotaConfig>
{
    public override string PolicyName => nameof(IInboundContext.Quota);

    protected override void Handle(GatewayContext context, QuotaConfig config)
    {
        var limiter = context.Services.Resolve<IRateLimiter>();
        if (limiter is not null)
        {
            var key = $"quota:{context.Subscription?.Id ?? "anonymous"}";
            var allowed = limiter.TryConsumeAsync(key, 1).GetAwaiter().GetResult();
            if (!allowed)
            {
                ResponseUtilities.Overwrite(context.Response, 403, "Quota Exceeded");
                throw new FinishSectionProcessingException();
            }

            return;
        }

        var subscriptionKey = $"quota:sub:{context.Subscription.Id}";
        var currentCount = context.RateLimitStore.GetCount(subscriptionKey);

        if (config.Calls is not null && currentCount >= config.Calls)
        {
            ResponseUtilities.Overwrite(context.Response, 403, "Quota Exceeded");
            throw new FinishSectionProcessingException();
        }

        context.RateLimitStore.Increment(subscriptionKey);
    }
}
