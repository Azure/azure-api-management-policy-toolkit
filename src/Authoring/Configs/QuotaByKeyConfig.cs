// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.ApiManagement.PolicyToolkit.Authoring;

/// <summary>
/// Configuration for the quota-by-key policy.
/// </summary>
public record QuotaByKeyConfig
{
    /// <summary>
    /// Specifies the key used to identify the quota counter.
    /// </summary>
    [ExpressionAllowed]
    public required string CounterKey { get; init; }

    /// <summary>
    /// Specifies the renewal period in seconds for the quota.
    /// </summary>
    public required int RenewalPeriod { get; init; }

    /// <summary>
    /// Specifies the maximum number of calls allowed within the renewal period.
    /// </summary>
    public int? Calls { get; init; }

    /// <summary>
    /// Specifies the maximum bandwidth allowed within the renewal period, in bytes.
    /// </summary>
    public int? Bandwidth { get; init; }

    /// <summary>
    /// Specifies a condition that must be met for the quota counter to be incremented.
    /// </summary>
    [ExpressionAllowed]
    public bool? IncrementCondition { get; init; }

    /// <summary>
    /// Specifies the number of increments to apply to the quota counter when the condition is met.
    /// </summary>
    [ExpressionAllowed]
    public int? IncrementCount { get; init; }

    /// <summary>
    /// Specifies the start time for the first quota period.
    /// </summary>
    public string? FirstPeriodStart { get; init; }
}