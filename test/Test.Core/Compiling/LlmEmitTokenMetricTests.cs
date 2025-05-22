// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling;

[TestClass]
public class LlmEmitTokenMetricTests
{
    [TestMethod]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) 
            {
                context.LlmEmitTokenMetric(new EmitTokenMetricConfig
                {
                    Dimensions = [
                        new MetricDimensionConfig { Name = "API ID" }
                    ]
                });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <llm-emit-token-metric>
                    <dimension name="API ID" />
                </llm-emit-token-metric>
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile llm-emit-token-metric policy in sections"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) 
            {
                context.LlmEmitTokenMetric(new EmitTokenMetricConfig
                {
                    Namespace = "contoso.example"
                    Dimensions = [
                        new MetricDimensionConfig { Name = "API ID" }
                    ]
                });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <llm-emit-token-metric namespace="contoso.example">
                    <dimension name="API ID" />
                </llm-emit-token-metric>
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile llm-emit-token-metric policy with namespace"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) 
            {
                context.LlmEmitTokenMetric(new EmitTokenMetricConfig
                {
                    Dimensions = [
                        new MetricDimensionConfig { Name = "API ID" },
                        new MetricDimensionConfig { Name = "Operation ID" }
                    ]
                });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <llm-emit-token-metric>
                    <dimension name="API ID" />
                    <dimension name="Operation ID" />
                </llm-emit-token-metric>
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile llm-emit-token-metric policy with multiple dimensions"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) 
            {
                context.LlmEmitTokenMetric(new EmitTokenMetricConfig
                {
                    Dimensions = [
                        new MetricDimensionConfig { Name = "custom", Value="value" },
                    ]
                });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <llm-emit-token-metric>
                    <dimension name="custom" value="value" />
                </llm-emit-token-metric>
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile llm-emit-token-metric policy with value in dimension"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) 
            {
                context.LlmEmitTokenMetric(new EmitTokenMetricConfig
                {
                    Dimensions = [
                        new MetricDimensionConfig { Name = "EmailDomain", Value=Exp(context.ExpressionContext) },
                    ]
                });
            }
            string Exp(IExpressionContext context) => context.User.Email.Split('@')[1];
        }
        """,
        """
        <policies>
            <inbound>
                <llm-emit-token-metric>
                    <dimension name="EmailDomain" value="@(context.User.Email.Split('@')[1])" />
                </llm-emit-token-metric>
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile llm-emit-token-metric policy with expression in value in dimension"
    )]
    public void ShouldCompileLlmEmitTokenMetricPolicy(string code, string expectedXml)
    {
        code.CompileDocument().Should().BeSuccessful().And.DocumentEquivalentTo(expectedXml);
    }
}