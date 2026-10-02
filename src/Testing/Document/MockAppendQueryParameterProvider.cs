// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Policies;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Document;

public static class MockAppendQueryParameterProvider
{
    public static Setup AppendQueryParameter(this MockPoliciesProvider<IInboundContext> mock) =>
        AppendQueryParameter(mock, (_, _, _) => true);

    public static Setup AppendQueryParameter(this MockPoliciesProvider<IBackendContext> mock) =>
        AppendQueryParameter(mock, (_, _, _) => true);

    public static Setup AppendQueryParameter(this MockPoliciesProvider<IOutboundContext> mock) =>
        AppendQueryParameter(mock, (_, _, _) => true);

    public static Setup AppendQueryParameter(this MockPoliciesProvider<IOnErrorContext> mock) =>
        AppendQueryParameter(mock, (_, _, _) => true);

    public static Setup AppendQueryParameter(
        this MockPoliciesProvider<IInboundContext> mock,
        Func<GatewayContext, string, string[], bool> predicate
    ) => AppendQueryParameter<IInboundContext>(mock, predicate);

    public static Setup AppendQueryParameter(
        this MockPoliciesProvider<IBackendContext> mock,
        Func<GatewayContext, string, string[], bool> predicate
    ) => AppendQueryParameter<IBackendContext>(mock, predicate);

    public static Setup AppendQueryParameter(
        this MockPoliciesProvider<IOutboundContext> mock,
        Func<GatewayContext, string, string[], bool> predicate
    ) => AppendQueryParameter<IOutboundContext>(mock, predicate);

    public static Setup AppendQueryParameter(
        this MockPoliciesProvider<IOnErrorContext> mock,
        Func<GatewayContext, string, string[], bool> predicate
    ) => AppendQueryParameter<IOnErrorContext>(mock, predicate);

    private static Setup AppendQueryParameter<TContext>(
        MockPoliciesProvider<TContext> mock,
        Func<GatewayContext, string, string[], bool> predicate
    )
        where TContext : class
    {
        var handler = mock.SectionContextProxy.GetHandler<AppendQueryParameterHandler>();
        return new Setup(predicate, handler);
    }

    public class Setup
    {
        private readonly Func<GatewayContext, string, string[], bool> _predicate;
        private readonly AppendQueryParameterHandler _handler;

        internal Setup(
            Func<GatewayContext, string, string[], bool> predicate,
            AppendQueryParameterHandler handler)
        {
            _predicate = predicate;
            _handler = handler;
        }

        public void WithCallback(Action<GatewayContext, string, string[]> callback) =>
            _handler.CallbackSetup.Add((_predicate, callback).ToTuple());
    }
}