// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling;

[TestClass]
public class AuthenticationCertificateTests
{
    [TestMethod]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) 
            { 
                context.AuthenticationCertificate(new CertificateAuthenticationConfig { CertificateId = "certId" });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <authentication-certificate certificate-id="certId" />
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile authentication-certificate policy with certificate id"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) 
            { 
                context.AuthenticationCertificate(new CertificateAuthenticationConfig { CertificateId = CertId(context.ExpressionContext) });
            }

            public string CertId(IExpressionContext context) => $"cert-{context.Api.Id}";
        }
        """,
        """
        <policies>
            <inbound>
                <authentication-certificate certificate-id="@($"cert-{context.Api.Id}")" />
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile authentication-certificate policy with expression in certificate id"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) 
            { 
                context.AuthenticationCertificate(new CertificateAuthenticationConfig { Thumbprint = "THUMBPRINT" });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <authentication-certificate thumbprint="THUMBPRINT" />
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile authentication-certificate policy with thumbprint"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) 
            { 
                context.AuthenticationCertificate(new CertificateAuthenticationConfig { Thumbprint = ThumbprintExp(context.ExpressionContext) });
            }
            public string ThumbprintExp(IExpressionContext context) => context.Variables.GetValueOrDefault<byte[]>("certThumbprint");
        }
        """,
        """
        <policies>
            <inbound>
                <authentication-certificate thumbprint="@(context.Variables.GetValueOrDefault<byte[]>("certThumbprint"))" />
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile authentication-certificate policy with expression in thumbprint"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) 
            { 
                context.AuthenticationCertificate(new CertificateAuthenticationConfig { Body = "BODY" });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <authentication-certificate body="BODY" />
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile authentication-certificate policy with body"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) 
            { 
                context.AuthenticationCertificate(new CertificateAuthenticationConfig { Body = BodyExp(context.ExpressionContext) });
            }
            public byte[] BodyExp(IExpressionContext context) => context.Variables.GetValueOrDefault<byte[]>("certBody")
        }
        """,
        """
        <policies>
            <inbound>
                <authentication-certificate body="@(context.Variables.GetValueOrDefault<byte[]>("certBody"))" />
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile authentication-certificate policy with expression in body"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) 
            { 
                context.AuthenticationCertificate(new CertificateAuthenticationConfig { Body = "BODY", Password = "PASSWORD" });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <authentication-certificate body="BODY" password="PASSWORD" />
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile authentication-certificate policy with body and password"
    )]
    public void ShouldCompileAuthenticationCertificatePolicy(string code, string expectedXml)
    {
        code.CompileDocument().Should().BeSuccessful().And.DocumentEquivalentTo(expectedXml);
    }
}