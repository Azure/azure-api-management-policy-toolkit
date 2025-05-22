// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Policies;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Document;

public static class MockCacheStoreProvider
{
    public static Setup CacheStore(this MockPoliciesProvider<IOutboundContext> mock) =>
        CacheStore(mock, (_, _, _) => true);

    public static Setup CacheStore(
        this MockPoliciesProvider<IOutboundContext> mock,
        Func<GatewayContext, uint, bool, bool> predicate
    )
    {
        var handler = mock.SectionContextProxy.GetHandler<CacheStoreHandler>();
        return new Setup(predicate, handler);
    }

    public class Setup
    {
        private readonly Func<GatewayContext, uint, bool, bool> _predicate;
        private readonly CacheStoreHandler _handler;

        internal Setup(
            Func<GatewayContext, uint, bool, bool> predicate,
            CacheStoreHandler handler)
        {
            _predicate = predicate;
            _handler = handler;
        }

        public void WithCallback(Action<GatewayContext, uint, bool> callback) =>
            _handler.CallbackHooks.Add((_predicate, callback).ToTuple());
    }
}