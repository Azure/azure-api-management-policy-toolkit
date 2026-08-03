// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling;

[TestClass]
public class ConstFoldingRewriterTests
{
    [TestMethod]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.SetVariable("issuer", GetIssuer(context.ExpressionContext));
            }
            string GetIssuer(IExpressionContext context) => context.User.Id == MyConstants.Issuer ? "yes" : "no";
        }
        """,
        """
        public static class MyConstants
        {
            public const string Issuer = "https://my-issuer.example.com";
        }
        """,
        """
        <policies>
            <inbound>
                <set-variable name="issuer" value="@(context.User.Id == "https://my-issuer.example.com" ? "yes" : "no")" />
            </inbound>
        </policies>
        """,
        DisplayName = "Should fold const field reference in expression method body"
    )]
    public void ShouldFoldConstInExpressionBody(string code, string constClass, string expectedXml)
    {
        code.CompileDocument(constClass).Should().BeSuccessful().And.DocumentEquivalentTo(expectedXml);
    }

    [TestMethod]
    [DataRow(
        """
        using System;

        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.SetVariable("match", IsMatch(context.ExpressionContext));
            }
            bool IsMatch(IExpressionContext context)
                => string.Equals(context.Request.IpAddress, "10.0.0.1", StringComparison.OrdinalIgnoreCase);
        }
        """,
        """
        <policies>
            <inbound>
                <set-variable name="match" value="@(string.Equals(context.Request.IpAddress, "10.0.0.1", StringComparison.OrdinalIgnoreCase))" />
            </inbound>
        </policies>
        """,
        DisplayName = "Should preserve enum member access in expression body"
    )]
    public void ShouldPreserveEnumMemberInExpressionBody(string code, string expectedXml)
    {
        code.CompileDocument().Should().BeSuccessful().And.DocumentEquivalentTo(expectedXml);
    }
}
