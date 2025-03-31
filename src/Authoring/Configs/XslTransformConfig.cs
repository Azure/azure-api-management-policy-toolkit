// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.ApiManagement.PolicyToolkit.Authoring;

/// <summary>
/// Configuration for the xsl-transform policy.<br />
/// Specifies the XSLT stylesheet and optional parameters.
/// </summary>
public record XslTransformConfig
{
    /// <summary>
    /// The XSLT stylesheet to use for the transformation. Policy expressions are allowed.
    /// </summary>
    [ExpressionAllowed]
    public required string StyleSheet { get; init; }

    /// <summary>
    /// Optional parameters to pass to the XSLT stylesheet.
    /// </summary>
    public XslTransformParameter[]? Parameters { get; init; }
}

/// <summary>
/// Represents a parameter to pass to the XSLT stylesheet.
/// </summary>
public record XslTransformParameter
{
    /// <summary>
    /// The name of the parameter.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// The value of the parameter. Policy expressions are allowed.
    /// </summary>
    [ExpressionAllowed]
    public required string Value { get; init; }
}