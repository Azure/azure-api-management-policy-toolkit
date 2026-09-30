// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Data;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Services;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Policies;

[Section(nameof(IOutboundContext))]
internal class LlmSemanticCacheStoreHandler : PolicyHandler<uint>, IPolicyHandler
{
    public override string PolicyName => nameof(IOutboundContext.LlmSemanticCacheStore);

    object? IPolicyHandler.Handle(GatewayContext context, object?[]? args)
    {
        try
        {
            return base.Handle(context, args);
        }
        finally
        {
            SemanticCacheLookupState.Clear(context);
        }
    }

    protected override void Handle(GatewayContext context, uint duration)
    {
        var state = SemanticCacheLookupState.GetRequired(context);
        SemanticCachePayload.ValidateRequest(context);
        if (state.Embedding is null || context.Response.StatusCode != 200)
        {
            return;
        }

        SemanticCachePayload.ValidateResponse(context.Response);

        var snapshot = new CachedResponse
        {
            StatusCode = context.Response.StatusCode,
            StatusReason = context.Response.StatusReason,
            Body = context.Response.Body.Content,
            Headers = context.Response.Headers.ToDictionary(
                header => header.Key, header => header.Value.ToArray(), context.Response.Headers.Comparer)
        };
        var ttl = TimeSpan.FromSeconds(duration);
        var updated = false;
        var task = state.Cache.GetOrCreateWithDynamicTtlAsync(state.PartitionKey, (previous, cancellation) =>
        {
            cancellation.ThrowIfCancellationRequested();
            var now = state.Clock.GetUtcNow();
            var expiresAt = now + ttl;
            var partition = SemanticCachePolicyServices.ReadPartition(previous);
            var entries = partition.Entries.Where(entry => now < entry.ExpiresAt).ToList();
            foreach (var entry in entries)
            {
                SemanticCacheVectors.ValidateDimensions(state.Embedding, entry.Embedding);
            }
            entries.RemoveAll(entry => entry.PromptKey == state.PromptKey);
            if (ttl > TimeSpan.Zero)
            {
                snapshot.ExpiresAt = expiresAt;
                entries.Add(new SemanticCacheEntry(state.PromptKey, state.Embedding, snapshot, expiresAt));
            }

            var remaining = entries.Count == 0 ? TimeSpan.Zero : entries.Max(entry => entry.ExpiresAt) - now;
            var value = new SemanticCachePartition(Array.AsReadOnly(entries.ToArray()));
            updated = true;
            return Task.FromResult(new CacheValueFactoryResult(value, remaining, remaining));
        }, forceRefresh: true) ?? throw new InvalidOperationException("The external cache returned no semantic store task.");
        var result = task.GetAwaiter().GetResult()
            ?? throw new InvalidOperationException("The external cache returned no semantic store result.");
        if (!updated || result.Value is not SemanticCachePartition)
        {
            throw new InvalidOperationException("The external cache did not update the semantic cache index.");
        }
    }
}