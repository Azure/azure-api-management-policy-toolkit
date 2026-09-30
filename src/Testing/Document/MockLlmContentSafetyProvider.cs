// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Policies;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Document;

public static class MockLlmContentSafetyProvider
{
    public static Setup LlmContentSafety(this MockPoliciesProvider<IInboundContext> mock) =>
        LlmContentSafety(mock, (_, _) => true);

    public static Setup LlmContentSafety(
        this MockPoliciesProvider<IInboundContext> mock,
        Func<GatewayContext, LlmContentSafetyConfig, bool> predicate)
    {
        var handler = mock.SectionContextProxy.GetHandler<LlmContentSafetyHandler>();
        return new Setup(predicate, handler);
    }

    public class Setup
    {
        private readonly Func<GatewayContext, LlmContentSafetyConfig, bool> _predicate;
        private readonly LlmContentSafetyHandler _handler;

        internal Setup(
            Func<GatewayContext, LlmContentSafetyConfig, bool> predicate,
            LlmContentSafetyHandler handler)
        {
            _predicate = predicate;
            _handler = handler;
        }

        public void WithCallback(Action<GatewayContext, LlmContentSafetyConfig> callback) =>
            _handler.CallbackSetup.Add((_predicate, callback).ToTuple());
    }
}
