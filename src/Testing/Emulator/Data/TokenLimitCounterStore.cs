// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Data;

/// <summary>
/// Thread-safe observed token history shared by both token-limit aliases and all policy scopes.
/// Register the same instance in multiple GatewayContext service registries to share counters.
/// Keys are ordinal and are independent of the call/bandwidth limiter store.
/// </summary>
/// <remarks>
/// Rates use a classic-style rolling 60-second window, not the v2 token bucket.
/// Quotas use fixed UTC Hourly, Daily, Weekly, Monthly, or Yearly windows.
/// Weekly windows start on Monday. History is retained for all supported current windows,
/// so rate-only and quota-only declarations with the same key observe the same usage.
/// Estimates reserve capacity until reconciliation or window expiration; they are not actual usage.
/// Actual tokens are recorded at final-response completion, once per key per logical request.
/// Use one consistent clock across contexts sharing this store.
/// </remarks>
public sealed class TokenLimitCounterStore
{
    private const long RateWindowTicks = TimeSpan.TicksPerMinute;
    private readonly object _sync = new();
    private readonly Dictionary<string, Counter> _counters = new(StringComparer.Ordinal);

    /// <summary>Gets actual tokens observed in the rolling minute ending at the supplied UTC time.</summary>
    public long GetRateTokens(string key, DateTimeOffset utcNow)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        lock (_sync)
        {
            return _counters.TryGetValue(key, out var counter)
                ? SumUsage(Prepare(counter, utcNow), RateStart(utcNow), inclusive: false)
                : 0;
        }
    }

    /// <summary>
    /// Gets actual tokens in the specified current fixed UTC quota window, excluding estimates.
    /// Period names are Hourly, Daily, Weekly, Monthly, and Yearly (case insensitive).
    /// </summary>
    public long GetQuotaTokens(string key, string period, DateTimeOffset utcNow)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        var window = GetQuotaWindow(period, utcNow);
        lock (_sync)
        {
            return _counters.TryGetValue(key, out var counter)
                ? SumUsage(Prepare(counter, utcNow), window.Start.UtcTicks)
                : 0;
        }
    }

    internal TokenLimitCounterResult TryReserve(
        TokenLimitConfig config, TokenLimitReservation reservation, long promptTokens, TimeProvider clock)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(promptTokens);
        lock (_sync)
        {
            var now = clock.GetUtcNow().ToUniversalTime();
            var counter = GetCounter(config.CounterKey, now);
            var alreadyAdmitted = counter.Reservations.ContainsKey(reservation.Id);
            var rateUsed = checked(SumUsage(counter, RateStart(now), inclusive: false)
                + SumReservations(counter, RateStart(now), reservation.Id, inclusive: false));
            var rateProjected = checked(rateUsed + promptTokens);
            var quotaWindow = config.TokenQuota is not null
                ? GetQuotaWindow(config.TokenQuotaPeriod!, now)
                : ((DateTimeOffset Start, DateTimeOffset End)?)null;
            var quotaUsed = quotaWindow is { } window
                ? checked(SumUsage(counter, window.Start.UtcTicks)
                    + SumReservations(counter, window.Start.UtcTicks, reservation.Id))
                : 0;
            var quotaProjected = checked(quotaUsed + promptTokens);
            var rateExceeded = config.TokensPerMinute is { } rate
                && Exceeded(rateUsed, rateProjected, rate, alreadyAdmitted);
            var quotaExceeded = config.TokenQuota is { } quota
                && Exceeded(quotaUsed, quotaProjected, quota, alreadyAdmitted);

            if (rateExceeded || quotaExceeded)
            {
                var snapshot = Snapshot(counter, config, now);
                return snapshot with
                {
                    Rejection = rateExceeded ? TokenLimitRejection.Rate : TokenLimitRejection.Quota,
                    RetryAfter = rateExceeded
                        ? RateRetryAfter(counter, reservation.Id, promptTokens, config.TokensPerMinute!.Value,
                            alreadyAdmitted, now)
                        : CeilingSeconds(quotaWindow!.Value.End.UtcTicks - now.UtcTicks)
                };
            }

            counter.Reservations[reservation.Id] = new TokenEntry(now.UtcTicks, promptTokens);
            return Snapshot(counter, config, now);
        }
    }

    internal TokenLimitCounterResult[] Complete(
        IReadOnlyList<TokenLimitReservation> reservations,
        IReadOnlyList<TokenLimitConfig> outputs,
        long actualTokens,
        TimeProvider clock)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(actualTokens);
        lock (_sync)
        {
            var now = clock.GetUtcNow().ToUniversalTime();
            var counters = reservations.Select(reservation => GetCounter(reservation.Key, now)).ToArray();
            for (var i = 0; i < counters.Length; i++)
            {
                if (!counters[i].Reservations.ContainsKey(reservations[i].Id))
                {
                    throw new InvalidOperationException("The token-limit prompt reservation is unavailable.");
                }

                // Validate every key before committing any actual usage or releasing estimates.
                _ = checked(SumUsage(counters[i], 0)
                    + SumReservations(counters[i], 0, reservations[i].Id) + actualTokens);
            }

            for (var i = 0; i < counters.Length; i++)
            {
                counters[i].Reservations.Remove(reservations[i].Id);
                if (actualTokens != 0)
                {
                    counters[i].Usage.Add(new TokenEntry(now.UtcTicks, actualTokens));
                }
            }

            return outputs.Select(config => Snapshot(_counters[config.CounterKey], config, now)).ToArray();
        }
    }

    internal TokenLimitCounterResult[] Cancel(
        IReadOnlyList<TokenLimitReservation> reservations,
        IReadOnlyList<TokenLimitConfig> outputs,
        TimeProvider clock)
    {
        lock (_sync)
        {
            var now = clock.GetUtcNow().ToUniversalTime();
            foreach (var config in outputs)
            {
                GetCounter(config.CounterKey, now);
            }

            foreach (var reservation in reservations)
            {
                _counters[reservation.Key].Reservations.Remove(reservation.Id);
            }

            return outputs.Select(config => Snapshot(_counters[config.CounterKey], config, now)).ToArray();
        }
    }

    internal static void ValidateQuotaPeriod(string period) => _ = GetQuotaWindow(period, DateTimeOffset.UnixEpoch);

    private Counter GetCounter(string key, DateTimeOffset now)
    {
        if (!_counters.TryGetValue(key, out var counter))
        {
            counter = new Counter();
            _counters.Add(key, counter);
        }

        return Prepare(counter, now);
    }

    private static Counter Prepare(Counter counter, DateTimeOffset now)
    {
        if (counter.LastObservedTicks is { } last && now.UtcTicks < last)
        {
            throw new InvalidOperationException(
                "TokenLimitCounterStore requires a consistent, nondecreasing UTC clock for each shared key.");
        }

        counter.LastObservedTicks = now.UtcTicks;
        var year = new DateTimeOffset(now.UtcDateTime.Year, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var week = WeekStart(now);
        var oldest = Math.Min(RateStart(now), Math.Min(year.UtcTicks, week.UtcTicks));
        counter.Usage.RemoveAll(entry => entry.Timestamp < oldest);
        return counter;
    }

    private static long RateStart(DateTimeOffset now) => now.UtcTicks - RateWindowTicks;

    private static long SumUsage(Counter counter, long start, bool inclusive = true)
    {
        var total = 0L;
        foreach (var entry in counter.Usage)
        {
            if (inclusive ? entry.Timestamp >= start : entry.Timestamp > start)
            {
                total = checked(total + entry.Tokens);
            }
        }

        return total;
    }

    private static long SumReservations(Counter counter, long start, Guid? except = null, bool inclusive = true)
    {
        var total = 0L;
        foreach (var (id, entry) in counter.Reservations)
        {
            if (id != except && (inclusive ? entry.Timestamp >= start : entry.Timestamp > start))
            {
                total = checked(total + entry.Tokens);
            }
        }

        return total;
    }

    private static TokenLimitCounterResult Snapshot(Counter counter, TokenLimitConfig config, DateTimeOffset now)
    {
        long? remainingRate = null;
        long? remainingQuota = null;
        if (config.TokensPerMinute is { } rate)
        {
            var used = checked(SumUsage(counter, RateStart(now), inclusive: false)
                + SumReservations(counter, RateStart(now), inclusive: false));
            remainingRate = Math.Max(0, rate - used);
        }

        if (config.TokenQuota is { } quota)
        {
            var start = GetQuotaWindow(config.TokenQuotaPeriod!, now).Start.UtcTicks;
            var used = checked(SumUsage(counter, start) + SumReservations(counter, start));
            remainingQuota = Math.Max(0, quota - used);
        }

        return new TokenLimitCounterResult(TokenLimitRejection.None, remainingRate, remainingQuota, 0);
    }

    private static bool Exceeded(long used, long projected, int maximum, bool alreadyAdmitted) =>
        projected > maximum || (!alreadyAdmitted && used >= maximum);

    private static int RateRetryAfter(
        Counter counter, Guid reservationId, long promptTokens, int maximum, bool alreadyAdmitted, DateTimeOffset now)
    {
        var start = RateStart(now);
        var entries = counter.Usage
            .Concat(counter.Reservations.Where(pair => pair.Key != reservationId).Select(pair => pair.Value))
            .Where(entry => entry.Timestamp > start)
            .OrderBy(entry => entry.Timestamp)
            .ToArray();
        var used = 0L;
        foreach (var entry in entries)
        {
            used = checked(used + entry.Tokens);
        }

        foreach (var entry in entries)
        {
            used -= entry.Tokens;
            if (!Exceeded(used, checked(used + promptTokens), maximum, alreadyAdmitted))
            {
                return CeilingSeconds(entry.Timestamp + RateWindowTicks - now.UtcTicks);
            }
        }

        return 60;
    }

    private static (DateTimeOffset Start, DateTimeOffset End) GetQuotaWindow(string period, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(period);
        now = now.ToUniversalTime();
        var day = new DateTimeOffset(now.Year, now.Month, now.Day, 0, 0, 0, TimeSpan.Zero);
        return period.ToLowerInvariant() switch
        {
            "hourly" => HourWindow(now),
            "daily" => (day, day.AddDays(1)),
            "weekly" => (WeekStart(now), WeekStart(now).AddDays(7)),
            "monthly" => MonthWindow(now),
            "yearly" => YearWindow(now),
            _ => throw new ArgumentException(
                "TokenQuotaPeriod must be Hourly, Daily, Weekly, Monthly, or Yearly.", nameof(period))
        };
    }

    private static (DateTimeOffset, DateTimeOffset) HourWindow(DateTimeOffset now)
    {
        var start = new DateTimeOffset(now.Year, now.Month, now.Day, now.Hour, 0, 0, TimeSpan.Zero);
        return (start, start.AddHours(1));
    }

    private static (DateTimeOffset, DateTimeOffset) MonthWindow(DateTimeOffset now)
    {
        var start = new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero);
        return (start, start.AddMonths(1));
    }

    private static (DateTimeOffset, DateTimeOffset) YearWindow(DateTimeOffset now)
    {
        var start = new DateTimeOffset(now.Year, 1, 1, 0, 0, 0, TimeSpan.Zero);
        return (start, start.AddYears(1));
    }

    private static DateTimeOffset WeekStart(DateTimeOffset now)
    {
        var utc = now.UtcDateTime;
        var day = new DateTimeOffset(utc.Year, utc.Month, utc.Day, 0, 0, 0, TimeSpan.Zero);
        return day.AddDays(-(((int)utc.DayOfWeek + 6) % 7));
    }

    private static int CeilingSeconds(long ticks)
    {
        var seconds = Math.DivRem(Math.Max(0, ticks), TimeSpan.TicksPerSecond, out var remainder);
        return checked((int)(seconds + (remainder == 0 ? 0 : 1)));
    }

    private sealed class Counter
    {
        public long? LastObservedTicks { get; set; }
        public List<TokenEntry> Usage { get; } = [];
        public Dictionary<Guid, TokenEntry> Reservations { get; } = [];
    }

    private readonly record struct TokenEntry(long Timestamp, long Tokens);
}

internal readonly record struct TokenLimitReservation(string Key, Guid Id);

internal enum TokenLimitRejection
{
    None,
    Rate,
    Quota
}

internal readonly record struct TokenLimitCounterResult(
    TokenLimitRejection Rejection, long? RemainingTokens, long? RemainingQuotaTokens, int RetryAfter);