// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

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

        if (!string.IsNullOrWhiteSpace(config.Thumbprint))
        {
            context.Request.Certificate = certificateStore.ByThumbprint.GetValueOrDefault(config.Thumbprint);
        }
        else if (!string.IsNullOrWhiteSpace(config.CertificateId))
        {
            context.Request.Certificate = certificateStore.ById.GetValueOrDefault(config.CertificateId);
        }
        else if (config.Body is not null)
        {
            context.Request.Certificate = new X509Certificate2(config.Body, config.Password);
        }
        else
        {
            throw new InvalidOperationException(
                "AuthenticationCertificatePolicy doesn't have certificate source defined");
        }
    }
}