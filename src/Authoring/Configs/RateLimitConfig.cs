// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.ApiManagement.PolicyToolkit.Authoring;

public record RateLimitConfig
{
    public required int Calls { get; init; }
    public required int RenewalPeriod { get; init; }

    public string? RetryAfterHeaderName { get; init; }
    public string? RetryAfterVariableName { get; init; }
    public string? RemainingCallsHeaderName { get; init; }
    public string? RemainingCallsVariableName { get; init; }
    public string? TotalCallsHeaderName { get; init; }

    public ApiRateLimit[]? Apis { get; init; }
}

public record ApiRateLimit : EntityLimitConfig
{
    public OperationRateLimit[]? Operations { get; init; }
}

public record OperationRateLimit : EntityLimitConfig
{
}

public abstract record EntityLimitConfig
{
    public string? Name { get; init; }
    public string? Id { get; init; }
    public required int Calls { get; init; }
    public required int RenewalPeriod { get; init; }
}