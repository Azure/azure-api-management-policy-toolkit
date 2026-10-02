// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Services;

/// <summary>
/// Injected service for send-service-bus-message. Register an implementation with <see cref="ServiceRegistry"/>.
/// No Service Bus client or managed identity authentication is performed by the emulator.
/// </summary>
public interface IServiceBusMessageService
{
    /// <summary>
    /// Sends the evaluated message to its queue or topic. A null client ID selects the system-assigned identity.
    /// Implementations own namespace resolution and identity simulation, and report send failures by throwing.
    /// </summary>
    Task SendAsync(ServiceBusMessageRequest request, CancellationToken cancellationToken = default);
}

/// <summary>
/// Captures the evaluated configuration sent to a Service Bus message service.
/// </summary>
/// <param name="QueueName">Queue destination, or null when targeting a topic.</param>
/// <param name="TopicName">Topic destination, or null when targeting a queue.</param>
/// <param name="Namespace">Optional namespace FQDN, passed through without inventing a default.</param>
/// <param name="ClientId">User-assigned managed identity client ID, or null for system-assigned identity.</param>
/// <param name="MessageProperties">A case-sensitive snapshot of message properties.</param>
/// <param name="Payload">Message payload.</param>
public sealed record ServiceBusMessageRequest(
    string? QueueName,
    string? TopicName,
    string? Namespace,
    string? ClientId,
    IReadOnlyDictionary<string, string> MessageProperties,
    string Payload);
