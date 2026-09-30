// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.ObjectModel;

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Services;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Policies;

[
    Section(nameof(IInboundContext)),
    Section(nameof(IOutboundContext)),
    Section(nameof(IOnErrorContext))
]
internal class SendServiceBusMessageHandler : PolicyHandler<SendServiceBusMessageConfig>
{
    public override string PolicyName => nameof(IInboundContext.SendServiceBusMessage);

    protected override void Handle(GatewayContext context, SendServiceBusMessageConfig config)
    {
        if ((config.QueueName is null) == (config.TopicName is null))
        {
            throw new ArgumentException("Exactly one of QueueName or TopicName must be specified.", nameof(config));
        }

        if (config.QueueName is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(config.QueueName, nameof(config.QueueName));
        }

        if (config.TopicName is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(config.TopicName, nameof(config.TopicName));
        }

        if (config.Namespace is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(config.Namespace, nameof(config.Namespace));
        }

        if (config.ClientId is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(config.ClientId, nameof(config.ClientId));
        }

        ArgumentNullException.ThrowIfNull(config.Payload, nameof(config.Payload));

        var properties = new Dictionary<string, string>(StringComparer.Ordinal);
        if (config.MessageProperties is not null)
        {
            if (config.MessageProperties.Length == 0)
            {
                throw new ArgumentException("MessageProperties must not be empty when specified.", nameof(config.MessageProperties));
            }

            foreach (var property in config.MessageProperties)
            {
                if (property is null)
                {
                    throw new ArgumentException("Message properties cannot contain null items.", nameof(config.MessageProperties));
                }

                ArgumentException.ThrowIfNullOrWhiteSpace(property.Name, nameof(config.MessageProperties));
                ArgumentNullException.ThrowIfNull(property.Value, nameof(config.MessageProperties));
                if (!properties.TryAdd(property.Name, property.Value))
                {
                    throw new ArgumentException(
                        $"Duplicate message property '{property.Name}'.", nameof(config.MessageProperties));
                }
            }
        }

        var request = new ServiceBusMessageRequest(
            config.QueueName, config.TopicName, config.Namespace, config.ClientId,
            new ReadOnlyDictionary<string, string>(properties), config.Payload);
        var service = context.Services.Resolve<IServiceBusMessageService>() ?? throw new InvalidOperationException(
            "No IServiceBusMessageService registered. Register one via test.Context.Services.Register<IServiceBusMessageService>(service) " +
            "or use a policy callback override.");

        service.SendAsync(request).GetAwaiter().GetResult();
    }
}
