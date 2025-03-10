// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.ApiManagement.PolicyToolkit.Authoring;

public record LogToEventHubConfig
{
    public required string LoggerId { get; init; }
    public required string Value { get; init; }
    public string? PartitionId { get; init; }
    public string? PartitionKey { get; init; }
}