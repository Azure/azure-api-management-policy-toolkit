// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Policies;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Document;

public static class MockLlmEmitTokenMetricProvider
{
    public static Setup LlmEmitTokenMetric(
        this MockPoliciesProvider<IInboundContext> mock) => LlmEmitTokenMetric(mock, (_, _) => true);

    public static Setup LlmEmitTokenMetric(
        this MockPoliciesProvider<IInboundContext> mock,
        Func<GatewayContext, EmitTokenMetricConfig, bool> predicate)
    {
        var handler = mock.SectionContextProxy.GetHandler<LlmEmitTokenMetricHandler>();
        return new Setup(predicate, handler);
    }

    public class Setup
    {
        private readonly Func<GatewayContext, EmitTokenMetricConfig, bool> _predicate;
        private readonly LlmEmitTokenMetricHandler _handler;

        internal Setup(
            Func<GatewayContext, EmitTokenMetricConfig, bool> predicate,
            LlmEmitTokenMetricHandler handler)
        {
            _predicate = predicate;
            _handler = handler;
        }

        public void WithCallback(Action<GatewayContext, EmitTokenMetricConfig> callback) =>
            _handler.CallbackSetup.Add((_predicate, callback).ToTuple());
    }
}