// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;

/// <summary>
/// Configuration for the cache-lookup policy which checks the API Management cache for a valid cached response.
/// </summary>
public record CacheLookupConfig
{
    /// <summary>
    /// Specifies whether to cache responses per developer key.<br/>
    /// When true, different API keys will have separate cache entries even for identical requests.
    /// </summary>
    [ExpressionAllowed]
    public required bool VaryByDeveloper { get; init; }

    /// <summary>
    /// Specifies whether to cache responses per developer group.<br/>
    /// When true, users belonging to different groups will have separate cache entries even for identical requests.
    /// </summary>
    [ExpressionAllowed]
    public required bool VaryByDeveloperGroups { get; init; }

    /// <summary>
    /// Specifies the type of cache to use.<br/>
    /// Valid values: "internal" to use the built-in API Management cache, "external" to use the external cache as configured in <see href="https://learn.microsoft.com/en-us/azure/api-management/api-management-howto-cache-external">External caching</see>, or "prefer-external" to use external cache if configured or fall back to internal cache.
    /// </summary>
    public string? CachingType { get; init; }

    /// <summary>
    /// Controls whether downstream caches (clients and proxies) may cache the response.<br/>
    /// Valid values: "none" (default) - downstream caching is not allowed, "private" - downstream private caching is allowed, "public" - private and shared downstream caching is allowed.
    /// </summary>
    [ExpressionAllowed]
    public string? DownstreamCachingType { get; init; }

    /// <summary>
    /// When downstream caching is enabled, turns the must-revalidate cache control directive in gateway responses on or off.<br/>
    /// Default is true.
    /// </summary>
    [ExpressionAllowed]
    public bool? MustRevalidate { get; init; }

    /// <summary>
    /// When true, allows caching of requests that contain an Authorization header.<br/>
    /// Default is false. Use with caution, as this may lead to private information shared between clients.
    /// </summary>
    [ExpressionAllowed]
    public bool? AllowPrivateResponseCaching { get; init; }

    /// <summary>
    /// Specifies HTTP headers that should be used to differentiate cache entries.<br/>
    /// For example, caching separately based on Accept or Accept-Language headers allows for content negotiation.
    /// </summary>
    public string[]? VaryByHeaders { get; init; }

    /// <summary>
    /// Specifies query parameters that should be used to differentiate cache entries.<br/>
    /// For example, caching separately based on "id" or "page" parameters allows responses to be cached per resource identifier.
    /// </summary>
    public string[]? VaryByQueryParameters { get; init; }
}