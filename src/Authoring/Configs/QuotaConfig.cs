// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.ApiManagement.PolicyToolkit.Authoring;

/// <summary>
/// Configuration for the quota policy, specifying the quota limits, renewal period, and optional API and operation-specific quotas.
/// </summary>
public record QuotaConfig : BaseQuotaConfig
{
    /// <summary>
    /// Specifies the renewal period for the quota in seconds. Policy expressions are not allowed.
    /// </summary>
    public required int RenewalPeriod { get; init; }

    /// <summary>
    /// Specifies the API-specific quotas.
    /// </summary>
    public ApiQuota[]? Apis { get; init; }
}

/// <summary>
/// Configuration for API-specific quotas.
/// </summary>
public record ApiQuota : EntityQuotaConfig
{
    /// <summary>
    /// Specifies the operation-specific quotas.
    /// </summary>
    public OperationQuota[]? Operations { get; init; }
}

/// <summary>
/// Configuration for operation-specific quotas.
/// </summary>
public record OperationQuota : EntityQuotaConfig
{
}

/// <summary>
/// Base configuration for quotas, specifying the quota limits.
/// </summary>
public abstract record EntityQuotaConfig : BaseQuotaConfig
{
    /// <summary>
    /// Specifies the name of the entity (API or operation).
    /// </summary>
    public string? Name { get; init; }

    /// <summary>
    /// Specifies the ID of the entity (API or operation).
    /// </summary>
    public string? Id { get; init; }
}

/// <summary>
/// Base configuration for quotas, specifying the quota limits.
/// </summary>
public abstract record BaseQuotaConfig
{
    /// <summary>
    /// Specifies the maximum number of calls allowed within the renewal period.
    /// </summary>
    public int? Calls { get; init; }

    /// <summary>
    /// Specifies the maximum bandwidth allowed within the renewal period in kilobytes.
    /// </summary>
    public int? Bandwidth { get; init; }
}