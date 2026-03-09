// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling;

[TestClass]
public class InvokeRequestTests
{
    [TestMethod]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context)
            {
                context.InvokeRequest(new InvokeRequestConfig
                {
                    Url = "https://example.com/path"
                });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <invoke-request url="https://example.com/path" />
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile invoke-request policy with url"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context)
            {
                context.InvokeRequest(new InvokeRequestConfig
                {
                    Method = MethodExp(context.ExpressionContext),
                    Url = UrlExp(context.ExpressionContext),
                    BackendId = BackendIdExp(context.ExpressionContext),
                    ResponseVariableName = "invokeResponse",
                    Headers = new[]
                    {
                        new HeaderConfig
                        {
                            Name = "x-test",
                            Values = new[] { HeaderExp(context.ExpressionContext) }
                        }
                    },
                    Body = new BodyConfig
                    {
                        Content = BodyExp(context.ExpressionContext)
                    }
                });
            }

            string MethodExp(IExpressionContext context) => "POST";
            string UrlExp(IExpressionContext context) => context.Request.Url.Path;
            string BackendIdExp(IExpressionContext context) => "backend-a";
            string HeaderExp(IExpressionContext context) => context.Api.Id;
            string BodyExp(IExpressionContext context) => context.Request.Url.QueryString;
        }
        """,
        """
        <policies>
            <inbound>
                <invoke-request method="@("POST")" url="@(context.Request.Url.Path)" backend-id="@("backend-a")" response-variable-name="invokeResponse">
                    <header name="x-test" value="@(context.Api.Id)" />
                    <body>@(context.Request.Url.QueryString)</body>
                </invoke-request>
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile invoke-request policy with expressions and content"
    )]
    public void ShouldCompileInvokeRequestPolicy(string code, string expectedXml)
    {
        code.CompileDocument().Should().BeSuccessful().And.DocumentEquivalentTo(expectedXml);
    }
}
