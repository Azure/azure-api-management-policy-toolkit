// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Policies;

/// <summary>Shares LLM semantic-cache storage while retaining its own outbound callback registration.</summary>
[Section(nameof(IOutboundContext))]
internal class AzureOpenAiSemanticCacheStoreHandler : LlmSemanticCacheStoreHandler
{
    public override string PolicyName => nameof(IOutboundContext.AzureOpenAiSemanticCacheStore);
}