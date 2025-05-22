// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Document;

namespace Test.Emulator.Emulator.Policies;

[TestClass]
public class AzureOpenAiSemanticCacheLookupTests
{
    class SimpleAzureOpenAiSemanticCacheLookup : IDocument
    {
        public void Inbound(IInboundContext context)
        {
            context.AzureOpenAiSemanticCacheLookup(new SemanticCacheLookupConfig()
            {
                ScoreThreshold = 0.05M, EmbeddingsBackendId = "backend-id", EmbeddingsBackendAuth = "token"
            });
        }
    }

    [TestMethod]
    public void AzureOpenAiEmitTokenMetric_Callback()
    {
        var test = new SimpleAzureOpenAiSemanticCacheLookup().AsTestDocument();
        var executedCallback = false;
        test.SetupInbound().AzureOpenAiSemanticCacheLookup().WithCallback((_, _) =>
        {
            executedCallback = true;
        });

        test.RunInbound();

        executedCallback.Should().BeTrue();
    }
}