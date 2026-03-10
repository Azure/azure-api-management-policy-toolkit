// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Policies;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Document;

public static class MockSendOneWayRequestProvider
{
    public static Setup SendOneWayRequest<T>(this MockPoliciesProvider<T> mock) where T : class =>
        SendOneWayRequest(mock, (_, _) => true);

    public static Setup SendOneWayRequest<T>(
        this MockPoliciesProvider<T> mock,
        Func<GatewayContext, SendOneWayRequestConfig, bool> predicate
    ) where T : class
    {
        var handler = mock.SectionContextProxy.GetHandler<SendOneWayRequestHandler>();
        return new Setup(predicate, handler);
    }

    public class Setup
    {
        private readonly Func<GatewayContext, SendOneWayRequestConfig, bool> _predicate;
        private readonly SendOneWayRequestHandler _handler;

        internal Setup(
            Func<GatewayContext, SendOneWayRequestConfig, bool> predicate,
            SendOneWayRequestHandler handler)
        {
            _predicate = predicate;
            _handler = handler;
        }

        public void WithCallback(Action<GatewayContext, SendOneWayRequestConfig> callback) =>
            _handler.CallbackSetup.Add((_predicate, callback).ToTuple());
    }
}
