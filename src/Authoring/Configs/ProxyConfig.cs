// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.ApiManagement.PolicyToolkit.Authoring;

/// <summary>
/// Configuration for the proxy policy.<br />
/// Specifies the proxy server URL, and optionally the username and password for authentication.
/// </summary>
public record ProxyConfig
{
    /// <summary>
    /// The URL of the proxy server.<br />
    /// Policy expressions are allowed.
    /// </summary>
    [ExpressionAllowed]
    public required string Url { get; init; }

    /// <summary>
    /// The username for proxy authentication.<br />
    /// Policy expressions are allowed.
    /// </summary>
    [ExpressionAllowed]
    public string? Username { get; init; }

    /// <summary>
    /// The password for proxy authentication.<br />
    /// Policy expressions are allowed.
    /// </summary>
    [ExpressionAllowed]
    public string? Password { get; init; }
}