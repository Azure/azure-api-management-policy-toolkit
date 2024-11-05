namespace Azure.ApiManagement.PolicyToolkit.Authoring;

/// <summary>
/// Configuration of forward request policy
/// </summary>
public record ForwardRequestConfig
{
    /// <summary>
    /// The amount of time in seconds to wait for the HTTP response headers to be returned by the backend service before a timeout error is raised.
    /// Minimum value is 0 seconds. Values greater than 240 seconds may not be honored, because the underlying network infrastructure can drop idle connections after this time.
    /// Policy expressions are allowed. You can specify either timeout or timeout-ms but not both.
    /// </summary>
    public uint? Timeout { get; init; }

    /// <summary>
    /// The amount of time in milliseconds to wait for the HTTP response headers to be returned by the backend service before a timeout error is raised.
    /// Minimum value is 0 ms. Policy expressions are allowed. You can specify either timeout or timeout-ms but not both.
    /// </summary>
    public uint? TimeoutMs { get; init; }

    /// <summary>
    /// The amount of time in seconds to wait for a 100 Continue status code to be returned by the backend service before a timeout error is raised. Policy expressions are allowed.
    /// </summary>
    public uint? ContinueTimeout { get; init; }

    /// <summary>
    /// The HTTP spec version to use when sending the HTTP response to the backend service. When using 2or1, the gateway will favor HTTP /2 over /1, but fall back to HTTP /1 if HTTP /2 doesn't work.
    /// </summary>
    public string? HttpVersion { get; init; }

    /// <summary>
    /// Specifies whether redirects from the backend service are followed by the gateway or returned to the caller. Policy expressions are allowed.
    /// </summary>
    public bool? FollowRedirects { get; init; }

    /// <summary>
    /// When set to true, request is buffered and will be reused on retry.
    /// </summary>
    public bool? BufferRequestBody { get; init; }

    /// <summary>
    /// Affects processing of chunked responses. When set to false, each chunk received from the backend is immediately returned to the caller.
    /// When set to true, chunks are buffered (8 KB, unless end of stream is detected) and only then returned to the caller.
    /// Set to false with backends such as those implementing server-sent events (SSE) that require content to be returned or streamed immediately to the caller. Policy expressions aren't allowed.
    /// </summary>
    public bool? BufferResponse { get; init; }

    /// <summary>
    /// When set to true, triggers on-error section for response codes in the range from 400 to 599 inclusive. Policy expressions aren't allowed.
    /// </summary>
    public bool? FailOnErrorStatusCode { get; init; }

    public ForwardRequestConfig()
    {
        if (Timeout.HasValue && TimeoutMs.HasValue)
        {
            throw new ArgumentException("You can specify either timeout or timeout-ms but not both.");
        }
    }
}