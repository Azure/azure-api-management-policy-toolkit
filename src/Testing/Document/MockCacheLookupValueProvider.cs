// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Policies;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Document;

public static class MockCacheLookupValueProvider
{
    public static Setup CacheLookupValue<T>(this MockPoliciesProvider<T> mock) where T : class =>
        CacheLookupValue(mock, (_, _) => true);

    public static Setup CacheLookupValue<T>(
        this MockPoliciesProvider<T> mock,
        Func<GatewayContext, CacheLookupValueConfig, bool> predicate
    ) where T : class
    {
        var handler = mock.SectionContextProxy.GetHandler<CacheLookupValueHandler>();
        return new Setup(predicate, handler);
    }

    public class Setup
    {
        private readonly Func<GatewayContext, CacheLookupValueConfig, bool> _predicate;
        private readonly CacheLookupValueHandler _handler;

        internal Setup(
            Func<GatewayContext, CacheLookupValueConfig, bool> predicate,
            CacheLookupValueHandler handler)
        {
            _predicate = predicate;
            _handler = handler;
        }

        public void WithCallback(Action<GatewayContext, CacheLookupValueConfig> callback) =>
            _handler.CallbackSetup.Add((_predicate, callback).ToTuple());

        public void WithValue(object value) => _handler.ValueSetup.Add((_predicate, value).ToTuple());
    }
}