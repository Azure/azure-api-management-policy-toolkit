// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Security.Cryptography;
using System.Text;

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Data;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Services;

internal static class SemanticCachePolicyServices
{
    internal static ISemanticCacheSimilarity DefaultSimilarity { get; } = new SemanticCacheCosineSimilarity();

    internal static SemanticCacheLookupState PrepareLookup(GatewayContext context, SemanticCacheLookupConfig config)
    {
        ValidateConfig(config);
        var prompt = SemanticCachePrompt.Create(context, config.IgnoreSystemMessages == true);
        var provider = context.Services.Resolve<ISemanticCacheEmbeddingProvider>(config.EmbeddingsBackendId)
            ?? context.Services.Resolve<ISemanticCacheEmbeddingProvider>()
            ?? throw new InvalidOperationException(
                $"No ISemanticCacheEmbeddingProvider registered for embeddings backend '{config.EmbeddingsBackendId}'. " +
                "Register a provider via GatewayContext.Services, optionally keyed by that backend ID.");
        var cache = ResolveCache(context, config.CacheId);
        var clock = CachePolicyServices.GetTimeProvider(context, cache);
        var partition = SemanticCachePrompt.Canonicalize(new
        {
            config.CacheId,
            config.EmbeddingsBackendId,
            config.EmbeddingsBackendAuth,
            ApiId = context.Api.Id,
            OperationId = context.Operation.Id,
            Endpoint = context.Request.OriginalUrl.ToUri().AbsoluteUri,
            prompt.Schema,
            prompt.Parameters,
            IgnoreSystemMessages = config.IgnoreSystemMessages == true,
            VaryBy = config.VaryBy ?? []
        });
        var state = new SemanticCacheLookupState(
            context.RequestId, context.Request, cache, clock,
            "__semantic_cache:v1:" + Hash(partition), Hash(prompt.Content), null);
        // Zero is also the authored representation of an omitted max-message-count.
        if (config.MaxMessageCount != 0 && prompt.MessageCount > config.MaxMessageCount)
        {
            return state;
        }

        var task = provider.GenerateAsync(new SemanticCacheEmbeddingRequest
        {
            BackendId = config.EmbeddingsBackendId,
            Authentication = config.EmbeddingsBackendAuth,
            Prompt = prompt.Content
        }) ?? throw new InvalidOperationException("The embedding provider returned no embedding task.");
        var embedding = SemanticCacheVectors.Snapshot(task.GetAwaiter().GetResult());
        return state with { Embedding = embedding };
    }

    internal static SemanticCachePartition ReadPartition(object? value) => value switch
    {
        null => new SemanticCachePartition(Array.Empty<SemanticCacheEntry>()),
        SemanticCachePartition partition => partition,
        _ => throw new InvalidOperationException("The external cache returned an invalid semantic cache index.")
    };

    private static ICache ResolveCache(GatewayContext context, string? cacheId)
    {
        if (cacheId is null)
        {
            return CachePolicyServices.ResolveRequired(context, "external");
        }

        var cache = context.Services.Resolve<ICache>(cacheId)
            ?? throw new InvalidOperationException(
                $"The named external cache '{cacheId}' is not configured. Register an ICache service with that exact key.");
        if (cache is CacheStore store)
        {
            if (context.Services.Resolve<TimeProvider>() is { } clock)
            {
                store.WithTimeProvider(clock);
            }
            return store.GetCacheService("external")
                ?? throw new InvalidOperationException($"The named external cache '{cacheId}' is not configured as an external cache.");
        }
        return cache;
    }

    private static void ValidateConfig(SemanticCacheLookupConfig config)
    {
        if (config.ScoreThreshold is < 0 or > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(config.ScoreThreshold), config.ScoreThreshold,
                "ScoreThreshold must be between zero and one; lower values require closer matches.");
        }
        ArgumentException.ThrowIfNullOrWhiteSpace(config.EmbeddingsBackendId, nameof(config.EmbeddingsBackendId));
        ArgumentException.ThrowIfNullOrWhiteSpace(config.EmbeddingsBackendAuth, nameof(config.EmbeddingsBackendAuth));
        if (config.EmbeddingsBackendAuth != "system-assigned")
        {
            throw new ArgumentException("EmbeddingsBackendAuth must be system-assigned.", nameof(config.EmbeddingsBackendAuth));
        }
        if (config.CacheId is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(config.CacheId, nameof(config.CacheId));
        }
        foreach (var value in config.VaryBy ?? [])
        {
            ArgumentNullException.ThrowIfNull(value, nameof(config.VaryBy));
        }
    }

    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}