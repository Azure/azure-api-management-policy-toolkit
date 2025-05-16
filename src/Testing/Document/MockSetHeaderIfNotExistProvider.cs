// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Policies;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Document;

public static class MockSetHeaderIfNotExistProvider
{
    public static Setup SetHeaderIfNotExist(this MockPoliciesProvider<IInboundContext> mock) =>
        SetHeaderIfNotExist(mock, (_, _, _) => true);

    public static Setup SetHeaderIfNotExist(this MockPoliciesProvider<IOutboundContext> mock) =>
        SetHeaderIfNotExist(mock, (_, _, _) => true);

    public static Setup SetHeaderIfNotExist(this MockPoliciesProvider<IOnErrorContext> mock) =>
        SetHeaderIfNotExist(mock, (_, _, _) => true);

    public static Setup SetHeaderIfNotExist(
        this MockPoliciesProvider<IInboundContext> mock,
        Func<GatewayContext, string, string[], bool> predicate
    ) => SetHeaderIfNotExist<IInboundContext, SetHeaderIfNotExistRequestHandler>(mock, predicate);

    public static Setup SetHeaderIfNotExist(
        this MockPoliciesProvider<IOutboundContext> mock,
        Func<GatewayContext, string, string[], bool> predicate
    ) => SetHeaderIfNotExist<IOutboundContext, SetHeaderIfNotExistResponseHandler>(mock, predicate);

    public static Setup SetHeaderIfNotExist(
        this MockPoliciesProvider<IOnErrorContext> mock,
        Func<GatewayContext, string, string[], bool> predicate
    ) => SetHeaderIfNotExist<IOnErrorContext, SetHeaderIfNotExistResponseHandler>(mock, predicate);

    private static Setup SetHeaderIfNotExist<TContext, THandler>(
        MockPoliciesProvider<TContext> mock,
        Func<GatewayContext, string, string[], bool> predicate
    )
        where TContext : class
        where THandler : SetHeaderIfNotExistHandler
    {
        var handler = mock.SectionContextProxy.GetHandler<THandler>();
        return new Setup(predicate, handler);
    }

    public class Setup
    {
        private readonly Func<GatewayContext, string, string[], bool> _predicate;
        private readonly SetHeaderIfNotExistHandler _handler;

        internal Setup(
            Func<GatewayContext, string, string[], bool> predicate,
            SetHeaderIfNotExistHandler handler)
        {
            _predicate = predicate;
            _handler = handler;
        }

        public void WithCallback(Action<GatewayContext, string, string[]> callback) =>
            _handler.CallbackSetup.Add((_predicate, callback).ToTuple());
    }
}