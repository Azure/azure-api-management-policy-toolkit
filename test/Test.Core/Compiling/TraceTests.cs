// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling;

[TestClass]
public class TraceTests
{
    [TestMethod]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.Trace(new TraceConfig 
                {
                    Source = "inbound-source",
                    Message = "inbound-message"
                });
            }
            public void Backend(IBackendContext context) {
                context.Trace(new TraceConfig
                { 
                    Source = "backend-source",
                    Message = "backend-message"
                });
            }
            public void Outbound(IOutboundContext context) {
                context.Trace(new TraceConfig 
                {
                    Source = "outbound-source",
                    Message = "outbound-message"
                });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <trace source="inbound-source">
                    <message>inbound-message</message>
                </trace>
            </inbound>
            <backend>
                <trace source="backend-source">
                    <message>backend-message</message>
                </trace>
            </backend>
            <outbound>
                <trace source="outbound-source">
                    <message>outbound-message</message>
                </trace>
            </outbound>
        </policies>
        """,
        DisplayName = "Should compile trace policy in sections"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.Trace(new TraceConfig
                { 
                    Source = "inbound-source",
                    Message = GetTraceMessage(context.ExpressionContext),
                });
            }
            string GetTraceMessage(IExpressionContext context) => context.Variables["trace-message"].ToString();
        }
        """,
        """
        <policies>
            <inbound>
                <trace source="inbound-source">
                    <message>@(context.Variables["trace-message"].ToString())</message>
                </trace>
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile trace policy with expression in message"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.Trace(new TraceConfig
                { 
                    Source = "inbound-source",
                    Message = "inbound-message",
                    Severity = "information"
                });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <trace source="inbound-source" severity="information">
                    <message>inbound-message</message>
                </trace>
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile trace policy with severity"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.Trace(new TraceConfig
                { 
                    Source = "inbound-source",
                    Message = "inbound-message",
                    Metadata = [
                        new TraceMetadata { Name = "key1", Value = "value1" },
                        new TraceMetadata 
                        {
                            Name = GetMetadataName(context.ExpressionContext),
                            Value = GetMetadataValue(context.ExpressionContext)
                        }
                    ]
                });
            }
            
            string GetMetadataName(IExpressionContext context) => context.Variables["nKey"].ToString();
            string GetMetadataValue(IExpressionContext context) => context.Variables["vKey"].ToString();
        }
        """,
        """
        <policies>
            <inbound>
                <trace source="inbound-source">
                    <message>inbound-message</message>
                    <metadata name="key1" value="value1" />
                    <metadata name="@(context.Variables["nKey"].ToString())" value="@(context.Variables["vKey"].ToString())" />
                </trace>
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile trace policy with metadata"
    )]
    public void ShouldCompileTracePolicy(string code, string expectedXml)
    {
        code.CompileDocument().Should().BeSuccessful().And.DocumentEquivalentTo(expectedXml);
    }
}