// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Security.Cryptography.X509Certificates;

using Azure.ApiManagement.PolicyToolkit.Authoring;
using Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Policies;

namespace Azure.ApiManagement.PolicyToolkit.Testing.Document;

public static class MockAuthenticationCertificateProvider
{
    public static Setup AuthenticationCertificate(this MockPoliciesProvider<IInboundContext> mock) =>
        AuthenticationCertificate(mock, (_, _) => true);

    public static Setup AuthenticationCertificate(
        this MockPoliciesProvider<IInboundContext> mock,
        Func<GatewayContext, CertificateAuthenticationConfig, bool> predicate)
    {
        var handler = mock.SectionContextProxy.GetHandler<AuthenticationCertificateHandler>();
        return new Setup(predicate, handler);
    }

    public class Setup
    {
        private readonly Func<GatewayContext, CertificateAuthenticationConfig, bool> _predicate;
        private readonly AuthenticationCertificateHandler _handler;

        internal Setup(
            Func<GatewayContext, CertificateAuthenticationConfig, bool> predicate,
            AuthenticationCertificateHandler handler)
        {
            _predicate = predicate;
            _handler = handler;
        }

        public void WithCallback(Action<GatewayContext, CertificateAuthenticationConfig> callback) =>
            _handler.CallbackSetup.Add((_predicate, callback).ToTuple());

        public void WithCertificate(X509Certificate2 certificate) =>
            _handler.CertificateSetup.Add((_predicate, certificate).ToTuple());
    }
}