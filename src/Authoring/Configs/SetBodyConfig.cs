// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.ApiManagement.PolicyToolkit.Authoring;

/// <summary>
/// Configuration for the set-body policy.<br />
/// Specifies template, xsi:nil, and parse date settings.
/// </summary>
public record SetBodyConfig
{
    /// <summary>
    /// Optional. Specifies a template to use for the body content.
    /// </summary>
    public string? Template { get; init; }

    /// <summary>
    /// Optional. Specifies whether to include xsi:nil attribute in the XML output.
    /// </summary>
    public string? XsiNil { get; init; }

    /// <summary>
    /// Optional. Specifies whether to parse date values in the body content.
    /// </summary>
    public bool? ParseDate { get; init; }
}