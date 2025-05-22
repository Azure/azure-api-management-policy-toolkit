// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Policies;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Document;

public static class MockRemoveHeaderProvider
{
    public static Setup RemoveHeader(this MockPoliciesProvider<IInboundContext> mock) =>
        RemoveHeader(mock, (_, _) => true);

    public static Setup RemoveHeader(this MockPoliciesProvider<IOutboundContext> mock) =>
        RemoveHeader(mock, (_, _) => true);

    public static Setup RemoveHeader(this MockPoliciesProvider<IOnErrorContext> mock) =>
        RemoveHeader(mock, (_, _) => true);

    public static Setup RemoveHeader(
        this MockPoliciesProvider<IInboundContext> mock,
        Func<GatewayContext, string, bool> predicate
    ) => RemoveHeader<IInboundContext, RemoveHeaderRequestHandler>(mock, predicate);

    public static Setup RemoveHeader(
        this MockPoliciesProvider<IOutboundContext> mock,
        Func<GatewayContext, string, bool> predicate
    ) => RemoveHeader<IOutboundContext, RemoveHeaderRequestHandler>(mock, predicate);

    public static Setup RemoveHeader(this MockPoliciesProvider<IOnErrorContext> mock,
        Func<GatewayContext, string, bool> predicate
    ) => RemoveHeader<IOnErrorContext, RemoveHeaderRequestHandler>(mock, predicate);

    private static Setup RemoveHeader<TContext, THandler>(
        MockPoliciesProvider<TContext> mock,
        Func<GatewayContext, string, bool> predicate
    )
        where TContext : class
        where THandler : RemoveHeaderHandler
    {
        var handler = mock.SectionContextProxy.GetHandler<THandler>();
        return new Setup(predicate, handler);
    }

    public class Setup
    {
        private readonly Func<GatewayContext, string, bool> _predicate;
        private readonly RemoveHeaderHandler _handler;

        internal Setup(
            Func<GatewayContext, string, bool> predicate,
            RemoveHeaderHandler handler)
        {
            _predicate = predicate;
            _handler = handler;
        }

        public void WithCallback(Action<GatewayContext, string> callback) =>
            _handler.CallbackSetup.Add((_predicate, callback).ToTuple());
    }
}