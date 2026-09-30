// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Immutable;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Data;

/// <summary>
/// Observed token counts for an LLM response or an injected usage provider.
/// Only supplied counts are emitted: absent counts are not estimated, summed, or replaced with zero.
/// Counts must be non-negative and at most 2^53, preserving exact values in the double-valued metric sink.
/// </summary>
public record LlmTokenUsage
{
    /// <summary>Gets the observed total count, emitted as Total Tokens when supplied.</summary>
    public long? TotalTokens { get; init; }

    /// <summary>Gets the observed prompt or input count, emitted as Prompt Tokens when supplied.</summary>
    public long? PromptTokens { get; init; }

    /// <summary>Gets the observed completion or output count, emitted as Completion Tokens when supplied.</summary>
    public long? CompletionTokens { get; init; }

    /// <summary>
    /// Gets additional observed categories keyed by metric name, such as Cached Tokens, Reasoning Tokens,
    /// or Thinking Tokens. Names must be non-empty and cannot reuse the three primary metric names.
    /// JSON categories without a known display name retain their original property path as the metric name.
    /// Additional categories are emitted in ordinal name order after the available primary counts.
    /// </summary>
    public IReadOnlyDictionary<string, long> AdditionalTokens { get; init; } = ImmutableDictionary<string, long>.Empty;
}
