// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.ApiManagement.PolicyToolkit.Authoring;

/// <summary>
/// Configuration for the emit-metric policy which emits custom metrics to Azure Monitor.
/// This policy allows monitoring API usage patterns and performance data.
/// </summary>
public record EmitMetricConfig
{
    /// <summary>
    /// Required. Specifies the name of the metric.
    /// The metric appears in Azure Monitor with this name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Required. Specifies dimensions to include with the metric.
    /// Dimensions provide additional context and filtering capabilities for the metric in Azure Monitor.
    /// </summary>
    public required MetricDimensionConfig[] Dimensions { get; init; }

    /// <summary>
    /// Optional. Specifies the metric namespace. 
    /// Defaults to "apim" if not specified.
    /// </summary>
    public string? Namespace { get; init; }

    /// <summary>
    /// Optional. Specifies the metric value.
    /// Defaults to 1 if not specified.
    /// </summary>
    [ExpressionAllowed]
    public double? Value { get; init; }
}