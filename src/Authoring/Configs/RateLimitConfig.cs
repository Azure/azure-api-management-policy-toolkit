// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.ApiManagement.PolicyToolkit.Authoring;

/// <summary>
/// Configuration for the rate-limit policy.<br/>
/// Specifies the maximum number of calls allowed within a specified renewal period.
/// </summary>
public record RateLimitConfig
{
    /// <summary>
    /// Maximum number of calls allowed within the renewal period.
    /// </summary>
    public required int Calls { get; init; }

    /// <summary>
    /// Renewal period in seconds after which the call count is reset.
    /// </summary>
    public required int RenewalPeriod { get; init; }

    /// <summary>
    /// Optional. Name of the HTTP header to set with the number of seconds until the limit resets.
    /// </summary>
    public string? RetryAfterHeaderName { get; init; }

    /// <summary>
    /// Optional. Name of the variable to set with the number of seconds until the limit resets.
    /// </summary>
    public string? RetryAfterVariableName { get; init; }

    /// <summary>
    /// Optional. Name of the HTTP header to set with the number of remaining calls allowed.
    /// </summary>
    public string? RemainingCallsHeaderName { get; init; }

    /// <summary>
    /// Optional. Name of the variable to set with the number of remaining calls allowed.
    /// </summary>
    public string? RemainingCallsVariableName { get; init; }

    /// <summary>
    /// Optional. Name of the HTTP header to set with the total number of calls allowed.
    /// </summary>
    public string? TotalCallsHeaderName { get; init; }

    /// <summary>
    /// Optional. Specifies rate limits for specific APIs and their operations.
    /// </summary>
    public ApiRateLimit[]? Apis { get; init; }
}

/// <summary>
/// Specifies rate limit configuration for a specific API.
/// </summary>
public record ApiRateLimit : EntityLimitConfig
{
    /// <summary>
    /// Optional. Specifies rate limits for specific operations within the API.
    /// </summary>
    public OperationRateLimit[]? Operations { get; init; }
}

/// <summary>
/// Specifies rate limit configuration for a specific operation.
/// </summary>
public record OperationRateLimit : EntityLimitConfig
{
}

/// <summary>
/// Base configuration for entity-specific rate limits.
/// </summary>
public abstract record EntityLimitConfig
{
    /// <summary>
    /// Optional. Name of the API or operation to which the rate limit applies.
    /// </summary>
    public string? Name { get; init; }

    /// <summary>
    /// Optional. Identifier of the API or operation to which the rate limit applies.
    /// </summary>
    public string? Id { get; init; }

    /// <summary>
    /// Maximum number of calls allowed within the renewal period for the specified entity.
    /// </summary>
    public required int Calls { get; init; }

    /// <summary>
    /// Renewal period in seconds after which the call count is reset for the specified entity.
    /// </summary>
    public required int RenewalPeriod { get; init; }
}