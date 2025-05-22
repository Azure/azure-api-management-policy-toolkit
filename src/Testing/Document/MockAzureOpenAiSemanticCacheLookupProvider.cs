// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Policies;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Document;

public static class MockAzureOpenAiSemanticCacheLookupProvider
{
    public static Setup AzureOpenAiSemanticCacheLookup(
        this MockPoliciesProvider<IInboundContext> mock) => AzureOpenAiSemanticCacheLookup(mock, (_, _) => true);

    public static Setup AzureOpenAiSemanticCacheLookup(
        this MockPoliciesProvider<IInboundContext> mock,
        Func<GatewayContext, SemanticCacheLookupConfig, bool> predicate)
    {
        var handler = mock.SectionContextProxy.GetHandler<AzureOpenAiSemanticCacheLookupHandler>();
        return new Setup(predicate, handler);
    }

    public class Setup
    {
        private readonly Func<GatewayContext, SemanticCacheLookupConfig, bool> _predicate;
        private readonly AzureOpenAiSemanticCacheLookupHandler _handler;

        internal Setup(
            Func<GatewayContext, SemanticCacheLookupConfig, bool> predicate,
            AzureOpenAiSemanticCacheLookupHandler handler)
        {
            _predicate = predicate;
            _handler = handler;
        }

        public void WithCallback(Action<GatewayContext, SemanticCacheLookupConfig> callback) =>
            _handler.CallbackSetup.Add((_predicate, callback).ToTuple());
    }
}