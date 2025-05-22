// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Policies;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Document;

public static class MockAuthenticationManagedIdentityProvider
{
    public static Setup AuthenticationManagedIdentity(
        this MockPoliciesProvider<IInboundContext> mock) => AuthenticationManagedIdentity(mock, (_, _) => true);

    public static Setup AuthenticationManagedIdentity(
        this MockPoliciesProvider<IInboundContext> mock,
        Func<GatewayContext, ManagedIdentityAuthenticationConfig, bool> predicate)
    {
        var handler = mock.SectionContextProxy.GetHandler<AuthenticationManagedIdentityHandler>();
        return new Setup(predicate, handler);
    }

    public class Setup
    {
        private readonly Func<GatewayContext, ManagedIdentityAuthenticationConfig, bool> _predicate;
        private readonly AuthenticationManagedIdentityHandler _handler;

        internal Setup(
            Func<GatewayContext, ManagedIdentityAuthenticationConfig, bool> predicate,
            AuthenticationManagedIdentityHandler handler)
        {
            _predicate = predicate;
            _handler = handler;
        }

        public void WithCallback(Action<GatewayContext, ManagedIdentityAuthenticationConfig> callback) =>
            _handler.CallbackSetup.Add((_predicate, callback).ToTuple());

        public void WithTokenProviderHook(Func<string, string?, string> hook) =>
            _handler.ProvideTokenHooks.Add((_predicate, hook).ToTuple());

        public void ReturnsToken(string token) => this.WithTokenProviderHook((_, _) => token);

        public void WithError(string error) =>
            this.WithTokenProviderHook((_, _) => throw new HttpRequestException(error));
    }
}