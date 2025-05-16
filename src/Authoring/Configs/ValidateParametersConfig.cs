// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;

/// <summary>
/// Configuration for the validate-parameters policy.<br/>
/// Specifies the validation rules for headers, query parameters, and path parameters, including actions for specified and unspecified parameters.
/// </summary>
public record ValidateParametersConfig
{
    /// <summary>
    /// Action to take for specified parameters. Policy expressions are allowed.
    /// </summary>
    [ExpressionAllowed]
    public required string SpecifiedParameterAction { get; init; }

    /// <summary>
    /// Action to take for unspecified parameters. Policy expressions are allowed.
    /// </summary>
    [ExpressionAllowed]
    public required string UnspecifiedParameterAction { get; init; }

    /// <summary>
    /// Optional variable name to store validation errors.
    /// </summary>
    public string? ErrorsVariableName { get; init; }

    /// <summary>
    /// Validation rules for header parameters.
    /// </summary>
    public ValidateHeaderParameters? Headers { get; init; }

    /// <summary>
    /// Validation rules for query parameters.
    /// </summary>
    public ValidateQueryParameters? Query { get; init; }

    /// <summary>
    /// Validation rules for path parameters.
    /// </summary>
    public ValidatePathParameters? Path { get; init; }
}

/// <summary>
/// Configuration for validating header parameters.
/// </summary>
public record ValidateHeaderParameters
{
    /// <summary>
    /// Action to take for specified header parameters. Policy expressions are allowed.
    /// </summary>
    [ExpressionAllowed]
    public required string SpecifiedParameterAction { get; init; }

    /// <summary>
    /// Action to take for unspecified header parameters. Policy expressions are allowed.
    /// </summary>
    [ExpressionAllowed]
    public required string UnspecifiedParameterAction { get; init; }

    /// <summary>
    /// List of header parameters to validate.
    /// </summary>
    public ValidateParameter[]? Parameters { get; init; }
}

/// <summary>
/// Configuration for validating query parameters.
/// </summary>
public record ValidateQueryParameters
{
    /// <summary>
    /// Action to take for specified query parameters. Policy expressions are allowed.
    /// </summary>
    [ExpressionAllowed]
    public required string SpecifiedParameterAction { get; init; }

    /// <summary>
    /// Action to take for unspecified query parameters. Policy expressions are allowed.
    /// </summary>
    [ExpressionAllowed]
    public required string UnspecifiedParameterAction { get; init; }

    /// <summary>
    /// List of query parameters to validate.
    /// </summary>
    public ValidateParameter[]? Parameters { get; init; }
}

/// <summary>
/// Configuration for validating path parameters.
/// </summary>
public record ValidatePathParameters
{
    /// <summary>
    /// Action to take for specified path parameters. Policy expressions are allowed.
    /// </summary>
    [ExpressionAllowed]
    public required string SpecifiedParameterAction { get; init; }

    /// <summary>
    /// List of path parameters to validate.
    /// </summary>
    public ValidateParameter[]? Parameters { get; init; }
}

/// <summary>
/// Configuration for a single parameter validation.
/// </summary>
public record ValidateParameter
{
    /// <summary>
    /// Name of the parameter to validate.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Action to take for the parameter. Policy expressions are allowed.
    /// </summary>
    [ExpressionAllowed]
    public required string Action { get; init; }
}