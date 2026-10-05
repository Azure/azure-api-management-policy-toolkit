// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;

/// <summary>
/// Defines a dimension for token usage metrics.
/// Each dimension is a key-value pair that adds context to the metric.
/// </summary>
public class MetricDimensionConfig
{
    /// <summary>
    /// Required. The name of the dimension. Can be a static value or a policy expression.
    /// </summary>
    [ExpressionAllowed]
    public required string Name { get; init; }

    /// <summary>
    /// The value for the dimension. Can be a static value or a policy expression.
    /// Can only be omitted if the name matches one of the default dimensions.
    /// Examples: "gpt-4", "completions", or expressions like "@(context.Request.Headers.GetValueOrDefault("x-operation-id"))".
    /// </summary>
    [ExpressionAllowed]
    public string? Value { get; init; }
}