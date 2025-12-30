// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling;

[TestClass]
public class CorsTests
{
    [TestMethod]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.Cors(new CorsConfig()
                {
                    AllowedOrigins = ["contoso.com"],
                    AllowedHeaders = ["accept"],
                });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <cors>
                    <allowed-origins>
                        <origin>contoso.com</origin>
                    </allowed-origins>
                    <allowed-headers>
                        <header>accept</header>
                    </allowed-headers>
                </cors>
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile cors policy"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.Cors(new CorsConfig()
                {
                    AllowedOrigins = ["contoso.com", "fabrikam.com"],
                    AllowedHeaders = ["accept"],
                });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <cors>
                    <allowed-origins>
                        <origin>contoso.com</origin>
                        <origin>fabrikam.com</origin>
                    </allowed-origins>
                    <allowed-headers>
                        <header>accept</header>
                    </allowed-headers>
                </cors>
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile cors policy with multiple origins"
    )]
        [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.Cors(new CorsConfig()
                {
                    AllowedOrigins = [GetCorsOrigin(context.ExpressionContext)],
                    AllowedHeaders = ["accept"],
                });
            }
            string GetCorsOrigin(IExpressionContext context) => (string)context.Variables["CorsOrigin"];
        }
        """,
        """
        <policies>
            <inbound>
                <cors>
                    <allowed-origins>
                        <origin>@((string)context.Variables["CorsOrigin"])</origin>
                    </allowed-origins>
                    <allowed-headers>
                        <header>accept</header>
                    </allowed-headers>
                </cors>
            </inbound>
        </policies>
        """,
        DisplayName = "Should allow origin from an expression"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.Cors(new CorsConfig()
                {
                    AllowedOrigins = ["contoso.com"],
                    AllowedHeaders = ["accept", "content-type"],
                });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <cors>
                    <allowed-origins>
                        <origin>contoso.com</origin>
                    </allowed-origins>
                    <allowed-headers>
                        <header>accept</header>
                        <header>content-type</header>
                    </allowed-headers>
                </cors>
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile cors policy with multiple headers"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.Cors(new CorsConfig()
                {
                    AllowCredentials = true,
                    AllowedOrigins = ["contoso.com"],
                    AllowedHeaders = ["accept"],
                });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <cors allow-credentials="true">
                    <allowed-origins>
                        <origin>contoso.com</origin>
                    </allowed-origins>
                    <allowed-headers>
                        <header>accept</header>
                    </allowed-headers>
                </cors>
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile cors policy with allow credentials"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.Cors(new CorsConfig()
                {
                    AllowedOrigins = ["contoso.com"],
                    AllowedHeaders = ["accept"],
                    AllowedMethods = ["PUT", "DELETE"],
                });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <cors>
                    <allowed-origins>
                        <origin>contoso.com</origin>
                    </allowed-origins>
                    <allowed-headers>
                        <header>accept</header>
                    </allowed-headers>
                    <allowed-methods>
                        <method>PUT</method>
                        <method>DELETE</method>
                    </allowed-methods>
                </cors>
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile cors policy with allow methods"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.Cors(new CorsConfig()
                {
                    AllowedOrigins = ["contoso.com"],
                    AllowedHeaders = ["accept"],
                    AllowedMethods = ["PUT", "DELETE"],
                    PreflightResultMaxAge = 100,
                });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <cors>
                    <allowed-origins>
                        <origin>contoso.com</origin>
                    </allowed-origins>
                    <allowed-headers>
                        <header>accept</header>
                    </allowed-headers>
                    <allowed-methods preflight-result-max-age="100">
                        <method>PUT</method>
                        <method>DELETE</method>
                    </allowed-methods>
                </cors>
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile cors policy with allow methods and preflight result max age"
    )]
        [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.Cors(new CorsConfig()
                {
                    AllowedOrigins = ["contoso.com"],
                    AllowedHeaders = ["accept"],
                    AllowedMethods = ["PUT", "DELETE"],
                    PreflightResultMaxAge = 100,
                    PreflightResultMaxAge = GetPreflightResultMaxAge(context.ExpressionContext),
                });
            }
            int GetPreflightResultMaxAge(IExpressionContext context) => (int)context.Variables["PreflightResultMaxAge"];
        }
        """,
        """
        <policies>
            <inbound>
                <cors>
                    <allowed-origins>
                        <origin>contoso.com</origin>
                    </allowed-origins>
                    <allowed-headers>
                        <header>accept</header>
                    </allowed-headers>
                    <allowed-methods preflight-result-max-age="@((int)context.Variables["PreflightResultMaxAge"])">
                        <method>PUT</method>
                        <method>DELETE</method>
                    </allowed-methods>
                </cors>
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile cors policy with allow methods and preflight result max age from expression"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.Cors(new CorsConfig()
                {
                    AllowedOrigins = ["contoso.com"],
                    AllowedHeaders = ["accept"],
                    ExposeHeaders = ["accept", "content-type"],
                });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <cors>
                    <allowed-origins>
                        <origin>contoso.com</origin>
                    </allowed-origins>
                    <allowed-headers>
                        <header>accept</header>
                    </allowed-headers>
                    <expose-headers>
                        <header>accept</header>
                        <header>content-type</header>
                    </expose-headers>
                </cors>
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile cors policy with expose headers"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.Cors(new CorsConfig()
                {
                    AllowedOrigins = ["contoso.com"],
                    AllowedHeaders = ["accept"],
                    TerminateUnmatchedRequest = true,
                });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <cors terminate-unmatched-request="true">
                    <allowed-origins>
                        <origin>contoso.com</origin>
                    </allowed-origins>
                    <allowed-headers>
                        <header>accept</header>
                    </allowed-headers>
                </cors>
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile cors policy with terminate unmatched request explicitly enabled"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.Cors(new CorsConfig()
                {
                    AllowedOrigins = ["contoso.com"],
                    AllowedHeaders = ["accept"],
                    TerminateUnmatchedRequest = false,
                });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <cors terminate-unmatched-request="false">
                    <allowed-origins>
                        <origin>contoso.com</origin>
                    </allowed-origins>
                    <allowed-headers>
                        <header>accept</header>
                    </allowed-headers>
                </cors>
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile cors policy with terminate unmatched request disabled"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.Cors(new CorsConfig()
                {
                    AllowedOrigins = ["contoso.com"],
                    AllowedHeaders = ["accept"],
                    TerminateUnmatchedRequest = GetTerminateUnmatchedRequest(context.ExpressionContext),
                });
            }
           bool GetTerminateUnmatchedRequest(IExpressionContext context) => (bool)context.Variables["TerminateUnmatchedRequest"];
        }
        """,
        """
        <policies>
            <inbound>
                <cors terminate-unmatched-request="@((bool)context.Variables["TerminateUnmatchedRequest"])">
                    <allowed-origins>
                        <origin>contoso.com</origin>
                    </allowed-origins>
                    <allowed-headers>
                        <header>accept</header>
                    </allowed-headers>
                </cors>
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile cors policy with terminate unmatched request from expression"
    )]
    public void ShouldCompileCorsPolicy(string code, string expectedXml)
    {
        code.CompileDocument().Should().BeSuccessful().And.DocumentEquivalentTo(expectedXml);
    }
}