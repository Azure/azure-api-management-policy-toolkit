// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;

/// <summary>
/// Configuration for the rate-limit-by-key policy, specifying limits and behavior for rate limiting based on a key.
/// </summary>
public record RateLimitByKeyConfig
{
    /// <summary>
    /// Maximum number of calls allowed within the renewal period. Policy expressions are allowed.
    /// </summary>
    [ExpressionAllowed]
    public required int Calls { get; init; }

    /// <summary>
    /// Renewal period in seconds after which the call count is reset. Policy expressions are allowed.
    /// </summary>
    [ExpressionAllowed]
    public required int RenewalPeriod { get; init; }

    /// <summary>
    /// Key used to identify the caller for rate limiting purposes. Policy expressions are allowed.
    /// </summary>
    [ExpressionAllowed]
    public required string CounterKey { get; init; }

    /// <summary>
    /// Condition under which the call count is incremented. If false, the call count is not incremented. Policy expressions are allowed.
    /// </summary>
    [ExpressionAllowed]
    public bool? IncrementCondition { get; init; }

    /// <summary>
    /// Number by which the call count is incremented for each request. Default is 1. Policy expressions are allowed.
    /// </summary>
    [ExpressionAllowed]
    public int? IncrementCount { get; init; }

    /// <summary>
    /// Name of the HTTP header to return the number of seconds to wait before retrying.
    /// </summary>
    public string? RetryAfterHeaderName { get; init; }

    /// <summary>
    /// Name of the variable to store the number of seconds to wait before retrying.
    /// </summary>
    public string? RetryAfterVariableName { get; init; }

    /// <summary>
    /// Name of the HTTP header to return the number of remaining calls allowed.
    /// </summary>
    public string? RemainingCallsHeaderName { get; init; }

    /// <summary>
    /// Name of the variable to store the number of remaining calls allowed.
    /// </summary>
    public string? RemainingCallsVariableName { get; init; }

    /// <summary>
    /// Name of the HTTP header to return the total number of calls allowed.
    /// </summary>
    public string? TotalCallsHeaderName { get; init; }
}