// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.ApiManagement.PolicyToolkit.Authoring;

/// <summary>
/// Configuration for the LLM and Azure OpenAI emit token metric policies to emit metrics about token usage.
/// </summary>
public record EmitTokenMetricConfig
{
    /// <summary>
    /// Required. The dimensions to include with the metric.<br/>
    /// These dimensions can be used to filter and group the metrics in monitoring systems.
    /// </summary>
    [ExpressionAllowed]
    public required MetricDimensionConfig[] Dimensions { get; init; }

    /// <summary>
    /// Optional. The namespace to use for the metrics.<br/>
    /// If not specified, the default namespace is used.
    /// </summary>
    [ExpressionAllowed]
    public string? Namespace { get; init; }
}