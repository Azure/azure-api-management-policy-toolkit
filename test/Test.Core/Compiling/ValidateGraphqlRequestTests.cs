// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling;

[TestClass]
public class ValidateGraphqlRequestTests
{
    [TestMethod]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.ValidateGraphqlRequest(new ValidateGraphqlRequestConfig
                {
                    ErrorVariableName = "graphql-error"
                });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <validate-graphql-request error-variable-name="graphql-error" />
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile validate-graphql-request policy with error-variable-name"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.ValidateGraphqlRequest(new ValidateGraphqlRequestConfig
                {
                    MaxDepth = 3
                });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <validate-graphql-request max-depth="3" />
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile validate-graphql-request policy with max-depth"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.ValidateGraphqlRequest(new ValidateGraphqlRequestConfig
                {
                    MaxSize = 204800
                });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <validate-graphql-request max-size="204800" />
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile validate-graphql-request policy with max-size"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.ValidateGraphqlRequest(new ValidateGraphqlRequestConfig
                {
                    MaxTotalDepth = 15
                });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <validate-graphql-request max-total-depth="15" />
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile validate-graphql-request policy with max-total-depth"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.ValidateGraphqlRequest(new ValidateGraphqlRequestConfig
                {
                    MaxComplexity = 200
                });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <validate-graphql-request max-complexity="200" />
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile validate-graphql-request policy with max-complexity"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.ValidateGraphqlRequest(new ValidateGraphqlRequestConfig
                {
                    ErrorVariableName = "graphql-error",
                    MaxDepth = 5,
                    MaxSize = 102400,
                    MaxTotalDepth = 10,
                    MaxComplexity = 100
                });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <validate-graphql-request error-variable-name="graphql-error" max-depth="5" max-size="102400" max-total-depth="10" max-complexity="100" />
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile validate-graphql-request policy with all attributes"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.ValidateGraphqlRequest(new ValidateGraphqlRequestConfig
                {
                    ErrorVariableName = "graphql-error",
                    MaxDepth = 5,
                    MaxSize = 102400,
                    MaxTotalDepth = 10,
                    MaxComplexity = 100,
                    Authorize = new AuthorizeConfig
                    {
                        Rules = new AuthorizeRuleConfig[]
                        {
                            new AuthorizeRuleConfig { Path = "/Query/users", Action = "allow" }
                        }
                    }
                });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <validate-graphql-request error-variable-name="graphql-error" max-depth="5" max-size="102400" max-total-depth="10" max-complexity="100">
                    <authorize>
                        <rule path="/Query/users" action="allow" />
                    </authorize>
                </validate-graphql-request>
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile validate-graphql-request policy with all attributes and authorize"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.ValidateGraphqlRequest(new ValidateGraphqlRequestConfig
                {
                    Authorize = new AuthorizeConfig
                    {
                        Rules = new AuthorizeRuleConfig[]
                        {
                            new AuthorizeRuleConfig { Path = "/Query/users", Action = "allow" },
                            new AuthorizeRuleConfig { Path = "/Mutation/deleteUser", Action = "deny" }
                        }
                    }
                });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <validate-graphql-request>
                    <authorize>
                        <rule path="/Query/users" action="allow" />
                        <rule path="/Mutation/deleteUser" action="deny" />
                    </authorize>
                </validate-graphql-request>
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile validate-graphql-request policy with multiple authorize rules"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.ValidateGraphqlRequest(new ValidateGraphqlRequestConfig
                {
                    Authorize = new AuthorizeConfig
                    {
                        Rules = new AuthorizeRuleConfig[]
                        {
                            new AuthorizeRuleConfig { Path = "/Mutation/deleteUser" }
                        }
                    }
                });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <validate-graphql-request>
                    <authorize>
                        <rule path="/Mutation/deleteUser" />
                    </authorize>
                </validate-graphql-request>
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile validate-graphql-request policy with rule without action"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.ValidateGraphqlRequest(new ValidateGraphqlRequestConfig
                {
                    MaxDepth = 5,
                    Authorize = new AuthorizeConfig
                    {
                        Rules = new AuthorizeRuleConfig[]
                        {
                            new AuthorizeRuleConfig { Path = "/Query/users", Action = "allow" }
                        }
                    }
                });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <validate-graphql-request max-depth="5">
                    <authorize>
                        <rule path="/Query/users" action="allow" />
                    </authorize>
                </validate-graphql-request>
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile validate-graphql-request policy with single attribute and authorize"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.ValidateGraphqlRequest(new ValidateGraphqlRequestConfig
                {
                    ErrorVariableName = GetErrorVariableName(context.ExpressionContext)
                });
            }

            private string GetErrorVariableName(IExpressionContext context) => context.Variables["error-var"];
        }
        """,
        """
        <policies>
            <inbound>
                <validate-graphql-request error-variable-name="@(context.Variables["error-var"])" />
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile validate-graphql-request policy with expression in error-variable-name"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.ValidateGraphqlRequest(new ValidateGraphqlRequestConfig
                {
                    ErrorVariableName = GetErrorVariableName(context.ExpressionContext),
                    MaxDepth = GetMaxDepth(context.ExpressionContext),
                    MaxSize = GetMaxSize(context.ExpressionContext),
                    MaxTotalDepth = GetMaxTotalDepth(context.ExpressionContext),
                    MaxComplexity = GetMaxComplexity(context.ExpressionContext)
                });
            }

            private string GetErrorVariableName(IExpressionContext context) => context.Variables["error-var"];
            private int GetMaxDepth(IExpressionContext context) => (int)context.Variables["max-depth"];
            private long GetMaxSize(IExpressionContext context) => (long)context.Variables["max-size"];
            private int GetMaxTotalDepth(IExpressionContext context) => (int)context.Variables["max-total-depth"];
            private int GetMaxComplexity(IExpressionContext context) => (int)context.Variables["max-complexity"];
        }
        """,
        """
        <policies>
            <inbound>
                <validate-graphql-request error-variable-name="@(context.Variables["error-var"])" max-depth="@((int)context.Variables["max-depth"])" max-size="@((long)context.Variables["max-size"])" max-total-depth="@((int)context.Variables["max-total-depth"])" max-complexity="@((int)context.Variables["max-complexity"])" />
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile validate-graphql-request policy with expressions in all attributes"
    )]
    public void ShouldCompileValidateGraphqlRequestPolicy(string code, string expectedXml)
    {
        code.CompileDocument().Should().BeSuccessful().And.DocumentEquivalentTo(expectedXml);
    }
}
