// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Policies;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Document;

public static class MockSendServiceBusMessageProvider
{
    public static Setup SendServiceBusMessage<T>(this MockPoliciesProvider<T> mock) where T : class =>
        SendServiceBusMessage(mock, (_, _) => true);

    public static Setup SendServiceBusMessage<T>(
        this MockPoliciesProvider<T> mock,
        Func<GatewayContext, SendServiceBusMessageConfig, bool> predicate
    ) where T : class
    {
        var handler = mock.SectionContextProxy.GetHandler<SendServiceBusMessageHandler>();
        return new Setup(predicate, handler);
    }

    public class Setup
    {
        private readonly Func<GatewayContext, SendServiceBusMessageConfig, bool> _predicate;
        private readonly SendServiceBusMessageHandler _handler;

        internal Setup(
            Func<GatewayContext, SendServiceBusMessageConfig, bool> predicate,
            SendServiceBusMessageHandler handler)
        {
            _predicate = predicate;
            _handler = handler;
        }

        public void WithCallback(Action<GatewayContext, SendServiceBusMessageConfig> callback) =>
            _handler.CallbackSetup.Add((_predicate, callback).ToTuple());
    }
}
