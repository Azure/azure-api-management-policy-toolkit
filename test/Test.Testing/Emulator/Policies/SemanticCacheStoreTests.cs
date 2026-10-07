// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Document;

namespace Test.Emulator.Emulator.Policies;

[TestClass]
public class SemanticCacheStoreTests
{
    class SemanticCacheStores : IDocument
    {
        public void Outbound(IOutboundContext context)
        {
            context.LlmSemanticCacheStore(10);
            context.AzureOpenAiSemanticCacheStore(20, true);
        }
    }

    [TestMethod]
    public void SemanticCacheStore_RunsWithAndWithoutCacheResponse()
    {
        var test = new SemanticCacheStores().AsTestDocument();
        var durations = new List<uint>();
        test.SetupOutbound().LlmSemanticCacheStore().WithCallback((_, duration) => durations.Add(duration));
        test.SetupOutbound().AzureOpenAiSemanticCacheStore().WithCallback((_, duration) => durations.Add(duration));

        test.RunOutbound();

        durations.Should().Equal(10u, 20u);
    }
}
