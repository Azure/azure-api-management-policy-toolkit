// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling;

[TestClass]
public class AzureOpenAiEmitTokenMetricTests
{
    [TestMethod]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) 
            {
                context.AzureOpenAiEmitTokenMetric(new EmitTokenMetricConfig
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
                <azure-openai-emit-token-metric>
                    <dimension name="API ID" />
                </azure-openai-emit-token-metric>
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile azure-openai-emit-token-metric policy in sections"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) 
            {
                context.AzureOpenAiEmitTokenMetric(new EmitTokenMetricConfig
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
                <azure-openai-emit-token-metric namespace="contoso.example">
                    <dimension name="API ID" />
                </azure-openai-emit-token-metric>
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile azure-openai-emit-token-metric policy with namespace"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) 
            {
                context.AzureOpenAiEmitTokenMetric(new EmitTokenMetricConfig
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
                <azure-openai-emit-token-metric>
                    <dimension name="API ID" />
                    <dimension name="Operation ID" />
                </azure-openai-emit-token-metric>
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile azure-openai-emit-token-metric policy with multiple dimensions"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) 
            {
                context.AzureOpenAiEmitTokenMetric(new EmitTokenMetricConfig
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
                <azure-openai-emit-token-metric>
                    <dimension name="custom" value="value" />
                </azure-openai-emit-token-metric>
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile azure-openai-emit-token-metric policy with value in dimension"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) 
            {
                context.AzureOpenAiEmitTokenMetric(new EmitTokenMetricConfig
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
                <azure-openai-emit-token-metric>
                    <dimension name="EmailDomain" value="@(context.User.Email.Split('@')[1])" />
                </azure-openai-emit-token-metric>
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile azure-openai-emit-token-metric policy with expression in value in dimension"
    )]
    public void ShouldCompileAzureOpenAiEmitTokenMetricPolicy(string code, string expectedXml)
    {
        code.CompileDocument().Should().BeSuccessful().And.DocumentEquivalentTo(expectedXml);
    }
}