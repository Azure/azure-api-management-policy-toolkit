// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Policies;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Document;

public static class MockValidateClientCertificateProvider
{
    public static Setup ValidateClientCertificate(this MockPoliciesProvider<IInboundContext> mock) =>
        ValidateClientCertificate(mock, (_, _) => true);

    public static Setup ValidateClientCertificate(
        this MockPoliciesProvider<IInboundContext> mock,
        Func<GatewayContext, ValidateClientCertificateConfig, bool> predicate)
    {
        var handler = mock.SectionContextProxy.GetHandler<ValidateClientCertificateHandler>();
        return new Setup(predicate, handler);
    }

    public class Setup
    {
        private readonly Func<GatewayContext, ValidateClientCertificateConfig, bool> _predicate;
        private readonly ValidateClientCertificateHandler _handler;

        internal Setup(
            Func<GatewayContext, ValidateClientCertificateConfig, bool> predicate,
            ValidateClientCertificateHandler handler)
        {
            _predicate = predicate;
            _handler = handler;
        }

        public void WithCallback(Action<GatewayContext, ValidateClientCertificateConfig> callback) =>
            _handler.CallbackSetup.Add((_predicate, callback).ToTuple());
    }
}
