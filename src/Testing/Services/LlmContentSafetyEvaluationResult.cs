// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Services;

/// <summary>
/// Observations returned by the injected safety dependency. The policy handler, not the dependency, enforces thresholds.
/// Incomplete or invalid observations fail explicitly rather than allowing content through.
/// </summary>
public sealed record LlmContentSafetyEvaluationResult
{
    /// <summary>
    /// Severity for every requested category. Valid categories are Hate, SelfHarm, Sexual, and Violence.
    /// FourSeverityLevels permits 0, 2, 4, and 6; EightSeverityLevels permits every integer from 0 through 7.
    /// Additional valid categories are allowed but do not trigger unconfigured rules.
    /// </summary>
    public required IReadOnlyDictionary<string, int> CategorySeverities { get; init; }

    /// <summary>
    /// IDs of blocklists with detected matches. Only IDs configured by the policy cause a violation.
    /// Return an empty collection when no matches are detected.
    /// </summary>
    public required IReadOnlyCollection<string> MatchedBlockListIds { get; init; }

    /// <summary>
    /// Whether a prompt attack was detected. A non-null result is required when prompt shielding is enabled;
    /// otherwise this observation is ignored.
    /// </summary>
    public bool? PromptAttackDetected { get; init; }
}
