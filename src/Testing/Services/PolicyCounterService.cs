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
/// Generated response headers survive backend forwarding, while subsequent explicit
/// response-header overrides and removals remain authoritative.
/// Typed Wait owns one service instance per context while sharing the logical request ledger.
/// Provider awaits never hold the shared ledger monitor. Same-key keyed quota admissions
/// are serialized separately so request calls and bandwidth remain counted once.
/// Admissions and deferred counts are not rolled back when a branch loses or fails.
/// Response-phase branch variable outputs are not copied to the parent after Wait completes;
/// shared response headers and counters still settle against the final logical response.
/// </remarks>
public sealed class PolicyCounterService
{
    private readonly GatewayContext _context;
    private readonly RequestState _state;
    private object _sync => _state.Sync;
    private Queue<DeferredRate> _rateIncrements => _state.RateIncrements;
    private Queue<IReadOnlyList<PolicyCounterLimit>> _responseBandwidth => _state.ResponseBandwidth;
    private HashSet<string> _quotaCallKeys => _state.QuotaCallKeys;
    private HashSet<string> _requestBandwidthKeys => _state.RequestBandwidthKeys;
    private HashSet<string> _responseBandwidthKeys => _state.ResponseBandwidthKeys;
    private ref Guid _requestId => ref _state.RequestId;
    private ref bool _settling => ref _state.Settling;

    /// <summary>
    /// Creates request bookkeeping for one gateway context, not shared request state.
    /// Share the underlying RateLimitStore instead.
    /// </summary>
    public PolicyCounterService(GatewayContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
        _state = new RequestState();
        _requestId = context.RequestId;
    }

    private PolicyCounterService(GatewayContext context, RequestState state)
    {
        _context = context;
        _state = state;
    }

    internal PolicyCounterService ForkForWait(GatewayContext context) => new(context, _state);

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
                service = context.WaitMessages is { Owner: var owner } && !ReferenceEquals(owner, context)
                    ? For(owner).ForkForWait(context) : new PolicyCounterService(context);
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
    /// errors propagate and leave the failed operation pending for a completion-only retry;
    /// already settled operations are not replayed. A limiter rejection sets the response
    /// and terminates processing.
    /// </summary>
    public void CompleteResponse()
    {
        Guid requestId;
        lock (_sync)
        {
            if (_settling)
            {
                throw new InvalidOperationException("Limiter response settlement is already running.");
            }

            if (HasPendingResponse && _requestId != _context.RequestId)
            {
                throw new InvalidOperationException("Complete the previous limiter response before changing RequestId.");
            }

            _settling = true;
            requestId = _requestId;
        }

        try
        {
            var terminated = false;
            while (PeekResponseWork(_rateIncrements) is { } increment)
            {
                try
                {
                    var result = ConsumeStore(increment.Limits, increment.Calls, requestId);
                    ApplyRateLimit(result, increment.Output, writeHeaders: !result.Allowed || !_context.ResponseTerminated,
                        writeVariables: ReferenceEquals(increment.Origin, _context));
                }
                catch (FinishSectionProcessingException)
                {
                    terminated = true;
                }
                lock (_sync)
                {
                    _rateIncrements.Dequeue();
                }
            }

            // Admission vetoes can replace the response; measure only the final payload.
            while (PeekResponseWork(_responseBandwidth) is { } accountBandwidth)
            {
                var bytes = GetMessageLength(_context.Response);
                lock (_sync)
                {
                    EnsureCurrentRequest(requestId);
                    if (bytes != 0)
                    {
                        Store.TryConsume(accountBandwidth, 0, bytes, UtcNow, checkLimits: false);
                    }
                    _responseBandwidth.Dequeue();
                }
            }

            if (terminated)
            {
                throw new FinishSectionProcessingException();
            }
        }
        finally
        {
            lock (_sync)
            {
                _settling = false;
            }
        }
    }

    private T? PeekResponseWork<T>(Queue<T> work) where T : class
    {
        lock (_sync)
        {
            return work.TryPeek(out var pending) ? pending : null;
        }
    }

