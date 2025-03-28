// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.ApiManagement.PolicyToolkit.Authoring;

/// <summary>
/// Configuration for semantic cache lookup policies (azure-openai-semantic-cache-lookup and llm-semantic-cache-lookup).<br/>
/// These policies use vector embeddings to find semantically similar requests in the cache.
/// </summary>
public record SemanticCacheLookupConfig
{
    /// <summary>
    /// Required. The similarity threshold (between 0 and 1) that determines whether a cached response is returned.<br/>
    /// Higher values require greater similarity between the current request and cached requests.<br/>
    /// Recommended values are between 0.7 and 0.9.
    /// </summary>
    [ExpressionAllowed]
    public required decimal ScoreThreshold { get; init; }

    /// <summary>
    /// Required. The name or ID of the backend service that will generate embeddings for semantic comparison.<br/>
    /// This must be an existing backend in API Management that points to a vector embedding service.
    /// </summary>
    [ExpressionAllowed]
    public required string EmbeddingsBackendId { get; init; }

    /// <summary>
    /// Required. Authentication setting for the embeddings backend service.<br/>
    /// Must be configured as a named value in API Management.
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
    /// Optional. Maximum number of messages to consider when comparing chat completions.<br/>
    /// Default is 4 messages.
    /// </summary>
    [ExpressionAllowed]
    public uint MaxMessageCount { get; init; }

    /// <summary>
    /// Optional. Array of request properties to vary the cache by.<br/>
    /// For example, to maintain separate caches for different users or contexts.
    /// </summary>
    [ExpressionAllowed]
    public string[]? VaryBy { get; init; }
}