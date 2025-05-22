// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Policies;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Document;

public static class MockLlmSemanticCacheLookupProvider
{
    public static Setup LlmSemanticCacheLookup(
        this MockPoliciesProvider<IInboundContext> mock) => LlmSemanticCacheLookup(mock, (_, _) => true);

    public static Setup LlmSemanticCacheLookup(
        this MockPoliciesProvider<IInboundContext> mock,
        Func<GatewayContext, SemanticCacheLookupConfig, bool> predicate)
    {
        var handler = mock.SectionContextProxy.GetHandler<LlmSemanticCacheLookupHandler>();
        return new Setup(predicate, handler);
    }

    public class Setup
    {
        private readonly Func<GatewayContext, SemanticCacheLookupConfig, bool> _predicate;
        private readonly LlmSemanticCacheLookupHandler _handler;

        internal Setup(
            Func<GatewayContext, SemanticCacheLookupConfig, bool> predicate,
            LlmSemanticCacheLookupHandler handler)
        {
            _predicate = predicate;
            _handler = handler;
        }

        public void WithCallback(Action<GatewayContext, SemanticCacheLookupConfig> callback) =>
            _handler.CallbackSetup.Add((_predicate, callback).ToTuple());
    }
}