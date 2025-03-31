// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.ApiManagement.PolicyToolkit.Authoring;

/// <summary>
/// Configuration for the set-body policy with content.<br />
/// Inherits from SetBodyConfig.
/// </summary>
public record BodyConfig : SetBodyConfig
{
    /// <summary>
    /// Required. Specifies the content to set as the body.
    /// </summary>
    [ExpressionAllowed]
    public required object? Content { get; init; }
}