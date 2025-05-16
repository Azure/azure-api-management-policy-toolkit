// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling;

[TestClass]
public class SendRequestTests
{
    [TestMethod]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context)
            {
                context.SendRequest(new SendRequestConfig {
                    ResponseVariableName = "variable"
                });
            }
            public void Backend(IBackendContext context)
            {
                context.SendRequest(new SendRequestConfig {
                    ResponseVariableName = "variable"
                });
            }
            public void Outbound(IOutboundContext context)
            {
                context.SendRequest(new SendRequestConfig {
                    ResponseVariableName = "variable"
                });
            }
            public void OnError(IOnErrorContext context)
            {
                context.SendRequest(new SendRequestConfig {
                    ResponseVariableName = "variable"
                });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <send-request response-variable-name="variable" />
            </inbound>
            <backend>
                <send-request response-variable-name="variable" />
            </backend>
            <outbound>
                <send-request response-variable-name="variable" />
            </outbound>
            <on-error>
                <send-request response-variable-name="variable" />
            </on-error>
        </policies>
        """,
        DisplayName = "Should compile send request policy in sections"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context)
            {
                context.SendRequest(new SendRequestConfig {
                    ResponseVariableName = "variable",
                    Mode = "new",
                });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <send-request response-variable-name="variable" mode="new" />
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile send request policy with mode"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context)
            {
                context.SendRequest(new SendRequestConfig {
                    ResponseVariableName = "variable",
                    Mode = Exp(context.ExpressionContext),
                });
            }

            private string Exp(IExpressionContext context) => "n" + "e" + "w";
        }
        """,
        """
        <policies>
            <inbound>
                <send-request response-variable-name="variable" mode="@("n" + "e" + "w")" />
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile send request policy with expression in mode"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context)
            {
                context.SendRequest(new SendRequestConfig {
                    ResponseVariableName = "variable",
                    Timeout = 100,
                });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <send-request response-variable-name="variable" timeout="100" />
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile send request policy with timeout"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context)
            {
                context.SendRequest(new SendRequestConfig {
                    ResponseVariableName = "variable",
                    Timeout = Exp(context.ExpressionContext),
                });
            }

            private int Exp(IExpressionContext context) => 80 + 20;
        }
        """,
        """
        <policies>
            <inbound>
                <send-request response-variable-name="variable" timeout="@(80 + 20)" />
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile send request policy with expression in timeout"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context)
            {
                context.SendRequest(new SendRequestConfig {
                    ResponseVariableName = "variable",
                    IgnoreError = false,
                });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <send-request response-variable-name="variable" ignore-error="false" />
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile send request policy with ignore error"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context)
            {
                context.SendRequest(new SendRequestConfig {
                    ResponseVariableName = "variable",
                    Url = "https://test.example",
                });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <send-request response-variable-name="variable">
                    <set-url>https://test.example</set-url>
                </send-request>
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile send request policy with url"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context)
            {
                context.SendRequest(new SendRequestConfig {
                    ResponseVariableName = "variable",
                    Method = "POST",
                });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <send-request response-variable-name="variable">
                    <set-method>POST</set-method>
                </send-request>
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile send request policy with method"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context)
            {
                context.SendRequest(new SendRequestConfig {
                    ResponseVariableName = "variable",
                    Headers = [
                        new HeaderConfig {
                            Name = "content-type",
                            ExistsAction = "append",
                            Values = ["plain/text"],
                        },
                        new HeaderConfig {
                            Name = "accept",
                            ExistsAction = "override",
                            Values = ["application/json", "application/xml"],
                        },
                    ],
                });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <send-request response-variable-name="variable">
                    <set-header name="content-type" exists-action="append">
                        <value>plain/text</value>
                    </set-header>
                    <set-header name="accept" exists-action="override">
                        <value>application/json</value>
                        <value>application/xml</value>
                    </set-header>
                </send-request>
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile send request policy with headers"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context)
            {
                context.SendRequest(new SendRequestConfig {
                    ResponseVariableName = "variable",
                    Headers = [
                        new HeaderConfig {
                            Name = "content-type",
                            ExistsAction = "append",
                            Values = ["plain/text"],
                        },
                        new HeaderConfig {
                            Name = "accept",
                            ExistsAction = ExpAction(context.ExpressionContext),
                            Values = [ExpValue(context.ExpressionContext), "application/xml"],
                        },
                    ],
                });
            }

            private string ExpAction(IExpressionContext context) => "over" + "ride";
            private string ExpValue(IExpressionContext context) => "application" + "/" + "json";
        }
        """,
        """
        <policies>
            <inbound>
                <send-request response-variable-name="variable">
                    <set-header name="content-type" exists-action="append">
                        <value>plain/text</value>
                    </set-header>
                    <set-header name="accept" exists-action="@("over" + "ride")">
                        <value>@("application" + "/" + "json")</value>
                        <value>application/xml</value>
                    </set-header>
                </send-request>
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile send request policy with expressions in headers"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context)
            {
                context.SendRequest(new SendRequestConfig {
                    ResponseVariableName = "variable",
                    Body = new BodyConfig {
                        Template = "liquid",
                        XsiNil = "blank",
                        ParseDate = false,
                        Content = "body",
                    },
                });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <send-request response-variable-name="variable">
                    <set-body template="liquid" xsi-nil="blank" parse-date="false">body</set-body>
                </send-request>
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile send request policy with body"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context)
            {
                context.SendRequest(new SendRequestConfig {
                    ResponseVariableName = "variable",
                    Body = new BodyConfig {
                        Content = Exp(context.ExpressionContext),
                    },
                });
            }
            private string Exp(IExpressionContext context) => "bo" + "dy";
        }
        """,
        """
        <policies>
            <inbound>
                <send-request response-variable-name="variable">
                    <set-body>@("bo" + "dy")</set-body>
                </send-request>
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile send request policy with expression in body"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context)
            {
                context.SendRequest(new SendRequestConfig {
                    ResponseVariableName = "variable",
                    Authentication = new CertificateAuthenticationConfig {
                        CertificateId = "example-domain-cert",
                    },
                });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <send-request response-variable-name="variable">
                    <authentication-certificate certificate-id="example-domain-cert" />
                </send-request>
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile send request policy with certificate authentication"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context)
            {
                context.SendRequest(new SendRequestConfig {
                    ResponseVariableName = "variable",
                    Authentication = new ManagedIdentityAuthenticationConfig {
                        Resource = "test.example/resource",
                        ClientId = "example-client-id",
                    },
                });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <send-request response-variable-name="variable">
                    <authentication-managed-identity resource="test.example/resource" client-id="example-client-id" />
                </send-request>
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile send request policy with managed identity authentication"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context)
            {
                context.SendRequest(new SendRequestConfig {
                    ResponseVariableName = "variable",
                    Proxy = new ProxyConfig() {
                        Url = "proxy.example",
                        Username = "test-user",
                        Password = "pass",
                    },
                });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <send-request response-variable-name="variable">
                    <proxy url="proxy.example" username="test-user" password="pass" />
                </send-request>
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile send request policy with proxy"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context)
            {
                context.SendRequest(new SendRequestConfig {
                    ResponseVariableName = "variable",
                    Mode = "new",
                    Timeout = 100,
                    IgnoreError = false,
                    Url = "https://test.example",
                    Method = "POST",
                    Headers = [
                        new HeaderConfig {
                            Name = "content-type",
                            Values = ["plain/text"],
                        },
                        new HeaderConfig {
                            Name = "accept",
                            Values = ["application/json", "application/xml"],
                        },
                    ],
                    Body = new BodyConfig {
                        Content = "body",
                    },
                    Authentication = new CertificateAuthenticationConfig {
                        CertificateId = "example-domain-cert",
                    },
                });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <send-request response-variable-name="variable" mode="new" timeout="100" ignore-error="false">
                    <set-url>https://test.example</set-url>
                    <set-method>POST</set-method>
                    <set-header name="content-type">
                        <value>plain/text</value>
                    </set-header>
                    <set-header name="accept">
                        <value>application/json</value>
                        <value>application/xml</value>
                    </set-header>
                    <set-body>body</set-body>
                    <authentication-certificate certificate-id="example-domain-cert" />
                </send-request>
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile send request policy"
    )]
    public void ShouldCompileSendRequestPolicy(string code, string expectedXml)
    {
        code.CompileDocument().Should().BeSuccessful().And.DocumentEquivalentTo(expectedXml);
    }
}