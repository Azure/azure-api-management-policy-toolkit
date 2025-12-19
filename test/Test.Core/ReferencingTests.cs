// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Test.Core;

[TestClass]
public class ReferencingTests
{
    [TestMethod]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) 
            { 
                context.AuthenticationBasic("{{username}}", Expressions.Password(context.ExpressionContext));
            }
        }
        """,
        """
        public static class Expressions
        {
            public static string Password(IExpressionContext context) => context.Subscription.Key;
        }
        """,
        """
        <policies>
            <inbound>
                <authentication-basic username="{{username}}" password="@(context.Subscription.Key)" />
            </inbound>
        </policies>
        """,
        DisplayName = "Should reference external expression code in policy document"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) 
            { 
                context.AuthenticationBasic(Constants.Username, "{{password}}");
            }
        }
        """,
        """
        public static class Constants
        {
            public const string Username = "{{username}}";
        }
        """,
        """
        <policies>
            <inbound>
                <authentication-basic username="{{username}}" password="{{password}}" />
            </inbound>
        </policies>
        """,
        DisplayName = "Should reference external constant in policy document"
    )]
    public void ShouldReference(string document, string externalCode, string expectedXml)
    {
        document.CompileDocument(externalCode).Should().BeSuccessful().And.DocumentEquivalentTo(expectedXml);
    }
}