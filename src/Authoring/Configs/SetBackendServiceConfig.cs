// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.ApiManagement.PolicyToolkit.Authoring;

public record SetBackendServiceConfig
{
    public string? BaseUrl { get; init; }
    public string? BackendId { get; init; }
    public bool? SfResolveCondition { get; init; }
    public string? SfServiceInstanceName { get; init; }
    public string? SfPartitionKey { get; init; }
    public string? SfListenerName { get; init; }
}