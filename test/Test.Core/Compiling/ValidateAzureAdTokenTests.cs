// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.ApiManagement.PolicyToolkit.Compiling;

[TestClass]
public class ValidateAzureAdTokenTests
{
    [TestMethod]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.ValidateAzureAdToken(new ValidateAzureAdTokenConfig
                {
                    TenantId = "{{aad-tenant-id}}",
                });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <validate-azure-ad-token tenant-id="{{aad-tenant-id}}" />
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile validate-azure-ad-token policy in sections"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.ValidateAzureAdToken(new ValidateAzureAdTokenConfig
                {
                    TenantId = GetTenantId(context.ExpressionContext),
                });
            }
            string GetTenantId(IExpressionContext context) => context.Variables["aad-tenant-id"].ToString();
        }
        """,
        """
        <policies>
            <inbound>
                <validate-azure-ad-token tenant-id="@(context.Variables["aad-tenant-id"].ToString())" />
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile validate-azure-ad-token policy with expression in tenant-id"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.ValidateAzureAdToken(new ValidateAzureAdTokenConfig
                {
                    TenantId = "{{aad-tenant-id}}",
                    HeaderName = "Authorization"
                });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <validate-azure-ad-token tenant-id="{{aad-tenant-id}}" header-name="Authorization" />
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile validate-azure-ad-token policy with header-name"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.ValidateAzureAdToken(new ValidateAzureAdTokenConfig
                {
                    TenantId = "{{aad-tenant-id}}",
                    HeaderName = GetHeader(context.ExpressionContext)
                });
            }
        string GetHeader(IExpressionContext context) => context.Variables["header"].ToString();
        }
        """,
        """
        <policies>
            <inbound>
                <validate-azure-ad-token tenant-id="{{aad-tenant-id}}" header-name="@(context.Variables["header"].ToString())" />
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile validate-azure-ad-token policy with expression in header-name"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.ValidateAzureAdToken(new ValidateAzureAdTokenConfig
                {
                    TenantId = "{{aad-tenant-id}}",
                    QueryParameterName = "{{query-parameter-name}}"
                });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <validate-azure-ad-token tenant-id="{{aad-tenant-id}}" query-parameter-name="{{query-parameter-name}}" />
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile validate-azure-ad-token policy with query-parameter-name"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.ValidateAzureAdToken(new ValidateAzureAdTokenConfig
                {
                    TenantId = "{{aad-tenant-id}}",
                    QueryParameterName = GetQueryParameter(context.ExpressionContext)
                });
            }
            string GetQueryParameter(IExpressionContext context) => context.Variables["query"].ToString();
        }
        """,
        """
        <policies>
            <inbound>
                <validate-azure-ad-token tenant-id="{{aad-tenant-id}}" query-parameter-name="@(context.Variables["query"].ToString())" />
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile validate-azure-ad-token policy with expression in query-parameter-name"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.ValidateAzureAdToken(new ValidateAzureAdTokenConfig
                {
                    TenantId = "{{aad-tenant-id}}",
                    TokenValue = "{{token-value}}"
                });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <validate-azure-ad-token tenant-id="{{aad-tenant-id}}" token-value="{{token-value}}" />
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile validate-azure-ad-token policy with token-value"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.ValidateAzureAdToken(new ValidateAzureAdTokenConfig
                {
                    TenantId = "{{aad-tenant-id}}",
                    TokenValue = GetToken(context.ExpressionContext)
                });
            }
            string GetToken(IExpressionContext context) => context.Variables["token"].ToString();
        }
        """,
        """
        <policies>
            <inbound>
                <validate-azure-ad-token tenant-id="{{aad-tenant-id}}" token-value="@(context.Variables["token"].ToString())" />
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile validate-azure-ad-token policy with expression in token-value"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.ValidateAzureAdToken(new ValidateAzureAdTokenConfig
                {
                    TenantId = "{{aad-tenant-id}}",
                    FailedValidationHttpCode = 401
                });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <validate-azure-ad-token tenant-id="{{aad-tenant-id}}" failed-validation-httpcode="401" />
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile validate-azure-ad-token policy with failed-validation-httpcode"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.ValidateAzureAdToken(new ValidateAzureAdTokenConfig
                {
                    TenantId = "{{aad-tenant-id}}",
                    FailedValidationHttpCode = GetCode(context.ExpressionContext)
                });
            }
            int GetCode(IExpressionContext context) => (int)context.Variables["code"];
        }
        """,
        """
        <policies>
            <inbound>
                <validate-azure-ad-token tenant-id="{{aad-tenant-id}}" failed-validation-httpcode="@((int)context.Variables["code"])" />
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile validate-azure-ad-token policy with expression in failed-validation-httpcode"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.ValidateAzureAdToken(new ValidateAzureAdTokenConfig
                {
                    TenantId = "{{aad-tenant-id}}",
                    FailedValidationErrorMessage = "{{failed-validation-error-message}}"
                });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <validate-azure-ad-token tenant-id="{{aad-tenant-id}}" failed-validation-error-message="{{failed-validation-error-message}}" />
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile validate-azure-ad-token policy with failed-validation-error-message"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.ValidateAzureAdToken(new ValidateAzureAdTokenConfig
                {
                    TenantId = "{{aad-tenant-id}}",
                    FailedValidationErrorMessage = GetMessage(context.ExpressionContext)
                });
            }
            string GetMessage(IExpressionContext context) => context.Variables["code"].ToString();
        }
        """,
        """
        <policies>
            <inbound>
                <validate-azure-ad-token tenant-id="{{aad-tenant-id}}" failed-validation-error-message="@(context.Variables["code"].ToString())" />
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile validate-azure-ad-token policy with expression in failed-validation-error-message"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.ValidateAzureAdToken(new ValidateAzureAdTokenConfig
                {
                    TenantId = "{{aad-tenant-id}}",
                    OutputTokenVariableName = "variable-name"
                });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <validate-azure-ad-token tenant-id="{{aad-tenant-id}}" output-token-variable-name="variable-name" />
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile validate-azure-ad-token policy with output-token-variable-name"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.ValidateAzureAdToken(new ValidateAzureAdTokenConfig
                {
                    TenantId = "{{aad-tenant-id}}",
                    BackendApplicationIds = ["app-1", "app-2"]
                });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <validate-azure-ad-token tenant-id="{{aad-tenant-id}}">
                    <backend-application-ids>
                        <application-id>app-1</application-id>
                        <application-id>app-2</application-id>
                    </backend-application-ids>
                </validate-azure-ad-token>
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile validate-azure-ad-token policy with backend-application-ids"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.ValidateAzureAdToken(new ValidateAzureAdTokenConfig
                {
                    TenantId = "{{aad-tenant-id}}",
                    ClientApplicationIds = ["app-1", "app-2"]
                });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <validate-azure-ad-token tenant-id="{{aad-tenant-id}}">
                    <client-application-ids>
                        <application-id>app-1</application-id>
                        <application-id>app-2</application-id>
                    </client-application-ids>
                </validate-azure-ad-token>
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile validate-azure-ad-token policy with client-application-ids"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.ValidateAzureAdToken(new ValidateAzureAdTokenConfig
                {
                    TenantId = "{{aad-tenant-id}}",
                    Audiences = [
                        "audience-1",
                        GetAudience(context.ExpressionContext),
                        "audience-3"
                    ]
                });
            }
            string GetAudience(IExpressionContext context) => context.Variables["audience"].ToString();
        }
        """,
        """
        <policies>
            <inbound>
                <validate-azure-ad-token tenant-id="{{aad-tenant-id}}">
                    <audiences>
                        <audience>audience-1</audience>
                        <audience>@(context.Variables["audience"].ToString())</audience>
                        <audience>audience-3</audience>
                    </audiences>
                </validate-azure-ad-token>
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile validate-azure-ad-token policy with audiences"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.ValidateAzureAdToken(new ValidateAzureAdTokenConfig
                {
                    TenantId = "{{aad-tenant-id}}",
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
                <validate-azure-ad-token tenant-id="{{aad-tenant-id}}">
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
                </validate-azure-ad-token>
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile validate-azure-ad-token policy with required claims"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.ValidateAzureAdToken(new ValidateAzureAdTokenConfig
                {
                    TenantId = "{{aad-tenant-id}}",
                    DecryptionKeys = [
                        new DecryptionKey { CertificateId = "cert-1" },
                        new DecryptionKey { CertificateId = "cert-2" },
                    ]
                });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <validate-azure-ad-token tenant-id="{{aad-tenant-id}}">
                    <decryption-keys>
                        <key certificate-id="cert-1" />
                        <key certificate-id="cert-2" />
                    </decryption-keys>
                </validate-azure-ad-token>
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile validate-azure-ad-token policy with decryption-keys"
    )]
    public void ShouldCompileValidateAzureAdTokenPolicy(string code, string expectedXml)
    {
        code.CompileDocument().Should().BeSuccessful().And.DocumentEquivalentTo(expectedXml);
    }
}