// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Policies;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Document;

public static class MockCacheLookupProvider
{
    public static Setup CacheLookup(
        this MockPoliciesProvider<IInboundContext> mock) => CacheLookup(mock, (_, _) => true);

    public static Setup CacheLookup(
        this MockPoliciesProvider<IInboundContext> mock,
        Func<GatewayContext, CacheLookupConfig, bool> predicate)
    {
        var handler = mock.SectionContextProxy.GetHandler<CacheLookupHandler>();
        return new Setup(predicate, handler);
    }

    public class Setup
    {
        private readonly Func<GatewayContext, CacheLookupConfig, bool> _predicate;
        private readonly CacheLookupHandler _handler;

        internal Setup(
            Func<GatewayContext, CacheLookupConfig, bool> predicate,
            CacheLookupHandler handler)
        {
            _predicate = predicate;
            _handler = handler;
        }

        public void WithCallback(Action<GatewayContext, CacheLookupConfig> callback) =>
            _handler.CallbackSetup.Add((_predicate, callback).ToTuple());
    }
}