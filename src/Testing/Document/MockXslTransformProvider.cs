// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Policies;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Document;

public static class MockXslTransformProvider
{
    public static Setup XslTransform(this MockPoliciesProvider<IInboundContext> mock) =>
        XslTransform(mock, (_, _) => true);

    public static Setup XslTransform(this MockPoliciesProvider<IOutboundContext> mock) =>
        XslTransform(mock, (_, _) => true);

    public static Setup XslTransform(this MockPoliciesProvider<IOnErrorContext> mock) =>
        XslTransform(mock, (_, _) => true);

    public static Setup XslTransform(
        this MockPoliciesProvider<IInboundContext> mock,
        Func<GatewayContext, XslTransformConfig, bool> predicate
    ) => XslTransform<IInboundContext, XslTransformRequestHandler>(mock, predicate);

    public static Setup XslTransform(
        this MockPoliciesProvider<IOutboundContext> mock,
        Func<GatewayContext, XslTransformConfig, bool> predicate
    ) => XslTransform<IOutboundContext, XslTransformResponseHandler>(mock, predicate);

    public static Setup XslTransform(
        this MockPoliciesProvider<IOnErrorContext> mock,
        Func<GatewayContext, XslTransformConfig, bool> predicate
    ) => XslTransform<IOnErrorContext, XslTransformResponseHandler>(mock, predicate);

    private static Setup XslTransform<TContext, THandler>(
        MockPoliciesProvider<TContext> mock,
        Func<GatewayContext, XslTransformConfig, bool> predicate
    )
        where TContext : class
        where THandler : XslTransformHandler
    {
        var handler = mock.SectionContextProxy.GetHandler<THandler>();
        return new Setup(predicate, handler);
    }

    public class Setup
    {
        private readonly Func<GatewayContext, XslTransformConfig, bool> _predicate;
        private readonly XslTransformHandler _handler;

        internal Setup(
            Func<GatewayContext, XslTransformConfig, bool> predicate,
            XslTransformHandler handler)
        {
            _predicate = predicate;
            _handler = handler;
        }

        public void WithCallback(Action<GatewayContext, XslTransformConfig> callback) =>
            _handler.CallbackSetup.Add((_predicate, callback).ToTuple());
    }
}
