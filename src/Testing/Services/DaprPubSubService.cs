// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring.Expressions;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Services;

/// <summary>
/// Injected service for publish-to-dapr. Register an implementation with <see cref="ServiceRegistry"/>.
/// No Dapr runtime or network client is provided by the emulator.
/// </summary>
public interface IDaprPubSubService
{
    /// <summary>
    /// Publishes the content with the supplied component, topic, and optional template settings.
    /// Implementations own template rendering and return the Dapr response, including error responses.
    /// Responses stored in variables are normalized for gateway policies without consuming their body.
    /// Transport failures may be reported as <see cref="HttpRequestException"/>,
    /// <see cref="TimeoutException"/>, or <see cref="OperationCanceledException"/>.
    /// </summary>
    Task<IResponse> PublishAsync(DaprPublishRequest request, CancellationToken cancellationToken = default);
}

/// <summary>
/// Captures the evaluated configuration sent to a Dapr pub/sub service.
/// </summary>
/// <param name="PubSubName">Explicit component name, or the component prefix extracted from the topic.</param>
/// <param name="Topic">Topic name without an implicit component prefix.</param>
/// <param name="Content">Message content or template source.</param>
/// <param name="Template">Template engine to apply to content, or null for literal content.</param>
/// <param name="ContentType">Configured content type, or null when omitted.</param>
/// <param name="Timeout">
/// Publish deadline, converted from the authoring config's seconds; the default is five seconds.
/// </param>
public sealed record DaprPublishRequest(
    string PubSubName,
    string Topic,
    string Content,
    string? Template,
    string? ContentType,
    TimeSpan Timeout);
