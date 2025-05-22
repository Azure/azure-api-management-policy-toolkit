// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;

/// <summary>
/// Configuration for the cache-remove-value policy, which removes a value from the cache.<br/>
/// This policy is used to remove an item from the cache using a specified key.<br/>
/// Learn more: <a href="https://learn.microsoft.com/en-us/azure/api-management/cache-remove-value-policy">cache-remove-value policy</a>
/// </summary>
public record CacheRemoveValueConfig
{
    /// <summary>
    /// Required. Specifies the key for which to remove the value from the cache.<br/>
    /// Policy expressions are allowed.
    /// </summary>
    [ExpressionAllowed]
    public required string Key { get; init; }

    /// <summary>
    /// Optional. One of the following values: prefer-external, external, internal.<br/>
    /// Default is prefer-external.<br/>
    /// Policy expressions are not allowed.
    /// </summary>
    public string? CachingType { get; init; }
}