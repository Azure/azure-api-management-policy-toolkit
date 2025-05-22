// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling;

[TestClass]
public class EmitMetricTests
{
    [TestMethod]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) 
            {
                context.EmitMetric(new EmitMetricConfig
                {
                    Name = "inbound",
                    Dimensions = [
                        new MetricDimensionConfig { Name = "API ID" }
                    ]
                });
            }
            public void Outbound(IOutboundContext context)
            {
                context.EmitMetric(new EmitMetricConfig
                {
                    Name = "outbound",
                    Dimensions = [
                        new MetricDimensionConfig { Name = "API ID" }
                    ]
                });
            }
            public void Backend(IBackendContext context)
            {
                context.EmitMetric(new EmitMetricConfig
                {
                    Name = "backend",
                    Dimensions = [
                        new MetricDimensionConfig { Name = "API ID" }
                    ]
                });
            }
            public void OnError(IOnErrorContext context)
            {
                context.EmitMetric(new EmitMetricConfig
                {
                    Name = "on-error",
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
                <emit-metric name="inbound">
                    <dimension name="API ID" />
                </emit-metric>
            </inbound>
            <outbound>
                <emit-metric name="outbound">
                    <dimension name="API ID" />
                </emit-metric>
            </outbound>
            <backend>
                <emit-metric name="backend">
                    <dimension name="API ID" />
                </emit-metric>
            </backend>
            <on-error>
                <emit-metric name="on-error">
                    <dimension name="API ID" />
                </emit-metric>
            </on-error>
        </policies>
        """,
        DisplayName = "Should compile emit-metric policy in sections"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) 
            {
                context.EmitMetric(new EmitMetricConfig
                {
                    Name = "inbound",
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
                <emit-metric name="inbound" namespace="contoso.example">
                    <dimension name="API ID" />
                </emit-metric>
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile emit-metric policy with namespace"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) 
            {
                context.EmitMetric(new EmitMetricConfig
                {
                    Name = "inbound",
                    Value = 1
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
                <emit-metric name="inbound" value="1">
                    <dimension name="API ID" />
                </emit-metric>
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile emit-metric policy with value"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) 
            {
                context.EmitMetric(new EmitMetricConfig
                {
                    Name = "inbound",
                    Value = Exp(context.ExpressionContext)
                    Dimensions = [
                        new MetricDimensionConfig { Name = "API ID" }
                    ]
                });
            }
            bool Exp(IExpressionContext context) 
                => context.User.Email.EndsWith("@contoso.example") ? 1 : 2;
        }
        """,
        """
        <policies>
            <inbound>
                <emit-metric name="inbound" value="@(context.User.Email.EndsWith("@contoso.example") ? 1 : 2)">
                    <dimension name="API ID" />
                </emit-metric>
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile emit-metric policy with expression in value"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) 
            {
                context.EmitMetric(new EmitMetricConfig
                {
                    Name = "inbound",
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
                <emit-metric name="inbound">
                    <dimension name="API ID" />
                    <dimension name="Operation ID" />
                </emit-metric>
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile emit-metric policy with multiple dimensions"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) 
            {
                context.EmitMetric(new EmitMetricConfig
                {
                    Name = "inbound",
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
                <emit-metric name="inbound">
                    <dimension name="custom" value="value" />
                </emit-metric>
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile emit-metric policy with value in dimension"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) 
            {
                context.EmitMetric(new EmitMetricConfig
                {
                    Name = "inbound",
                    Dimensions = [
                        new MetricDimensionConfig { Name = "EmailDomain", Value=Exp(context.ExpressionContext) },
                    ]
                });
            }
            bool Exp(IExpressionContext context) => context.User.Email.Split('@')[1];
        }
        """,
        """
        <policies>
            <inbound>
                <emit-metric name="inbound">
                    <dimension name="EmailDomain" value="@(context.User.Email.Split('@')[1])" />
                </emit-metric>
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile emit-metric policy with expression in value in dimension"
    )]
    public void ShouldCompileEmitMetricPolicy(string code, string expectedXml)
    {
        code.CompileDocument().Should().BeSuccessful().And.DocumentEquivalentTo(expectedXml);
    }
}