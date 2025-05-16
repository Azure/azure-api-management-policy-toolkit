// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling;

[TestClass]
public class AuthenticationBasicTests
{
    [TestMethod]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) 
            { 
                context.AuthenticationBasic("username", "password");
            }
        }
        """,
        """
        <policies>
            <inbound>
                <authentication-basic username="username" password="password" />
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile authentication basic policy"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) 
            { 
                context.AuthenticationBasic(Username(context.ExpressionContext), Password(context.ExpressionContext));
            }

            public string Username(IExpressionContext context) => context.Subscription.Id;
            public string Password(IExpressionContext context) => context.Subscription.Key;
        }
        """,
        """
        <policies>
            <inbound>
                <authentication-basic username="@(context.Subscription.Id)" password="@(context.Subscription.Key)" />
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile authentication basic policy with expressions"
    )]
    public void ShouldCompileAuthenticationBasicPolicy(string code, string expectedXml)
    {
        code.CompileDocument().Should().BeSuccessful().And.DocumentEquivalentTo(expectedXml);
    }
}