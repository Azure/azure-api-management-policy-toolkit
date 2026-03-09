// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Policies;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Document;

public static class MockInvokeRequestProvider
{
    public static Setup InvokeRequest<T>(this MockPoliciesProvider<T> mock) where T : class =>
        InvokeRequest(mock, (_, _) => true);

    public static Setup InvokeRequest<T>(
        this MockPoliciesProvider<T> mock,
        Func<GatewayContext, InvokeRequestConfig, bool> predicate
    ) where T : class
    {
        var handler = mock.SectionContextProxy.GetHandler<InvokeRequestHandler>();
        return new Setup(predicate, handler);
    }

    public class Setup
    {
        private readonly Func<GatewayContext, InvokeRequestConfig, bool> _predicate;
        private readonly InvokeRequestHandler _handler;

        internal Setup(
            Func<GatewayContext, InvokeRequestConfig, bool> predicate,
            InvokeRequestHandler handler)
        {
            _predicate = predicate;
            _handler = handler;
        }

        public void WithCallback(Action<GatewayContext, InvokeRequestConfig> callback) =>
            _handler.CallbackHooks.Add((_predicate, callback).ToTuple());
    }
}
