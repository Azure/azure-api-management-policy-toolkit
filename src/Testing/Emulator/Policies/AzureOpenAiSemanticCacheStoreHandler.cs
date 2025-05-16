// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Policies;

[Section(nameof(IOutboundContext))]
internal class AzureOpenAiSemanticCacheStoreHandler : LlmSemanticCacheStoreHandler
{
    public override string PolicyName => nameof(IOutboundContext.AzureOpenAiSemanticCacheStore);
}