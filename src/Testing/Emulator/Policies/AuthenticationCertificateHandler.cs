// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Policies;

[Section(nameof(IInboundContext))]
internal class AuthenticationCertificateHandler : PolicyHandler<CertificateAuthenticationConfig>
{
    public List<Tuple<
        Func<GatewayContext, CertificateAuthenticationConfig, bool>,
        X509Certificate2
    >> CertificateSetup { get; } = new();

    public override string PolicyName => nameof(IInboundContext.AuthenticationCertificate);

    protected override void Handle(GatewayContext context, CertificateAuthenticationConfig config)
    {
        var certificateFromCallback = CertificateSetup.Find(tuple => tuple.Item1(context, config))?.Item2;
        if (certificateFromCallback is not null)
        {
            context.Request.Certificate = certificateFromCallback;
            return;
        }

        var certificateStore = context.CertificateStore;
        var hasThumbprint = !string.IsNullOrWhiteSpace(config.Thumbprint);
        var hasCertificateId = !string.IsNullOrWhiteSpace(config.CertificateId);
        var sourceCount = (hasThumbprint ? 1 : 0) + (hasCertificateId ? 1 : 0) + (config.Body is not null ? 1 : 0);
        if (sourceCount > 1)
        {
            throw new InvalidOperationException("AuthenticationCertificate requires exactly one certificate source.");
        }

        if (hasThumbprint)
        {
            context.Request.Certificate = certificateStore.ByThumbprint.GetValueOrDefault(config.Thumbprint!)
                ?? throw new InvalidOperationException("The certificate with the configured thumbprint was not found.");
        }
        else if (hasCertificateId)
        {
            context.Request.Certificate = certificateStore.ById.GetValueOrDefault(config.CertificateId!)
                ?? throw new InvalidOperationException("The certificate with the configured resource identifier was not found.");
        }
        else if (config.Body is not null)
        {
            if (config.Body.Length == 0)
            {
                throw new CryptographicException("The client certificate body must not be empty.");
            }

            context.Request.Certificate = new X509Certificate2(config.Body, config.Password);
        }
        else
        {
            throw new InvalidOperationException(
                "AuthenticationCertificatePolicy doesn't have certificate source defined");
        }
    }
}