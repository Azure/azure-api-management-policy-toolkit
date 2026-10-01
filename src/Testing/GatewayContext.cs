// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Document;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Data;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Expressions;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Services;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing;

public class GatewayContext : MockExpressionContext
{
    private readonly object _executionSync = new();
    private int _requestExecutionDepth;
    private Guid _requestExecutionId;
    private Guid? _completedLimiterRequestId;
    private Guid? _backendResponseRequestId;
    private Guid? _backendFailureRequestId;
    private long _backendResponseVersion;
    private long _terminalResponseVersion;

    internal readonly SectionContextProxy<IInboundContext> InboundProxy;
    internal readonly SectionContextProxy<IBackendContext> BackendProxy;
    internal readonly SectionContextProxy<IOutboundContext> OutboundProxy;
    internal readonly SectionContextProxy<IOnErrorContext> OnErrorProxy;
    internal readonly CertificateStore CertificateStore = new();
    internal readonly CacheStore CacheStore = new();
    internal readonly ResponseExampleStore ResponseExampleStore = new();
    internal readonly LoggerStore LoggerStore = new();
    internal readonly RateLimitStore RateLimitStore = new();

    /// <summary>
    /// Registry for pre-registered fragment instances used by the IncludeFragment policy.
    /// </summary>
    internal Dictionary<string, IFragment> FragmentRegistry { get; } = new(StringComparer.OrdinalIgnoreCase);

