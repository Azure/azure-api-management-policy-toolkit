// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;

/// <summary>
/// Configuration for semantic cache lookup policies (azure-openai-semantic-cache-lookup and llm-semantic-cache-lookup).<br/>
/// These policies use vector embeddings to find semantically similar requests in the cache.
/// </summary>
public record SemanticCacheLookupConfig
{
    /// <summary>
    /// Required. The score threshold (between 0.0 and 1.0) that determines how closely an incoming prompt must match a cached prompt to return its stored response.<br/>
    /// Lower values require higher semantic similarity for a match.<br/>
    /// Start with a low value such as 0.05; values above 0.2 may lead to cache mismatches.<br/>
    /// Policy expressions aren't allowed.
    /// </summary>
    public required decimal ScoreThreshold { get; init; }

    /// <summary>
    /// Required. The name or ID of the backend service that will generate embeddings for semantic comparison.<br/>
    /// This must be an existing backend in API Management that points to a vector embedding service.
    /// </summary>
    [ExpressionAllowed]
    public required string EmbeddingsBackendId { get; init; }

    /// <summary>
    /// Required. Authentication setting for the embeddings backend service.<br/>
    /// Must be set to "system-assigned".
    /// </summary>
    [ExpressionAllowed]
    public required string EmbeddingsBackendAuth { get; init; }

    /// <summary>
    /// Optional. Whether to ignore system messages when comparing prompts semantically.<br/>
    /// If true, only user messages are considered for similarity comparison.<br/>
    /// Default is false.
    /// </summary>
    [ExpressionAllowed]
    public bool? IgnoreSystemMessages { get; init; }

    /// <summary>
    /// Optional. If specified, the number of remaining dialog messages after which caching is skipped.
    /// </summary>
    [ExpressionAllowed]
    public int? MaxMessageCount { get; init; }

    /// <summary>
    /// Optional. Identifier of a named cache instance to use for semantic cache lookup.<br/>
    /// When specified, targets a specific external cache rather than the built-in cache.
    /// </summary>
    public string? CacheId { get; init; }

    /// <summary>
    /// Optional. Array of request properties to vary the cache by.<br/>
    /// For example, to maintain separate caches for different users or contexts.
    /// </summary>
    [ExpressionAllowed]
    public string[]? VaryBy { get; init; }
}