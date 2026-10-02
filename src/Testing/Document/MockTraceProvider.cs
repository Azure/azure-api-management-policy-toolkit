// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Policies;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Document;

public static class MockTraceProvider
{
    public static Setup Trace<T>(this MockPoliciesProvider<T> mock) where T : class =>
        Trace(mock, (_, _) => true);

    public static Setup Trace<T>(
        this MockPoliciesProvider<T> mock,
        Func<GatewayContext, TraceConfig, bool> predicate
    ) where T : class
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