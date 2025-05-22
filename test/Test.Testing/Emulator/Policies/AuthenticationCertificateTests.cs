// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring.Expressions;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Document;

namespace Test.Emulator.Emulator.Policies;

[TestClass]
public class AuthenticationCertificateTests
{
    class ByIdCertificate : IDocument
    {
        public void Inbound(IInboundContext context)
        {
            context.AuthenticationCertificate(new CertificateAuthenticationConfig { CertificateId = "abcdefgh" });
        }
    }

    class ByThumbprintCertificate : IDocument
    {
        public void Inbound(IInboundContext context)
        {
            context.AuthenticationCertificate(new CertificateAuthenticationConfig { Thumbprint = "abcdefgh" });
        }
    }

    class ByBodyCertificate : IDocument
    {
        public void Inbound(IInboundContext context)
        {
            context.AuthenticationCertificate(
                new CertificateAuthenticationConfig
                {
                    Body = GetCertBody(context.ExpressionContext), Password = "testPass"
                });
        }

        public byte[] GetCertBody(IExpressionContext context) =>
            context.Deployment.Certificates["someKey"].Export(X509ContentType.Pfx, "testPass");
    }

    [TestMethod]
    public void AuthenticationCertificate_Callback()
    {
        var test = new ByIdCertificate().AsTestDocument();
        var executedCallback = false;
        test.SetupInbound().AuthenticationCertificate().WithCallback((_, _) =>
        {
            executedCallback = true;
        });

        test.RunInbound();

        executedCallback.Should().BeTrue();
    }

    [TestMethod]
    public void AuthenticationCertificate_ReturnCertificate()
    {
        var certificate = CreateTestCertificate();
        var test = new ByIdCertificate().AsTestDocument();
        test.SetupInbound().AuthenticationCertificate().WithCertificate(certificate);

        test.RunInbound();

        test.Context.Request.Certificate.Should().Be(certificate);
    }

    [TestMethod]
    public void AuthenticationCertificate_SetupCertificateStore_WithCertificateByThumbprint()
    {
        var certificate = CreateTestCertificate();
        var test = new ByThumbprintCertificate().AsTestDocument();
        test.SetupCertificateStore().WithCertificateByThumbprint("abcdefgh", certificate);

        test.RunInbound();

        test.Context.Request.Certificate.Should().Be(certificate);
    }

    [TestMethod]
    public void AuthenticationCertificate_SetupCertificateStore_WithCertificateById()
    {
        var certificate = CreateTestCertificate();
        var test = new ByIdCertificate().AsTestDocument();
        test.SetupCertificateStore().WithCertificateById("abcdefgh", certificate);

        test.RunInbound();

        test.Context.Request.Certificate.Should().Be(certificate);
    }

    [TestMethod]
    public void AuthenticationCertificate_Body()
    {
        var certificate = CreateTestCertificate();
        var test = new ByBodyCertificate().AsTestDocument();
        test.Context.Deployment.Certificates.Add("someKey", certificate);

        test.RunInbound();

        test.Context.Request.Certificate.Should().Be(certificate);
    }

    public X509Certificate2 CreateTestCertificate()
    {
        using RSA rsa = RSA.Create(2048);
        var request = new CertificateRequest(
            "CN=MyCertificate",
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);

        // Add extensions
        request.CertificateExtensions.Add(
            new X509BasicConstraintsExtension(false, false, 0, false));
        request.CertificateExtensions.Add(
            new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature, false));
        request.CertificateExtensions.Add(
            new X509SubjectKeyIdentifierExtension(request.PublicKey, false));

        var certificate = request.CreateSelfSigned(
            DateTimeOffset.Now,
            DateTimeOffset.Now.AddYears(1));
        return certificate;
    }
}