﻿// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.ApiManagement.PolicyToolkit.Compiling;

[TestClass]
public class SetMethodTests
{
    [TestMethod]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.SetMethod("GET");
            }
            public void OnError(IOutboundContext context) {
                context.SetMethod("POST");
            }
        }
        """,
        """
        <policies>
            <inbound>
                <set-method>GET</set-method>
            </inbound>
            <on-error>
                <set-method>POST</set-method>
            </on-error>
        </policies>
        """,
        DisplayName = "Should compile set method policy in sections"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.SetMethod(Exp(context.ExpressionContext));
            }
        
            public string Exp(IExpressionContext context) => "PO" + "ST";
        }
        """,
        """
        <policies>
            <inbound>
                <set-method>@("PO" + "ST")</set-method>
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile set method policy with expressions"
    )]
    public void ShouldCompileSetMethodPolicy(string code, string expectedXml)
    {
        code.CompileDocument().Should().BeSuccessful().And.DocumentEquivalentTo(expectedXml);
    }
}