// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.ApiManagement.PolicyToolkit.Authoring;

public record ClaimConfig
{
    public required string Name { get; init; }
    [ExpressionAllowed] public string? Match { get; init; }
    public string? Separator { get; init; }
    public string[]? Values { get; init; }
}