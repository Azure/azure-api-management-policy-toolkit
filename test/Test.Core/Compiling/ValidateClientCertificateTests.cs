// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.ApiManagement.PolicyToolkit.Compiling;

[TestClass]
public class ValidateClientCertificateTests
{
    [TestMethod]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.ValidateClientCertificate(new ValidateClientCertificateConfig
                {
                    ValidateRevocation = true,
                });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <validate-client-certificate validate-revocation="true" />
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile validate-client-certificate policy with validate-revocation"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.ValidateClientCertificate(new ValidateClientCertificateConfig
                {
                    ValidateTrust = true,
                });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <validate-client-certificate validate-trust="true" />
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile validate-client-certificate policy with validate-trust"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.ValidateClientCertificate(new ValidateClientCertificateConfig
                {
                    ValidateNotBefore = true,
                });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <validate-client-certificate validate-not-before="true" />
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile validate-client-certificate policy with validate-not-before"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.ValidateClientCertificate(new ValidateClientCertificateConfig
                {
                    ValidateNotAfter = true,
                });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <validate-client-certificate validate-not-after="true" />
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile validate-client-certificate policy with validate-not-after"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.ValidateClientCertificate(new ValidateClientCertificateConfig
                {
                    IgnoreError = true,
                });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <validate-client-certificate ignore-error="true" />
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile validate-client-certificate policy with ignore-error"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.ValidateClientCertificate(new ValidateClientCertificateConfig
                {
                    Identities = [
                        new CertificateIdentity
                        {
                            Thumbprint = "1234567890ABCDEF1234567890ABCDEF12345678"
                        }
                    ],
                });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <validate-client-certificate>
                    <identities>
                        <identity thumbprint="1234567890ABCDEF1234567890ABCDEF12345678" />
                    </identities>
                </validate-client-certificate>
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile validate-client-certificate policy with thumbprint in identities"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.ValidateClientCertificate(new ValidateClientCertificateConfig
                {
                    Identities = [
                        new CertificateIdentity
                        {
                            SerialNumber = "1234567890"
                        }
                    ],
                });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <validate-client-certificate>
                    <identities>
                        <identity serial-number="1234567890" />
                    </identities>
                </validate-client-certificate>
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile validate-client-certificate policy with serial-number in identities"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.ValidateClientCertificate(new ValidateClientCertificateConfig
                {
                    Identities = [
                        new CertificateIdentity
                        {
                            CommonName = "some-name"
                        }
                    ],
                });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <validate-client-certificate>
                    <identities>
                        <identity common-name="some-name" />
                    </identities>
                </validate-client-certificate>
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile validate-client-certificate policy with common-name in identities"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.ValidateClientCertificate(new ValidateClientCertificateConfig
                {
                    Identities = [
                        new CertificateIdentity
                        {
                            Subject = "CN=MyName"
                        }
                    ],
                });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <validate-client-certificate>
                    <identities>
                        <identity subject="CN=MyName" />
                    </identities>
                </validate-client-certificate>
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile validate-client-certificate policy with subject in identities"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.ValidateClientCertificate(new ValidateClientCertificateConfig
                {
                    Identities = [
                        new CertificateIdentity
                        {
                            DnsName = "contoso.exmaple"
                        }
                    ],
                });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <validate-client-certificate>
                    <identities>
                        <identity dns-name="contoso.exmaple" />
                    </identities>
                </validate-client-certificate>
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile validate-client-certificate policy with dns-name in identities"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.ValidateClientCertificate(new ValidateClientCertificateConfig
                {
                    Identities = [
                        new CertificateIdentity
                        {
                            IssuerSubject = "CN=MyName"
                        }
                    ],
                });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <validate-client-certificate>
                    <identities>
                        <identity issuer-subject="CN=MyName" />
                    </identities>
                </validate-client-certificate>
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile validate-client-certificate policy with issuer-subject in identities"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.ValidateClientCertificate(new ValidateClientCertificateConfig
                {
                    Identities = [
                        new CertificateIdentity
                        {
                            IssuerThumbprint = "1234567890ABCDEF1234567890ABCDEF12345678"
                        }
                    ],
                });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <validate-client-certificate>
                    <identities>
                        <identity issuer-thumbprint="1234567890ABCDEF1234567890ABCDEF12345678" />
                    </identities>
                </validate-client-certificate>
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile validate-client-certificate policy with issuer-thumbprint in identities"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.ValidateClientCertificate(new ValidateClientCertificateConfig
                {
                    Identities = [
                        new CertificateIdentity
                        {
                            IssuerCertificateId = "cert-id-123"
                        }
                    ],
                });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <validate-client-certificate>
                    <identities>
                        <identity issuer-certificate-id="cert-id-123" />
                    </identities>
                </validate-client-certificate>
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile validate-client-certificate policy with issuer-certificate-id in identities"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.ValidateClientCertificate(new ValidateClientCertificateConfig
                {
                    Identities = [
                        new CertificateIdentity
                        {
                            Thumbprint = "1234567890ABCDEF1234567890ABCDEF12345678"
                        },
                        new CertificateIdentity
                        {
                            Subject = "CN=MyName"
                        },
                        new CertificateIdentity
                        {
                            IssuerThumbprint = "ABCDEF1234567890ABCDEF1234567890ABCDEF12"
                        }
                    ],
                });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <validate-client-certificate>
                    <identities>
                        <identity thumbprint="1234567890ABCDEF1234567890ABCDEF12345678" />
                        <identity subject="CN=MyName" />
                        <identity issuer-thumbprint="ABCDEF1234567890ABCDEF1234567890ABCDEF12" />
                    </identities>
                </validate-client-certificate>
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile validate-client-certificate policy with multiple identities"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.ValidateClientCertificate(new ValidateClientCertificateConfig
                {
                    ValidateRevocation = true,
                    ValidateTrust = true,
                    ValidateNotBefore = true,
                    ValidateNotAfter = true,
                    IgnoreError = false,
                    Identities = [
                        new CertificateIdentity
                        {
                            Subject = "C=US, ST=Illinois, L=Chicago, O=\"Contoso, Inc.\", CN=*.contoso.com",
                            IssuerSubject = "C=BE, O=FabrikamSign nv-sa, OU=Root CA, CN=FabrikamSign Root CA"
                        }
                    ]
                });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <validate-client-certificate validate-revocation="true" validate-trust="true" validate-not-before="true" validate-not-after="true" ignore-error="false">
                    <identities>
                        <identity subject="C=US, ST=Illinois, L=Chicago, O=&quot;Contoso, Inc.&quot;, CN=*.contoso.com" issuer-subject="C=BE, O=FabrikamSign nv-sa, OU=Root CA, CN=FabrikamSign Root CA" />
                    </identities>
                </validate-client-certificate>
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile validate-client-certificate policy"
    )]
    public void ShouldCompileValidateClientCertificatePolicy(string code, string expectedXml)
    {
        code.CompileDocument().Should().BeSuccessful().And.DocumentEquivalentTo(expectedXml);
    }
}