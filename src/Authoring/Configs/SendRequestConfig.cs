// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.ApiManagement.PolicyToolkit.Authoring;

/// <summary>
/// Configuration for the <a href="https://learn.microsoft.com/en-us/azure/api-management/send-request-policy">send-request</a> policy.
/// </summary>
public record SendRequestConfig
{
    /// <summary>
    /// Specifies the name of the variable to store the response.
    /// </summary>
    public required string ResponseVariableName { get; init; }

    /// <summary>
    /// Specifies the mode of the request. Possible values are "new" or "copy".
    /// </summary>
    [ExpressionAllowed]
    public string? Mode { get; init; }

    /// <summary>
    /// Specifies the timeout for the request in seconds.
    /// </summary>
    [ExpressionAllowed]
    public int? Timeout { get; init; }

    /// <summary>
    /// Specifies whether to ignore errors. If set to true, errors are ignored.
    /// </summary>
    public bool? IgnoreError { get; init; }

    /// <summary>
    /// Specifies the URL to send the request to.
    /// </summary>
    [ExpressionAllowed]
    public string? Url { get; init; }

    /// <summary>
    /// Specifies the HTTP method to use for the request.
    /// </summary>
    public string? Method { get; init; }

    /// <summary>
    /// Specifies the headers to include in the request.
    /// </summary>
    public HeaderConfig[]? Headers { get; init; }

    /// <summary>
    /// Specifies the body of the request.
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