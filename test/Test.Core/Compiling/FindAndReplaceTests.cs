// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.ApiManagement.PolicyToolkit.Compiling;

[TestClass]
public class FindAndReplaceTests
{
    [TestMethod]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context)
            {
                context.FindAndReplace("cat", "dog");
            }
        
            public void Backend(IBackendContext context)
            {
                context.FindAndReplace("cat", "dog");
            }
        
            public void Outbound(IOutboundContext context)
            {
                context.FindAndReplace("cat", "dog");
            }
        
            public void OnError(IOnErrorContext context)
            {
                context.FindAndReplace("cat", "dog");
            }
        }
        """,
        """
        <policies>
            <inbound>
                <find-and-replace from="cat" to="dog" />
            </inbound>
            <backend>
                <find-and-replace from="cat" to="dog" />
            </backend>
            <outbound>
                <find-and-replace from="cat" to="dog" />
            </outbound>
            <on-error>
                <find-and-replace from="cat" to="dog" />
            </on-error>
        </policies>
        """,
        DisplayName = "Should compile find-and-replace policy in sections"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context)
            {
                context.FindAndReplace(FromEmail(context.ExpressionContext), "issues@contoso.com");
            }
        
            private string FromEmail(IExpressionContext context) => context.User.Email;
        }
        """,
        """
        <policies>
            <inbound>
                <find-and-replace from="@(context.User.Email)" to="issues@contoso.com" />
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile find-and-replace policy with dynamic expression in 'from' parameter"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context)
            {
                context.FindAndReplace("admin@contoso.com", ToEmail(context.ExpressionContext));
            }
        
            private string ToEmail(IExpressionContext context) => context.User.Email;
        }
        """,
        """
        <policies>
            <inbound>
                <find-and-replace from="admin@contoso.com" to="@(context.User.Email)" />
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile find-and-replace policy with dynamic expression in 'to' parameter"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context)
            {
                context.FindAndReplace(FromId(context.ExpressionContext), ToName(context.ExpressionContext));
            }
        
            private string FromId(IExpressionContext context) => context.User.Id;
            private string ToName(IExpressionContext context) => context.User.Name;
        }
        """,
        """
        <policies>
            <inbound>
                <find-and-replace from="@(context.User.Id)" to="@(context.User.Name)" />
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile find-and-replace policy with dynamic expressions in both parameters"
    )]
    public void ShouldCompileFindAndReplacePolicy(string code, string expectedXml)
    {
        code.CompileDocument().Should().BeSuccessful().And.DocumentEquivalentTo(expectedXml);
    }
}