// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.ApiManagement.PolicyToolkit.Authoring;

/// <summary>
/// Configuration for the set-backend-service policy.<br/>
/// Specifies the backend service details, including base URL, backend ID, Service Fabric settings, and Dapr settings.<br/>
/// Compiled to <a href="https://learn.microsoft.com/en-us/azure/api-management/set-backend-service-policy">set-backend-service</a> policy.
/// </summary>
public record SetBackendServiceConfig
{
    /// <summary>
    /// Specifies the base URL of the backend service. Policy expressions are allowed.
    /// </summary>
    [ExpressionAllowed]
    public string? BaseUrl { get; init; }

    /// <summary>
    /// Specifies the backend ID of the backend service. Policy expressions are allowed.
    /// </summary>
    [ExpressionAllowed]
    public string? BackendId { get; init; }

    /// <summary>
    /// Specifies the condition to resolve the Service Fabric service. Policy expressions are allowed.
    /// </summary>
    [ExpressionAllowed]
    public bool? SfResolveCondition { get; init; }

    /// <summary>
    /// Specifies the Service Fabric service instance name. Policy expressions are allowed.
    /// </summary>
    [ExpressionAllowed]
    public string? SfServiceInstanceName { get; init; }

    /// <summary>
    /// Specifies the Service Fabric partition key. Policy expressions are allowed.
    /// </summary>
    [ExpressionAllowed]
    public string? SfPartitionKey { get; init; }

    /// <summary>
    /// Specifies the Service Fabric listener name. Policy expressions are allowed.
    /// </summary>
    [ExpressionAllowed]
    public string? SfListenerName { get; init; }

    /// <summary>
    /// Specifies the Dapr application ID. Policy expressions are allowed.
    /// </summary>
    [ExpressionAllowed]
    public string? DaprAppId { get; init; }

    /// <summary>
    /// Specifies the Dapr method to invoke. Policy expressions are allowed.
    /// </summary>
    [ExpressionAllowed]
    public string? DaprMethod { get; init; }

    /// <summary>
    /// Specifies the Dapr namespace. Policy expressions are allowed.
    /// </summary>
    [ExpressionAllowed]
    public string? DaprNamespace { get; init; }
}