// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Services;

/// <summary>
/// Injected content safety dependency for the inbound LlmContentSafety policy. No cloud calls are made by the emulator.
/// Register an implementation using GatewayContext.Services.Register&lt;ILlmContentSafetyEvaluator&gt;(evaluator),
/// optionally keyed by the configured backend ID. A keyed registration takes precedence over an unkeyed registration.
/// </summary>
public interface ILlmContentSafetyEvaluator
{
    /// <summary>
    /// Evaluates the preserved inbound request content against the requested safety checks.
    /// Implementations interpret any API-specific request envelope, including LLM prompts or chat messages.
    /// Configurations must enable at least one category, blocklist, or prompt shield rule.
    /// Exceptions propagate through the emulator's PolicyException semantics; they are not treated as safe results or violations.
    /// </summary>
    Task<LlmContentSafetyEvaluationResult> EvaluateAsync(
        LlmContentSafetyEvaluationRequest request, CancellationToken cancellationToken = default);
}
