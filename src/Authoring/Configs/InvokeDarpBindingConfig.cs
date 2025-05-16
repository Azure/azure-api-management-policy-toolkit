// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;

/// <summary>
/// Configuration for invoking a Dapr binding.
/// </summary>
public record InvokeDarpBindingConfig
{
    /// <summary>
    /// Specifies the name of the Dapr binding to invoke. This is a required property.
    /// </summary>
    [ExpressionAllowed]
    public required string Name { get; init; }

    /// <summary>
    /// Specifies the operation to perform on the Dapr binding.
    /// </summary>
    public string? Operation { get; init; }

    /// <summary>
    /// Indicates whether to ignore errors during the Dapr binding invocation.
    /// </summary>
    public bool? IgnoreError { get; init; }

    /// <summary>
    /// Specifies the name of the variable to store the Dapr binding response.
    /// </summary>
    public string? ResponseVariableName { get; init; }

    /// <summary>
    /// Specifies the timeout for the Dapr binding invocation in seconds.
    /// </summary>
    [ExpressionAllowed]
    public int? Timeout { get; init; }

    /// <summary>
    /// Specifies the template to use for the Dapr binding invocation.
    /// </summary>
    public string? Template { get; init; }

    /// <summary>
    /// Specifies the content type for the Dapr binding invocation.
    /// </summary>
    public string? ContentType { get; init; }

    /// <summary>
    /// Specifies the metadata for the Dapr binding invocation.
    /// </summary>
    public DarpMetaData[]? MetaData { get; init; }

    /// <summary>
    /// Specifies the data for the Dapr binding invocation.
    /// </summary>
    [ExpressionAllowed]
    public string? Data { get; init; }
}

/// <summary>
/// Represents metadata for a Dapr binding invocation.
/// </summary>
public record DarpMetaData
{
    /// <summary>
    /// Specifies the key of the Dapr metadata item. This is a required property.
    /// </summary>
    public required string Key { get; init; }

    /// <summary>
    /// Specifies the value of the Dapr metadata item. This is a required property.
    /// </summary>
    [ExpressionAllowed]
    public required string Value { get; init; }
}