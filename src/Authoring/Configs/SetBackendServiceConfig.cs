// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.ApiManagement.PolicyToolkit.Authoring;

public record SetBackendServiceConfig
{
    [ExpressionAllowed] public string? BaseUrl { get; init; }
    [ExpressionAllowed] public string? BackendId { get; init; }
    [ExpressionAllowed] public bool? SfResolveCondition { get; init; }
    [ExpressionAllowed] public string? SfServiceInstanceName { get; init; }
    [ExpressionAllowed] public string? SfPartitionKey { get; init; }
    [ExpressionAllowed] public string? SfListenerName { get; init; }
    [ExpressionAllowed] public string? DaprAppId { get; init; }
    [ExpressionAllowed] public string? DaprMethod { get; init; }
    [ExpressionAllowed] public string? DaprNamespace { get; init; }
}