    internal PolicyCounterResult Consume(
        IReadOnlyList<PolicyCounterLimit> limits,
        int calls,
        long bandwidth = 0,
        bool oncePerRequest = false,
        bool countRequest = true) =>
        ConsumeCore(limits, calls, bandwidth, oncePerRequest, countRequest, null, false);

    internal PolicyCounterResult ConsumeRate(
        IReadOnlyList<PolicyCounterLimit> limits, int calls, bool deferred, RateLimitOutput output)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(calls);
        var increment = deferred && calls != 0 ? new DeferredRate(limits, calls, output, _context) : null;
        return ConsumeCore(limits, deferred ? 0 : calls, 0, false, true, increment, false);
    }

    internal PolicyCounterResult ConsumeQuota(
        IReadOnlyList<PolicyCounterLimit> limits, int calls, long bandwidth,
        bool oncePerRequest = false, bool countRequest = true) =>
        ConsumeCore(limits, calls, bandwidth, oncePerRequest, countRequest, null, countRequest);

    private PolicyCounterResult ConsumeCore(
        IReadOnlyList<PolicyCounterLimit> limits, int calls, long bandwidth,
        bool oncePerRequest, bool countRequest, DeferredRate? deferredRate, bool responseBandwidth)
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

        var cancellationToken = HttpPolicyTransport.GetCancellationToken(_context);
        cancellationToken.ThrowIfCancellationRequested();
        var key = limits[0].Key;
        SemaphoreSlim? admission = null;
        Guid requestId;
        lock (_sync)
        {
            EnsureRequest();
            requestId = _requestId;
            EnsureAdmission(requestId, cancellationToken);
            if (oncePerRequest && !_state.QuotaAdmissions.TryGetValue(key, out admission))
            {
                admission = new SemaphoreSlim(1, 1);
                _state.QuotaAdmissions.Add(key, admission);
            }
        }

        admission?.Wait(cancellationToken);
        try
        {
            bool callsAlreadyCounted;
            bool bandwidthAlreadyCounted;
            lock (_sync)
            {
                EnsureAdmission(requestId, cancellationToken);
                callsAlreadyCounted = oncePerRequest && _quotaCallKeys.Contains(key);
                bandwidthAlreadyCounted = oncePerRequest && _requestBandwidthKeys.Contains(key);
            }
            var callsToConsume = callsAlreadyCounted ? 0 : calls;
            var bandwidthToConsume = bandwidthAlreadyCounted ? 0 : bandwidth;
            var store = Store;
            var result = CheckStoreAdmission(
                store, limits, callsToConsume, bandwidthToConsume, checkLimits: true,
                callsAlreadyCounted, bandwidthAlreadyCounted, checkAdmission: !callsAlreadyCounted);

            lock (_sync)
            {
                EnsureAdmission(requestId, cancellationToken);
                if (!result.Allowed)
                {
                    return result;
                }

                result = store.TryConsume(
                    limits, callsToConsume, bandwidthToConsume, UtcNow,
                    callsAlreadyCounted: callsAlreadyCounted, bandwidthAlreadyCounted: bandwidthAlreadyCounted);
                if (result.Allowed)
                {
                    if (oncePerRequest && countRequest)
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
                    if (deferredRate is not null)
                    {
                        _rateIncrements.Enqueue(deferredRate);
                    }
                    if (responseBandwidth && limits.Any(limit => limit.Bandwidth is not null)
                        && (!oncePerRequest || _responseBandwidthKeys.Add(key)))
                    {
                        _responseBandwidth.Enqueue(limits);
                    }
                }

                return result;
            }
        }
        finally
        {
            admission?.Release();
        }
    }

    private void EnsureAdmission(Guid requestId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        EnsureCurrentRequest(requestId);
        if (_settling)
        {
            throw new InvalidOperationException("Cannot add limiter work during final response settlement.");
        }
    }

    private void EnsureCurrentRequest(Guid requestId)
    {
        if (_requestId != requestId || _context.RequestId != requestId)
        {
            throw new InvalidOperationException("RequestId must remain unchanged until limiter admission or settlement completes.");
        }
    }

    private PolicyCounterResult ConsumeStore(IReadOnlyList<PolicyCounterLimit> limits, int calls, Guid requestId)
    {
        var store = Store;
        var result = CheckStoreAdmission(store, limits, calls, 0, checkLimits: false);
        lock (_sync)
        {
            HttpPolicyTransport.GetCancellationToken(_context).ThrowIfCancellationRequested();
            EnsureCurrentRequest(requestId);
            return result.Allowed
                ? store.TryConsume(limits, calls, 0, UtcNow, checkLimits: false)
                : result;
        }
    }

    private PolicyCounterResult CheckStoreAdmission(
        RateLimitStore store, IReadOnlyList<PolicyCounterLimit> limits, int calls, long bandwidth,
        bool checkLimits,
        bool callsAlreadyCounted = false,
        bool bandwidthAlreadyCounted = false,
        bool checkAdmission = true)
    {
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
            var cancellationToken = HttpPolicyTransport.GetCancellationToken(_context);
            foreach (var limit in limits)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!PolicyServiceAwaiter.Wait(_context,
                        limiter.TryConsumeAsync(limit.LimiterKey, calls, cancellationToken)))
                {
                    return new PolicyCounterResult(false, 0, store.GetRetryAfter(limit, calls, bandwidth, UtcNow));
                }
            }
        }

        return result;
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
        _state.QuotaAdmissions.Clear();
    }

    internal void ApplyRateLimit(
        PolicyCounterResult result,
        RateLimitOutput output,
        bool restoreSuccessfulResponse = false,
        bool writeHeaders = true,
        bool writeVariables = true)
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

            ResponseHeaderUtilities.RemoveCaseVariants(_context.Response.Headers, output.RetryAfterHeaderName ?? "Retry-After");
            PolicyResponseHeaderOverlay.ForgetHeader(_context, output.RetryAfterHeaderName ?? "Retry-After");
            _context.ResponseTerminated = false;
        }

        var remaining = result.Allowed ? result.RemainingCalls : 0;
        if (writeHeaders && output.RemainingCallsHeaderName is not null)
        {
            WriteHeader(output.RemainingCallsHeaderName, remaining);
        }

        if (writeVariables && output.RemainingCallsVariableName is not null)
        {
            _context.Variables[output.RemainingCallsVariableName] = remaining;
        }

        if (writeHeaders && output.TotalCallsHeaderName is not null)
        {
            WriteHeader(output.TotalCallsHeaderName, output.Calls);
        }

        if (writeVariables && output.RetryAfterVariableName is not null)
        {
            _context.Variables[output.RetryAfterVariableName] = result.RetryAfter;
        }

        if (!result.Allowed)
        {
            WriteHeader(output.RetryAfterHeaderName ?? "Retry-After", result.RetryAfter);
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
            WriteHeader("Retry-After", result.RetryAfter);
        }

        throw new FinishSectionProcessingException();
    }

    private void WriteHeader(string? name, long value) =>
        PolicyResponseHeaderOverlay.SetNumericHeader(_context, name, value, typeof(PolicyCounterService));

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

    private sealed record DeferredRate(
        IReadOnlyList<PolicyCounterLimit> Limits, int Calls, RateLimitOutput Output, GatewayContext Origin);

    private sealed class RequestState
    {
        internal readonly object Sync = new();
        internal readonly Queue<DeferredRate> RateIncrements = [];
        internal readonly Queue<IReadOnlyList<PolicyCounterLimit>> ResponseBandwidth = [];
        internal readonly HashSet<string> QuotaCallKeys = new(StringComparer.Ordinal);
        internal readonly HashSet<string> RequestBandwidthKeys = new(StringComparer.Ordinal);
        internal readonly HashSet<string> ResponseBandwidthKeys = new(StringComparer.Ordinal);
        internal readonly Dictionary<string, SemaphoreSlim> QuotaAdmissions = new(StringComparer.Ordinal);
        internal Guid RequestId;
        internal bool Settling;
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