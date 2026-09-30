// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Globalization;
using System.Text;

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Data;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Expressions;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Services;

/// <summary>
/// Request-local limiter bookkeeping and response settlement. Handlers register this
/// service automatically. A request runner should call <see cref="CompleteResponse"/>
/// after outbound processing, including an early return-response, before changing RequestId.
/// </summary>
/// <remarks>
/// Register the clock as TimeProvider, the optional limiter as IRateLimiter, and
/// optionally a shared RateLimitStore in the context's services.
/// Quota bandwidth is UTF-8 payload volume in 1024-byte kilobytes, excluding HTTP framing.
/// Request bytes are charged at admission; final response bytes are charged at completion
/// and any response-volume overage is enforced on the next request.
/// IRateLimiter is an additional admission gate: zero permits checks admission without
/// consuming calls. Its Boolean contract supplies no remote remaining/reset metadata or rollback.
/// Multi-scope provider calls and a concurrent local admission cannot be rolled back remotely.
/// Expression values are already materialized by the authored C# invocation;
/// IncrementAfterResponse defers counting, not re-evaluation of those scalar values.
/// </remarks>
public sealed class PolicyCounterService
{
    private readonly GatewayContext _context;
    private readonly object _sync = new();
    private readonly Queue<Action> _rateIncrements = [];
    private readonly Queue<Action> _responseBandwidth = [];
    private readonly HashSet<string> _quotaCallKeys = new(StringComparer.Ordinal);
    private readonly HashSet<string> _requestBandwidthKeys = new(StringComparer.Ordinal);
    private readonly HashSet<string> _responseBandwidthKeys = new(StringComparer.Ordinal);
    private Guid _requestId;

    /// <summary>
    /// Creates request bookkeeping for one gateway context, not shared request state.
    /// Share the underlying RateLimitStore instead.
    /// </summary>
    public PolicyCounterService(GatewayContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
        _requestId = context.RequestId;
    }

    private RateLimitStore Store => _context.Services.Resolve<RateLimitStore>() ?? _context.RateLimitStore;
    private DateTimeOffset UtcNow => (_context.Services.Resolve<TimeProvider>() ?? TimeProvider.System).GetUtcNow();
    internal bool HasPendingResponse
    {
        get
        {
            lock (_sync)
            {
                return _rateIncrements.Count != 0 || _responseBandwidth.Count != 0;
            }
        }
    }

    internal static PolicyCounterService For(GatewayContext context)
    {
        lock (context.Services)
        {
            var service = context.Services.Resolve<PolicyCounterService>();
            if (service is null)
            {
                service = new PolicyCounterService(context);
                context.Services.Register(service);
            }
            else if (!ReferenceEquals(service._context, context))
            {
                throw new InvalidOperationException("PolicyCounterService belongs to another gateway context. Share RateLimitStore instead.");
            }

            return service;
        }
    }

    /// <summary>
    /// Settles deferred call counts and final response bandwidth once. Provider and payload
    /// errors propagate. A limiter rejection sets the response and terminates processing.
    /// </summary>
    public void CompleteResponse()
    {
        lock (_sync)
        {
            if (HasPendingResponse && _requestId != _context.RequestId)
            {
                throw new InvalidOperationException("Complete the previous limiter response before changing RequestId.");
            }

            var terminated = false;
            while (_rateIncrements.TryDequeue(out var increment))
            {
                try
                {
                    increment();
                }
                catch (FinishSectionProcessingException)
                {
                    terminated = true;
                }
            }

            // Admission vetoes can replace the response; measure only the final payload.
            while (_responseBandwidth.TryDequeue(out var accountBandwidth))
            {
                accountBandwidth();
            }

            if (terminated)
            {
                throw new FinishSectionProcessingException();
            }
        }
    }

    internal PolicyCounterResult Consume(
        IReadOnlyList<PolicyCounterLimit> limits,
        int calls,
        long bandwidth = 0,
        bool oncePerRequest = false,
        bool countRequest = true)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(calls);
        ArgumentOutOfRangeException.ThrowIfNegative(bandwidth);
        foreach (var limit in limits)
        {
            limit.Validate();
        }

        if (limits.Count == 0 || limits.Select(limit => limit.Key).Distinct(StringComparer.Ordinal).Count() != limits.Count)
        {
            throw new ArgumentException("Each counter must be configured once per policy call.", nameof(limits));
        }

