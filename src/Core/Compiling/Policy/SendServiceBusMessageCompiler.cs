// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Xml.Linq;

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling.Diagnostics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling.Policy;

public class SendServiceBusMessageCompiler : IMethodPolicyHandler
{
    public string MethodName => nameof(IInboundContext.SendServiceBusMessage);

    public void Handle(IDocumentCompilationContext context, InvocationExpressionSyntax node)
    {
        if (!node.TryExtractingConfigParameter<SendServiceBusMessageConfig>(context, "send-service-bus-message",
                out IReadOnlyDictionary<string, InitializerValue>? values))
        {
            return;
        }

        var element = new XElement("send-service-bus-message");

        bool addedQueueName =
            element.AddAttribute(values, nameof(SendServiceBusMessageConfig.QueueName), "queue-name");
        bool addedTopicName =
            element.AddAttribute(values, nameof(SendServiceBusMessageConfig.TopicName), "topic-name");

        if (!(addedQueueName ^ addedTopicName))
        {
            context.Report(Diagnostic.Create(
                CompilationErrors.OnlyOneOfTwoShouldBeDefined,
                node.GetLocation(),
                "send-service-bus-message",
                nameof(SendServiceBusMessageConfig.QueueName),
                nameof(SendServiceBusMessageConfig.TopicName)
            ));
            return;
        }

        element.AddAttribute(values, nameof(SendServiceBusMessageConfig.Namespace), "namespace");
        element.AddAttribute(values, nameof(SendServiceBusMessageConfig.ClientId), "client-id");

        if (values.TryGetValue(nameof(SendServiceBusMessageConfig.MessageProperties), out var messageProperties))
        {
            var messagePropertiesElement = new XElement("message-properties");
            var items = messageProperties.UnnamedValues ?? [];
            if (items.Count == 0)
            {
                context.Report(Diagnostic.Create(
                    CompilationErrors.RequiredParameterIsEmpty,
                    messageProperties.Node.GetLocation(),
                    "send-service-bus-message",
                    nameof(SendServiceBusMessageConfig.MessageProperties)
                ));
                return;
            }

            foreach (var property in items)
            {
                if (!property.TryGetValues<ServiceBusMessageProperty>(out var propertyValues))
                {
                    context.Report(Diagnostic.Create(
                        CompilationErrors.PolicyArgumentIsNotOfRequiredType,
                        property.Node.GetLocation(),
                        "send-service-bus-message.message-property",
                        nameof(ServiceBusMessageProperty)
                    ));
                    continue;
                }

                if (!propertyValues.TryGetValue(nameof(ServiceBusMessageProperty.Name), out var nameValue) ||
                    nameValue.Value is null)
                {
                    context.Report(Diagnostic.Create(
                        CompilationErrors.RequiredParameterNotDefined,
                        property.Node.GetLocation(),
                        "send-service-bus-message.message-property",
                        nameof(ServiceBusMessageProperty.Name)
                    ));
                    continue;
                }

                if (!propertyValues.TryGetValue(nameof(ServiceBusMessageProperty.Value), out var valueValue) ||
                    valueValue.Value is null)
                {
                    context.Report(Diagnostic.Create(
                        CompilationErrors.RequiredParameterNotDefined,
                        property.Node.GetLocation(),
                        "send-service-bus-message.message-property",
                        nameof(ServiceBusMessageProperty.Value)
                    ));
                    continue;
                }

                var messagePropertyElement = new XElement("message-property");
                messagePropertyElement.SetAttributeValue("name", nameValue.Value);
                messagePropertyElement.Add(valueValue.Value);
                messagePropertiesElement.Add(messagePropertyElement);
            }

            element.Add(messagePropertiesElement);
        }

        if (!values.TryGetValue(nameof(SendServiceBusMessageConfig.Payload), out var payload) ||
            payload.Value is null)
        {
            context.Report(Diagnostic.Create(
                CompilationErrors.RequiredParameterNotDefined,
                node.GetLocation(),
                "send-service-bus-message",
                nameof(SendServiceBusMessageConfig.Payload)
            ));
            return;
        }

        element.Add(new XElement("payload", payload.Value));

        context.AddPolicy(element);
    }
}
