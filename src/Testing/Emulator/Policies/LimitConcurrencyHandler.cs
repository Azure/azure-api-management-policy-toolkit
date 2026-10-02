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
internal class LimitConcurrencyHandler : PolicyHandler<LimitConcurrencyConfig, Action>
{
    public override string PolicyName => nameof(IInboundContext.LimitConcurrency);

    protected override void Handle(GatewayContext context, LimitConcurrencyConfig config, Action section)
    {
        ArgumentException.ThrowIfNullOrEmpty(config.Key);
        ArgumentOutOfRangeException.ThrowIfNegative(config.MaxCount);
        ArgumentNullException.ThrowIfNull(section);

        IConcurrencyLimiter limiter;
        lock (context.Services)
        {
            limiter = context.Services.Resolve<IConcurrencyLimiter>() ?? new KeyedConcurrencyLimiter();
            if (!context.Services.HasService<IConcurrencyLimiter>())
            {
                context.Services.Register(limiter);
            }
        }

        var permit = limiter.TryAcquire(config.Key, config.MaxCount);
        if (permit is null)
        {
            ResponseUtilities.Overwrite(context.Response, 429, "Too Many Requests");
            context.ResponseTerminated = true;
            throw new FinishSectionProcessingException();
        }

        try
        {
            section();
            if (context.ResponseTerminated)
            {
                throw new FinishSectionProcessingException();
            }
        }
        finally
        {
            permit.Dispose();
        }
    }
}