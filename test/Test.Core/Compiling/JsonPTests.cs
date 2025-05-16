// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling;

[TestClass]
public class JsonPTests
{
    [TestMethod]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Outbound(IOutboundContext context) {
                context.JsonP("cb");
            }
        }
        """,
        """
        <policies>
            <outbound>
                <jsonp callback-parameter-name="cb" />
            </outbound>
        </policies>
        """,
        DisplayName = "Should compile jsonp policy"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Outbound(IOutboundContext context) {
                context.JsonP(Exp(context.ExpressionContext));
            }
            public string Exp(IExpressionContext context) => context.User.Name;
        }
        """,
        """
        <policies>
            <outbound>
                <jsonp callback-parameter-name="@(context.User.Name)" />
            </outbound>
        </policies>
        """,
        DisplayName = "Should compile jsonp policy with expression"
    )]
    public void ShouldCompileJsonPPolicy(string code, string expectedXml)
    {
        code.CompileDocument().Should().BeSuccessful().And.DocumentEquivalentTo(expectedXml);
    }
}