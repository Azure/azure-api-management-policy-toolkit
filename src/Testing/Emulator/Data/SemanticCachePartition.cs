// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Services;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Data;

internal sealed record SemanticCachePartition(IReadOnlyList<SemanticCacheEntry> Entries);

internal sealed record SemanticCacheEntry(
    string PromptKey,
    IReadOnlyList<double> Embedding,
    CachedResponse Response,
    DateTimeOffset ExpiresAt);