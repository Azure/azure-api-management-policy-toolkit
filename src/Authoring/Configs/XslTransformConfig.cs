// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.ApiManagement.PolicyToolkit.Authoring;

public record XslTransformConfig
{
    [ExpressionAllowed] public required string StyleSheet { get; init; }
    public XslTransformParameter[]? Parameters { get; init; }
}

public record XslTransformParameter
{
    public required string Name { get; init; }
    [ExpressionAllowed] public required string Value { get; init; }
}