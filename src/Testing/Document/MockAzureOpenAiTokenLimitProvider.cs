// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Policies;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Document;

/// <summary>Callback overrides for the inbound Azure OpenAI token-limit policy.</summary>
public static class MockAzureOpenAiTokenLimitProvider
{
    public static Setup AzureOpenAiTokenLimit(this MockPoliciesProvider<IInboundContext> mock) =>
        AzureOpenAiTokenLimit(mock, (_, _) => true);

    public static Setup AzureOpenAiTokenLimit(
        this MockPoliciesProvider<IInboundContext> mock,
        Func<GatewayContext, TokenLimitConfig, bool> predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        return new Setup(predicate, mock.SectionContextProxy.GetHandler<AzureOpenAiTokenLimitHandler>());
    }

    public sealed class Setup
    {
        private readonly Func<GatewayContext, TokenLimitConfig, bool> _predicate;
        private readonly AzureOpenAiTokenLimitHandler _handler;

        internal Setup(Func<GatewayContext, TokenLimitConfig, bool> predicate, AzureOpenAiTokenLimitHandler handler)
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