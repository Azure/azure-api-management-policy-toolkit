// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Services;

/// <summary>
/// Supplies embeddings for semantic-cache policies without making cloud calls in the emulator.
/// Register an implementation as ISemanticCacheEmbeddingProvider, optionally keyed by the embeddings backend ID.
/// A keyed registration takes precedence over an unkeyed provider.
/// </summary>
/// <remarks>
/// Both LLM and Azure OpenAI aliases use the same implementation and cache partitions, with independent callbacks.
/// An external ICache is required: register it with key "external", register an unkeyed ICache, or enable
/// WithExternalCacheSetup(). CacheId requires an exact keyed ICache registration and never falls back to another cache.
/// A registered CacheStore must have its external partition enabled.
/// Partitions include the API, operation, original endpoint, request options, embeddings backend, system-message filter,
/// CacheId, and ordered VaryBy values. Configure a shared TimeProvider for deterministic expiration.
/// MaxMessageCount skips caching above the remaining dialog limit; zero means omitted because the authored uint
/// cannot distinguish omission from an explicit zero. The duration-only authored store caches HTTP 200 responses.
/// ICache implementations must retain emulator index objects and honor atomic, force-refreshed value factories.
/// Cache failures are surfaced for testing, rather than silently applying APIM's production cache-unavailability fallback.
/// Unsupported tool/audio/media payloads are checked before lookup, before store, and before replaying cached responses.
/// Streaming RPCs in the current/original request or backend URL are rejected even when the response uses JSON.
/// Empty tool lists and explicitly disabled tool choices remain supported for text-only requests.
/// Response tool_choice="auto" is inert metadata when no tools are enabled; request-side tool rejection is unchanged.
/// Declared JSON responses are parsed strictly. Explicit non-JSON text remains opaque, including bracket/brace text.
/// Without a Content-Type, JSON-shaped model response envelopes are validated. Completion chunk discriminators
/// are rejected even for a final usage-only chunk with no choices or deltas.
/// </remarks>
public interface ISemanticCacheEmbeddingProvider
{
    /// <summary>
    /// Generates a finite, nonzero vector for the preserved, normalized prompt.
    /// The provider models the configured backend and system-assigned authentication; the emulator does not acquire tokens.
    /// Backend failures propagate as PolicyException rather than being interpreted as cache misses.
    /// </summary>
    Task<IReadOnlyList<double>> GenerateAsync(
        SemanticCacheEmbeddingRequest request, CancellationToken cancellationToken = default);
}