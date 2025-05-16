// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;

/// <summary>
/// Configuration for the json-to-xml policy.
/// </summary>
public record JsonToXmlConfig
{
    /// <summary>
    /// Specifies when the policy should be applied. Policy expressions are allowed.
    /// </summary>
    [ExpressionAllowed]
    public required string Apply { get; init; }

    /// <summary>
    /// Specifies whether to consider the Accept header when determining if the policy should be applied. Policy expressions are allowed.
    /// </summary>
    [ExpressionAllowed]
    public bool? ConsiderAcceptHeader { get; init; }

    /// <summary>
    /// Specifies whether to parse dates in the JSON content. Policy expressions are allowed.
    /// </summary>
    [ExpressionAllowed]
    public bool? ParseDate { get; init; }

    /// <summary>
    /// Specifies the character to use as a namespace separator in the XML output. Policy expressions are allowed.
    /// </summary>
    [ExpressionAllowed]
    public char? NamespaceSeparator { get; init; }

    /// <summary>
    /// Specifies the prefix to use for namespaces in the XML output. Policy expressions are allowed.
    /// </summary>
    [ExpressionAllowed]
    public string? NamespacePrefix { get; init; }

    /// <summary>
    /// Specifies the name of the block that contains attributes in the XML output. Policy expressions are allowed.
    /// </summary>
    [ExpressionAllowed]
    public string? AttributeBlockName { get; init; }
}