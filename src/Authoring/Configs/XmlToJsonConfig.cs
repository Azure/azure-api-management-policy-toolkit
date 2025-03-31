// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.ApiManagement.PolicyToolkit.Authoring;

/// <summary>
/// Configuration for the xml-to-json policy.
/// </summary>
public record XmlToJsonConfig
{
    /// <summary>
    /// Specifies the kind of transformation to apply.
    /// </summary>
    public required string Kind { get; init; }

    /// <summary>
    /// Specifies how to apply the transformation.
    /// </summary>
    public required string Apply { get; init; }

    /// <summary>
    /// Optional. Specifies whether to consider the Accept header when applying the transformation.
    /// </summary>
    public bool? ConsiderAcceptHeader { get; init; }

    /// <summary>
    /// Optional. Specifies whether to always treat child elements as arrays.
    /// </summary>
    public bool? AlwaysArrayChildElements { get; init; }
}