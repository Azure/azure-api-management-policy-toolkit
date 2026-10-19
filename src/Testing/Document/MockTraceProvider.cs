// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.


using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Policies;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Document;

public static class MockTraceProvider
{
    public static Setup Trace(this MockPoliciesProvider<IInboundContext> mock) =>
        Trace(mock, (_, _) => true);

    public static Setup Trace(this MockPoliciesProvider<IOutboundContext> mock) =>
        Trace(mock, (_, _) => true);

    public static Setup Trace(this MockPoliciesProvider<IBackendContext> mock) =>
        Trace(mock, (_, _) => true);

    public static Setup Trace(
        this MockPoliciesProvider<IInboundContext> mock,
        Func<GatewayContext, TraceConfig, bool> predicate
    ) => Trace<IInboundContext>(mock, predicate);

    public static Setup Trace(
        this MockPoliciesProvider<IOutboundContext> mock,
        Func<GatewayContext, TraceConfig, bool> predicate
    ) => Trace<IOutboundContext>(mock, predicate);

    public static Setup Trace(
        this MockPoliciesProvider<IBackendContext> mock,
        Func<GatewayContext, TraceConfig, bool> predicate
    ) => Trace<IBackendContext>(mock, predicate);

    private static Setup Trace<TContext>(
        MockPoliciesProvider<TContext> mock,
        Func<GatewayContext, TraceConfig, bool> predicate
    ) where TContext : class
    {
        var handler = mock.SectionContextProxy.GetHandler<TraceHandler>();
        return new Setup(predicate, handler);
    }

    public class Setup
    {
        private readonly Func<GatewayContext, TraceConfig, bool> _predicate;
        private readonly TraceHandler _handler;

        internal Setup(
            Func<GatewayContext, TraceConfig, bool> predicate,
            TraceHandler handler)
        {
            _predicate = predicate;
            _handler = handler;
        }

        public void WithCallback(Action<GatewayContext, TraceConfig> callback) =>
            _handler.CallbackSetup.Add((_predicate, callback).ToTuple());
    }
}