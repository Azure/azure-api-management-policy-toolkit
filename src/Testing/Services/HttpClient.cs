// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Services;

/// <summary>
/// Abstraction for HTTP calls made by send-request and send-one-way-request policies.
/// Register an implementation via <see cref="ServiceRegistry"/> to control HTTP behavior in tests.
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

    /// <summary>
    /// The last request sent through this client, for assertion purposes.
    /// </summary>
    public HttpRequestMessage? LastRequest { get; private set; }

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

    public async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
        CancellationToken cancellationToken = default)
    {
        LastRequest = request;
        if (_asyncHandler is not null)
        {
            return await _asyncHandler(request);
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
