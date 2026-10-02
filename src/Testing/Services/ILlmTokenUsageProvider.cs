// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Data;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Services;

/// <summary>
/// Supplies observed LLM token counts when the current response has no usable JSON usage section.
/// Register an implementation with GatewayContext.Services for either inbound emit-token-metric policy.
/// Response usage takes precedence; this provider is not a tokenizer and must not invent counts.
/// </summary>
/// <remarks>
/// Usage is required when the inbound policy executes; no later pipeline hook is installed.
/// A non-empty response must be a JSON object. Malformed payloads and invalid counts are surfaced
/// as policy errors, not hidden by provider fallback. Streaming events are not parsed or aggregated.
/// </remarks>
public interface ILlmTokenUsageProvider
{
    /// <summary>
    /// Gets usage for the current invocation, or null when no observed counts are available.
    /// Missing usage causes a policy error rather than emitting fabricated metrics.
    /// </summary>
    LlmTokenUsage? GetUsage(GatewayContext context);
}
