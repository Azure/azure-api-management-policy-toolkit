// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;

/// <summary>
/// Configuration for a required claim in the JWT.
/// </summary>
public record ClaimConfig
{
    /// <summary>
    /// Specifies the name of the claim. Policy expressions are allowed.
    /// </summary>
    [ExpressionAllowed]
    public required string Name { get; init; }

    /// <summary>
    /// Specifies the match expression for the claim value. Policy expressions are allowed.
    /// </summary>
    [ExpressionAllowed]
    public string? Match { get; init; }

    /// <summary>
    /// Specifies the separator for multiple claim values. Policy expressions are allowed.
    /// </summary>
    [ExpressionAllowed]
    public string? Separator { get; init; }

    /// <summary>
    /// Specifies the allowed values for the claim.
    /// </summary>
    public string[]? Values { get; init; }
}