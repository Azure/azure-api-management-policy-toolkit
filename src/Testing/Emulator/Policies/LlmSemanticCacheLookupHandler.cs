// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Data;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Services;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Policies;

[Section(nameof(IInboundContext))]
internal class LlmSemanticCacheLookupHandler : PolicyHandler<SemanticCacheLookupConfig>, IPolicyHandler
{
    public override string PolicyName => nameof(IInboundContext.LlmSemanticCacheLookup);

    object? IPolicyHandler.Handle(GatewayContext context, object?[]? args)
    {
        SemanticCacheLookupState.Clear(context);
        return base.Handle(context, args);
    }

    protected override void Handle(GatewayContext context, SemanticCacheLookupConfig config)
    {
        var state = SemanticCachePolicyServices.PrepareLookup(context, config);
        if (state.Embedding is null)
        {
            state.Remember(context);
            return;
        }

        var task = state.Cache.GetAsync(state.PartitionKey, HttpPolicyTransport.GetCancellationToken(context))
            ?? throw new InvalidOperationException("The external cache returned no semantic lookup task.");
        var partition = SemanticCachePolicyServices.ReadPartition(PolicyServiceAwaiter.Wait(context, task));
        var similarity = context.Services.Resolve<ISemanticCacheSimilarity>() ?? SemanticCachePolicyServices.DefaultSimilarity;
        var matches = new List<(double Distance, SemanticCacheEntry Entry)>();
        foreach (var entry in partition.Entries)
        {
            if (state.Clock.GetUtcNow() >= entry.ExpiresAt)
            {
                continue;
            }

            SemanticCacheVectors.ValidateDimensions(state.Embedding, entry.Embedding);
            var distance = similarity.GetDistance(state.Embedding, entry.Embedding);
            if (!double.IsFinite(distance) || distance is < 0 or > 2)
            {
                throw new InvalidOperationException("Semantic cache similarity must return a finite distance between zero and two.");
            }
            if (distance <= (double)config.ScoreThreshold)
            {
                matches.Add((distance, entry));
            }
        }

        var now = state.Clock.GetUtcNow();
        var hit = matches.Where(match => now < match.Entry.ExpiresAt)
            .OrderBy(match => match.Distance)
            .ThenBy(match => match.Entry.PromptKey, StringComparer.Ordinal)
            .Select(match => match.Entry)
            .FirstOrDefault();
        if (hit is null)
        {
            state.Remember(context);
            return;
        }

        SemanticCachePayload.ValidateResponse(hit.Response.Body, hit.Response.Headers);
        if (!ResponseUtilities.TryCopyCachedResponse(hit.Response, context.Response))
        {
            throw new InvalidOperationException("The semantic cache entry does not contain a response snapshot.");
        }
        context.ResponseTerminated = true;
        throw new FinishSectionProcessingException();
    }
}