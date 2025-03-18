// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.ApiManagement.PolicyToolkit.Compiling;

[TestClass]
public class ProxyTests
{
    [TestMethod]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.Proxy(new ProxyConfig() {
                    Url = "proxy.example"
                });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <proxy url="proxy.example" />
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile proxy policy"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.Proxy(new ProxyConfig() {
                    Url = GetUrl(context.ExpressionContext)
                });
            }
            string GetUrl(IExpressionContext context) => context.Variables["url"].ToString();
        }
        """,
        """
        <policies>
            <inbound>
                <proxy url="@(context.Variables["url"].ToString())" />
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile proxy policy with expression in url"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.Proxy(new ProxyConfig() {
                    Url = "proxy.example",
                    Username = "{{username}}",
                    Password = "{{password}}"
                });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <proxy url="proxy.example" username="{{username}}" password="{{password}}" />
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile proxy policy with username and password"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.Proxy(new ProxyConfig() {
                    Url = "proxy.example",
                    Username = GetUsername(context.ExpressionContext),
                    Password = GetPassword(context.ExpressionContext)
                });
            }
            string GetUsername(IExpressionContext context) => context.Variables["username"].ToString();
            string GetPassword(IExpressionContext context) => context.Variables["password"].ToString();
        }
        """,
        """
        <policies>
            <inbound>
                <proxy url="proxy.example" username="@(context.Variables["username"].ToString())" password="@(context.Variables["password"].ToString())" />
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile proxy policy with expression in username and password"
    )]
    public void ShouldCompileProxyPolicy(string code, string expectedXml)
    {
        code.CompileDocument().Should().BeSuccessful().And.DocumentEquivalentTo(expectedXml);
    }
}