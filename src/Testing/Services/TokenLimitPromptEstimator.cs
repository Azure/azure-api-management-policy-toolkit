// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Services;

/// <summary>
/// Supplies a model-appropriate prompt token estimate for the inbound token-limit policies.
/// Register an implementation as ITokenLimitPromptEstimator in GatewayContext.Services.
/// No default tokenizer, character-count heuristic, or network call is provided.
/// </summary>
/// <remarks>
/// Implementations must use an actual tokenizer or injected deterministic tokenization fixture,
/// account for the model's text prompt schema, and return a nonnegative count.
/// Streaming and estimated image accounting are not supported by these handlers.
/// Actual prompt and completion usage replaces the estimate at the final request boundary.
/// </remarks>
public interface ITokenLimitPromptEstimator
{
    /// <summary>Estimates the prompt tokens for the current request without consuming its body.</summary>
    long EstimatePromptTokens(GatewayContext context);
}