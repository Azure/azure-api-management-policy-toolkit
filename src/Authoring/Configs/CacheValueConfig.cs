// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;

/// <summary>
/// Configuration for the cache-value policy which provides a unified caching solution
/// with stampede protection by combining cache lookup and store operations.
/// </summary>
public record CacheValueConfig
{
    /// <summary>
    /// The cache key to use when looking up and storing the value.
    /// Policy expressions are allowed.
    /// </summary>
    [ExpressionAllowed]
    public required string Key { get; init; }

    /// <summary>
    /// Name of the variable that will contain the cached value.
    /// </summary>
    public required string VariableName { get; init; }

    /// <summary>
    /// Duration in seconds after which the cached value expires.
    /// Policy expressions are allowed.
    /// </summary>
    [ExpressionAllowed]
    public int? ExpiresAfter { get; init; }

    /// <summary>
    /// Duration in seconds after which the cached value is refreshed via the nested value block.
    /// Must be less than ExpiresAfter for stampede protection.
    /// Policy expressions are allowed.
    /// </summary>
    [ExpressionAllowed]
    public int? RefreshAfter { get; init; }

    /// <summary>
    /// Optional evaluator called after the value block to re-evaluate ExpiresAfter.
    /// Used when the expression depends on variables set within the value block.
    /// </summary>
    public Func<int>? ExpiresAfterEvaluator { get; init; }

    /// <summary>
    /// Optional evaluator called after the value block to re-evaluate RefreshAfter.
    /// Used when the expression depends on variables set within the value block.
    /// </summary>
    public Func<int>? RefreshAfterEvaluator { get; init; }

    /// <summary>
    /// Default value to assign if the cache lookup misses and the nested value block does not set a value.
    /// Policy expressions are allowed.
    /// </summary>
    [ExpressionAllowed]
    public object? DefaultValue { get; init; }

    /// <summary>
    /// Type of cache to use. Valid values are "internal", "external", or "prefer-external".
    /// If not specified, "prefer-external" is used.
    /// </summary>
    public string? CachingType { get; init; }
}
