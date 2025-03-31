// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.ApiManagement.PolicyToolkit.Authoring;

public record HeaderConfig
{
    /// <summary>
    /// Specifies the name of the header. Policy expressions are allowed.
    /// </summary>
    [ExpressionAllowed]
    public required string Name { get; init; }

    /// <summary>
    /// Specifies the action to take if the header already exists. Possible values are "override" and "skip". Policy expressions are allowed.
    /// </summary>
    [ExpressionAllowed]
    public string? ExistsAction { get; init; }

    /// <summary>
    /// Specifies the values of the header to be set. Policy expressions are allowed.
    /// </summary>
    [ExpressionAllowed]
    public string[]? Values { get; init; }
}