// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling;

[TestClass]
public class SendServiceBusMessageTests
{
    [TestMethod]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.SendServiceBusMessage(new SendServiceBusMessageConfig
                {
                    QueueName = "orders",
                    Payload = "message content",
                });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <send-service-bus-message queue-name="orders">
                    <payload>message content</payload>
                </send-service-bus-message>
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile send-service-bus-message policy with queue-name and payload"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.SendServiceBusMessage(new SendServiceBusMessageConfig
                {
                    TopicName = "orders",
                    Payload = "message content",
                });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <send-service-bus-message topic-name="orders">
                    <payload>message content</payload>
                </send-service-bus-message>
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile send-service-bus-message policy with topic-name and payload"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.SendServiceBusMessage(new SendServiceBusMessageConfig
                {
                    QueueName = "orders",
                    Namespace = "my-sb.servicebus.windows.net",
                    Payload = "message content",
                });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <send-service-bus-message queue-name="orders" namespace="my-sb.servicebus.windows.net">
                    <payload>message content</payload>
                </send-service-bus-message>
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile send-service-bus-message policy with namespace"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.SendServiceBusMessage(new SendServiceBusMessageConfig
                {
                    QueueName = "orders",
                    ClientId = "00001111-aaaa-2222-bbbb-3333cccc4444",
                    Payload = "message content",
                });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <send-service-bus-message queue-name="orders" client-id="00001111-aaaa-2222-bbbb-3333cccc4444">
                    <payload>message content</payload>
                </send-service-bus-message>
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile send-service-bus-message policy with client-id"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.SendServiceBusMessage(new SendServiceBusMessageConfig
                {
                    QueueName = "orders",
                    MessageProperties = [
                        new ServiceBusMessageProperty { Name = "Customer", Value = "Contoso" },
                        new ServiceBusMessageProperty { Name = "Priority", Value = "High" },
                    ],
                    Payload = "message content",
                });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <send-service-bus-message queue-name="orders">
                    <message-properties>
                        <message-property name="Customer">Contoso</message-property>
                        <message-property name="Priority">High</message-property>
                    </message-properties>
                    <payload>message content</payload>
                </send-service-bus-message>
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile send-service-bus-message policy with message-properties"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.SendServiceBusMessage(new SendServiceBusMessageConfig
                {
                    QueueName = QueueNameExp(context.ExpressionContext),
                    Payload = "message content",
                });
            }
            string QueueNameExp(IExpressionContext context) => "queue-" + context.Deployment.ServiceName;
        }
        """,
        """
        <policies>
            <inbound>
                <send-service-bus-message queue-name="@("queue-" + context.Deployment.ServiceName)">
                    <payload>message content</payload>
                </send-service-bus-message>
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile send-service-bus-message policy with expression in queue-name"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.SendServiceBusMessage(new SendServiceBusMessageConfig
                {
                    TopicName = TopicNameExp(context.ExpressionContext),
                    Payload = "message content",
                });
            }
            string TopicNameExp(IExpressionContext context) => "topic-" + context.Deployment.ServiceName;
        }
        """,
        """
        <policies>
            <inbound>
                <send-service-bus-message topic-name="@("topic-" + context.Deployment.ServiceName)">
                    <payload>message content</payload>
                </send-service-bus-message>
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile send-service-bus-message policy with expression in topic-name"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.SendServiceBusMessage(new SendServiceBusMessageConfig
                {
                    QueueName = "orders",
                    Namespace = NamespaceExp(context.ExpressionContext),
                    Payload = "message content",
                });
            }
            string NamespaceExp(IExpressionContext context) => context.Deployment.ServiceName + ".servicebus.windows.net";
        }
        """,
        """
        <policies>
            <inbound>
                <send-service-bus-message queue-name="orders" namespace="@(context.Deployment.ServiceName + ".servicebus.windows.net")">
                    <payload>message content</payload>
                </send-service-bus-message>
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile send-service-bus-message policy with expression in namespace"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.SendServiceBusMessage(new SendServiceBusMessageConfig
                {
                    QueueName = "orders",
                    ClientId = ClientIdExp(context.ExpressionContext),
                    Payload = "message content",
                });
            }
            string ClientIdExp(IExpressionContext context) => context.Deployment.ServiceName;
        }
        """,
        """
        <policies>
            <inbound>
                <send-service-bus-message queue-name="orders" client-id="@(context.Deployment.ServiceName)">
                    <payload>message content</payload>
                </send-service-bus-message>
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile send-service-bus-message policy with expression in client-id"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.SendServiceBusMessage(new SendServiceBusMessageConfig
                {
                    QueueName = "orders",
                    Payload = PayloadExp(context.ExpressionContext),
                });
            }
            string PayloadExp(IExpressionContext context) => context.Request.Body.As<string>(preserveContent: true);
        }
        """,
        """
        <policies>
            <inbound>
                <send-service-bus-message queue-name="orders">
                    <payload>@(context.Request.Body.As<string>(preserveContent: true))</payload>
                </send-service-bus-message>
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile send-service-bus-message policy with expression in payload"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.SendServiceBusMessage(new SendServiceBusMessageConfig
                {
                    QueueName = "orders",
                    MessageProperties = [
                        new ServiceBusMessageProperty { Name = "Customer", Value = ValueExp(context.ExpressionContext) },
                    ],
                    Payload = "message content",
                });
            }
            string ValueExp(IExpressionContext context) => context.Request.Headers.GetValueOrDefault("X-Customer", "Unknown");
        }
        """,
        """
        <policies>
            <inbound>
                <send-service-bus-message queue-name="orders">
                    <message-properties>
                        <message-property name="Customer">@(context.Request.Headers.GetValueOrDefault("X-Customer", "Unknown"))</message-property>
                    </message-properties>
                    <payload>message content</payload>
                </send-service-bus-message>
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile send-service-bus-message policy with expression in message-property value"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.SendServiceBusMessage(new SendServiceBusMessageConfig
                {
                    QueueName = "inbound-queue",
                    Payload = "inbound-payload",
                });
            }
            public void Outbound(IOutboundContext context) {
                context.SendServiceBusMessage(new SendServiceBusMessageConfig
                {
                    QueueName = "outbound-queue",
                    Payload = "outbound-payload",
                });
            }
            public void OnError(IOnErrorContext context) {
                context.SendServiceBusMessage(new SendServiceBusMessageConfig
                {
                    QueueName = "on-error-queue",
                    Payload = "on-error-payload",
                });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <send-service-bus-message queue-name="inbound-queue">
                    <payload>inbound-payload</payload>
                </send-service-bus-message>
            </inbound>
            <outbound>
                <send-service-bus-message queue-name="outbound-queue">
                    <payload>outbound-payload</payload>
                </send-service-bus-message>
            </outbound>
            <on-error>
                <send-service-bus-message queue-name="on-error-queue">
                    <payload>on-error-payload</payload>
                </send-service-bus-message>
            </on-error>
        </policies>
        """,
        DisplayName = "Should compile send-service-bus-message policy in sections"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.SendServiceBusMessage(new SendServiceBusMessageConfig
                {
                    QueueName = "orders",
                    Namespace = "my-sb.servicebus.windows.net",
                    ClientId = "00001111-aaaa-2222-bbbb-3333cccc4444",
                    MessageProperties = [
                        new ServiceBusMessageProperty { Name = "Customer", Value = "Contoso" },
                        new ServiceBusMessageProperty { Name = "Priority", Value = "High" },
                    ],
                    Payload = "message content",
                });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <send-service-bus-message queue-name="orders" namespace="my-sb.servicebus.windows.net" client-id="00001111-aaaa-2222-bbbb-3333cccc4444">
                    <message-properties>
                        <message-property name="Customer">Contoso</message-property>
                        <message-property name="Priority">High</message-property>
                    </message-properties>
                    <payload>message content</payload>
                </send-service-bus-message>
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile send-service-bus-message policy with all options"
    )]
    public void ShouldCompileSendServiceBusMessagePolicy(string code, string expectedXml)
    {
        code.CompileDocument().Should().BeSuccessful().And.DocumentEquivalentTo(expectedXml);
    }
}
