// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Policies;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Document;

public static class MockAppendHeaderProvider
{
    public static Setup AppendHeader(this MockPoliciesProvider<IInboundContext> mock) =>
        AppendHeader(mock, (_, _, _) => true);

    public static Setup AppendHeader(this MockPoliciesProvider<IOutboundContext> mock) =>
        AppendHeader(mock, (_, _, _) => true);

    public static Setup AppendHeader(this MockPoliciesProvider<IOnErrorContext> mock) =>
        AppendHeader(mock, (_, _, _) => true);

    public static Setup AppendHeader(
        this MockPoliciesProvider<IInboundContext> mock,
        Func<GatewayContext, string, string[], bool> predicate
    ) => AppendHeader<IInboundContext, AppendHeaderRequestHandler>(mock, predicate);

    public static Setup AppendHeader(
        this MockPoliciesProvider<IOutboundContext> mock,
        Func<GatewayContext, string, string[], bool> predicate
    ) => AppendHeader<IOutboundContext, AppendHeaderResponseHandler>(mock, predicate);

    public static Setup AppendHeader(this MockPoliciesProvider<IOnErrorContext> mock,
        Func<GatewayContext, string, string[], bool> predicate
    ) => AppendHeader<IOnErrorContext, AppendHeaderResponseHandler>(mock, predicate);

    private static Setup AppendHeader<TContext, THandler>(
        MockPoliciesProvider<TContext> mock,
        Func<GatewayContext, string, string[], bool> predicate
    )
        where TContext : class
        where THandler : AppendHeaderHandler
    {
        var handler = mock.SectionContextProxy.GetHandler<THandler>();
        return new Setup(predicate, handler);
    }

    public class Setup
    {
        private readonly Func<GatewayContext, string, string[], bool> _predicate;
        private readonly AppendHeaderHandler _handler;

        internal Setup(
            Func<GatewayContext, string, string[], bool> predicate,
            AppendHeaderHandler handler)
        {
            _predicate = predicate;
            _handler = handler;
        }

        public void WithCallback(Action<GatewayContext, string, string[]> callback) =>
            _handler.CallbackSetup.Add((_predicate, callback).ToTuple());
    }
}