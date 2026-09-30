// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Policies;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Document;

public static class MockRemoveQueryParameterProvider
{
    public static Setup RemoveQueryParameter(this MockPoliciesProvider<IInboundContext> mock) =>
        RemoveQueryParameter(mock, (_, _) => true);

    public static Setup RemoveQueryParameter(this MockPoliciesProvider<IBackendContext> mock) =>
        RemoveQueryParameter(mock, (_, _) => true);

    public static Setup RemoveQueryParameter(this MockPoliciesProvider<IOutboundContext> mock) =>
        RemoveQueryParameter(mock, (_, _) => true);

    public static Setup RemoveQueryParameter(this MockPoliciesProvider<IOnErrorContext> mock) =>
        RemoveQueryParameter(mock, (_, _) => true);

    public static Setup RemoveQueryParameter(
        this MockPoliciesProvider<IInboundContext> mock,
        Func<GatewayContext, string, bool> predicate
    ) => RemoveQueryParameter<IInboundContext>(mock, predicate);

    public static Setup RemoveQueryParameter(
        this MockPoliciesProvider<IBackendContext> mock,
        Func<GatewayContext, string, bool> predicate
    ) => RemoveQueryParameter<IBackendContext>(mock, predicate);

    public static Setup RemoveQueryParameter(
        this MockPoliciesProvider<IOutboundContext> mock,
        Func<GatewayContext, string, bool> predicate
    ) => RemoveQueryParameter<IOutboundContext>(mock, predicate);

    public static Setup RemoveQueryParameter(
        this MockPoliciesProvider<IOnErrorContext> mock,
        Func<GatewayContext, string, bool> predicate
    ) => RemoveQueryParameter<IOnErrorContext>(mock, predicate);

    private static Setup RemoveQueryParameter<TContext>(
        MockPoliciesProvider<TContext> mock,
        Func<GatewayContext, string, bool> predicate
    )
        where TContext : class
    {
        var handler = mock.SectionContextProxy.GetHandler<RemoveQueryParameterHandler>();
        return new Setup(predicate, handler);
    }

    public class Setup
    {
        private readonly Func<GatewayContext, string, bool> _predicate;
        private readonly RemoveQueryParameterHandler _handler;

        internal Setup(
            Func<GatewayContext, string, bool> predicate,
            RemoveQueryParameterHandler handler)
        {
            _predicate = predicate;
            _handler = handler;
        }

        public void WithCallback(Action<GatewayContext, string> callback) =>
            _handler.CallbackSetup.Add((_predicate, callback).ToTuple());
    }
}