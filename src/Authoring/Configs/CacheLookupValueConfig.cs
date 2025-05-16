// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;

/// <summary>
/// Configuration for the cache-lookup-value policy which retrieves a cached value and stores it in a variable.
/// </summary>
public record CacheLookupValueConfig
{
    /// <summary>
    /// The key to use when looking up a value in the cache.
    /// Policy expressions are allowed.
    /// </summary>
    [ExpressionAllowed]
    public required string Key { get; init; }

    /// <summary>
    /// Name of the variable that will contain the cached value if the lookup is successful.
    /// </summary>
    public required string VariableName { get; init; }

    /// <summary>
    /// Value to assign to the named variable if the key isn't found in the cache or is expired.
    /// If not specified, the variable will contain null when the lookup fails.
    /// Policy expressions are allowed.
    /// </summary>
    [ExpressionAllowed]
    public object? DefaultValue { get; init; }

    /// <summary>
    /// Type of cache to use. Valid values are "internal" (per-gateway private cache) 
    /// or "external" (shared cache as configured in the policy).
    /// If not specified, "external" is used.
    /// </summary>
    public string? CachingType { get; init; }
}