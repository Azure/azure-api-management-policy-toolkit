// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling;

[TestClass]
public class AzureOpenAiSemanticCacheLookupTests
{
    [TestMethod]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context)
            {
                context.AzureOpenAiSemanticCacheLookup(new SemanticCacheLookupConfig
                {
                    ScoreThreshold = 0.05,
                    EmbeddingsBackendId = "llm-backend",
                    EmbeddingsBackendAuth = "system-assigned"
                });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <azure-openai-semantic-cache-lookup score-threshold="0.05" embeddings-backend-id="llm-backend" embeddings-backend-auth="system-assigned" />
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile azure-openai-semantic-cache-lookup policy"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context)
            {
                context.AzureOpenAiSemanticCacheLookup(new SemanticCacheLookupConfig
                {
                    ScoreThreshold = 0.05,
                    EmbeddingsBackendId = "llm-backend",
                    EmbeddingsBackendAuth = "system-assigned",
                    IgnoreSystemMessages = true
                });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <azure-openai-semantic-cache-lookup score-threshold="0.05" embeddings-backend-id="llm-backend" embeddings-backend-auth="system-assigned" ignore-system-messages="true" />
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile azure-openai-semantic-cache-lookup policy with ignore-system-message"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context)
            {
                context.AzureOpenAiSemanticCacheLookup(new SemanticCacheLookupConfig
                {
                    ScoreThreshold = 0.05,
                    EmbeddingsBackendId = "llm-backend",
                    EmbeddingsBackendAuth = "system-assigned",
                    MaxMessageCount = 10
                });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <azure-openai-semantic-cache-lookup score-threshold="0.05" embeddings-backend-id="llm-backend" embeddings-backend-auth="system-assigned" max-message-count="10" />
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile azure-openai-semantic-cache-lookup policy with max-message-count"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context)
            {
                context.AzureOpenAiSemanticCacheLookup(new SemanticCacheLookupConfig
                {
                    ScoreThreshold = 0.05,
                    EmbeddingsBackendId = "llm-backend",
                    EmbeddingsBackendAuth = "system-assigned",
                    VaryBy = [
                        ApiId(context.ExpressionContext),
                        EmailDomain(context.ExpressionContext),
                    ]
                });
            }
            string ApiId(IExpressionContext context) => context.Api.Id;
            string EmailDomain(IExpressionContext context) => context.User.Email.Split('@')[1];
        }
        """,
        """
        <policies>
            <inbound>
                <azure-openai-semantic-cache-lookup score-threshold="0.05" embeddings-backend-id="llm-backend" embeddings-backend-auth="system-assigned">
                    <vary-by>@(context.Api.Id)</vary-by>
                    <vary-by>@(context.User.Email.Split('@')[1])</vary-by>
                </azure-openai-semantic-cache-lookup>
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile azure-openai-semantic-cache-lookup policy with vary-by"
    )]
    public void ShouldCompileAzureOpenAiSemanticCacheLookupPolicy(string code, string expectedXml)
    {
        code.CompileDocument().Should().BeSuccessful().And.DocumentEquivalentTo(expectedXml);
    }
}