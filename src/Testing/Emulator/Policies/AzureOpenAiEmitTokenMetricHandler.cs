// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Policies;

[Section(nameof(IInboundContext))]
internal class AzureOpenAiEmitTokenMetricHandler : LlmEmitTokenMetricHandler
{
    public override string PolicyName => nameof(IInboundContext.AzureOpenAiEmitTokenMetric);
}