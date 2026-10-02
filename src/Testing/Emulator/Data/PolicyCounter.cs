// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Data;

internal sealed record PolicyCounterLimit(
    string Key,
    string LimiterKey,
    int? Calls,
    long? Bandwidth,
    int RenewalPeriod,
    bool Sliding,
    DateTimeOffset? FirstPeriodStart = null,
    bool SubscriptionScoped = false)
{
    public void Validate()
    {
        ArgumentException.ThrowIfNullOrEmpty(Key);
        ArgumentException.ThrowIfNullOrEmpty(LimiterKey);
        if (Calls is { } calls)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(calls, nameof(Calls));
        }

        if (Bandwidth is { } bandwidth)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(bandwidth, nameof(Bandwidth));
        }

        if (Calls is null && Bandwidth is null)
        {
            throw new ArgumentException("A counter requires a calls or bandwidth limit.");
        }

        ArgumentOutOfRangeException.ThrowIfNegative(RenewalPeriod);
        if (Sliding && RenewalPeriod == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(RenewalPeriod), "Rate limits require a positive renewal period.");
        }
    }
}

internal readonly record struct PolicyCounterResult(bool Allowed, int RemainingCalls, int RetryAfter);