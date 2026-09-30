// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Concurrent;
using System.Security.Cryptography.X509Certificates;

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Services;

/// <summary>
/// Evaluated routing and protocol options attached to outgoing requests. Injected <see cref="IHttpClient"/>
/// implementations can inspect these options to emulate proxy, certificate, redirect, and buffering behavior.
/// The emulator itself does not open connections or simulate wire-level streaming.
/// </summary>
public sealed record HttpTransportOptions
{
    /// <summary>The typed request option used by the HTTP policy handlers.</summary>
    public static HttpRequestOptionsKey<HttpTransportOptions> Key { get; } = new("PolicyToolkit.HttpTransport");

    /// <summary>The proxy selected for this request, including optional credentials.</summary>
    public ProxyConfig? Proxy { get; init; }

    /// <summary>The selected client certificate. Registered certificates remain caller-owned.</summary>
    public X509Certificate2? Certificate { get; init; }

    /// <summary>The effective request timeout.</summary>
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(60);

    /// <summary>
    /// When true, the timeout ends when response headers arrive. Body reading remains subject to caller cancellation.
    /// ForwardRequest uses this scope; send and invoke requests use a total response timeout.
    /// </summary>
    public bool TimeoutAppliesToHeadersOnly { get; init; }

    /// <summary>The requested timeout for a 100 Continue response.</summary>
    public TimeSpan? ContinueTimeout { get; init; }

    /// <summary>Whether the injected transport should follow redirects.</summary>
    public bool FollowRedirects { get; init; }

    /// <summary>Whether a forwarded request body should be reusable for retries.</summary>
    public bool BufferRequestBody { get; init; }

    /// <summary>Whether the injected transport should buffer a forwarded response.</summary>
    public bool BufferResponse { get; init; } = true;
}

/// <summary>
/// Per-context HTTP state. Register an instance to supply caller cancellation; handlers create one otherwise.
/// One-way requests are tracked rather than synchronously waiting for their responses.
/// </summary>
public sealed class HttpTransportState
{
    private readonly ConcurrentQueue<Task> _pendingOneWayRequests = new();

    /// <summary>Caller cancellation linked to every outgoing HTTP policy request.</summary>
    public CancellationToken CancellationToken { get; init; }

    /// <summary>The proxy configured by the inbound proxy policy.</summary>
    public ProxyConfig? Proxy { get; internal set; }

    /// <summary>A snapshot of initiated asynchronous one-way operations, including faulted operations.</summary>
    public IReadOnlyList<Task> PendingOneWayRequests => _pendingOneWayRequests.ToArray();

    /// <summary>Waits for currently tracked one-way operations and propagates their errors.</summary>
    public Task DrainAsync() => Task.WhenAll(PendingOneWayRequests);

    internal void TrackOneWayRequest(Task operation, GatewayContext context)
    {
        var observed = operation.ContinueWith(completed =>
        {
            if (completed.IsFaulted)
            {
                context.Trace($"SendOneWayRequest asynchronous transport failed: {completed.Exception!.GetBaseException().Message}");
            }
            else if (completed.IsCanceled)
            {
                context.Trace("SendOneWayRequest asynchronous transport was canceled.");
            }

            completed.GetAwaiter().GetResult();
        }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        _pendingOneWayRequests.Enqueue(observed);
        HttpPolicyTransport.ObserveFault(observed);
    }
}