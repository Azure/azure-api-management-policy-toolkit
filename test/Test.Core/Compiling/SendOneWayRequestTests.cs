// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling;

[TestClass]
public class SendOneWayRequestTests
{
    [TestMethod]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context)
            {
                context.SendOneWayRequest(new SendOneWayRequestConfig {
                    Mode = "copy"
                });
            }
            public void Backend(IBackendContext context)
            {
                context.SendOneWayRequest(new SendOneWayRequestConfig {
                    Mode = "copy"
                });
            }
            public void Outbound(IOutboundContext context)
            {
                context.SendOneWayRequest(new SendOneWayRequestConfig {
                    Mode = "copy"
                });
            }
            public void OnError(IOnErrorContext context)
            {
                context.SendOneWayRequest(new SendOneWayRequestConfig {
                    Mode = "copy"
                });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <send-one-way-request mode="copy" />
            </inbound>
            <backend>
                <send-one-way-request mode="copy" />
            </backend>
            <outbound>
                <send-one-way-request mode="copy" />
            </outbound>
            <on-error>
                <send-one-way-request mode="copy" />
            </on-error>
        </policies>
        """,
        DisplayName = "Should compile send one way request policy in sections"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context)
            {
                context.SendOneWayRequest(new SendOneWayRequestConfig {
                    Mode = Exp(context.ExpressionContext),
                });
            }

            private string Exp(IExpressionContext context) => "n" + "e" + "w";
        }
        """,
        """
        <policies>
            <inbound>
                <send-one-way-request mode="@("n" + "e" + "w")" />
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile send one way request policy with expression in mode"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context)
            {
                context.SendOneWayRequest(new SendOneWayRequestConfig {
                    Timeout = 100,
                });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <send-one-way-request timeout="100" />
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile send one way request policy with timeout"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context)
            {
                context.SendOneWayRequest(new SendOneWayRequestConfig {
                    Timeout = Exp(context.ExpressionContext),
                });
            }

            private int Exp(IExpressionContext context) => 80 + 20;
        }
        """,
        """
        <policies>
            <inbound>
                <send-one-way-request timeout="@(80 + 20)" />
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile send one way request policy with expression in timeout"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context)
            {
                context.SendOneWayRequest(new SendOneWayRequestConfig {
                    Url = "https://test.example",
                });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <send-one-way-request>
                    <set-url>https://test.example</set-url>
                </send-one-way-request>
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile send one way request policy with url"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context)
            {
                context.SendOneWayRequest(new SendOneWayRequestConfig {
                    Method = "POST",
                });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <send-one-way-request>
                    <set-method>POST</set-method>
                </send-one-way-request>
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile send one way request policy with method"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context)
            {
                context.SendOneWayRequest(new SendOneWayRequestConfig {
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
                <send-one-way-request>
                    <set-header name="content-type" exists-action="append">
                        <value>plain/text</value>
                    </set-header>
                    <set-header name="accept" exists-action="override">
                        <value>application/json</value>
                        <value>application/xml</value>
                    </set-header>
                </send-one-way-request>
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile send one way request policy with headers"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context)
            {
                context.SendOneWayRequest(new SendOneWayRequestConfig {
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
                <send-one-way-request>
                    <set-header name="content-type" exists-action="append">
                        <value>plain/text</value>
                    </set-header>
                    <set-header name="accept" exists-action="@("over" + "ride")">
                        <value>@("application" + "/" + "json")</value>
                        <value>application/xml</value>
                    </set-header>
                </send-one-way-request>
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile send one way request policy with expressions in headers"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context)
            {
                context.SendOneWayRequest(new SendOneWayRequestConfig {
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
                <send-one-way-request>
                    <set-body template="liquid" xsi-nil="blank" parse-date="false">body</set-body>
                </send-one-way-request>
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile send one way request policy with body"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context)
            {
                context.SendOneWayRequest(new SendOneWayRequestConfig {
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
                <send-one-way-request>
                    <set-body>@("bo" + "dy")</set-body>
                </send-one-way-request>
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile send one way request policy with expression in body"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context)
            {
                context.SendOneWayRequest(new SendOneWayRequestConfig {
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
                <send-one-way-request>
                    <authentication-certificate certificate-id="example-domain-cert" />
                </send-one-way-request>
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile send one way request policy with certificate authentication"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context)
            {
                context.SendOneWayRequest(new SendOneWayRequestConfig {
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
                <send-one-way-request>
                    <authentication-managed-identity resource="test.example/resource" client-id="example-client-id" />
                </send-one-way-request>
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile send one way request policy with managed identity authentication"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context)
            {
                context.SendOneWayRequest(new SendOneWayRequestConfig {
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
                <send-one-way-request>
                    <proxy url="proxy.example" username="test-user" password="pass" />
                </send-one-way-request>
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile send one way request policy with proxy"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context)
            {
                context.SendOneWayRequest(new SendOneWayRequestConfig {
                    Mode = "new",
                    Timeout = 100,
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
                <send-one-way-request mode="new" timeout="100">
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
                </send-one-way-request>
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile send one way request policy"
    )]
    public void ShouldCompileSendOneWayRequestPolicy(string code, string expectedXml)
    {
        code.CompileDocument().Should().BeSuccessful().And.DocumentEquivalentTo(expectedXml);
    }
}