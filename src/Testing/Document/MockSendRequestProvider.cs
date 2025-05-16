// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Policies;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Document;

public static class MockSendRequestProvider
{
    public static Setup SendRequest<T>(this MockPoliciesProvider<T> mock) where T : class =>
        SendRequest(mock, (_, _) => true);

    public static Setup SendRequest<T>(
        this MockPoliciesProvider<T> mock,
        Func<GatewayContext, SendRequestConfig, bool> predicate
    ) where T : class
    {
        var handler = mock.SectionContextProxy.GetHandler<SendRequestHandler>();
        return new Setup(predicate, handler);
    }

    public class Setup
    {
        private readonly Func<GatewayContext, SendRequestConfig, bool> _predicate;
        private readonly SendRequestHandler _handler;

        internal Setup(
            Func<GatewayContext, SendRequestConfig, bool> predicate,
            SendRequestHandler handler)
        {
            _predicate = predicate;
            _handler = handler;
        }

        public void WithCallback(Action<GatewayContext, SendRequestConfig> callback) =>
            _handler.CallbackSetup.Add((_predicate, callback).ToTuple());
    }
}