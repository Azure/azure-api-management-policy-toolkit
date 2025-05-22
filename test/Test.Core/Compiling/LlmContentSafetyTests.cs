// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling;

[TestClass]
public class LlmContentSafetyTests
{
    [TestMethod]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) 
            { 
                context.LlmContentSafety(new LlmContentSafetyConfig { BackendId = "backendId" });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <llm-content-safety backend-id="backendId" />
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile llm-content-safety policy with backend id"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) 
            { 
                context.LlmContentSafety(new LlmContentSafetyConfig { BackendId = "backendId", ShieldPrompt = true });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <llm-content-safety backend-id="backendId" shield-prompt="true" />
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile llm-content-safety policy with shield prompt"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) 
            { 
                context.LlmContentSafety(new LlmContentSafetyConfig { BackendId = "backendId", ShieldPrompt = GetShieldPrompt(context.ExpressionContext) });
            }
            bool GetShieldPrompt(IExpressionContext context) => (bool)context.Variables["shieldPrompt"];
        }
        """,
        """
        <policies>
            <inbound>
                <llm-content-safety backend-id="backendId" shield-prompt="@((bool)context.Variables["shieldPrompt"])" />
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile llm-content-safety policy with expression in shield prompt"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) 
            { 
                context.LlmContentSafety(new LlmContentSafetyConfig 
                { 
                    BackendId = "backendId", 
                    Categories = new ContentSafetyCategories 
                    { 
                        OutputType = "outputType", 
                        Categories = new[] 
                        { 
                            new ContentSafetyCategory { Name = "category1", Threshold = 1 } 
                        } 
                    } 
                });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <llm-content-safety backend-id="backendId">
                    <categories output-type="outputType">
                        <category name="category1" threshold="1" />
                    </categories>
                </llm-content-safety>
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile llm-content-safety policy with categories"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) 
            { 
                context.LlmContentSafety(new LlmContentSafetyConfig 
                { 
                    BackendId = "backendId", 
                    BlockLists = new ContentSafetyBlockLists 
                    { 
                        Ids = new[] { "blockList1", "blockList2" } 
                    } 
                });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <llm-content-safety backend-id="backendId">
                    <block-lists>
                        <id>blockList1</id>
                        <id>blockList2</id>
                    </block-lists>
                </llm-content-safety>
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile llm-content-safety policy with block lists"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) 
            { 
                context.LlmContentSafety(new LlmContentSafetyConfig 
                { 
                    BackendId = "backendId", 
                    Categories = new ContentSafetyCategories 
                    { 
                        OutputType = GetOutputType(context.ExpressionContext), 
                        Categories = new[] 
                        { 
                            new ContentSafetyCategory { Name = "category1", Threshold = 1 } 
                        } 
                    } 
                });
            }
            string GetOutputType(IExpressionContext context) => context.Variables["outputType"].ToString();
        }
        """,
        """
        <policies>
            <inbound>
                <llm-content-safety backend-id="backendId">
                    <categories output-type="@(context.Variables["outputType"].ToString())">
                        <category name="category1" threshold="1" />
                    </categories>
                </llm-content-safety>
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile llm-content-safety policy with expression in output type"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) 
            { 
                context.LlmContentSafety(new LlmContentSafetyConfig 
                { 
                    BackendId = "backendId", 
                    Categories = new ContentSafetyCategories 
                    { 
                        OutputType = "outputType", 
                        Categories = new[] 
                        { 
                            new ContentSafetyCategory { Name = "category1", Threshold = 1 },
                            new ContentSafetyCategory { Name = "category2", Threshold = 2 }
                        } 
                    } 
                });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <llm-content-safety backend-id="backendId">
                    <categories output-type="outputType">
                        <category name="category1" threshold="1" />
                        <category name="category2" threshold="2" />
                    </categories>
                </llm-content-safety>
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile llm-content-safety policy with multiple categories"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) 
            { 
                context.LlmContentSafety(new LlmContentSafetyConfig 
                { 
                    BackendId = "backendId", 
                    Categories = new ContentSafetyCategories 
                    { 
                        OutputType = "outputType", 
                        Categories = new[] 
                        { 
                            new ContentSafetyCategory { Name = GetCategoryName(context.ExpressionContext), Threshold = 1 } 
                        } 
                    } 
                });
            }
            string GetCategoryName(IExpressionContext context) => context.Variables["categoryName"].ToString();
        }
        """,
        """
        <policies>
            <inbound>
                <llm-content-safety backend-id="backendId">
                    <categories output-type="outputType">
                        <category name="@(context.Variables["categoryName"].ToString())" threshold="1" />
                    </categories>
                </llm-content-safety>
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile llm-content-safety policy with expression in category name"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) 
            { 
                context.LlmContentSafety(new LlmContentSafetyConfig 
                { 
                    BackendId = "backendId", 
                    BlockLists = new ContentSafetyBlockLists 
                    { 
                        Ids = new[] { GetBlockListId(context.ExpressionContext), "blockList2" } 
                    } 
                });
            }
            string GetBlockListId(IExpressionContext context) => context.Variables["blockListId"].ToString();
        }
        """,
        """
        <policies>
            <inbound>
                <llm-content-safety backend-id="backendId">
                    <block-lists>
                        <id>@(context.Variables["blockListId"].ToString())</id>
                        <id>blockList2</id>
                    </block-lists>
                </llm-content-safety>
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile llm-content-safety policy with expression in block list id"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) 
            { 
                context.LlmContentSafety(new LlmContentSafetyConfig 
                { 
                    BackendId = "backendId", 
                    Categories = new ContentSafetyCategories 
                    { 
                        OutputType = "outputType", 
                        Categories = new[] 
                        { 
                            new ContentSafetyCategory { Name = "category1", Threshold = GetThreshold(context.ExpressionContext) } 
                        } 
                    } 
                });
            }
            int GetThreshold(IExpressionContext context) => (int)context.Variables["threshold"];
        }
        """,
        """
        <policies>
            <inbound>
                <llm-content-safety backend-id="backendId">
                    <categories output-type="outputType">
                        <category name="category1" threshold="@((int)context.Variables["threshold"])" />
                    </categories>
                </llm-content-safety>
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile llm-content-safety policy with expression in threshold"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) 
            { 
                context.LlmContentSafety(new LlmContentSafetyConfig 
                { 
                    BackendId = "realBackendId", 
                    ShieldPrompt = true, 
                    Categories = new ContentSafetyCategories 
                    { 
                        OutputType = "json", 
                        Categories = new[] 
                        { 
                            new ContentSafetyCategory { Name = "violence", Threshold = 3 },
                            new ContentSafetyCategory { Name = "hate_speech", Threshold = 2 }
                        } 
                    },
                    BlockLists = new ContentSafetyBlockLists 
                    { 
                        Ids = new[] { "blockList1", "blockList2" } 
                    }
                });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <llm-content-safety backend-id="realBackendId" shield-prompt="true">
                    <categories output-type="json">
                        <category name="violence" threshold="3" />
                        <category name="hate_speech" threshold="2" />
                    </categories>
                    <block-lists>
                        <id>blockList1</id>
                        <id>blockList2</id>
                    </block-lists>
                </llm-content-safety>
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile llm-content-safety policy with real-life configuration"
    )]
    public void ShouldCompileLlmContentSafetyPolicy(string code, string expectedXml)
    {
        code.CompileDocument().Should().BeSuccessful().And.DocumentEquivalentTo(expectedXml);
    }
}