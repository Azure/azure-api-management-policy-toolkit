// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Policies;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Document;

public static class MockSetBodyProvider
{
    public static Setup SetBody(this MockPoliciesProvider<IInboundContext> mock) =>
        SetBody(mock, (_, _, _) => true);

    public static Setup SetBody(this MockPoliciesProvider<IOutboundContext> mock) =>
        SetBody(mock, (_, _, _) => true);

    public static Setup SetBody(this MockPoliciesProvider<IOnErrorContext> mock) =>
        SetBody(mock, (_, _, _) => true);

    public static Setup SetBody(
        this MockPoliciesProvider<IInboundContext> mock,
        Func<GatewayContext, string, SetBodyConfig?, bool> predicate
    )
    {
        var handler = mock.SectionContextProxy.GetHandler<SetBodyRequestHandler>();
        return new Setup(predicate, handler);
    }

    public static Setup SetBody(
        this MockPoliciesProvider<IOutboundContext> mock,
        Func<GatewayContext, string, SetBodyConfig?, bool> predicate
    )
    {
        var handler = mock.SectionContextProxy.GetHandler<SetBodyResponseHandler>();
        return new Setup(predicate, handler);
    }

    public static Setup SetBody(
        this MockPoliciesProvider<IOnErrorContext> mock,
        Func<GatewayContext, string, SetBodyConfig?, bool> predicate
    )
    {
        var handler = mock.SectionContextProxy.GetHandler<SetBodyResponseHandler>();
        return new Setup(predicate, handler);
    }

    public class Setup
    {
        private readonly Func<GatewayContext, string, SetBodyConfig?, bool> _predicate;
        private readonly SetBodyHandler _handler;

        internal Setup(
            Func<GatewayContext, string, SetBodyConfig?, bool> predicate,
            SetBodyHandler handler)
        {
            _predicate = predicate;
            _handler = handler;
        }

        public void WithCallback(Action<GatewayContext, string, SetBodyConfig?> callback) =>
            _handler.CallbackHooks.Add((_predicate, callback).ToTuple());
    }
}