        lock (_sync)
        {
            EnsureRequest();
            var key = limits[0].Key;
            var callsAlreadyCounted = oncePerRequest && _quotaCallKeys.Contains(key);
            var bandwidthAlreadyCounted = oncePerRequest && _requestBandwidthKeys.Contains(key);
            var callsToConsume = callsAlreadyCounted ? 0 : calls;
            var bandwidthToConsume = bandwidthAlreadyCounted ? 0 : bandwidth;
            var result = ConsumeStore(
                limits, callsToConsume, bandwidthToConsume,
                callsAlreadyCounted: callsAlreadyCounted,
                bandwidthAlreadyCounted: bandwidthAlreadyCounted,
                checkAdmission: !callsAlreadyCounted);
            if (result.Allowed && oncePerRequest && countRequest)
            {
                if (callsToConsume != 0)
                {
                    _quotaCallKeys.Add(key);
                }

                if (bandwidthToConsume != 0)
                {
                    _requestBandwidthKeys.Add(key);
                }
            }

            return result;
        }
    }

    internal void DeferRateIncrement(IReadOnlyList<PolicyCounterLimit> limits, int calls, RateLimitOutput output)
    {
        if (calls == 0)
        {
            return;
        }

        lock (_sync)
        {
            EnsureRequest();
            _rateIncrements.Enqueue(() =>
            {
                var result = ConsumeStore(limits, calls, 0, checkLimits: false);
                ApplyRateLimit(result, output, writeHeaders: !result.Allowed || !_context.ResponseTerminated);
            });
        }
    }

    internal void DeferQuotaResponseBandwidth(IReadOnlyList<PolicyCounterLimit> limits, bool oncePerRequest = false)
    {
        if (!limits.Any(limit => limit.Bandwidth is not null))
        {
            return;
        }

        lock (_sync)
        {
            EnsureRequest();
            if (oncePerRequest && !_responseBandwidthKeys.Add(limits[0].Key))
            {
                return;
            }

            _responseBandwidth.Enqueue(() =>
            {
                var bytes = GetMessageLength(_context.Response);
                if (bytes != 0)
                {
                    Store.TryConsume(limits, 0, bytes, UtcNow, checkLimits: false);
                }
            });
        }
    }

    private PolicyCounterResult ConsumeStore(
        IReadOnlyList<PolicyCounterLimit> limits,
        int calls,
        long bandwidth,
        bool checkLimits = true,
        bool callsAlreadyCounted = false,
        bool bandwidthAlreadyCounted = false,
        bool checkAdmission = true)
    {
        var store = Store;
        var result = store.TryConsume(
            limits, calls, bandwidth, UtcNow, checkLimits: checkLimits, commit: false,
            callsAlreadyCounted: callsAlreadyCounted, bandwidthAlreadyCounted: bandwidthAlreadyCounted);
        if (!result.Allowed)
        {
            return result;
        }

        var limiter = checkAdmission ? _context.Services.Resolve<IRateLimiter>() : null;
        if (limiter is not null)
        {
            foreach (var limit in limits)
            {
                if (!limiter.TryConsumeAsync(limit.LimiterKey, calls).GetAwaiter().GetResult())
                {
                    return new PolicyCounterResult(false, 0, store.GetRetryAfter(limit, calls, bandwidth, UtcNow));
                }
            }
        }

        return store.TryConsume(
            limits, calls, bandwidth, UtcNow, checkLimits: checkLimits,
            callsAlreadyCounted: callsAlreadyCounted, bandwidthAlreadyCounted: bandwidthAlreadyCounted);
    }

    private void EnsureRequest()
    {
        if (_requestId == _context.RequestId)
        {
            return;
        }

        if (HasPendingResponse)
        {
            throw new InvalidOperationException("Complete the previous limiter response before changing RequestId.");
        }

        _requestId = _context.RequestId;
        _quotaCallKeys.Clear();
        _requestBandwidthKeys.Clear();
        _responseBandwidthKeys.Clear();
    }

    internal void ApplyRateLimit(
        PolicyCounterResult result,
        RateLimitOutput output,
        bool restoreSuccessfulResponse = false,
        bool writeHeaders = true)
    {
        if (!result.Allowed)
        {
            ResponseUtilities.Overwrite(_context.Response, 429, "Too Many Requests");
            _context.ResponseTerminated = true;
        }
        else if (restoreSuccessfulResponse && _context.Response.StatusCode == 429)
        {
            var headers = _context.Response.Headers.ToArray();
            ResponseUtilities.Overwrite(_context.Response, 200, "OK");
            foreach (var header in headers)
            {
                _context.Response.Headers[header.Key] = header.Value;
            }

            _context.Response.Headers.Remove(output.RetryAfterHeaderName ?? "Retry-After");
            _context.ResponseTerminated = false;
        }

        var remaining = result.Allowed ? result.RemainingCalls : 0;
        if (writeHeaders && output.RemainingCallsHeaderName is not null)
        {
            _context.Response.Headers[output.RemainingCallsHeaderName] = [remaining.ToString(CultureInfo.InvariantCulture)];
        }

        if (output.RemainingCallsVariableName is not null)
        {
            _context.Variables[output.RemainingCallsVariableName] = remaining;
        }

        if (writeHeaders && output.TotalCallsHeaderName is not null)
        {
            _context.Response.Headers[output.TotalCallsHeaderName] = [output.Calls.ToString(CultureInfo.InvariantCulture)];
        }

        if (output.RetryAfterVariableName is not null)
        {
            _context.Variables[output.RetryAfterVariableName] = result.RetryAfter;
        }

        if (!result.Allowed)
        {
            _context.Response.Headers[output.RetryAfterHeaderName ?? "Retry-After"] =
                [result.RetryAfter.ToString(CultureInfo.InvariantCulture)];
            throw new FinishSectionProcessingException();
        }
    }

    internal void ApplyQuota(PolicyCounterResult result)
    {
        if (result.Allowed)
        {
            return;
        }

        ResponseUtilities.Overwrite(_context.Response, 403, "Quota Exceeded");
        _context.ResponseTerminated = true;
        if (result.RetryAfter > 0)
        {
            _context.Response.Headers["Retry-After"] = [result.RetryAfter.ToString(CultureInfo.InvariantCulture)];
        }

        throw new FinishSectionProcessingException();
    }

    internal static long GetMessageLength(MockMessage message)
    {
        if (message.Body.Content is { } content)
        {
            return Encoding.UTF8.GetByteCount(content);
        }

        var lengths = message.Headers
            .Where(header => string.Equals(header.Key, "Content-Length", StringComparison.OrdinalIgnoreCase))
            .SelectMany(header => header.Value)
            .ToArray();
        if (lengths.Length == 0)
        {
            return 0;
        }

        if (lengths.Length != 1 ||
            !long.TryParse(lengths[0], NumberStyles.None, CultureInfo.InvariantCulture, out var bytes) || bytes < 0)
        {
            throw new FormatException("Content-Length must contain one nonnegative integer.");
        }

        return bytes;
    }

    internal static DateTimeOffset ParseFirstPeriodStart(string? start)
    {
        if (start is null)
        {
            return DateTimeOffset.MinValue;
        }

        if (!DateTimeOffset.TryParseExact(start, "yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsed))
        {
            throw new FormatException("FirstPeriodStart must have the format yyyy-MM-ddTHH:mm:ssZ.");
        }

        return parsed;
    }

    internal static DateTimeOffset SubscriptionStart(GatewayContext context)
    {
        var start = context.Subscription.StartDate ?? context.Subscription.CreatedDate;
        return new DateTimeOffset(start.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(start, DateTimeKind.Utc) : start);
    }

    internal static bool MatchesEntity(string? id, string? name, string contextId, string contextName)
    {
        if (string.IsNullOrEmpty(id) && string.IsNullOrEmpty(name))
        {
            throw new ArgumentException("An API or operation limit requires an id or name.");
        }

        return id is not null
            ? string.Equals(id, contextId, StringComparison.OrdinalIgnoreCase)
            : string.Equals(name, contextName, StringComparison.OrdinalIgnoreCase);
    }
}

internal sealed record RateLimitOutput(
    int Calls,
    string? RetryAfterHeaderName,
    string? RetryAfterVariableName,
    string? RemainingCallsHeaderName,
    string? RemainingCallsVariableName,
    string? TotalCallsHeaderName)
{
    public static RateLimitOutput From(RateLimitConfig config) => new(
        config.Calls, config.RetryAfterHeaderName, config.RetryAfterVariableName,
        config.RemainingCallsHeaderName, config.RemainingCallsVariableName, config.TotalCallsHeaderName);

    public static RateLimitOutput From(RateLimitByKeyConfig config) => new(
        config.Calls, config.RetryAfterHeaderName, config.RetryAfterVariableName,
        config.RemainingCallsHeaderName, config.RemainingCallsVariableName, config.TotalCallsHeaderName);
}