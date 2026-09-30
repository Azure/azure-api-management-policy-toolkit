// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Data;

/// <summary>
/// Per-context rate and quota counters. Register a shared instance in
/// <see cref="ServiceRegistry"/> to model multiple requests against the same counters.
/// Handlers use the context's registered <see cref="TimeProvider"/> for renewal.
/// </summary>
/// <remarks>
/// Subscription keys retain the forms sub:{id} and quota:sub:{id}, with optional
/// :api:{id-or-name}:op:{id-or-name} suffixes. Keyed policies use
/// rate-limit-by-key:{key} and quota-by-key:{key} to avoid cross-policy collisions.
/// Subscription quota anchors are captured on first use so newly created mocks for
/// the same subscription cannot restart its quota. Reset a fixed counter before
/// changing its renewal period or a keyed quota's first-period-start.
/// </remarks>
public class RateLimitStore
{
    private readonly object _sync = new();
    private readonly Dictionary<string, Counter> _counters = new(StringComparer.Ordinal);

    internal int Increment(string key, int amount = 1)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentOutOfRangeException.ThrowIfNegative(amount);
        lock (_sync)
        {
            var count = checked((int)(GetCounter(key).Calls + amount));
            _counters[key] = new Counter { Calls = count };
            return count;
        }
    }

    /// <summary>
    /// Returns the last observed call count. Throws on counts larger than an Int32;
    /// use <see cref="GetCallCount"/> for deferred weighted counters.
    /// </summary>
    public int GetCount(string key) => checked((int)GetCallCount(key));

    /// <summary>
    /// Returns the last observed call count without narrowing deferred increments.
    /// Expiration is applied when a policy next accesses the counter.
    /// </summary>
    public long GetCallCount(string key)
    {
        ArgumentNullException.ThrowIfNull(key);
        lock (_sync)
        {
            return _counters.TryGetValue(key, out var counter) ? counter.Calls : 0;
        }
    }

    /// <summary>
    /// Returns accounted payload bytes, including response bytes settled at completion.
    /// </summary>
    public long GetBandwidth(string key)
    {
        ArgumentNullException.ThrowIfNull(key);
        lock (_sync)
        {
            return _counters.TryGetValue(key, out var counter) ? counter.Bandwidth : 0;
        }
    }

    /// <summary>
    /// Seeds a counter, replacing its previous window and bandwidth.
    /// Its renewal window is initialized on the next policy execution.
    /// </summary>
    public RateLimitStore SetCount(string key, int count)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        lock (_sync)
        {
            _counters[key] = new Counter { Calls = count };
        }

        return this;
    }

    /// <summary>
    /// Removes every counter and its renewal state.
    /// </summary>
    public RateLimitStore Reset()
    {
        lock (_sync)
        {
            _counters.Clear();
        }

        return this;
    }

    /// <summary>
    /// Removes the selected counter and its renewal state.
    /// </summary>
    public RateLimitStore Reset(string key)
    {
        ArgumentNullException.ThrowIfNull(key);
        lock (_sync)
        {
            _counters.Remove(key);
        }

        return this;
    }

    internal PolicyCounterResult TryConsume(
        IReadOnlyList<PolicyCounterLimit> limits,
        int calls,
        long bandwidth,
        DateTimeOffset now,
        bool checkLimits = true,
        bool commit = true,
        bool callsAlreadyCounted = false,
        bool bandwidthAlreadyCounted = false)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(calls);
        ArgumentOutOfRangeException.ThrowIfNegative(bandwidth);
        lock (_sync)
        {
            var counters = limits.Select(limit => Prepare(limit, now)).ToArray();
            var allowed = true;
            var retryAfter = 0;

            for (var i = 0; i < limits.Count; i++)
            {
                if (checkLimits && Exceeded(
                        limits[i], counters[i].Calls, counters[i].Bandwidth, calls, bandwidth,
                        callsAlreadyCounted, bandwidthAlreadyCounted))
                {
                    allowed = false;
                    retryAfter = Math.Max(retryAfter, GetRetryAfter(limits[i], counters[i], calls, bandwidth, now));
                }
            }

            if (!allowed)
            {
                return new PolicyCounterResult(false, 0, retryAfter);
            }

            var updated = counters.Select(counter =>
                (Calls: checked(counter.Calls + calls), Bandwidth: checked(counter.Bandwidth + bandwidth))).ToArray();

            var remaining = int.MaxValue;
            for (var i = 0; i < limits.Count; i++)
            {
                if (commit)
                {
                    counters[i].Calls = updated[i].Calls;
                    counters[i].Bandwidth = updated[i].Bandwidth;
                    if (limits[i].Sliding && (calls != 0 || bandwidth != 0))
                    {
                        counters[i].Increments.Enqueue(new CounterIncrement(now, calls, bandwidth));
                    }
                }

                if (limits[i].Calls is { } maximum)
                {
                    remaining = Math.Min(remaining, (int)Math.Max(0, maximum - updated[i].Calls));
                }
            }

            return new PolicyCounterResult(true, remaining, 0);
        }
    }

    internal int GetRetryAfter(PolicyCounterLimit limit, int calls, long bandwidth, DateTimeOffset now)
    {
        lock (_sync)
        {
            return GetRetryAfter(limit, Prepare(limit, now), calls, bandwidth, now);
        }
    }

    private Counter GetCounter(string key)
    {
        if (!_counters.TryGetValue(key, out var counter))
        {
            counter = new Counter();
            _counters.Add(key, counter);
        }

        return counter;
    }

    private Counter Prepare(PolicyCounterLimit limit, DateTimeOffset now)
    {
        var counter = GetCounter(limit.Key);
        if (counter.Sliding is null)
        {
            counter.Sliding = limit.Sliding;
            if (limit.Sliding && (counter.Calls != 0 || counter.Bandwidth != 0))
            {
                counter.Increments.Enqueue(new CounterIncrement(now, counter.Calls, counter.Bandwidth));
            }
        }
        else if (counter.Sliding != limit.Sliding)
        {
            throw new InvalidOperationException($"Counter '{limit.Key}' cannot mix rate and quota windows.");
        }

        if (limit.Sliding)
        {
            var duration = TimeSpan.FromSeconds(limit.RenewalPeriod);
            while (counter.Increments.TryPeek(out var increment) && now - increment.Timestamp >= duration)
            {
                counter.Increments.Dequeue();
                counter.Calls -= increment.Calls;
                counter.Bandwidth -= increment.Bandwidth;
            }
        }
        else
        {
            var anchor = limit.FirstPeriodStart ?? DateTimeOffset.MinValue;
            if (counter.FixedRenewalPeriod is { } period && period != limit.RenewalPeriod)
            {
                throw new InvalidOperationException($"Reset counter '{limit.Key}' before changing its quota renewal period.");
            }

            if (!limit.SubscriptionScoped && counter.FirstPeriodStart is { } previousStart && previousStart != anchor)
            {
                throw new InvalidOperationException($"Reset counter '{limit.Key}' before changing its first-period-start.");
            }

            counter.FixedRenewalPeriod ??= limit.RenewalPeriod;
            counter.FirstPeriodStart ??= anchor;
            var start = limit.RenewalPeriod == 0
                ? DateTimeOffset.MinValue
                : GetFixedWindowStart(now, counter.FirstPeriodStart.Value, limit.RenewalPeriod);
            if (counter.WindowStart is not null && counter.WindowStart != start)
            {
                counter.Calls = 0;
                counter.Bandwidth = 0;
            }

            counter.WindowStart = start;
        }

        return counter;
    }

    private static DateTimeOffset GetFixedWindowStart(DateTimeOffset now, DateTimeOffset anchor, int period)
    {
        var duration = (long)period * TimeSpan.TicksPerSecond;
        var periods = Math.DivRem(now.UtcTicks - anchor.UtcTicks, duration, out var remainder);
        if (remainder < 0)
        {
            periods--;
        }

        return new DateTimeOffset(anchor.UtcTicks + (periods * duration), TimeSpan.Zero);
    }

    private static bool Exceeded(
        PolicyCounterLimit limit,
        long currentCalls,
        long currentBandwidth,
        int calls,
        long bandwidth,
        bool callsAlreadyCounted = false,
        bool bandwidthAlreadyCounted = false)
    {
        var callsExceeded = limit.Calls is { } maximumCalls &&
            (callsAlreadyCounted
                ? currentCalls > maximumCalls
                : currentCalls >= maximumCalls || calls > maximumCalls - currentCalls);
        var bandwidthExceeded = limit.Bandwidth is { } maximumBandwidth &&
            (bandwidthAlreadyCounted
                ? currentBandwidth > maximumBandwidth
                : currentBandwidth >= maximumBandwidth || bandwidth > maximumBandwidth - currentBandwidth);
        return callsExceeded || bandwidthExceeded;
    }

    private static int GetRetryAfter(
        PolicyCounterLimit limit,
        Counter counter,
        int calls,
        long bandwidth,
        DateTimeOffset now)
    {
        if (limit.RenewalPeriod == 0)
        {
            return 0;
        }

        if (!limit.Sliding)
        {
            return CeilingSeconds(counter.WindowStart!.Value.UtcTicks +
                ((long)limit.RenewalPeriod * TimeSpan.TicksPerSecond) - now.UtcTicks);
        }

        var remainingCalls = counter.Calls;
        var remainingBandwidth = counter.Bandwidth;
        foreach (var increment in counter.Increments)
        {
            remainingCalls -= increment.Calls;
            remainingBandwidth -= increment.Bandwidth;
            if (!Exceeded(limit, remainingCalls, remainingBandwidth, calls, bandwidth))
            {
                return CeilingSeconds(((long)limit.RenewalPeriod * TimeSpan.TicksPerSecond) -
                    (now - increment.Timestamp).Ticks);
            }
        }

        return limit.RenewalPeriod;
    }

    private static int CeilingSeconds(long ticks) =>
        (int)Math.Clamp((ticks + TimeSpan.TicksPerSecond - 1) / TimeSpan.TicksPerSecond, 0, int.MaxValue);

    private sealed class Counter
    {
        public long Calls { get; set; }
        public long Bandwidth { get; set; }
        public bool? Sliding { get; set; }
        public DateTimeOffset? WindowStart { get; set; }
        public DateTimeOffset? FirstPeriodStart { get; set; }
        public int? FixedRenewalPeriod { get; set; }
        public Queue<CounterIncrement> Increments { get; } = [];
    }

    private readonly record struct CounterIncrement(DateTimeOffset Timestamp, long Calls, long Bandwidth);
}