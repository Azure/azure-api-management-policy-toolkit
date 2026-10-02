// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Services;

/// <summary>A snapshot of the embedding request evaluated by an inbound semantic-cache lookup.</summary>
public sealed record SemanticCacheEmbeddingRequest
{
    /// <summary>The evaluated embeddings backend ID.</summary>
    public required string BackendId { get; init; }

    /// <summary>The validated authentication mode, system-assigned.</summary>
    public required string Authentication { get; init; }

    /// <summary>
    /// Canonical JSON containing the remaining text-only dialog messages, preserving roles, order, and message metadata.
    /// System messages are removed only when IgnoreSystemMessages is true.
    /// OpenAI Chat Completions/Responses, Anthropic Messages, and Vertex text envelopes are normalized locally.
    /// Vertex text Parts may include boolean thought and string thoughtSignature metadata, which is preserved
    /// in the normalized prompt without interpreting the opaque signature.
    /// Tool declarations/active choices, tool-call turns, audio references, multimodal content, and streaming
    /// cannot be faithfully represented by the buffered emulator and are rejected before embedding or cache access.
    /// </summary>
    public required string Prompt { get; init; }
}