// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling;

[TestClass]
public class RewriteUriTests
{
    [TestMethod]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.RewriteUri("/test/{id}/tset");
            }
        }
        """,
        """
        <policies>
            <inbound>
                <rewrite-uri template="/test/{id}/tset" />
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile rewrite uri policy"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.RewriteUri(Exp(context.ExpressionContext));
            }
            
            public string Exp(IExpressionContext context) => $"/test/{context.RequestId}/tset";
        }
        """,
        """
        <policies>
            <inbound>
                <rewrite-uri template="@($"/test/{context.RequestId}/tset")" />
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile rewrite uri policy with expressions in template"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.RewriteUri("/test/{id}/tset?param1={param1}", false);
            }
        }
        """,
        """
        <policies>
            <inbound>
                <rewrite-uri template="/test/{id}/tset?param1={param1}" copy-unmatched-params="false" />
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile rewrite uri policy with copy unmatched params"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.RewriteUri("/test/{id}/tset?param1={param1}", Exp(context.ExpressionContext));
            }

            public bool Exp(IExpressionContext context) => 0 > 1;
        }
        """,
        """
        <policies>
            <inbound>
                <rewrite-uri template="/test/{id}/tset?param1={param1}" copy-unmatched-params="@(0 > 1)" />
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile rewrite uri policy with expression in copy unmatched params"
    )]
    public void ShouldCompileRewriteUriPolicy(string code, string expectedXml)
    {
        code.CompileDocument().Should().BeSuccessful().And.DocumentEquivalentTo(expectedXml);
    }
}