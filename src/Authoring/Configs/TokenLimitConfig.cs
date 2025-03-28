// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.ApiManagement.PolicyToolkit.Authoring;

public record TokenLimitConfig
{
    [ExpressionAllowed] public required string CounterKey { get; init; }
    [ExpressionAllowed] public required bool EstimatePromptToken { get; init; }

    [ExpressionAllowed] public int? TokensPerMinute { get; init; }
    [ExpressionAllowed] public int? TokenQuota { get; init; }
    public string? TokenQuotaPeriod { get; init; }

    public string? RetryAfterHeaderName { get; init; }
    public string? RetryAfterVariableName { get; init; }
    public string? RemainingQuotaTokensHeaderName { get; init; }
    public string? RemainingQuotaTokensVariableName { get; init; }
    public string? RemainingTokensHeaderName { get; init; }
    public string? RemainingTokensVariableName { get; init; }
    public string? TokensConsumedHeaderName { get; init; }
    public string? TokensConsumedVariableName { get; init; }
}