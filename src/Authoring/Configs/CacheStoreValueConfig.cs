// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;

/// <summary>
/// Configuration for the cache-store-value policy, which stores a value in the cache.<br/>
/// This policy is used to store a value in the cache using a specified key.<br/>
/// Learn more: <a href="https://learn.microsoft.com/en-us/azure/api-management/cache-store-value-policy">cache-store-value policy</a>
/// </summary>
public record CacheStoreValueConfig
{
    /// <summary>
    /// Required. Specifies the key under which the value will be stored in the cache.<br/>
    /// Policy expressions are allowed.
    /// </summary>
    [ExpressionAllowed]
    public required string Key { get; init; }

    /// <summary>
    /// Required. Specifies the value to store in the cache.<br/>
    /// Policy expressions are allowed.
    /// </summary>
    [ExpressionAllowed]
    public required string Value { get; init; }

    /// <summary>
    /// Required. Specifies the duration in seconds for which the cached value is valid.<br/>
    /// Policy expressions are allowed.
    /// </summary>
    [ExpressionAllowed]
    public required int Duration { get; init; }

    /// <summary>
    /// Optional. One of the following values: prefer-external, external, internal.<br/>
    /// Default is prefer-external.<br/>
    /// Policy expressions are not allowed.
    /// </summary>
    public string? CachingType { get; init; }
}