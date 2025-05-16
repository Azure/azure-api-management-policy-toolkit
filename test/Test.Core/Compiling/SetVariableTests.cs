// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling;

[TestClass]
public class SetVariableTests
{
    [TestMethod]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.SetVariable("Inbound", "setting");
            }
            public void Backend(IBackendContext context) {
                context.SetVariable("Backend", "setting");
            }
            public void Outbound(IOutboundContext context) {
                context.SetVariable("Outbound", "setting");
            }
            public void OnError(IOnErrorContext context) {
                context.SetVariable("OnError", "setting");
            }
        }
        """,
        """
        <policies>
            <inbound>
                <set-variable name="Inbound" value="setting" />
            </inbound>
            <backend>
                <set-variable name="Backend" value="setting" />
            </backend>
            <outbound>
                <set-variable name="Outbound" value="setting" />
            </outbound>
            <on-error>
                <set-variable name="OnError" value="setting" />
            </on-error>
        </policies>
        """,
        DisplayName = "Should compile set variable policy in sections"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.SetVariable("Inbound", Exp(context.ExpressionContext));
            }
            
            string Exp(IExpressionContext context)
                => context.RequestId.ToString();
        }
        """,
        """
        <policies>
            <inbound>
                <set-variable name="Inbound" value="@(context.RequestId.ToString())" />
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile set variable policy with one line expression"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.SetVariable("Inbound", Exp(context.ExpressionContext));
            }
            
            string Exp(IExpressionContext context) {
                return context.RequestId.ToString();
            }
        }
        """,
        """
        <policies>
            <inbound>
                <set-variable name="Inbound" value="@{return context.RequestId.ToString();}" />
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile set variable policy with multi line expression"
    )]
    public void ShouldCompileSetVariablePolicy(string code, string expectedXml)
    {
        code.CompileDocument().Should().BeSuccessful().And.DocumentEquivalentTo(expectedXml);
    }
}