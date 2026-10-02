// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring.Expressions;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Document;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator;

namespace Test.Emulator.Emulator.Policies;

[TestClass]
public class AuthenticationCertificateTests
{
    class ConfiguredCertificate(CertificateAuthenticationConfig config) : IDocument
    {
        public void Inbound(IInboundContext context)
        {
            context.AuthenticationCertificate(config);
            context.SetVariable("continued", true);
        }
    }

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

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void AuthenticationCertificate_MissingStoredCertificateFailsWithoutReplacingExistingCertificate(bool byId)
    {
        using var existing = CreateTestCertificate();
        var config = byId
            ? new CertificateAuthenticationConfig { CertificateId = "missing-certificate" }
            : new CertificateAuthenticationConfig { Thumbprint = "missing-thumbprint" };
        var test = new ConfiguredCertificate(config).AsTestDocument();
        test.Context.Request.Certificate = existing;

        var error = Assert.ThrowsExactly<PolicyException>(() => test.RunInbound());

        error.Policy.Should().Be(nameof(IInboundContext.AuthenticationCertificate));
        error.Section.Should().Be(nameof(IInboundContext));
        error.InnerException.Should().BeOfType<InvalidOperationException>();
        test.Context.Request.Certificate.Should().BeSameAs(existing);
        test.Context.Variables.Should().NotContainKey("continued");
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void AuthenticationCertificate_RequiresCertificateSource(bool passwordOnly)
    {
        var config = new CertificateAuthenticationConfig { Password = passwordOnly ? "password" : null };
        var test = new ConfiguredCertificate(config).AsTestDocument();

        var error = Assert.ThrowsExactly<PolicyException>(() => test.RunInbound());

        error.InnerException.Should().BeOfType<InvalidOperationException>();
        test.Context.Request.Certificate.Should().BeNull();
        test.Context.Variables.Should().NotContainKey("continued");
    }

    [TestMethod]
    public void AuthenticationCertificate_WhitespaceIsNotCertificateSource()
    {
        var test = new ConfiguredCertificate(new CertificateAuthenticationConfig
        {
            CertificateId = " ", Thumbprint = " "
        }).AsTestDocument();

        var error = Assert.ThrowsExactly<PolicyException>(() => test.RunInbound());

        error.InnerException.Should().BeOfType<InvalidOperationException>();
        test.Context.Variables.Should().NotContainKey("continued");
    }

    [TestMethod]
    [DataRow("id-thumbprint")]
    [DataRow("id-body")]
    [DataRow("thumbprint-body")]
    public void AuthenticationCertificate_RejectsMultipleCertificateSources(string sources)
    {
        using var certificate = CreateTestCertificate();
        var config = sources switch
        {
            "id-thumbprint" => new CertificateAuthenticationConfig { CertificateId = "id", Thumbprint = "thumbprint" },
            "id-body" => new CertificateAuthenticationConfig { CertificateId = "id", Body = [1, 2, 3] },
            _ => new CertificateAuthenticationConfig { Thumbprint = "thumbprint", Body = [1, 2, 3] }
        };
        var test = new ConfiguredCertificate(config).AsTestDocument();
        test.SetupCertificateStore().WithCertificateById("id", certificate);
        test.SetupCertificateStore().WithCertificateByThumbprint("thumbprint", certificate);

        var error = Assert.ThrowsExactly<PolicyException>(() => test.RunInbound());

        error.InnerException.Should().BeOfType<InvalidOperationException>();
        test.Context.Request.Certificate.Should().BeNull();
        test.Context.Variables.Should().NotContainKey("continued");
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void AuthenticationCertificate_RejectsInvalidCertificateBody(bool empty)
    {
        var test = new ConfiguredCertificate(new CertificateAuthenticationConfig
        {
            Body = empty ? [] : [1, 2, 3], Password = "password"
        }).AsTestDocument();

        var error = Assert.ThrowsExactly<PolicyException>(() => test.RunInbound());

        error.InnerException.Should().BeAssignableTo<CryptographicException>();
        test.Context.Request.Certificate.Should().BeNull();
        test.Context.Variables.Should().NotContainKey("continued");
    }

    [TestMethod]
    public void AuthenticationCertificate_RejectsIncorrectPassword()
    {
        using var certificate = CreateTestCertificate();
        var test = new ConfiguredCertificate(new CertificateAuthenticationConfig
        {
            Body = certificate.Export(X509ContentType.Pfx, "correct-password"),
            Password = "incorrect-password"
        }).AsTestDocument();

        var error = Assert.ThrowsExactly<PolicyException>(() => test.RunInbound());

        error.InnerException.Should().BeAssignableTo<CryptographicException>();
        test.Context.Request.Certificate.Should().BeNull();
        test.Context.Variables.Should().NotContainKey("continued");
    }

    [TestMethod]
    public void AuthenticationCertificate_UnmatchedMockDoesNotConcealMissingCertificate()
    {
        using var certificate = CreateTestCertificate();
        var test = new ByIdCertificate().AsTestDocument();
        test.SetupInbound().AuthenticationCertificate((_, config) => config.CertificateId == "other")
            .WithCertificate(certificate);

        var error = Assert.ThrowsExactly<PolicyException>(() => test.RunInbound());

        error.InnerException.Should().BeOfType<InvalidOperationException>();
        test.Context.Request.Certificate.Should().BeNull();
    }

    [TestMethod]
    public void AuthenticationCertificate_CallbackFailureIsReported()
    {
        var test = new ByIdCertificate().AsTestDocument();
        var failure = new CryptographicException("mock certificate failure");
        test.SetupInbound().AuthenticationCertificate().WithCallback((_, _) => throw failure);

        var error = Assert.ThrowsExactly<PolicyException>(() => test.RunInbound());

        error.InnerException.Should().BeSameAs(failure);
        test.Context.Request.Certificate.Should().BeNull();
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