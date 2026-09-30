// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Data;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Expressions;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing;

public class GatewayContext : MockExpressionContext
{
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
    }
}