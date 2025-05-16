// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling;

[TestClass]
public class ChooseTests
{
    [TestMethod]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                if(Exp(context.ExpressionContext))
                {
                    context.Base();
                }
            }
            public void Backend(IBackendContext context) {
                if(Exp(context.ExpressionContext))
                {
                    context.Base();
                }
            }
            public void Outbound(IOutboundContext context) {
                if(Exp(context.ExpressionContext))
                {
                    context.Base();
                }
            }
            public void OnError(IOnErrorContext context) {
                if(Exp(context.ExpressionContext))
                {
                    context.Base();
                }
            }
            
            bool Exp(IExpressionContext context) => 1 > 0;
        }
        """,
        """
        <policies>
            <inbound>
                <choose>
                    <when condition="@(1 > 0)">
                        <base />
                    </when>
                </choose>
            </inbound>
            <backend>
                <choose>
                    <when condition="@(1 > 0)">
                        <base />
                    </when>
                </choose>
            </backend>
            <outbound>
                <choose>
                    <when condition="@(1 > 0)">
                        <base />
                    </when>
                </choose>
            </outbound>
            <on-error>
                <choose>
                    <when condition="@(1 > 0)">
                        <base />
                    </when>
                </choose>
            </on-error>
        </policies>
        """,
        DisplayName = "Should compile choose in sections"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                if(Exp1(context.ExpressionContext))
                {
                    context.Base();
                }
                else if(Exp2(context.ExpressionContext))
                {
                    context.Base();
                }
            }

            bool Exp1(IExpressionContext context) => 1 > 0;
            bool Exp2(IExpressionContext context) => 1 != 0;
        }
        """,
        """
        <policies>
            <inbound>
                <choose>
                    <when condition="@(1 > 0)">
                        <base />
                    </when>
                    <when condition="@(1 != 0)">
                        <base />
                    </when>
                </choose>
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile choose with multiple cases"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                if(Exp(context.ExpressionContext))
                {
                    context.Base();
                }
                else
                {
                    context.Base();
                }
            }

            bool Exp(IExpressionContext context) => 1 > 0;
        }
        """,
        """
        <policies>
            <inbound>
                <choose>
                    <when condition="@(1 > 0)">
                        <base />
                    </when>
                    <otherwise>
                        <base />
                    </otherwise>
                </choose>
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile choose with otherwise case"
    )]
    public void ShouldCompileChoosePolicy(string code, string expectedXml)
    {
        code.CompileDocument().Should().BeSuccessful().And.DocumentEquivalentTo(expectedXml);
    }
}