    internal HashSet<string> ActiveFragments { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Tracks the handler map of the currently executing section proxy so that
    /// IncludeFragmentHandler can create a fragment context with the correct handlers.
    /// </summary>
    internal Dictionary<string, IPolicyHandler>? CurrentSectionHandlers { get; set; }

    internal string? CurrentSectionName { get; set; }

    internal bool BackendResponseReceived => _backendResponseRequestId == RequestId;
    internal bool BackendExecutionFailed => _backendFailureRequestId == RequestId;
    internal long BackendResponseVersion => _backendResponseVersion;

    /// <summary>
    /// Service registry for injecting custom service implementations (e.g., IHttpClient, ICache).
    /// </summary>
    public ServiceRegistry Services { get; } = new();

    /// <summary>
    /// Set to true when a policy terminates pipeline execution, signaling that
    /// coordinated execution should stop subsequent scopes and sections.
    /// InvokeRequest stops only its current section and does not set this flag.
    /// Standalone and independent section invocations do not consult this flag.
    /// </summary>
    public bool ResponseTerminated { get; set; }

    /// <summary>
    /// Backend service base URL set by the set-backend-service policy.
    /// Used by forward-request to determine the target URL.
    /// </summary>
    public string? BackendUrl { get; set; }

    /// <summary>
    /// When set, authentication-managed-identity handler uses this function
    /// to generate tokens instead of the default JWT generator.
    /// Parameters: (resourceId, clientId) → token string.
    /// </summary>
    public Func<string, string?, string>? ManagedIdentityTokenProvider { get; set; }

    public GatewayContext()
    {
        InboundProxy = SectionContextProxy<IInboundContext>.Create(this);
        BackendProxy = SectionContextProxy<IBackendContext>.Create(this);
        OutboundProxy = SectionContextProxy<IOutboundContext>.Create(this);
        OnErrorProxy = SectionContextProxy<IOnErrorContext>.Create(this);
    }

    internal void RegisterFragment(string fragmentId, IFragment fragment)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fragmentId);
        ArgumentNullException.ThrowIfNull(fragment);
        FragmentRegistry[fragmentId] = fragment;
    }

    internal void RecordTermination(FinishSectionProcessingException termination, string? policyName = null)
    {
        termination.TerminatesPipeline ??= policyName != nameof(IInboundContext.InvokeRequest);
        ResponseTerminated |= termination.TerminatesPipeline == true;
        if (termination.TerminatesPipeline == true)
        {
            _terminalResponseVersion++;
        }

        if (policyName == nameof(IInboundContext.InvokeRequest))
        {
            ObserveBackendResponse();
        }
    }

    internal void ObserveBackendResponse()
    {
        var tokenLimits = Services.Resolve<TokenLimitService>();
        tokenLimits?.BeginBackendExecution(this);
        _backendResponseRequestId = RequestId;
        _backendResponseVersion++;
        tokenLimits?.ObserveBackendResponse(this);
    }

    internal void ExecuteBackend(Action backend) => ExecuteSection(() =>
    {
        Services.Resolve<TokenLimitService>()?.BeginBackendExecution(this);
        var requestId = RequestId;
        var terminalVersion = _terminalResponseVersion;
        var responseVersion = _backendResponseVersion;
        var response = Response;
        var responseBody = Response.Body.Content;
        var returned = false;
        try
        {
            backend();
            returned = true;
        }
        finally
        {
            if (!returned)
            {
                _backendFailureRequestId = requestId;
            }
        }

        if (_requestExecutionDepth != 0)
        {
            EnsureRequestId();
        }

        var unusedTerminal = _terminalResponseVersion != terminalVersion
            && !BackendResponseReceived && !BackendExecutionFailed;
        var responseChanged = _backendResponseVersion == responseVersion
            && _terminalResponseVersion == terminalVersion
            && (!ReferenceEquals(response, Response) || responseBody != Response.Body.Content);
        Services.Resolve<TokenLimitService>()?.ObserveBackendSectionResponse(this, unusedTerminal, responseChanged);
    });

    internal void ExecuteRequest(Action request)
    {
        ArgumentNullException.ThrowIfNull(request);
        EnterExecution();
        try
        {
            if (_requestExecutionDepth == 0)
            {
                _requestExecutionId = RequestId;
            }
            EnsureRequestId();
            EnsureNoLateLimiterWork();
            _requestExecutionDepth++;
            try
            {
                request();
                EnsureRequestId();
                EnsureNoLateLimiterWork();
                if (_requestExecutionDepth != 1 || _completedLimiterRequestId == RequestId)
                {
                    return;
                }

                var tokenLimits = Services.Resolve<TokenLimitService>();
                tokenLimits?.CompleteResponse(this);
                EnsureRequestId();
                this.CompleteLimiterResponse();
                EnsureRequestId();
                if (Services.Resolve<PolicyCounterService>() is { } counters)
                {
                    if (counters.HasPendingResponse)
                    {
                        throw new InvalidOperationException(
                            "Limiter response completion left pending work. Complete it before changing RequestId.");
                    }

                    _completedLimiterRequestId = RequestId;
                }

                if (tokenLimits?.HasCompletedResponse == true)
                {
                    _completedLimiterRequestId = RequestId;
                }
            }
            finally
            {
                _requestExecutionDepth--;
            }
        }
        finally
        {
            Monitor.Exit(_executionSync);
        }
    }

    internal void ExecuteSection(Action section)
    {
        ArgumentNullException.ThrowIfNull(section);
        EnterExecution();
        try
        {
            if (_requestExecutionDepth != 0)
            {
                EnsureRequestId();
            }
            if (_completedLimiterRequestId == RequestId)
            {
                throw new InvalidOperationException(
                    $"The limiter response for RequestId '{RequestId}' has completed. " +
                    "Use a new RequestId before executing more sections.");
            }

            section();
            if (_requestExecutionDepth != 0)
            {
                EnsureRequestId();
            }
        }
        finally
        {
            Monitor.Exit(_executionSync);
        }
    }

    private void EnterExecution()
    {
        if (!Monitor.TryEnter(_executionSync))
        {
            throw new InvalidOperationException(
                "Concurrent section or request execution on the same GatewayContext is not supported.");
        }
    }

    private void EnsureRequestId()
    {
        if (RequestId != _requestExecutionId)
        {
            throw new InvalidOperationException(
                "RequestId must remain unchanged until the outer request boundary completes limiter settlement.");
        }
    }

    private void EnsureNoLateLimiterWork()
    {
        if (_completedLimiterRequestId == RequestId
            && Services.Resolve<PolicyCounterService>()?.HasPendingResponse == true)
        {
            throw new InvalidOperationException(
                $"Deferred limiter work was added after RequestId '{RequestId}' completed. " +
                "Use a new RequestId for a new request; late work cannot be settled onto a completed response.");
        }
    }
}