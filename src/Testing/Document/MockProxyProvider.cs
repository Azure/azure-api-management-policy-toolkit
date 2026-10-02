// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Policies;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Document;

public static class MockProxyProvider
{
    public static Setup Proxy(this MockPoliciesProvider<IInboundContext> mock) =>
        Proxy(mock, (_, _) => true);

    public static Setup Proxy(
        this MockPoliciesProvider<IInboundContext> mock,
        Func<GatewayContext, ProxyConfig, bool> predicate)
    {
        var handler = mock.SectionContextProxy.GetHandler<ProxyHandler>();
        return new Setup(predicate, handler);
    }

    public class Setup
    {
        private readonly Func<GatewayContext, ProxyConfig, bool> _predicate;
        private readonly ProxyHandler _handler;

        internal Setup(Func<GatewayContext, ProxyConfig, bool> predicate, ProxyHandler handler)
        {
            _predicate = predicate;
            _handler = handler;
        }

        public void WithCallback(Action<GatewayContext, ProxyConfig> callback) =>
            _handler.CallbackSetup.Add((_predicate, callback).ToTuple());
    }
}