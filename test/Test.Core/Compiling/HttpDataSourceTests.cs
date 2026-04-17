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
                    Body = new BodyConfig { Content = "{\"query\": \"test\"}" }
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
                        <set-body>{"query": "test"}</set-body>
                    </http-request>
                </http-data-source>
            </backend>
            <outbound />
            <on-error />
        </policies>
        """,
        DisplayName = "Should compile http-data-source with POST method and body"
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
                        new HeaderConfig
                        {
                            Name = "Content-Type",
                            Values = ["application/json"]
                        }
                    ],
                    Authentication = new ManagedIdentityAuthenticationConfig
                    {
                        Resource = "https://resource.azure.com"
                    }
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
                        <set-header name="Content-Type">
                            <value>application/json</value>
                        </set-header>
                        <authentication-managed-identity resource="https://resource.azure.com" />
                    </http-request>
                </http-data-source>
            </backend>
            <outbound />
            <on-error />
        </policies>
        """,
        DisplayName = "Should compile http-data-source with headers and managed identity auth"
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
                    ResponseBody = new BodyConfig { Content = "@(context.Response.Body.As(\"string\"))" }
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
                        <set-body>@(context.Response.Body.As("string"))</set-body>
                    </http-response>
                </http-data-source>
            </backend>
            <outbound />
            <on-error />
        </policies>
        """,
        DisplayName = "Should compile http-data-source with response body"
    )]
    public void HttpDataSource(string code, string expectedXml)
    {
        code.CompileDocument().Should().BeSuccessful().And.DocumentEquivalentTo(expectedXml);
    }
}
