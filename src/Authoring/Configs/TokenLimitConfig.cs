// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.ApiManagement.PolicyToolkit.Authoring;

/// <summary>
/// Configuration for the azure-openai-token-limit and llm-token-limit policies.
/// These policies limit the rate and/or quota of tokens consumed by AI services.
/// </summary>
public record TokenLimitConfig
{
    /// <summary>
    /// Specifies a key used to identify the counter. Policy expressions are allowed.
    /// </summary>
    [ExpressionAllowed]
    public required string CounterKey { get; init; }

    /// <summary>
    /// Indicates whether to estimate the token count of the prompt. Policy expressions are allowed.
    /// </summary>
    [ExpressionAllowed]
    public required bool EstimatePromptToken { get; init; }

    /// <summary>
    /// Specifies the maximum number of tokens that can be consumed per minute. Policy expressions are allowed.
    /// </summary>
    [ExpressionAllowed]
    public int? TokensPerMinute { get; init; }

    /// <summary>
    /// Specifies the maximum number of tokens that can be consumed per time period. Policy expressions are allowed.
    /// </summary>
    [ExpressionAllowed]
    public int? TokenQuota { get; init; }

    /// <summary>
    /// Specifies the time period for the token quota. Valid values are "day", "week", and "month".
    /// </summary>
    public string? TokenQuotaPeriod { get; init; }

    /// <summary>
    /// Name of the header that will contain the retry-after value when rate limiting is triggered.
    /// </summary>
    public string? RetryAfterHeaderName { get; init; }

    /// <summary>
    /// Name of the variable that will contain the retry-after value when rate limiting is triggered.
    /// </summary>
    public string? RetryAfterVariableName { get; init; }

    /// <summary>
    /// Name of the header that will contain the remaining quota tokens.
    /// </summary>
    public string? RemainingQuotaTokensHeaderName { get; init; }

    /// <summary>
    /// Name of the variable that will contain the remaining quota tokens.
    /// </summary>
    public string? RemainingQuotaTokensVariableName { get; init; }

    /// <summary>
    /// Name of the header that will contain the remaining tokens per minute.
    /// </summary>
    public string? RemainingTokensHeaderName { get; init; }

    /// <summary>
    /// Name of the variable that will contain the remaining tokens per minute.
    /// </summary>
    public string? RemainingTokensVariableName { get; init; }

    /// <summary>
    /// Name of the header that will contain the number of tokens consumed by the current request.
    /// </summary>
    public string? TokensConsumedHeaderName { get; init; }

    /// <summary>
    /// Name of the variable that will contain the number of tokens consumed by the current request.
    /// </summary>
    public string? TokensConsumedVariableName { get; init; }
}