// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Expressions;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Services;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Policies;

[Section(nameof(IInboundContext))]
internal class QuotaByKeyHandler : PolicyHandler<QuotaByKeyConfig>
{
    public override string PolicyName => nameof(IInboundContext.QuotaByKey);

    protected override void Handle(GatewayContext context, QuotaByKeyConfig config)
    {
        var limiter = context.Services.Resolve<IRateLimiter>();
        if (limiter is not null)
        {
            var allowed = limiter.TryConsumeAsync(config.CounterKey, 1).GetAwaiter().GetResult();
            if (!allowed)
            {
                ResponseUtilities.Overwrite(context.Response, 429, "Too Many Requests");
                throw new FinishSectionProcessingException();
            }

            return;
        }

        var incrementCondition = config.IncrementCondition ?? true;
        var counterKey = $"quota:{config.CounterKey}";
        var currentCount = context.RateLimitStore.GetCount(counterKey);

        if (config.Calls is not null && currentCount >= config.Calls)
        {
            ResponseUtilities.Overwrite(context.Response, 403, "Quota Exceeded");
            throw new FinishSectionProcessingException();
        }

        if (incrementCondition)
        {
            var incrementCount = config.IncrementCount ?? 1;
            context.RateLimitStore.Increment(counterKey, incrementCount);
        }
    }
}
