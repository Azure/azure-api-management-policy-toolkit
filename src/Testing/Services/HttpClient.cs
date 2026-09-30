// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Services;

/// <summary>
/// Abstraction for HTTP calls made by forwarding, send, one-way, and invoke policies.
/// Register an implementation via <see cref="ServiceRegistry"/> to control HTTP behavior in tests.
/// Requests carry evaluated <see cref="HttpTransportOptions"/> under <see cref="HttpTransportOptions.Key"/>.
/// Implementations should honor proxy, certificate, protocol, and cancellation settings; no real client is created.
/// Complete the response task when headers are available; expose delayed bodies through response content.
/// String requests honor the effective Content-Type charset, while explicit byte arrays are not transcoded.
/// Response payloads use the emulator's UTF-8 text representation with normalized Content-Length.
/// HEAD and other bodyless responses preserve valid representation lengths rather than generating payload lengths.
/// </summary>
public interface IHttpClient
{
    Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken = default);
}

/// <summary>
/// A stub HTTP client that returns responses from a user-provided handler function.
/// </summary>
public class StubHttpClient : IHttpClient
{
    private readonly Func<HttpRequestMessage, HttpResponseMessage>? _handler;
    private readonly Func<HttpRequestMessage, Task<HttpResponseMessage>>? _asyncHandler;
    private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>>? _cancellableHandler;

    /// <summary>
    /// The last request sent through this client, for metadata assertions.
    /// The policy transport disposes completed requests; inspect or capture content inside the handler.
    /// </summary>
    public HttpRequestMessage? LastRequest { get; private set; }

    /// <summary>The cancellation token passed to the last initiated request.</summary>
    public CancellationToken LastCancellationToken { get; private set; }

    /// <summary>
    /// Creates a stub client with a synchronous handler.
    /// </summary>
    public StubHttpClient(Func<HttpRequestMessage, HttpResponseMessage> handler)
    {
        _handler = handler ?? throw new ArgumentNullException(nameof(handler));
    }

    /// <summary>
    /// Creates a stub client with an async handler.
    /// </summary>
    public StubHttpClient(Func<HttpRequestMessage, Task<HttpResponseMessage>> asyncHandler)
    {
        _asyncHandler = asyncHandler ?? throw new ArgumentNullException(nameof(asyncHandler));
    }

    /// <summary>Creates a stub with an asynchronous handler that receives the policy cancellation token.</summary>
    public StubHttpClient(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler)
    {
        _cancellableHandler = handler ?? throw new ArgumentNullException(nameof(handler));
    }

    public async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        LastRequest = request;
        LastCancellationToken = cancellationToken;
        if (_cancellableHandler is not null)
        {
            return await _cancellableHandler(request, cancellationToken).ConfigureAwait(false);
        }

        if (_asyncHandler is not null)
        {
            return await _asyncHandler(request).ConfigureAwait(false);
        }

        return _handler!(request);
    }

    /// <summary>Creates a stub that always returns 200 OK with the specified body.</summary>
    public static StubHttpClient Ok(string? body = null) =>
        new(req => new HttpResponseMessage(System.Net.HttpStatusCode.OK)
        {
            Content = body is not null ? new StringContent(body) : null
        });

    /// <summary>Creates a stub that always returns 404 Not Found.</summary>
    public static StubHttpClient NotFound() =>
        new(req => new HttpResponseMessage(System.Net.HttpStatusCode.NotFound));
}