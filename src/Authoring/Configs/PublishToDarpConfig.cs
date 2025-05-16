// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;

/// <summary>
/// Configuration for the publish-to-dapr policy.<br />
/// Specifies the topic, content, and other optional settings for publishing a message to a Dapr topic.
/// </summary>
public record PublishToDarpConfig
{
    /// <summary>
    /// Specifies the topic to which the message will be published. Policy expressions are allowed.
    /// </summary>
    [ExpressionAllowed]
    public required string Topic { get; init; }

    /// <summary>
    /// Specifies the content of the message to be published. Policy expressions are allowed.
    /// </summary>
    [ExpressionAllowed]
    public required string Content { get; init; }

    /// <summary>
    /// Specifies the name of the Dapr pub/sub component. Policy expressions are allowed.
    /// </summary>
    [ExpressionAllowed]
    public string? PubSubName { get; init; }

    /// <summary>
    /// Specifies whether to ignore errors during message publishing.
    /// </summary>
    public bool? IgnoreError { get; init; }

    /// <summary>
    /// Specifies the name of the variable to store the response from the Dapr pub/sub component.
    /// </summary>
    public string? ResponseVariableName { get; init; }

    /// <summary>
    /// Specifies the timeout in milliseconds for the publish operation.
    /// </summary>
    public int? Timeout { get; init; }

    /// <summary>
    /// Specifies the template to use for the message content.
    /// </summary>
    public string? Template { get; init; }

    /// <summary>
    /// Specifies the content type of the message.
    /// </summary>
    public string? ContentType { get; init; }
}