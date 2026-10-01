// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Data;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Services;

/// <summary>
/// Request-local token-limit registration and observed final-response settlement.
/// Handlers register this service automatically; share TokenLimitCounterStore, not this service.
/// </summary>
/// <remarks>
/// RunAll or the successful outer RunRequest settles usage after all response processing,
/// before deferred call limiters may overwrite it. Standalone sections require an explicit
/// request boundary or CompleteResponse. Keep RequestId unchanged until completion.
/// Each key is charged once, even when configured in multiple scopes or through both aliases.
/// Remaining/consumed variables are Int64; retry seconds are Int32. Actual consumed outputs
/// are published at backend observation, before outbound. Counters settle only at completion.
/// Provisional consumed variables are the prompt estimate, or zero.
/// JSON usage takes precedence over ILlmTokenUsageProvider. Both actual prompt and completion
/// counts are required after backend participation, including explicit observed zero for failures.
/// Body formatting alone does not replace observed usage. Manual backend fixtures may supply
/// a JSON usage envelope; forwarding callbacks are observed when they successfully return.
/// Later denials preserve earlier observed admissions; denied keys are not charged.
/// Proven terminal responses before any backend participation release unused prompt reservations.
/// Observed categories are nonnegative integers at most 2^53, matching the shared metric reader;
/// prompt + completion and accumulated counter arithmetic use checked Int64 values.
/// Missing/invalid usage leaves pending work available for corrected response or on-error processing.
/// Streaming and estimated image accounting are unsupported; unestimated images require actual usage.
/// A later call-limiter response replacement can clear token output headers; settled counts and
/// token variables still describe the observed LLM response, not the replacement error payload.
/// No backend calls, tokenizer heuristics, or fallback token counts are supplied.
/// </remarks>
public sealed class TokenLimitService
{
    private readonly GatewayContext _context;
    private readonly object _sync = new();
    private readonly Dictionary<string, PendingKey> _pending = new(StringComparer.Ordinal);
    private readonly List<Invocation> _invocations = [];
    private readonly HashSet<Guid> _completedRequests = [];
    private TokenLimitCounterStore _store = null!;
    private TimeProvider _clock = null!;
    private Guid _requestId;
    private bool _hasRequest;
    private bool _completed;
    private bool _settling;
    private long? _observedTokens;
    private bool _observationFailed;

    /// <summary>
    /// Creates bookkeeping owned by one context. Register shared storage and TimeProvider
    /// before admitting a request; dependencies must remain stable while usage is pending.
    /// </summary>
    public TokenLimitService(GatewayContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
        _requestId = context.RequestId;
        CaptureDependencies();
    }

    /// <summary>Gets whether observed final response usage is still required.</summary>
    public bool HasPendingResponse
    {
        get
        {
            lock (_sync)
            {
                return _pending.Count != 0;
            }
        }
    }

    internal bool HasCompletedResponse
    {
        get
        {
            lock (_sync)
            {
                return _hasRequest && _completed && _requestId == _context.RequestId;
            }
        }
    }

    internal static TokenLimitService For(GatewayContext context)
    {
        lock (context.Services)
        {
            var service = context.Services.Resolve<TokenLimitService>();
            if (service is null)
            {
                service = new TokenLimitService(context);
                context.Services.Register(service);
            }
            else
            {
                service.EnsureOwner(context);
            }

            return service;
        }
    }

    internal static void ValidateConfiguration(TokenLimitConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentException.ThrowIfNullOrWhiteSpace(config.CounterKey);
        if (config.TokensPerMinute is null && config.TokenQuota is null)
        {
            throw new ArgumentException("At least one of TokensPerMinute or TokenQuota must be configured.", nameof(config));
        }

        if (config.TokensPerMinute is { } rate)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(rate, nameof(config.TokensPerMinute));
        }

