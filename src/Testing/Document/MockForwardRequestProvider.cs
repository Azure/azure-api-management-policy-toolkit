// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Policies;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Document;

public static class MockForwardRequestProvider
{
    public static Setup ForwardRequest(this MockPoliciesProvider<IBackendContext> mock) =>
        ForwardRequest(mock, (_, _) => true);

    public static Setup ForwardRequest<T>(
        this MockPoliciesProvider<T> mock,
        Func<GatewayContext, ForwardRequestConfig?, bool> predicate
    ) where T : class
    {
        var handler = mock.SectionContextProxy.GetHandler<ForwardRequestHandler>();
        return new Setup(predicate, handler);
    }

    public class Setup
    {
        private readonly Func<GatewayContext, ForwardRequestConfig?, bool> _predicate;
        private readonly ForwardRequestHandler _handler;

        internal Setup(
            Func<GatewayContext, ForwardRequestConfig?, bool> predicate,
            ForwardRequestHandler handler)
        {
            _predicate = predicate;
            _handler = handler;
        }

        public void WithCallback(Action<GatewayContext, ForwardRequestConfig?> callback) =>
            _handler.CallbackSetup.Add((_predicate, callback).ToTuple());
    }
}