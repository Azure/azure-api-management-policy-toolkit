// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Policies;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Document;

public static class MockIpFilterProvider
{
    public static Setup IpFilter(this MockPoliciesProvider<IInboundContext> mock) =>
        IpFilter(mock, (_, _) => true);

    public static Setup IpFilter(
        this MockPoliciesProvider<IInboundContext> mock,
        Func<GatewayContext, IpFilterConfig, bool> predicate
    )
    {
        var handler = mock.SectionContextProxy.GetHandler<IpFilterHandler>();
        return new Setup(predicate, handler);
    }

    public class Setup
    {
        private readonly Func<GatewayContext, IpFilterConfig, bool> _predicate;
        private readonly IpFilterHandler _handler;

        internal Setup(
            Func<GatewayContext, IpFilterConfig, bool> predicate,
            IpFilterHandler handler)
        {
            _predicate = predicate;
            _handler = handler;
        }

        public void WithCallback(Action<GatewayContext, IpFilterConfig> callback) =>
            _handler.CallbackSetup.Add((_predicate, callback).ToTuple());

        public void OnIpDeny(Action<GatewayContext, IpFilterConfig> onIpDeny) =>
            _handler.OnIpDenied.Add((_predicate, onIpDeny).ToTuple());

        public void OnIpAllow(Action<GatewayContext, IpFilterConfig> onIpAllow) =>
            _handler.OnIpAllowed.Add((_predicate, onIpAllow).ToTuple());
    }
}