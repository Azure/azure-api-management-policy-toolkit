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
                <llm-semantic-cache-store duration="@((uint)(context.User.Email.EndsWith("@contoso.example") ? 10 : 60))" />
            </outbound>
        </policies>
        """,
        DisplayName = "Should compile llm-semantic-cache-store policy with expression"
    )]
    public void ShouldCompileLlmSemanticCacheStorePolicy(string code, string expectedXml)
    {
        code.CompileDocument().Should().BeSuccessful().And.DocumentEquivalentTo(expectedXml);
    }

    [TestMethod]
    [DataRow("LlmSemanticCacheStore", "llm-semantic-cache-store")]
    [DataRow("AzureOpenAiSemanticCacheStore", "azure-openai-semantic-cache-store")]
    public void ShouldCompileSemanticCacheStoreWithCacheResponse(string method, string policy)
    {
        var code =
            $$"""
              [Document]
              public class PolicyDocument : IDocument
              {
                  public void Outbound(IOutboundContext context)
                  {
                      context.{{method}}(60, true);
                      context.{{method}}(120);
                  }
              }
              """;

        code.CompileDocument().Should().BeSuccessful().And.DocumentEquivalentTo(
            $"""
             <policies>
                 <outbound>
                     <{policy} duration="60" cache-response="true" />
                     <{policy} duration="120" />
                 </outbound>
             </policies>
             """);
    }
}
