// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.ApiManagement.PolicyToolkit.Authoring;

/// <summary>
/// Configuration of forward request policy.<br/>
/// Compiled to <a href="https://learn.microsoft.com/en-us/azure/api-management/forward-request-policy">forward-request</a> policy.
/// </summary>
public record ForwardRequestConfig
{
    /// <summary>
    /// The amount of time in seconds to wait for the HTTP response headers to be returned by the backend service before a timeout error is raised.<br/>
    /// Minimum value is 0 seconds. Values greater than 240 seconds may not be honored, because the underlying network infrastructure can drop idle connections after this time.<br/>
    /// Policy expressions are allowed. You can specify either timeout or timeout-ms but not both.
    /// </summary>
    [ExpressionAllowed]
    public uint? Timeout { get; init; }

    /// <summary>
    /// The amount of time in milliseconds to wait for the HTTP response headers to be returned by the backend service before a timeout error is raised.<br/>
    /// Minimum value is 0 ms. Policy expressions are allowed. You can specify either timeout or timeout-ms but not both.
    /// </summary>
    [ExpressionAllowed]
    public uint? TimeoutMs { get; init; }

    /// <summary>
    /// The amount of time in seconds to wait for a 100 Continue status code to be returned by the backend service before a timeout error is raised.<br/>
    /// Policy expressions are allowed.
    /// </summary>
    [ExpressionAllowed]
    public uint? ContinueTimeout { get; init; }

    /// <summary>
    /// The HTTP spec version to use when sending the HTTP request to the backend service.<br/>
    /// When using "2or1", the gateway will favor HTTP/2 over HTTP/1, but fall back to HTTP/1 if HTTP/2 doesn't work.<br/>
    /// Policy expressions are not allowed.
    /// </summary>
    public string? HttpVersion { get; init; }

    /// <summary>
    /// Specifies whether redirects from the backend service are followed by the gateway or returned to the caller.<br/>
    /// Policy expressions are allowed.
    /// </summary>
    [ExpressionAllowed]
    public bool? FollowRedirects { get; init; }

    /// <summary>
    /// When set to true, request is buffered and will be reused on retry.<br/>
    /// Policy expressions are not allowed.
    /// </summary>
    public bool? BufferRequestBody { get; init; }

    /// <summary>
    /// Affects processing of chunked responses. When set to false, each chunk received from the backend is immediately returned to the caller.<br/>
    /// When set to true, chunks are buffered (8 KB, unless end of stream is detected) and only then returned to the caller.<br/>
    /// Set to false with backends such as those implementing server-sent events (SSE) that require content to be returned or streamed immediately to the caller.<br/>
    /// Policy expressions are not allowed.
    /// </summary>
    public bool? BufferResponse { get; init; }

    /// <summary>
    /// When set to true, triggers on-error section for response codes in the range from 400 to 599 inclusive.<br/>
    /// Policy expressions are not allowed.
    /// </summary>
    public bool? FailOnErrorStatusCode { get; init; }
}