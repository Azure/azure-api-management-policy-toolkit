// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling;

[TestClass]
public class LlmSemanticCacheStoreTests
{
    [TestMethod]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Outbound(IOutboundContext context)
            {
                context.LlmSemanticCacheStore(60);
            }
        }
        """,
        """
        <policies>
            <outbound>
                <llm-semantic-cache-store duration="60" />
            </outbound>
        </policies>
        """,
        DisplayName = "Should compile llm-semantic-cache-store policy"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Outbound(IOutboundContext context)
            {
                context.LlmSemanticCacheStore(Duration(context.ExpressionContext));
            }
            
            uint Duration(IExpressionContext context) => context.User.Email.EndsWith("@contoso.example") ? 10 : 60;
        }
        """,
        """
        <policies>
            <outbound>
                <llm-semantic-cache-store duration="@(context.User.Email.EndsWith("@contoso.example") ? 10 : 60)" />
            </outbound>
        </policies>
        """,
        DisplayName = "Should compile llm-semantic-cache-store policy with expression"
    )]
    public void ShouldCompileLlmSemanticCacheStorePolicy(string code, string expectedXml)
    {
        code.CompileDocument().Should().BeSuccessful().And.DocumentEquivalentTo(expectedXml);
    }
}