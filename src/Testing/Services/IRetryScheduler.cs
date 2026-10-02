// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Services;

/// <summary>
/// Controls retry delays and exponential jitter. Register an implementation through
/// <see cref="ServiceRegistry"/> to make policy timing deterministic without sleeping.
/// </summary>
public interface IRetryScheduler
{
    /// <summary>
    /// Schedules a nonnegative delay before an additional retry attempt.
    /// </summary>
    void Delay(TimeSpan delay);

    /// <summary>
    /// Returns an exponential retry jitter factor between 0.8 and 1.2, inclusive.
    /// </summary>
    double NextJitterFactor();
}
