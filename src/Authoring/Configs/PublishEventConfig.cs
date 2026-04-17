// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;

/// <summary>
/// Configuration for the publish-event policy.<br />
/// Specifies targets to which GraphQL subscription events are published.
/// </summary>
public record PublishEventConfig
{
    /// <summary>
    /// Specifies an array of GraphQL subscription configurations to target.
    /// </summary>
    public required GraphqlSubscriptionConfig[] Subscriptions { get; init; }
}

/// <summary>
/// Configuration for a single GraphQL subscription target within a publish-event policy.
/// </summary>
public record GraphqlSubscriptionConfig
{
    /// <summary>
    /// Specifies the identifier of the GraphQL subscription.
    /// </summary>
    public required string Id { get; init; }
}
