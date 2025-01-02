// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.ApiManagement.PolicyToolkit.Authoring;
using Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Policies;

namespace Azure.ApiManagement.PolicyToolkit.Testing.Document;

public static class MockCacheRemoveValueProvider
{
    public static Setup CacheRemoveValue<T>(this MockPoliciesProvider<T> mock) where T : class =>
        CacheRemoveValue(mock, (_, _) => true);

    public static Setup CacheRemoveValue<T>(
        this MockPoliciesProvider<T> mock,
        Func<GatewayContext, CacheRemoveValueConfig, bool> predicate
    ) where T : class
    {
        var handler = mock.SectionContextProxy.GetHandler<CacheRemoveValueHandler>();
        return new Setup(predicate, handler);
    }

    public class Setup
    {
        private readonly Func<GatewayContext, CacheRemoveValueConfig, bool> _predicate;
        private readonly CacheRemoveValueHandler _handler;

        internal Setup(
            Func<GatewayContext, CacheRemoveValueConfig, bool> predicate,
            CacheRemoveValueHandler handler)
        {
            _predicate = predicate;
            _handler = handler;
        }

        public void WithCallback(Action<GatewayContext, CacheRemoveValueConfig> callback) =>
            _handler.CallbackSetup.Add((_predicate, callback).ToTuple());
    }
}