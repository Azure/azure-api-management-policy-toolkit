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
    /// Controls how the gateway interacts with response caching at downstream servers.<br/>
    /// Valid values: "none" disables downstream caching, "public" honors cache directives from downstream servers, "private" means the response is not cached in shared caches, and "internal" means API Management will handle caching ignoring downstream directives.
    /// </summary>
    [ExpressionAllowed]
    public string? DownstreamCachingType { get; init; }

    /// <summary>
    /// When true, the gateway will revalidate cached entries that have become stale, as per the Cache-Control directive.<br/>
    /// This may improve cache hit ratio at the cost of additional backend load.
    /// </summary>
    [ExpressionAllowed]
    public bool? MustRevalidate { get; init; }

    /// <summary>
    /// When true, responses with private cache directives will be stored in the API Management cache.<br/>
    /// Use with caution, as this may lead to private information shared between clients.
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