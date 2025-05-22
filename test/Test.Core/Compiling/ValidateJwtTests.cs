// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling;

[TestClass]
public class ValidateJwtTests
{
    [TestMethod]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.ValidateJwt(new ValidateJwtConfig
                {
                    HeaderName = "Authorization",
                    RequireScheme = "Bearer",
                    IssuerSigningKeys = [new Base64KeyConfig { Value = "{{jwt-signing-key}}" }],
                    Audiences = ["jwt-audience"],
                    Issuers = ["http://contoso.example/"],
                });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <validate-jwt header-name="Authorization" require-scheme="Bearer">
                    <issuer-signing-keys>
                        <key>{{jwt-signing-key}}</key>
                    </issuer-signing-keys>
                    <audiences>
                        <audience>jwt-audience</audience>
                    </audiences>
                    <issuers>
                        <issuer>http://contoso.example/</issuer>
                    </issuers>
                </validate-jwt>
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile validate jwt policy in sections"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.ValidateJwt(new ValidateJwtConfig
                {
                    HeaderName = "Authorization",
                });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <validate-jwt header-name="Authorization" />
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile validate jwt policy with header name"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.ValidateJwt(new ValidateJwtConfig
                {
                    HeaderName = Exp(context.ExpressionContext),
                });
            }

            string Exp(IExpressionContext context) => "Author" + "ization";
        }
        """,
        """
        <policies>
            <inbound>
                <validate-jwt header-name="@("Author" + "ization")" />
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile validate jwt policy with expression in header name"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.ValidateJwt(new ValidateJwtConfig
                {
                    QueryParameterName = "auth",
                });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <validate-jwt query-parameter-name="auth" />
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile validate jwt policy with query parameter name"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.ValidateJwt(new ValidateJwtConfig
                {
                    QueryParameterName = Exp(context.ExpressionContext),
                });
            }

            string Exp(IExpressionContext context) => "au" + "th";
        }
        """,
        """
        <policies>
            <inbound>
                <validate-jwt query-parameter-name="@("au" + "th")" />
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile validate jwt policy with expression in query parameter name"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.ValidateJwt(new ValidateJwtConfig
                {
                    TokenValue = Exp(context.ExpressionContext),
                });
            }

            string Exp(IExpressionContext context) => context.Request.Headers["Authorization"];
        }
        """,
        """
        <policies>
            <inbound>
                <validate-jwt token-value="@(context.Request.Headers["Authorization"])" />
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile validate jwt policy with token value"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.ValidateJwt(new ValidateJwtConfig
                {
                    HeaderName = "Authorization",
                    FailedValidationHttpCode = 400,
                });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <validate-jwt header-name="Authorization" failed-validation-httpcode="400" />
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile validate jwt policy with failed validation http code"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.ValidateJwt(new ValidateJwtConfig
                {
                    HeaderName = "Authorization",
                    FailedValidationHttpCode = Exp(context.ExpressionContext),
                });
            }

            string Exp(IExpressionContext context) => 400 + 1;
        }
        """,
        """
        <policies>
            <inbound>
                <validate-jwt header-name="Authorization" failed-validation-httpcode="@(400 + 1)" />
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile validate jwt policy with expression in failed validation http code"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.ValidateJwt(new ValidateJwtConfig
                {
                    HeaderName = "Authorization",
                    FailedValidationErrorMessage = "Invalid token",
                });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <validate-jwt header-name="Authorization" failed-validation-error-message="Invalid token" />
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile validate jwt policy with failed validation error message"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.ValidateJwt(new ValidateJwtConfig
                {
                    HeaderName = "Authorization",
                    FailedValidationErrorMessage = Exp(context.ExpressionContext),
                });
            }

            string Exp(IExpressionContext context) => "Invalid" + " " + "token";
        }
        """,
        """
        <policies>
            <inbound>
                <validate-jwt header-name="Authorization" failed-validation-error-message="@("Invalid" + " " + "token")" />
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile validate jwt policy with expression in failed validation error message"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.ValidateJwt(new ValidateJwtConfig
                {
                    HeaderName = "Authorization",
                    RequireExpirationTime = false,
                });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <validate-jwt header-name="Authorization" require-expiration-time="false" />
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile validate jwt policy with require expiration time"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.ValidateJwt(new ValidateJwtConfig
                {
                    HeaderName = "Authorization",
                    RequireExpirationTime = Exp(context.ExpressionContext),
                });
            }

            string Exp(IExpressionContext context) => context.User.Email.EndsWith("@contoso.example");
        }
        """,
        """
        <policies>
            <inbound>
                <validate-jwt header-name="Authorization" require-expiration-time="@(context.User.Email.EndsWith("@contoso.example"))" />
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile validate jwt policy with expression in require expiration time"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.ValidateJwt(new ValidateJwtConfig
                {
                    HeaderName = "Authorization",
                    RequireScheme = "SharedAccessSignature",
                });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <validate-jwt header-name="Authorization" require-scheme="SharedAccessSignature" />
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile validate jwt policy with require scheme"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.ValidateJwt(new ValidateJwtConfig
                {
                    HeaderName = "Authorization",
                    RequireScheme = Exp(context.ExpressionContext),
                });
            }

            string Exp(IExpressionContext context) => "Shared" + "Access" + "Signature";
        }
        """,
        """
        <policies>
            <inbound>
                <validate-jwt header-name="Authorization" require-scheme="@("Shared" + "Access" + "Signature")" />
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile validate jwt policy with expression in require scheme"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.ValidateJwt(new ValidateJwtConfig
                {
                    HeaderName = "Authorization",
                    RequireSignedTokens = false,
                });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <validate-jwt header-name="Authorization" require-signed-tokens="false" />
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile validate jwt policy with require signed tokens"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.ValidateJwt(new ValidateJwtConfig
                {
                    HeaderName = "Authorization",
                    RequireSignedTokens = Exp(context.ExpressionContext),
                });
            }

            string Exp(IExpressionContext context) => !context.User.Email.EndsWith("@contoso.example");
        }
        """,
        """
        <policies>
            <inbound>
                <validate-jwt header-name="Authorization" require-signed-tokens="@(!context.User.Email.EndsWith("@contoso.example"))" />
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile validate jwt policy with expression in require signed tokens"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.ValidateJwt(new ValidateJwtConfig
                {
                    HeaderName = "Authorization",
                    ClockSkew = 10,
                });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <validate-jwt header-name="Authorization" clock-skew="10" />
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile validate jwt policy with clock skew"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.ValidateJwt(new ValidateJwtConfig
                {
                    HeaderName = "Authorization",
                    ClockSkew = Exp(context.ExpressionContext),
                });
            }

            string Exp(IExpressionContext context) => context.User.Email.EndsWith("@contoso.example") ? 10 : 0;
        }
        """,
        """
        <policies>
            <inbound>
                <validate-jwt header-name="Authorization" clock-skew="@(context.User.Email.EndsWith("@contoso.example") ? 10 : 0)" />
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile validate jwt policy with expression in clock skew"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.ValidateJwt(new ValidateJwtConfig
                {
                    HeaderName = "Authorization",
                    OutputTokenVariableName = "some-variable",
                });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <validate-jwt header-name="Authorization" output-token-variable-name="some-variable" />
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile validate jwt policy with output token variable name"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.ValidateJwt(new ValidateJwtConfig
                {
                    HeaderName = "Authorization",
                    OpenIdConfigs = [
                        new OpenIdConfig { Url = "https://login.constoso.com/openid-configuration" },
                        new OpenIdConfig { Url = "https://login.microsoftonline.com/organizations/v2.0/.well-known/openid-configuration" },
                    ],
                });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <validate-jwt header-name="Authorization">
                    <openid-config url="https://login.constoso.com/openid-configuration" />
                    <openid-config url="https://login.microsoftonline.com/organizations/v2.0/.well-known/openid-configuration" />
                </validate-jwt>
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile validate jwt policy with open id configuration"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.ValidateJwt(new ValidateJwtConfig
                {
                    HeaderName = "Authorization",
                    IssuerSigningKeys = [
                        new Base64KeyConfig { Id = "kid1", Value = "Base64Key" },
                        new CertificateKeyConfig { Id = "kid2", CertificateId = "certificate-id" },
                        new AsymmetricKeyConfig { Id = "kid3", Modulus = "modulus", Exponent = "exponent" },
                    ],
                });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <validate-jwt header-name="Authorization">
                    <issuer-signing-keys>
                        <key id="kid1">Base64Key</key>
                        <key id="kid2" certificate-id="certificate-id" />
                        <key id="kid3" n="modulus" e="exponent" />
                    </issuer-signing-keys>
                </validate-jwt>
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile validate jwt policy with issuer signing keys"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.ValidateJwt(new ValidateJwtConfig
                {
                    HeaderName = "Authorization",
                    DescriptionKeys = [
                        new Base64KeyConfig { Id = "kid1", Value = "Base64Key" },
                        new CertificateKeyConfig { Id = "kid2", CertificateId = "certificate-id" },
                        new AsymmetricKeyConfig { Id = "kid3", Modulus = "modulus", Exponent = "exponent" },
                    ],
                });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <validate-jwt header-name="Authorization">
                    <decryption-keys>
                        <key id="kid1">Base64Key</key>
                        <key id="kid2" certificate-id="certificate-id" />
                        <key id="kid3" n="modulus" e="exponent" />
                    </decryption-keys>
                </validate-jwt>
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile validate jwt policy with decryption keys"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.ValidateJwt(new ValidateJwtConfig
                {
                    HeaderName = "Authorization",
                    Audiences = [
                        "audience A",
                        "audience B",
                        "audience C",
                    ],
                });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <validate-jwt header-name="Authorization">
                    <audiences>
                        <audience>audience A</audience>
                        <audience>audience B</audience>
                        <audience>audience C</audience>
                    </audiences>
                </validate-jwt>
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile validate jwt policy with audiences"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.ValidateJwt(new ValidateJwtConfig
                {
                    HeaderName = "Authorization",
                    RequiredClaims = [
                        new ClaimConfig { 
                            Name = "claimA",
                            Match = "all", 
                            Separator = " ",
                            Values = ["value A", "value B"]
                        },
                        new ClaimConfig {
                            Name = "claimB",
                            Match = Exp(context.ExpressionContext),
                            Separator = " ",
                            Values = ["value A", "value B"]
                        },
                    ],
                });
            }
            
            string Exp(IExpressionContext context) => context.User.Email.EndsWith("@contoso.example") ? "all" : "any";
        }
        """,
        """
        <policies>
            <inbound>
                <validate-jwt header-name="Authorization">
                    <required-claims>
                        <claim name="claimA" match="all" separator=" ">
                            <value>value A</value>
                            <value>value B</value>
                        </claim>
                        <claim name="claimB" match="@(context.User.Email.EndsWith("@contoso.example") ? "all" : "any")" separator=" ">
                            <value>value A</value>
                            <value>value B</value>
                        </claim>
                    </required-claims>
                </validate-jwt>
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile validate jwt policy with required claims"
    )]
    public void ShouldCompileValidateJwtPolicy(string code, string expectedXml)
    {
        code.CompileDocument().Should().BeSuccessful().And.DocumentEquivalentTo(expectedXml);
    }
}