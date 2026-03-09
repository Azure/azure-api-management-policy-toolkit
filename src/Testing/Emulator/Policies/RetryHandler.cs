// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Policies;

[
    Section(nameof(IInboundContext)),
    Section(nameof(IBackendContext)),
    Section(nameof(IOutboundContext)),
    Section(nameof(IOnErrorContext))
]
internal class RetryHandler : PolicyHandler<RetryConfig, Action>
{
    public override string PolicyName => nameof(IInboundContext.Retry);

    protected override void Handle(GatewayContext context, RetryConfig config, Action section)
    {
        var firstFastRetry = config.FirstFastRetry ?? false;
        var shouldRetry = config.ConditionEvaluator ?? (() => config.Condition);

        for (int attempt = 0; attempt <= config.Count; attempt++)
        {
            bool completedSuccessfully = false;

            try
            {
                section();
                completedSuccessfully = true;
            }
            catch (FinishSectionProcessingException)
            {
                throw;
            }
            catch when (attempt < config.Count && shouldRetry())
            {
                WaitBeforeRetry(attempt, config.Interval, firstFastRetry);
                continue;
            }

            if (attempt >= config.Count || !shouldRetry())
            {
                return;
            }

            if (completedSuccessfully)
            {
                WaitBeforeRetry(attempt, config.Interval, firstFastRetry);
            }
        }
    }

    private static void WaitBeforeRetry(int attempt, int? interval, bool firstFastRetry)
    {
        if (attempt == 0 && firstFastRetry)
        {
            return;
        }

        if ((interval ?? 0) > 0)
        {
            Thread.Sleep(TimeSpan.FromSeconds(interval!.Value));
        }
    }
}