        if (config.TokenQuota is { } quota)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(quota, nameof(config.TokenQuota));
            ArgumentException.ThrowIfNullOrWhiteSpace(config.TokenQuotaPeriod);
            TokenLimitCounterStore.ValidateQuotaPeriod(config.TokenQuotaPeriod);
        }
        else if (config.TokenQuotaPeriod is not null)
        {
            throw new ArgumentException("TokenQuotaPeriod requires TokenQuota.", nameof(config.TokenQuotaPeriod));
        }

        if (config.TokensPerMinute is null
            && (config.RemainingTokensHeaderName is not null || config.RemainingTokensVariableName is not null))
        {
            throw new ArgumentException("RemainingTokens outputs require TokensPerMinute.", nameof(config));
        }

        if (config.TokenQuota is null
            && (config.RemainingQuotaTokensHeaderName is not null || config.RemainingQuotaTokensVariableName is not null))
        {
            throw new ArgumentException("RemainingQuotaTokens outputs require TokenQuota.", nameof(config));
        }

        string?[] headers =
        [
            config.RetryAfterHeaderName ?? "Retry-After", config.RemainingTokensHeaderName,
            config.RemainingQuotaTokensHeaderName, config.TokensConsumedHeaderName
        ];
        foreach (var header in headers.Where(name => name is not null))
        {
            if (string.IsNullOrWhiteSpace(header) || header.Any(character =>
                    !char.IsAsciiLetterOrDigit(character) && !"!#$%&'*+-.^_`|~".Contains(character)))
            {
                throw new ArgumentException("Token-limit output headers must have valid HTTP header names.", nameof(config));
            }
        }

        ValidateOutputNames(headers, StringComparer.OrdinalIgnoreCase);
        ValidateOutputNames(
            [config.RetryAfterVariableName, config.RemainingTokensVariableName,
                config.RemainingQuotaTokensVariableName, config.TokensConsumedVariableName],
            StringComparer.Ordinal);
    }

    internal void Register(TokenLimitConfig config, string policyName)
    {
        lock (_sync)
        {
            if (_settling)
            {
                throw new InvalidOperationException("Cannot add token-limit work during final response settlement.");
            }

            EnsureRequest();
            if (_completed)
            {
                throw new InvalidOperationException(
                    "Token-limit response for this RequestId has completed. Use a fresh RequestId.");
            }

            if (!_hasRequest)
            {
                CaptureDependencies();
            }

            EnsureDependencies();
            _hasRequest = true;
            var estimator = config.EstimatePromptToken
                ? _context.Services.Resolve<ITokenLimitPromptEstimator>()
                    ?? throw new InvalidOperationException(
                        "EstimatePromptToken requires an actual tokenizer registered as ITokenLimitPromptEstimator.")
                : null;
            ValidateRequestSupport(config.EstimatePromptToken);
            var estimate = estimator?.EstimatePromptTokens(_context) ?? 0;
            ArgumentOutOfRangeException.ThrowIfNegative(estimate, nameof(estimate));
            EnsureCurrentRequestId();
            EnsureDependencies();

            var invocation = new Invocation(policyName, config);
            _pending.TryGetValue(config.CounterKey, out var pending);
            var reservation = pending?.Reservation ?? new TokenLimitReservation(config.CounterKey, Guid.NewGuid());
            var reservedPrompt = Math.Max(pending?.PromptEstimate ?? 0, estimate);
            var result = _store.TryReserve(config, reservation, reservedPrompt, _clock);
            if (result.Rejection != TokenLimitRejection.None)
            {
                Reject(invocation, result);
                return;
            }

            if (pending is null)
            {
                pending = new PendingKey(reservation);
                _pending.Add(config.CounterKey, pending);
            }

            pending.PromptEstimate = reservedPrompt;
            _invocations.Add(invocation);
            WriteOutputs(invocation, result, estimate, actual: false);
            if (!_observationFailed && _observedTokens is { } actualTokens)
            {
                WriteConsumed(invocation, actualTokens, actual: true);
            }
        }
    }

    internal void BeginBackendExecution(GatewayContext context)
    {
        EnsureOwner(context);
        lock (_sync)
        {
            EnsureRequest();
            if (_settling)
            {
                throw new InvalidOperationException("Cannot begin backend execution during token-limit settlement.");
            }

            if (_completed)
            {
                throw new InvalidOperationException(
                    "Token-limit response for this RequestId has completed. Use a fresh RequestId.");
            }
        }
    }

    internal void ObserveBackendResponse(GatewayContext context, bool optionalManualResponse = false)
    {
        EnsureOwner(context);
        lock (_sync)
        {
            var origin = _invocations.FirstOrDefault();
            var ownsObservation = false;
            try
            {
                EnsureRequest();
                if (_completed || _pending.Count == 0)
                {
                    return;
                }

                if (_settling)
                {
                    throw new InvalidOperationException("Token-limit usage observation or settlement is already running.");
                }

                _settling = true;
                ownsObservation = true;
                EnsureDependencies();
                _observationFailed = true;
                long actualTokens;
                if (optionalManualResponse)
                {
                    if (!HasManualResponseUsage())
                    {
                        _observationFailed = false;
                        return;
                    }

                    ValidateActualResponseSupport();
                    var usage = LlmTokenUsageReader.ReadResponseUsage(context);
                    if (usage is null)
                    {
                        _observationFailed = false;
                        return;
                    }

                    EnsureCurrentRequestId();
                    EnsureDependencies();
                    actualTokens = CountActualTokens(usage);
                }
                else
                {
                    actualTokens = ReadActualTokens();
                }

                _observedTokens = actualTokens;
                _observationFailed = false;
                foreach (var invocation in _invocations)
                {
                    if (invocation.Rejection is null)
                    {
                        WriteConsumed(invocation, actualTokens, actual: true);
                    }
                }
            }
            catch (Exception error) when (origin is not null && IsAccountingError(error))
            {
                throw CreatePolicyError(error, origin);
            }
            finally
            {
                if (ownsObservation)
                {
                    _settling = false;
                }
            }
        }
    }

    internal void ObserveBackendSectionResponse(GatewayContext context, bool unusedTerminal, bool responseChanged)
    {
        EnsureOwner(context);
        lock (_sync)
        {
            EnsureRequest();
            if (!_observationFailed && unusedTerminal)
            {
                return;
            }

            if (!_observationFailed && _observedTokens is not null)
            {
                if (responseChanged)
                {
                    ObserveBackendResponse(context, optionalManualResponse: true);
                }

                return;
            }

            ObserveBackendResponse(context);
        }
    }

    /// <summary>
    /// Settles observed prompt + completion tokens once per key. Validation/accounting errors retain
    /// policy context; other dependency exceptions propagate unchanged. Failures leave estimates and
    /// actual counters unchanged. Duplicate completion is a no-op.
    /// An empty service does not require token dependencies or change no-policy request behavior.
    /// </summary>
    public void CompleteResponse()
    {
        lock (_sync)
        {
            var origin = _invocations.FirstOrDefault();
            var ownsSettlement = false;
            try
            {
                if (_settling)
                {
                    throw new InvalidOperationException("Token-limit response settlement is already running.");
                }

                EnsureRequest();
                if (_completed || !_hasRequest)
                {
                    return;
                }

                if (_pending.Count == 0)
                {
                    MarkCompleted();
                    return;
                }

                _settling = true;
                ownsSettlement = true;
                EnsureDependencies();
                var unused = !_observationFailed && _observedTokens is null && !_context.BackendResponseReceived
                    && !_context.BackendExecutionFailed && _context.ResponseTerminated;
                var actualTokens = unused ? 0
                    : !_observationFailed && _observedTokens is { } observed ? observed
                    : ReadActualTokens();
                EnsureCurrentRequestId();
                EnsureDependencies();
                var invocations = _invocations.ToArray();
                var reservations = _pending.Values.Select(pending => pending.Reservation).ToArray();
                var outputs = invocations.Select(invocation => invocation.Config).ToArray();
                var results = unused
                    ? _store.Cancel(reservations, outputs, _clock)
                    : _store.Complete(reservations, outputs, actualTokens, _clock);
                _pending.Clear();
                _invocations.Clear();
                MarkCompleted();
                for (var i = 0; i < invocations.Length; i++)
                {
                    var invocation = invocations[i];
                    if (invocation.Rejection is { } rejection)
                    {
                        WriteLimitOutputs(invocation, results[i] with { RetryAfter = rejection.RetryAfter });
                        WriteHeader(invocation.Config.RetryAfterHeaderName ?? "Retry-After", rejection.RetryAfter);
                    }
                    else
                    {
                        WriteOutputs(invocation, results[i], actualTokens, actual: !unused);
                    }
                }
            }
            catch (Exception error) when (origin is not null && IsAccountingError(error))
            {
                throw CreatePolicyError(error, origin);
            }
            finally
            {
                if (ownsSettlement)
                {
                    _settling = false;
                }
            }
        }
    }

    internal void CompleteResponse(GatewayContext context)
    {
        EnsureOwner(context);
        CompleteResponse();
    }

    private void Reject(Invocation rejected, TokenLimitCounterResult result)
    {
        var unused = !_observationFailed && _observedTokens is null
            && !_context.BackendResponseReceived && !_context.BackendExecutionFailed;
        var invocations = unused ? _invocations.Append(rejected).ToArray() : new[] { rejected };
        var reservations = _pending.Values.Select(pending => pending.Reservation).ToArray();
        var snapshots = unused
            ? _store.Cancel(reservations, invocations.Select(invocation => invocation.Config).ToArray(), _clock)
            : new[] { result };
        if (unused)
        {
            _pending.Clear();
            _invocations.Clear();
        }
        else
        {
            _invocations.Add(rejected with { Rejection = result });
        }

        var rate = result.Rejection == TokenLimitRejection.Rate;
        ResponseUtilities.Overwrite(_context.Response, rate ? 429 : 403, rate ? "Too Many Requests" : "Forbidden");
        _context.ResponseTerminated = true;
        for (var i = 0; i < invocations.Length; i++)
        {
            WriteOutputs(invocations[i], snapshots[i] with { RetryAfter = result.RetryAfter }, 0, actual: false);
        }

        WriteHeader(rejected.Config.RetryAfterHeaderName ?? "Retry-After", result.RetryAfter);
        throw new FinishSectionProcessingException();
    }

    private void WriteOutputs(Invocation invocation, TokenLimitCounterResult result, long consumed, bool actual)
    {
        WriteLimitOutputs(invocation, result);
        WriteConsumed(invocation, consumed, actual);
    }

    private void WriteLimitOutputs(Invocation invocation, TokenLimitCounterResult result)
    {
        var config = invocation.Config;
        if (result.RemainingTokens is { } rate)
        {
            WriteHeader(config.RemainingTokensHeaderName, rate);
            WriteVariable(config.RemainingTokensVariableName, rate);
        }

        if (result.RemainingQuotaTokens is { } quota)
        {
            WriteHeader(config.RemainingQuotaTokensHeaderName, quota);
            WriteVariable(config.RemainingQuotaTokensVariableName, quota);
        }

        WriteVariable(config.RetryAfterVariableName, result.RetryAfter);
    }

    private void WriteConsumed(Invocation invocation, long consumed, bool actual)
    {
        WriteVariable(invocation.Config.TokensConsumedVariableName, consumed);
        if (actual)
        {
            WriteHeader(invocation.Config.TokensConsumedHeaderName, consumed);
        }
    }

    private long ReadActualTokens()
    {
        ValidateActualResponseSupport();
        var usage = LlmTokenUsageReader.ReadResponseUsage(_context)
            ?? _context.Services.Resolve<ILlmTokenUsageProvider>()?.GetUsage(_context)
            ?? throw new InvalidOperationException(
                "Actual backend token usage is unavailable. Supply JSON prompt/completion usage " +
                "or register ILlmTokenUsageProvider, including observed zero for backend failures.");
        EnsureCurrentRequestId();
        EnsureDependencies();
        return CountActualTokens(usage);
    }

    private bool HasManualResponseUsage()
    {
        var body = _context.Response.Body.Content;
        if (string.IsNullOrWhiteSpace(body))
        {
            return false;
        }

        // Without a new response observation, only a JSON usage envelope declares replacement
        // accounting. Other formats are postprocessing of the already observed response.
        try
        {
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;
            return root.ValueKind == JsonValueKind.Object
                && ((root.TryGetProperty("usage", out var usage) && usage.ValueKind != JsonValueKind.Null)
                    || (root.TryGetProperty("usageMetadata", out usage) && usage.ValueKind != JsonValueKind.Null));
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private void ValidateActualResponseSupport()
    {
        ValidateRequestSupport(estimate: _invocations.Any(invocation =>
            invocation.Rejection is null && invocation.Config.EstimatePromptToken));
        if (_context.Response.Headers.Any(header =>
                header.Key.Equals("Content-Type", StringComparison.OrdinalIgnoreCase)
                && header.Value.Any(value => value.Split(';')[0].Trim()
                    .Equals("text/event-stream", StringComparison.OrdinalIgnoreCase))))
        {
            throw new NotSupportedException("Token-limit streaming response accounting is not supported.");
        }
    }

    private static bool IsAccountingError(Exception error) => error is
        ArgumentException or InvalidOperationException or NotSupportedException or JsonException or OverflowException;

    private static PolicyException CreatePolicyError(Exception error, Invocation origin) => new(error)
    {
        Policy = origin.PolicyName,
        Section = nameof(IInboundContext),
        PolicyArgs = [origin.Config]
    };

    private void WriteHeader(string? name, long value) =>
        ResponseHeaderUtilities.SetNumericHeader(_context.Response.Headers, name, value);

    private void WriteVariable(string? name, object value)
    {
        if (name is not null)
        {
            _context.Variables[name] = value;
        }
    }

    private static long CountActualTokens(LlmTokenUsage usage)
    {
        if (usage.PromptTokens is not { } prompt || usage.CompletionTokens is not { } completion)
        {
            throw new ArgumentException("Actual usage must report both prompt and completion token counts.", nameof(usage));
        }

        var total = checked(prompt + completion);
        LlmTokenUsageReader.ValidateCount(prompt);
        LlmTokenUsageReader.ValidateCount(completion);
        if (usage.TotalTokens is { } reportedTotal)
        {
            LlmTokenUsageReader.ValidateCount(reportedTotal);
        }

        ArgumentNullException.ThrowIfNull(usage.AdditionalTokens, nameof(usage));
        foreach (var (name, value) in usage.AdditionalTokens)
        {
            if (string.IsNullOrWhiteSpace(name) || LlmTokenUsageReader.IsPrimaryMetric(name))
            {
                throw new ArgumentException("Additional usage must not reuse primary token count names.", nameof(usage));
            }

            LlmTokenUsageReader.ValidateCount(value);
        }

        return total;
    }

    private void ValidateRequestSupport(bool estimate)
    {
        var body = _context.Request.Body.Content;
        if (string.IsNullOrWhiteSpace(body))
        {
            return;
        }

        using var document = JsonDocument.Parse(body);
        if (document.RootElement.ValueKind != JsonValueKind.Object)
        {
            throw new ArgumentException("The LLM request must be a JSON object.", nameof(body));
        }

        if (document.RootElement.TryGetProperty("stream", out var stream))
        {
            if (stream.ValueKind == JsonValueKind.True)
            {
                throw new NotSupportedException("Token-limit streaming request accounting is not supported.");
            }

            if (stream.ValueKind != JsonValueKind.False)
            {
                throw new ArgumentException("The stream setting must be a JSON Boolean.", nameof(body));
            }
        }

        if (estimate && ContainsImage(document.RootElement))
        {
            throw new NotSupportedException(
                "Estimated image token accounting is not supported. Disable estimation and supply actual backend usage.");
        }
    }

    private static bool ContainsImage(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Array)
        {
            return element.EnumerateArray().Any(ContainsImage);
        }

        if (element.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        foreach (var property in element.EnumerateObject())
        {
            if (property.Value.ValueKind == JsonValueKind.String
                && ((property.Name == "type" && property.Value.GetString() is "image" or "image_url" or "input_image")
                    || (property.Name is "mimeType" or "mime_type"
                        && property.Value.GetString()!.StartsWith("image/", StringComparison.OrdinalIgnoreCase))))
            {
                return true;
            }

            if (ContainsImage(property.Value))
            {
                return true;
            }
        }

        return false;
    }

    private static void ValidateOutputNames(IEnumerable<string?> names, StringComparer comparer)
    {
        var supplied = names.Where(name => name is not null).ToArray();
        if (supplied.Any(string.IsNullOrWhiteSpace) || supplied.Distinct(comparer).Count() != supplied.Length)
        {
            throw new ArgumentException("Token-limit output names must be nonempty and distinct.", nameof(names));
        }
    }

    private void CaptureDependencies()
    {
        lock (_context.Services)
        {
            _store = _context.Services.Resolve<TokenLimitCounterStore>() ?? new TokenLimitCounterStore();
            _context.Services.Register(_store);
            _clock = _context.Services.Resolve<TimeProvider>() ?? TimeProvider.System;
        }
    }

    private void EnsureDependencies()
    {
        if (!ReferenceEquals(_store, _context.Services.Resolve<TokenLimitCounterStore>()))
        {
            throw new InvalidOperationException("TokenLimitCounterStore cannot change while a request is pending.");
        }

        if (!ReferenceEquals(_clock, _context.Services.Resolve<TimeProvider>() ?? TimeProvider.System))
        {
            throw new InvalidOperationException("TimeProvider cannot change while a token-limit request is pending.");
        }

        if (_context.Services.Resolve<TokenLimitService>() is { } registered && !ReferenceEquals(registered, this))
        {
            throw new InvalidOperationException("TokenLimitService cannot be replaced while a request is pending.");
        }
    }

    private void EnsureOwner(GatewayContext context)
    {
        if (!ReferenceEquals(_context, context))
        {
            throw new InvalidOperationException(
                "TokenLimitService belongs to another gateway context. Share TokenLimitCounterStore instead.");
        }
    }

    private void EnsureRequest()
    {
        if (_requestId == _context.RequestId)
        {
            return;
        }

        if (_pending.Count != 0)
        {
            throw new InvalidOperationException("Complete the pending token-limit response before changing RequestId.");
        }

        _requestId = _context.RequestId;
        _observedTokens = null;
        _observationFailed = false;
        _invocations.Clear();
        _hasRequest = false;
        _completed = _completedRequests.Contains(_requestId);
        CaptureDependencies();
    }

    private void EnsureCurrentRequestId()
    {
        if (_requestId != _context.RequestId)
        {
            throw new InvalidOperationException("RequestId must remain unchanged until token-limit settlement completes.");
        }
    }

    private void MarkCompleted()
    {
        _completed = true;
        _completedRequests.Add(_requestId);
    }

    private sealed record Invocation(
        string PolicyName, TokenLimitConfig Config, TokenLimitCounterResult? Rejection = null);

    private sealed class PendingKey(TokenLimitReservation reservation)
    {
        public TokenLimitReservation Reservation { get; } = reservation;
        public long PromptEstimate { get; set; }
    }
}