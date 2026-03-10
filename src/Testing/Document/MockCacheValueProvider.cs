// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Policies;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Document;

public static class MockCacheValueProvider
{
    public static Setup CacheValue<T>(this MockPoliciesProvider<T> mock) where T : class =>
        CacheValue(mock, (_, _) => true);

    public static Setup CacheValue<T>(
        this MockPoliciesProvider<T> mock,
        Func<GatewayContext, CacheValueConfig, bool> predicate
    ) where T : class
    {
        var handler = mock.SectionContextProxy.GetHandler<CacheValueHandler>();
        return new Setup(predicate, handler);
    }

    public class Setup
    {
        private readonly Func<GatewayContext, CacheValueConfig, bool> _predicate;
        private readonly CacheValueHandler _handler;

        internal Setup(
            Func<GatewayContext, CacheValueConfig, bool> predicate,
            CacheValueHandler handler)
        {
            _predicate = predicate;
            _handler = handler;
        }

        public void WithCallback(Action<GatewayContext, CacheValueConfig> callback) =>
            _handler.CallbackHooks.Add((_predicate, callback).ToTuple());
    }
}
