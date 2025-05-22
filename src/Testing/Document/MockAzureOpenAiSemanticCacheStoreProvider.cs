// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Policies;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Document;

public static class MockAzureOpenAiSemanticCacheStoreProvider
{
    public static Setup AzureOpenAiSemanticCacheStore(
        this MockPoliciesProvider<IOutboundContext> mock) => AzureOpenAiSemanticCacheStore(mock, (_, _) => true);

    public static Setup AzureOpenAiSemanticCacheStore(
        this MockPoliciesProvider<IOutboundContext> mock,
        Func<GatewayContext, uint, bool> predicate)
    {
        var handler = mock.SectionContextProxy.GetHandler<AzureOpenAiSemanticCacheStoreHandler>();
        return new Setup(predicate, handler);
    }

    public class Setup
    {
        private readonly Func<GatewayContext, uint, bool> _predicate;
        private readonly AzureOpenAiSemanticCacheStoreHandler _handler;

        internal Setup(
            Func<GatewayContext, uint, bool> predicate,
            AzureOpenAiSemanticCacheStoreHandler handler)
        {
            _predicate = predicate;
            _handler = handler;
        }

        public void WithCallback(Action<GatewayContext, uint> callback) =>
            _handler.CallbackSetup.Add((_predicate, callback).ToTuple());
    }
}