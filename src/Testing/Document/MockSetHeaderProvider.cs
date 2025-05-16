// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Policies;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Document;

public static class MockSetHeaderProvider
{
    public static Setup SetHeader(this MockPoliciesProvider<IInboundContext> mock) =>
        SetHeader(mock, (_, _, _) => true);

    public static Setup SetHeader(this MockPoliciesProvider<IOutboundContext> mock) =>
        SetHeader(mock, (_, _, _) => true);

    public static Setup SetHeader(this MockPoliciesProvider<IOnErrorContext> mock) =>
        SetHeader(mock, (_, _, _) => true);

    public static Setup SetHeader(
        this MockPoliciesProvider<IInboundContext> mock,
        Func<GatewayContext, string, string[], bool> predicate
    ) => SetHeader<IInboundContext, SetHeaderRequestHandler>(mock, predicate);

    public static Setup SetHeader(
        this MockPoliciesProvider<IOutboundContext> mock,
        Func<GatewayContext, string, string[], bool> predicate
    ) => SetHeader<IOutboundContext, SetHeaderResponseHandler>(mock, predicate);

    public static Setup SetHeader(
        this MockPoliciesProvider<IOnErrorContext> mock,
        Func<GatewayContext, string, string[], bool> predicate
    ) => SetHeader<IOnErrorContext, SetHeaderResponseHandler>(mock, predicate);

    private static Setup SetHeader<TContext, THandler>(
        MockPoliciesProvider<TContext> mock,
        Func<GatewayContext, string, string[], bool> predicate
    )
        where TContext : class
        where THandler : SetHeaderHandler
    {
        var handler = mock.SectionContextProxy.GetHandler<THandler>();
        return new Setup(predicate, handler);
    }

    public class Setup
    {
        private readonly Func<GatewayContext, string, string[], bool> _predicate;
        private readonly SetHeaderHandler _handler;

        internal Setup(
            Func<GatewayContext, string, string[], bool> predicate,
            SetHeaderHandler handler)
        {
            _predicate = predicate;
            _handler = handler;
        }

        public void WithCallback(Action<GatewayContext, string, string[]> callback) =>
            _handler.CallbackSetup.Add((_predicate, callback).ToTuple());
    }
}