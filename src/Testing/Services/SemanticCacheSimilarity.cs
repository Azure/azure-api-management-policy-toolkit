// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Services;

/// <summary>
/// Measures semantic-cache vector distance. Register an implementation as ISemanticCacheSimilarity to control scoring.
/// Lower distances are closer; a response matches when its distance is at most ScoreThreshold (between zero and one).
/// </summary>
public interface ISemanticCacheSimilarity
{
    /// <summary>
    /// Returns a finite distance between zero and two for equal-dimension, finite, nonzero vectors.
    /// The local default uses cosine distance (1 - cosine similarity); it does not reproduce a live Redis index or model.
    /// </summary>
    double GetDistance(IReadOnlyList<double> first, IReadOnlyList<double> second);
}

/// <summary>
/// Deterministic local cosine distance, ranging from zero for identical directions to two for opposite directions.
/// This is the default emulator scoring strategy, not a claim of parity with APIM's external vector index.
/// </summary>
public sealed class SemanticCacheCosineSimilarity : ISemanticCacheSimilarity
{
    /// <inheritdoc />
    public double GetDistance(IReadOnlyList<double> first, IReadOnlyList<double> second)
    {
        SemanticCacheVectors.Validate(first);
        SemanticCacheVectors.Validate(second);
        SemanticCacheVectors.ValidateDimensions(first, second);
        if (first.SequenceEqual(second))
        {
            return 0;
        }

        var firstScale = first.Max(value => Math.Abs(value));
        var secondScale = second.Max(value => Math.Abs(value));
        var dot = 0.0;
        var firstNorm = 0.0;
        var secondNorm = 0.0;
        for (var index = 0; index < first.Count; index++)
        {
            var left = first[index] / firstScale;
            var right = second[index] / secondScale;
            dot += left * right;
            firstNorm += left * left;
            secondNorm += right * right;
        }

        var cosine = dot / Math.Sqrt(firstNorm) / Math.Sqrt(secondNorm);
        return 1 - Math.Clamp(cosine, -1, 1);
    }
}

internal static class SemanticCacheVectors
{
    internal static IReadOnlyList<double> Snapshot(IReadOnlyList<double>? embedding)
    {
        if (embedding is null)
        {
            throw new InvalidOperationException("The embedding provider returned no vector.");
        }

        var copy = embedding.ToArray();
        Validate(copy);
        return Array.AsReadOnly(copy);
    }

    internal static void Validate(IReadOnlyList<double> embedding)
    {
        ArgumentNullException.ThrowIfNull(embedding);
        if (embedding.Count == 0 || embedding.Any(value => !double.IsFinite(value))
            || !embedding.Any(value => value != 0))
        {
            throw new InvalidOperationException("Semantic cache embeddings must be finite, nonempty, nonzero vectors.");
        }
    }

    internal static void ValidateDimensions(IReadOnlyList<double> first, IReadOnlyList<double> second)
    {
        if (first.Count != second.Count)
        {
            throw new InvalidOperationException(
                "Semantic cache embedding dimensions changed within one embeddings backend partition. " +
                "Use a distinct backend ID for a different embedding model.");
        }
    }
}