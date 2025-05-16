// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;

/// <summary>
/// Configuration for the CORS (Cross-Origin Resource Sharing) policy that enables web applications running in different domains
/// to make requests to your API.<br/>
/// For more information, see <a href="https://learn.microsoft.com/en-us/azure/api-management/cors-policy">CORS policy</a>.
/// </summary>
public record CorsConfig
{
    /// <summary>
    /// Specifies whether access to the resource should be allowed with credentials (such as cookies, authorization headers, or TLS client certificates).<br/>
    /// When set to true, browsers will send and allow credentials with cross-origin requests.<br/>
    /// Policy expressions are allowed.
    /// </summary>
    [ExpressionAllowed]
    public bool? AllowCredentials { get; init; }

    /// <summary>
    /// Specifies whether preflight OPTIONS requests will be terminated automatically without forwarding to the backend.<br/>
    /// Default value is "true". Set to "false" to pass OPTIONS requests to the backend.<br/>
    /// Policy expressions are allowed.
    /// </summary>
    [ExpressionAllowed]
    public string? TerminateUnmatchedRequest { get; init; }

    /// <summary>
    /// List of origins allowed to make cross-origin calls to your API.<br/>
    /// Wild card '*' is supported to allow all origins, but only one wildcard entry is allowed.<br/>
    /// Each origin is typically of the form "scheme://host:port" where port may be omitted.
    /// </summary>
    public required string[] AllowedOrigins { get; init; }

    /// <summary>
    /// Methods (HTTP verbs) allowed to be used for cross-origin requests.<br/>
    /// Wild card '*' is supported to allow all methods.
    /// </summary>
    public string[]? AllowedMethods { get; init; }

    /// <summary>
    /// Maximum time in seconds that a browser should cache the preflight request results.<br/>
    /// Longer cache times can improve performance by reducing the number of preflight requests.<br/>
    /// Policy expressions are allowed.
    /// </summary>
    [ExpressionAllowed]
    public uint? PreflightResultMaxAge { get; init; }

    /// <summary>
    /// Headers that can be included in the cross-origin request.<br/>
    /// Wild card '*' is supported to allow all headers.
    /// </summary>
    public required string[] AllowedHeaders { get; init; }

    /// <summary>
    /// Headers that can be accessed by client-side JavaScript in the response.<br/>
    /// By default, browsers only expose a limited set of response headers to JavaScript code.
    /// </summary>
    public string[]? ExposeHeaders { get; init; }
}