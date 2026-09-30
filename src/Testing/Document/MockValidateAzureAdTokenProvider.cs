// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Policies;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Document;

public static class MockValidateAzureAdTokenProvider
{
    public static Setup ValidateAzureAdToken(this MockPoliciesProvider<IInboundContext> mock) =>
        ValidateAzureAdToken(mock, (_, _) => true);

    public static Setup ValidateAzureAdToken(
        this MockPoliciesProvider<IInboundContext> mock,
        Func<GatewayContext, ValidateAzureAdTokenConfig, bool> predicate)
    {
        var handler = mock.SectionContextProxy.GetHandler<ValidateAzureAdTokenHandler>();
        return new Setup(predicate, handler);
    }

    public class Setup
    {
        private readonly Func<GatewayContext, ValidateAzureAdTokenConfig, bool> _predicate;
        private readonly ValidateAzureAdTokenHandler _handler;

        internal Setup(
            Func<GatewayContext, ValidateAzureAdTokenConfig, bool> predicate,
            ValidateAzureAdTokenHandler handler)
        {
            _predicate = predicate;
            _handler = handler;
        }

        public void WithCallback(Action<GatewayContext, ValidateAzureAdTokenConfig> callback) =>
            _handler.CallbackSetup.Add((_predicate, callback).ToTuple());
    }
}
