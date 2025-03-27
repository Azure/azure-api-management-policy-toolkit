// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.ApiManagement.PolicyToolkit.Authoring;

public record ValidateParametersConfig
{
    [ExpressionAllowed] public required string SpecifiedParameterAction { get; init; }
    [ExpressionAllowed] public required string UnspecifiedParameterAction { get; init; }
    public string? ErrorsVariableName { get; init; }

    public ValidateHeaderParameters? Headers { get; init; }
    public ValidateQueryParameters? Query { get; init; }
    public ValidatePathParameters? Path { get; init; }
}

public record ValidateHeaderParameters
{
    [ExpressionAllowed] public required string SpecifiedParameterAction { get; init; }
    [ExpressionAllowed] public required string UnspecifiedParameterAction { get; init; }
    public ValidateParameter[]? Parameters { get; init; }
}

public record ValidateQueryParameters
{
    [ExpressionAllowed] public required string SpecifiedParameterAction { get; init; }
    [ExpressionAllowed] public required string UnspecifiedParameterAction { get; init; }
    public ValidateParameter[]? Parameters { get; init; }
}

public record ValidatePathParameters
{
    [ExpressionAllowed] public required string SpecifiedParameterAction { get; init; }
    public ValidateParameter[]? Parameters { get; init; }
}

public record ValidateParameter
{
    public required string Name { get; init; }
    [ExpressionAllowed] public required string Action { get; init; }
}