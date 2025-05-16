// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;

/// <summary>
/// Configuration for the send-one-way-request policy.
/// </summary>
public record SendOneWayRequestConfig
{
    /// <summary>
    /// Specifies the request mode. Allowed values are "new" or "copy". Policy expressions are allowed.
    /// </summary>
    [ExpressionAllowed]
    public string? Mode { get; init; }

    /// <summary>
    /// Specifies the timeout interval in seconds for the request. Policy expressions are allowed.
    /// </summary>
    [ExpressionAllowed]
    public int? Timeout { get; init; }

    /// <summary>
    /// Specifies the URL to which the request is sent. Policy expressions are allowed.
    /// </summary>
    [ExpressionAllowed]
    public string? Url { get; init; }

    /// <summary>
    /// Specifies the HTTP method for the request (e.g., GET, POST). Policy expressions are allowed.
    /// </summary>
    public string? Method { get; init; }

    /// <summary>
    /// Specifies the headers to include in the request.
    /// </summary>
    public HeaderConfig[]? Headers { get; init; }

    /// <summary>
    /// Specifies the body content of the request.
    /// </summary>
    public BodyConfig? Body { get; init; }

    /// <summary>
    /// Specifies the authentication configuration for the request.
    /// </summary>
    public IAuthenticationConfig? Authentication { get; init; }

    /// <summary>
    /// Specifies the proxy configuration for the request.
    /// </summary>
    public ProxyConfig? Proxy { get; init; }
}