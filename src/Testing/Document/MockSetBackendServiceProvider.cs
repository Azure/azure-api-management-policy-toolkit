// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Policies;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Document;

public static class MockSetBackendServiceProvider
{
    public static Setup SetBackendService(this MockPoliciesProvider<IInboundContext> mock) =>
        SetBackendService(mock, (_, _) => true);

    public static Setup SetBackendService(this MockPoliciesProvider<IBackendContext> mock) =>
        SetBackendService(mock, (_, _) => true);

    public static Setup SetBackendService(
        this MockPoliciesProvider<IInboundContext> mock,
        Func<GatewayContext, SetBackendServiceConfig, bool> predicate
    ) => SetBackendService<IInboundContext>(mock, predicate);

    public static Setup SetBackendService(
        this MockPoliciesProvider<IBackendContext> mock,
        Func<GatewayContext, SetBackendServiceConfig, bool> predicate
    ) => SetBackendService<IBackendContext>(mock, predicate);

    private static Setup SetBackendService<T>(
        MockPoliciesProvider<T> mock,
        Func<GatewayContext, SetBackendServiceConfig, bool> predicate
    ) where T : class
    {
        var handler = mock.SectionContextProxy.GetHandler<SetBackendServiceHandler>();
        return new Setup(predicate, handler);
    }

    public class Setup
    {
        private readonly Func<GatewayContext, SetBackendServiceConfig, bool> _predicate;
        private readonly SetBackendServiceHandler _handler;

        internal Setup(
            Func<GatewayContext, SetBackendServiceConfig, bool> predicate,
            SetBackendServiceHandler handler)
        {
            _predicate = predicate;
            _handler = handler;
        }

        public void WithCallback(Action<GatewayContext, SetBackendServiceConfig> callback) =>
            _handler.CallbackSetup.Add((_predicate, callback).ToTuple());
    }
}