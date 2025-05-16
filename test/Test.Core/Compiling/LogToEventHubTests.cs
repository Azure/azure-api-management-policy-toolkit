// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling;

[TestClass]
public class LogToEventHubTests
{
    [TestMethod]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) { 
                context.LogToEventHub(new LogToEventHubConfig
                {
                    LoggerId = "inbound-loggerId",
                    Value = "inbound-value",
                });
            }
            public void Backend(IBackendContext context) { 
                context.LogToEventHub(new LogToEventHubConfig
                {
                    LoggerId = "backend-loggerId",
                    Value = "backend-value",
                });
            }
            public void Outbound(IOutboundContext context) { 
                context.LogToEventHub(new LogToEventHubConfig
                {
                    LoggerId = "outbound-loggerId",
                    Value = "outbound-value",
                });
            }
            public void OnError(IOnErrorContext context) { 
                context.LogToEventHub(new LogToEventHubConfig
                {
                    LoggerId = "on-error-loggerId",
                    Value = "on-error-value",
                });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <log-to-eventhub logger-id="inbound-loggerId">inbound-value</log-to-eventhub>
            </inbound>
            <backend>
                <log-to-eventhub logger-id="backend-loggerId">backend-value</log-to-eventhub>
            </backend>
            <outbound>
                <log-to-eventhub logger-id="outbound-loggerId">outbound-value</log-to-eventhub>
            </outbound>
            <on-error>
                <log-to-eventhub logger-id="on-error-loggerId">on-error-value</log-to-eventhub>
            </on-error>
        </policies>
        """,
        DisplayName = "Should compile log-to-eventhub policy in sections"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) { 
                context.LogToEventHub(new LogToEventHubConfig
                {
                    LoggerId = "loggerId",
                    Value = GetLogValue(context.ExpressionContext),
                });
            }
            string GetLogValue(IExpressionContext context) =>
                string.Join(",", DateTime.UtcNow, context.Deployment.ServiceName, context.RequestId, context.Request.IpAddress, context.Operation.Name);
        }
        """,
        """
        <policies>
            <inbound>
                <log-to-eventhub logger-id="loggerId">@(string.Join(",", DateTime.UtcNow, context.Deployment.ServiceName, context.RequestId, context.Request.IpAddress, context.Operation.Name))</log-to-eventhub>
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile log-to-eventhub policy with expression value"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) { 
                context.LogToEventHub(new LogToEventHubConfig
                {
                    LoggerId = "loggerId",
                    Value = GetLogValue(context.ExpressionContext),
                    PartitionId = "partitionId",
                });
            }
            string GetLogValue(IExpressionContext context) =>
                string.Join(",", DateTime.UtcNow, context.Deployment.ServiceName, context.RequestId, context.Request.IpAddress, context.Operation.Name);
        }
        """,
        """
        <policies>
            <inbound>
                <log-to-eventhub logger-id="loggerId" partition-id="partitionId">@(string.Join(",", DateTime.UtcNow, context.Deployment.ServiceName, context.RequestId, context.Request.IpAddress, context.Operation.Name))</log-to-eventhub>
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile log-to-eventhub policy with partition id"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) { 
                context.LogToEventHub(new LogToEventHubConfig
                {
                    LoggerId = "loggerId",
                    Value = GetLogValue(context.ExpressionContext),
                    PartitionKey = "partitionKey",
                });
            }
            string GetLogValue(IExpressionContext context) =>
                string.Join(",", DateTime.UtcNow, context.Deployment.ServiceName, context.RequestId, context.Request.IpAddress, context.Operation.Name);
        }
        """,
        """
        <policies>
            <inbound>
                <log-to-eventhub logger-id="loggerId" partition-key="partitionKey">@(string.Join(",", DateTime.UtcNow, context.Deployment.ServiceName, context.RequestId, context.Request.IpAddress, context.Operation.Name))</log-to-eventhub>
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile log-to-eventhub policy with partition key"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) { 
                context.LogToEventHub(new LogToEventHubConfig
                {
                    LoggerId = "loggerId",
                    Value = GetLogValue(context.ExpressionContext),
                    PartitionKey = GetPartitionKey(context.ExpressionContext),
                });
            }
            string GetPartitionKey(IExpressionContext context) => context.Deployment.ServiceName;
            string GetLogValue(IExpressionContext context) =>
                string.Join(",", DateTime.UtcNow, context.Deployment.ServiceName, context.RequestId, context.Request.IpAddress, context.Operation.Name);
        }
        """,
        """
        <policies>
            <inbound>
                <log-to-eventhub logger-id="loggerId" partition-key="@(context.Deployment.ServiceName)">@(string.Join(",", DateTime.UtcNow, context.Deployment.ServiceName, context.RequestId, context.Request.IpAddress, context.Operation.Name))</log-to-eventhub>
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile log-to-eventhub policy with expression in partition key"
    )]
    public void ShouldCompileLogToEventHubPolicy(string code, string expectedXml)
    {
        code.CompileDocument().Should().BeSuccessful().And.DocumentEquivalentTo(expectedXml);
    }
}