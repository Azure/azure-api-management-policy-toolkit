// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Policies;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Document;

public static class MockCacheStoreValueProvider
{
    public static Setup CacheStoreValue<T>(this MockPoliciesProvider<T> mock) where T : class =>
        CacheStoreValue(mock, (_, _) => true);

    public static Setup CacheStoreValue<T>(
        this MockPoliciesProvider<T> mock,
        Func<GatewayContext, CacheStoreValueConfig, bool> predicate
    ) where T : class
    {
        var handler = mock.SectionContextProxy.GetHandler<CacheStoreValueHandler>();
        return new Setup(predicate, handler);
    }

    public class Setup
    {
        private readonly Func<GatewayContext, CacheStoreValueConfig, bool> _predicate;
        private readonly CacheStoreValueHandler _handler;

        internal Setup(
            Func<GatewayContext, CacheStoreValueConfig, bool> predicate,
            CacheStoreValueHandler handler)
        {
            _predicate = predicate;
            _handler = handler;
        }

        public void WithCallback(Action<GatewayContext, CacheStoreValueConfig> callback) =>
            _handler.CallbackSetup.Add((_predicate, callback).ToTuple());
    }
}