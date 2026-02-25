// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;

/// <summary>
/// Configuration for the <a href="https://learn.microsoft.com/en-us/azure/api-management/send-service-bus-message-policy">send-service-bus-message</a> policy.<br/>
/// Sends a message to an Azure Service Bus queue or topic.
/// </summary>
public record SendServiceBusMessageConfig
{
    /// <summary>
    /// Optional. Specifies the name of the service bus queue to send the message to.
    /// Either QueueName or TopicName must be specified, but not both. Policy expressions are allowed.
    /// </summary>
    [ExpressionAllowed]
    public string? QueueName { get; init; }

    /// <summary>
    /// Optional. Specifies the name of the service bus topic to send the message to.
    /// Either QueueName or TopicName must be specified, but not both. Policy expressions are allowed.
    /// </summary>
    [ExpressionAllowed]
    public string? TopicName { get; init; }

    /// <summary>
    /// Optional. Specifies the fully qualified domain name of the service bus namespace. Policy expressions are allowed.
    /// </summary>
    [ExpressionAllowed]
    public string? Namespace { get; init; }

    /// <summary>
    /// Optional. Specifies the client ID of the user-assigned managed identity to authenticate with service bus.
    /// If not specified, the system-assigned identity is used. Policy expressions are allowed.
    /// </summary>
    [ExpressionAllowed]
    public string? ClientId { get; init; }

    /// <summary>
    /// Optional. A collection of message properties to pass with the message payload.
    /// </summary>
    public ServiceBusMessageProperty[]? MessageProperties { get; init; }

    /// <summary>
    /// Specifies the message payload to send to the service bus. Policy expressions are allowed.
    /// </summary>
    [ExpressionAllowed]
    public required string Payload { get; init; }
}

/// <summary>
/// Represents a message property for the send-service-bus-message policy.<br/>
/// Each property is a name-value pair sent as metadata with the message payload.
/// </summary>
public record ServiceBusMessageProperty
{
    /// <summary>
    /// Specifies the name of the message property.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Specifies the value of the message property. Policy expressions are allowed.
    /// </summary>
    [ExpressionAllowed]
    public required string Value { get; init; }
}
