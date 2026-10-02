// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Policies;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Document;

public static class MockSetQueryParameterProvider
{
    public static Setup SetQueryParameter(this MockPoliciesProvider<IInboundContext> mock) =>
        SetQueryParameter(mock, (_, _, _) => true);

    public static Setup SetQueryParameter(this MockPoliciesProvider<IBackendContext> mock) =>
        SetQueryParameter(mock, (_, _, _) => true);

    public static Setup SetQueryParameter(this MockPoliciesProvider<IOutboundContext> mock) =>
        SetQueryParameter(mock, (_, _, _) => true);

    public static Setup SetQueryParameter(this MockPoliciesProvider<IOnErrorContext> mock) =>
        SetQueryParameter(mock, (_, _, _) => true);

    public static Setup SetQueryParameter(
        this MockPoliciesProvider<IInboundContext> mock,
        Func<GatewayContext, string, string[], bool> predicate
    ) => SetQueryParameter<IInboundContext>(mock, predicate);

    public static Setup SetQueryParameter(
        this MockPoliciesProvider<IBackendContext> mock,
        Func<GatewayContext, string, string[], bool> predicate
    ) => SetQueryParameter<IBackendContext>(mock, predicate);

    public static Setup SetQueryParameter(
        this MockPoliciesProvider<IOutboundContext> mock,
        Func<GatewayContext, string, string[], bool> predicate
    ) => SetQueryParameter<IOutboundContext>(mock, predicate);

    public static Setup SetQueryParameter(
        this MockPoliciesProvider<IOnErrorContext> mock,
        Func<GatewayContext, string, string[], bool> predicate
    ) => SetQueryParameter<IOnErrorContext>(mock, predicate);

    private static Setup SetQueryParameter<TContext>(
        MockPoliciesProvider<TContext> mock,
        Func<GatewayContext, string, string[], bool> predicate
    )
        where TContext : class
    {
        var handler = mock.SectionContextProxy.GetHandler<SetQueryParameterHandler>();
        return new Setup(predicate, handler);
    }

    public class Setup
    {
        private readonly Func<GatewayContext, string, string[], bool> _predicate;
        private readonly SetQueryParameterHandler _handler;

        internal Setup(
            Func<GatewayContext, string, string[], bool> predicate,
            SetQueryParameterHandler handler)
        {
            _predicate = predicate;
            _handler = handler;
        }

        public void WithCallback(Action<GatewayContext, string, string[]> callback) =>
            _handler.CallbackSetup.Add((_predicate, callback).ToTuple());
    }
}