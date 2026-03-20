// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Xml.Linq;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Decompiling.Policy;

public class SendServiceBusMessageDecompiler : IPolicyDecompiler
{
    public string PolicyName => "send-service-bus-message";

    public void Decompile(CodeWriter writer, XElement element, string contextVar, PolicyDecompilerContext context)
    {
        var prefix = PolicyDecompilerContext.GetContextPrefix(element, contextVar);
        var props = new List<string>();

        context.AddOptionalStringProp(props, element, "queue-name", "QueueName");
        context.AddOptionalStringProp(props, element, "topic-name", "TopicName");
        context.AddOptionalStringProp(props, element, "namespace", "Namespace");
        context.AddOptionalStringProp(props, element, "client-id", "ClientId");

        var messagePropertiesEl = element.Element("message-properties");
        if (messagePropertiesEl != null)
        {
            var properties = messagePropertiesEl.Elements("message-property").Select(mp =>
            {
                var mpProps = new List<string>
                {
                    $"Name = {PolicyDecompilerContext.Literal(mp.Attribute("name")?.Value ?? "")}",
                    $"Value = {context.HandleValue(PolicyDecompilerContext.GetElementText(mp), "MessagePropertyValue")}"
                };
                return $"new ServiceBusMessageProperty {{ {string.Join(", ", mpProps)} }}";
            });
            props.Add($"MessageProperties = new ServiceBusMessageProperty[] {{ {string.Join(", ", properties)} }}");
        }

        var payloadEl = element.Element("payload");
        if (payloadEl != null)
        {
            var payloadContent = PolicyDecompilerContext.GetElementText(payloadEl);
            props.Add($"Payload = {context.HandleValue(payloadContent, "Payload")}");
        }

        PolicyDecompilerContext.EmitConfigCall(writer, prefix, "SendServiceBusMessage", "SendServiceBusMessageConfig", props);
    }
}
