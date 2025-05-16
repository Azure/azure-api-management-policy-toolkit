// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Policies;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Document;

public static class MockSetMethodProvider
{
    public static Setup SetMethod(this MockPoliciesProvider<IInboundContext> mock) =>
        SetMethod(mock, (_, _) => true);

    public static Setup SetMethod(this MockPoliciesProvider<IOnErrorContext> mock) =>
        SetMethod(mock, (_, _) => true);

    public static Setup SetMethod(
        this MockPoliciesProvider<IInboundContext> mock,
        Func<GatewayContext, string, bool> predicate
    ) => SetMethod<IInboundContext>(mock, predicate);

    public static Setup SetMethod(
        this MockPoliciesProvider<IOnErrorContext> mock,
        Func<GatewayContext, string, bool> predicate
    ) => SetMethod<IOnErrorContext>(mock, predicate);

    private static Setup SetMethod<T>(
        MockPoliciesProvider<T> mock,
        Func<GatewayContext, string, bool> predicate
    ) where T : class
    {
        var handler = mock.SectionContextProxy.GetHandler<SetMethodHandler>();
        return new Setup(predicate, handler);
    }

    public class Setup
    {
        private readonly Func<GatewayContext, string, bool> _predicate;
        private readonly SetMethodHandler _handler;

        internal Setup(
            Func<GatewayContext, string, bool> predicate,
            SetMethodHandler handler)
        {
            _predicate = predicate;
            _handler = handler;
        }

        public void WithCallback(Action<GatewayContext, string> callback) =>
            _handler.CallbackSetup.Add((_predicate, callback).ToTuple());
    }
}