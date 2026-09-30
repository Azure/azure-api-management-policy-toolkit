// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Services;

/// <summary>
/// A validated snapshot of the inbound content and safety configuration supplied to an injected evaluator.
/// </summary>
public sealed record LlmContentSafetyEvaluationRequest
{
    /// <summary>The backend ID evaluated from the policy configuration.</summary>
    public required string BackendId { get; init; }

    /// <summary>The complete, unconsumed inbound request body, including any API-specific prompt envelope.</summary>
    public required string Content { get; init; }

    /// <summary>FourSeverityLevels (the default) or EightSeverityLevels.</summary>
    public required string OutputType { get; init; }

    /// <summary>Configured harm categories and their inclusive blocking thresholds, between zero and seven.</summary>
    public required IReadOnlyDictionary<string, int> CategoryThresholds { get; init; }

    /// <summary>The configured blocklist IDs. Matching uses ordinal, case-sensitive IDs.</summary>
    public required IReadOnlyList<string> BlockListIds { get; init; }

    /// <summary>Whether the evaluator must check for prompt attacks.</summary>
    public required bool ShieldPrompt { get; init; }
}
