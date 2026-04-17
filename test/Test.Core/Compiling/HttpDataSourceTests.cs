// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling;

[TestClass]
public class HttpDataSourceTests
{
    [TestMethod]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) { }
            public void Backend(IBackendContext context)
            {
                context.HttpDataSource(new HttpDataSourceConfig
                {
                    Url = "https://example.com/api"
                });
            }
            public void Outbound(IOutboundContext context) { }
            public void OnError(IOnErrorContext context) { }
        }
        """,
        """
        <policies>
            <inbound />
            <backend>
                <http-data-source>
                    <http-request>
                        <set-url>https://example.com/api</set-url>
                    </http-request>
                </http-data-source>
            </backend>
            <outbound />
            <on-error />
        </policies>
        """,
        DisplayName = "Should compile http-data-source with URL only"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) { }
            public void Backend(IBackendContext context)
            {
                context.HttpDataSource(new HttpDataSourceConfig
                {
                    Url = "https://example.com/api",
                    Method = "POST",
                });
            }
            public void Outbound(IOutboundContext context) { }
            public void OnError(IOnErrorContext context) { }
        }
        """,
        """
        <policies>
            <inbound />
            <backend>
                <http-data-source>
                    <http-request>
                        <set-url>https://example.com/api</set-url>
                        <set-method>POST</set-method>
                    </http-request>
                </http-data-source>
            </backend>
            <outbound />
            <on-error />
        </policies>
        """,
        DisplayName = "Should compile http-data-source with method"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) { }
            public void Backend(IBackendContext context)
            {
                context.HttpDataSource(new HttpDataSourceConfig
                {
                    Url = "https://example.com/api",
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
            public void Outbound(IOutboundContext context) { }
            public void OnError(IOnErrorContext context) { }
        }
        """,
        """
        <policies>
            <inbound />
            <backend>
                <http-data-source>
                    <http-request>
                        <set-url>https://example.com/api</set-url>
                        <set-header name="content-type" exists-action="append">
                            <value>plain/text</value>
                        </set-header>
                        <set-header name="accept">
                            <value>application/json</value>
                            <value>application/xml</value>
                        </set-header>
                    </http-request>
                </http-data-source>
            </backend>
            <outbound />
            <on-error />
        </policies>
        """,
        DisplayName = "Should compile http-data-source with headers"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) { }
            public void Backend(IBackendContext context)
            {
                context.HttpDataSource(new HttpDataSourceConfig
                {
                    Url = "https://example.com/api",
                    Body = new BodyConfig {
                        Template = "liquid",
                        XsiNil = "blank",
                        ParseDate = false,
                        Content = "body-content",
                    },
                });
            }
            public void Outbound(IOutboundContext context) { }
            public void OnError(IOnErrorContext context) { }
        }
        """,
        """
        <policies>
            <inbound />
            <backend>
                <http-data-source>
                    <http-request>
                        <set-url>https://example.com/api</set-url>
                        <set-body template="liquid" xsi-nil="blank" parse-date="false">body-content</set-body>
                    </http-request>
                </http-data-source>
            </backend>
            <outbound />
            <on-error />
        </policies>
        """,
        DisplayName = "Should compile http-data-source with body"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) { }
            public void Backend(IBackendContext context)
            {
                context.HttpDataSource(new HttpDataSourceConfig
                {
                    Url = "https://example.com/api",
                    Body = new BodyConfig {
                        Content = Exp(context.ExpressionContext),
                    },
                });
            }
            public void Outbound(IOutboundContext context) { }
            public void OnError(IOnErrorContext context) { }
            private string Exp(IExpressionContext context) => "bo" + "dy";
        }
        """,
        """
        <policies>
            <inbound />
            <backend>
                <http-data-source>
                    <http-request>
                        <set-url>https://example.com/api</set-url>
                        <set-body>@("bo" + "dy")</set-body>
                    </http-request>
                </http-data-source>
            </backend>
            <outbound />
            <on-error />
        </policies>
        """,
        DisplayName = "Should compile http-data-source with expression in body"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) { }
            public void Backend(IBackendContext context)
            {
                context.HttpDataSource(new HttpDataSourceConfig
                {
                    Url = "https://example.com/api",
                    Authentication = new CertificateAuthenticationConfig {
                        CertificateId = "example-domain-cert",
                    },
                });
            }
            public void Outbound(IOutboundContext context) { }
            public void OnError(IOnErrorContext context) { }
        }
        """,
        """
        <policies>
            <inbound />
            <backend>
                <http-data-source>
                    <http-request>
                        <set-url>https://example.com/api</set-url>
                        <authentication-certificate certificate-id="example-domain-cert" />
                    </http-request>
                </http-data-source>
            </backend>
            <outbound />
            <on-error />
        </policies>
        """,
        DisplayName = "Should compile http-data-source with certificate authentication"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) { }
            public void Backend(IBackendContext context)
            {
                context.HttpDataSource(new HttpDataSourceConfig
                {
                    Url = "https://example.com/api",
                    Authentication = new ManagedIdentityAuthenticationConfig {
                        Resource = "https://resource.azure.com",
                        ClientId = "example-client-id",
                    },
                });
            }
            public void Outbound(IOutboundContext context) { }
            public void OnError(IOnErrorContext context) { }
        }
        """,
        """
        <policies>
            <inbound />
            <backend>
                <http-data-source>
                    <http-request>
                        <set-url>https://example.com/api</set-url>
                        <authentication-managed-identity resource="https://resource.azure.com" client-id="example-client-id" />
                    </http-request>
                </http-data-source>
            </backend>
            <outbound />
            <on-error />
        </policies>
        """,
        DisplayName = "Should compile http-data-source with managed identity authentication"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) { }
            public void Backend(IBackendContext context)
            {
                context.HttpDataSource(new HttpDataSourceConfig
                {
                    Url = "https://example.com/api",
                    Authentication = new BasicAuthenticationConfig {
                        Username = "test-user",
                        Password = "test-pass",
                    },
                });
            }
            public void Outbound(IOutboundContext context) { }
            public void OnError(IOnErrorContext context) { }
        }
        """,
        """
        <policies>
            <inbound />
            <backend>
                <http-data-source>
                    <http-request>
                        <set-url>https://example.com/api</set-url>
                        <authentication-basic username="test-user" password="test-pass" />
                    </http-request>
                </http-data-source>
            </backend>
            <outbound />
            <on-error />
        </policies>
        """,
        DisplayName = "Should compile http-data-source with basic authentication"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) { }
            public void Backend(IBackendContext context)
            {
                context.HttpDataSource(new HttpDataSourceConfig
                {
                    Url = "https://example.com/api",
                    ResponseHeaders = [
                        new HeaderConfig {
                            Name = "x-custom-header",
                            ExistsAction = "override",
                            Values = ["custom-value"],
                        },
                    ],
                });
            }
            public void Outbound(IOutboundContext context) { }
            public void OnError(IOnErrorContext context) { }
        }
        """,
        """
        <policies>
            <inbound />
            <backend>
                <http-data-source>
                    <http-request>
                        <set-url>https://example.com/api</set-url>
                    </http-request>
                    <http-response>
                        <set-header name="x-custom-header">
                            <value>custom-value</value>
                        </set-header>
                    </http-response>
                </http-data-source>
            </backend>
            <outbound />
            <on-error />
        </policies>
        """,
        DisplayName = "Should compile http-data-source with response headers"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) { }
            public void Backend(IBackendContext context)
            {
                context.HttpDataSource(new HttpDataSourceConfig
                {
                    Url = "https://example.com/api",
                    ResponseBody = new BodyConfig { Content = "response-body" },
                });
            }
            public void Outbound(IOutboundContext context) { }
            public void OnError(IOnErrorContext context) { }
        }
        """,
        """
        <policies>
            <inbound />
            <backend>
                <http-data-source>
                    <http-request>
                        <set-url>https://example.com/api</set-url>
                    </http-request>
                    <http-response>
                        <set-body>response-body</set-body>
                    </http-response>
                </http-data-source>
            </backend>
            <outbound />
            <on-error />
        </policies>
        """,
        DisplayName = "Should compile http-data-source with response body"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) { }
            public void Backend(IBackendContext context)
            {
                context.HttpDataSource(new HttpDataSourceConfig
                {
                    Url = "https://example.com/api",
                    ResponseHeaders = [
                        new HeaderConfig {
                            Name = "x-response-header",
                            Values = ["value1"],
                        },
                    ],
                    ResponseBody = new BodyConfig { Content = "transformed-response" },
                });
            }
            public void Outbound(IOutboundContext context) { }
            public void OnError(IOnErrorContext context) { }
        }
        """,
        """
        <policies>
            <inbound />
            <backend>
                <http-data-source>
                    <http-request>
                        <set-url>https://example.com/api</set-url>
                    </http-request>
                    <http-response>
                        <set-header name="x-response-header">
                            <value>value1</value>
                        </set-header>
                        <set-body>transformed-response</set-body>
                    </http-response>
                </http-data-source>
            </backend>
            <outbound />
            <on-error />
        </policies>
        """,
        DisplayName = "Should compile http-data-source with response headers and body"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) { }
            public void Backend(IBackendContext context)
            {
                context.HttpDataSource(new HttpDataSourceConfig
                {
                    Url = "https://example.com/api",
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
                        Content = "request-body",
                    },
                    Authentication = new CertificateAuthenticationConfig {
                        CertificateId = "example-domain-cert",
                    },
                    ResponseHeaders = [
                        new HeaderConfig {
                            Name = "x-custom",
                            ExistsAction = "override",
                            Values = ["custom-value"],
                        },
                    ],
                    ResponseBody = new BodyConfig {
                        Content = "response-body",
                    },
                });
            }
            public void Outbound(IOutboundContext context) { }
            public void OnError(IOnErrorContext context) { }
        }
        """,
        """
        <policies>
            <inbound />
            <backend>
                <http-data-source>
                    <http-request>
                        <set-url>https://example.com/api</set-url>
                        <set-method>POST</set-method>
                        <set-header name="content-type">
                            <value>plain/text</value>
                        </set-header>
                        <set-header name="accept">
                            <value>application/json</value>
                            <value>application/xml</value>
                        </set-header>
                        <set-body>request-body</set-body>
                        <authentication-certificate certificate-id="example-domain-cert" />
                    </http-request>
                    <http-response>
                        <set-header name="x-custom">
                            <value>custom-value</value>
                        </set-header>
                        <set-body>response-body</set-body>
                    </http-response>
                </http-data-source>
            </backend>
            <outbound />
            <on-error />
        </policies>
        """,
        DisplayName = "Should compile http-data-source with all options"
    )]
    public void HttpDataSource(string code, string expectedXml)
    {
        code.CompileDocument().Should().BeSuccessful().And.DocumentEquivalentTo(expectedXml);
    }
}
