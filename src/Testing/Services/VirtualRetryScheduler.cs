// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Services;

/// <summary>
/// Records virtual retry delays without waiting in real time. Exponential jitter uses
/// the deterministic midpoint factor of 1; inject <see cref="IRetryScheduler"/> for other timing.
/// </summary>
public sealed class VirtualRetryScheduler : IRetryScheduler
{
    private readonly List<TimeSpan> _delays = [];

    /// <summary>
    /// Gets the delays scheduled in execution order.
    /// </summary>
    public IReadOnlyList<TimeSpan> Delays => _delays.AsReadOnly();

    /// <inheritdoc />
    public void Delay(TimeSpan delay)
    {
        if (delay < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(delay), delay, "Retry delay cannot be negative.");
        }

        _delays.Add(delay);
    }

    /// <inheritdoc />
    public double NextJitterFactor() => 1;
}
