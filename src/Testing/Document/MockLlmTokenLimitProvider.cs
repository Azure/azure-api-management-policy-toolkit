// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Policies;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Document;

/// <summary>Callback overrides for the inbound LLM token-limit policy.</summary>
public static class MockLlmTokenLimitProvider
{
    public static Setup LlmTokenLimit(this MockPoliciesProvider<IInboundContext> mock) =>
        LlmTokenLimit(mock, (_, _) => true);

    public static Setup LlmTokenLimit(
        this MockPoliciesProvider<IInboundContext> mock,
        Func<GatewayContext, TokenLimitConfig, bool> predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        return new Setup(predicate, mock.SectionContextProxy.GetHandler<LlmTokenLimitHandler>());
    }

    public sealed class Setup
    {
        private readonly Func<GatewayContext, TokenLimitConfig, bool> _predicate;
        private readonly LlmTokenLimitHandler _handler;

        internal Setup(Func<GatewayContext, TokenLimitConfig, bool> predicate, LlmTokenLimitHandler handler)
        {
            _predicate = predicate;
            _handler = handler;
        }

        public void WithCallback(Action<GatewayContext, TokenLimitConfig> callback)
        {
            ArgumentNullException.ThrowIfNull(callback);
            _handler.CallbackSetup.Add((_predicate, callback).ToTuple());
        }
    }
}