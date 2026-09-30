// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Policies;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Document;

public static class MockCrossDomainProvider
{
    public static Setup CrossDomain(this MockPoliciesProvider<IInboundContext> mock) =>
        CrossDomain(mock, (_, _) => true);

    public static Setup CrossDomain(
        this MockPoliciesProvider<IInboundContext> mock,
        Func<GatewayContext, string, bool> predicate
    )
    {
        var handler = mock.SectionContextProxy.GetHandler<CrossDomainHandler>();
        return new Setup(predicate, handler);
    }

    public class Setup
    {
        private readonly Func<GatewayContext, string, bool> _predicate;
        private readonly CrossDomainHandler _handler;

        internal Setup(
            Func<GatewayContext, string, bool> predicate,
            CrossDomainHandler handler)
        {
            _predicate = predicate;
            _handler = handler;
        }

        public void WithCallback(Action<GatewayContext, string> callback) =>
            _handler.CallbackHooks.Add((_predicate, callback).ToTuple());
    }
}
