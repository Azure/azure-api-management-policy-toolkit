// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.ApiManagement.PolicyToolkit.Authoring;

public record LogToEventHubConfig
{
    /// <summary>
    /// Name of a logger entity that has been configured in API Management. Policy expressions are allowed.
    /// </summary>
    [ExpressionAllowed]
    public required string LoggerId { get; init; }

    /// <summary>
    /// The message to be logged to the event hub. Policy expressions are allowed.
    /// </summary>
    [ExpressionAllowed]
    public required string Value { get; init; }

    /// <summary>
    /// Optional. Routes messages into specific partitions in the event hub. Policy expressions aren't allowed.
    /// </summary>
    public string? PartitionId { get; init; }

    /// <summary>
    /// Optional. Used to compute a hash to map to a partition. Policy expressions are allowed.
    /// </summary>
    [ExpressionAllowed]
    public string? PartitionKey { get; init; }
}