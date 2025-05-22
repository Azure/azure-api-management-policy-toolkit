// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;

/// <summary>
/// Configuration for the limit-concurrency policy.<br />
/// Specifies the maximum number of concurrent calls allowed and the behavior when the limit is reached.
/// </summary>
public record LimitConcurrencyConfig
{
    /// <summary>
    /// Specifies the key used to identify the concurrency limit. Policy expressions are allowed.
    /// </summary>
    [ExpressionAllowed]
    public required string Key { get; init; }

    /// <summary>
    /// Specifies the maximum number of concurrent calls allowed. Policy expressions are not allowed.
    /// </summary>
    public required int MaxCount { get; init; }
}