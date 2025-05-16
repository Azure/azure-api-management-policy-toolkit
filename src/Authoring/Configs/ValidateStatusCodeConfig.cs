// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;

/// <summary>
/// Configuration for the validate-status-code policy.<br/>
/// Specifies the validation rules for status codes, including actions for specified and unspecified status codes.
/// </summary>
public record ValidateStatusCodeConfig
{
    /// <summary>
    /// Action to take for unspecified status codes. Possible values are "allow" or "deny".
    /// </summary>
    [ExpressionAllowed]
    public required string UnspecifiedStatusCodeAction { get; init; }

    /// <summary>
    /// Optional variable name to store validation errors.
    /// </summary>
    public string? ErrorVariableName { get; init; }

    /// <summary>
    /// List of status codes to validate.
    /// </summary>
    public ValidateStatusCode[]? StatusCodes { get; init; }
}

/// <summary>
/// Represents a status code to validate.
/// </summary>
public record ValidateStatusCode
{
    /// <summary>
    /// The status code to validate.
    /// </summary>
    public required uint Code { get; init; }

    /// <summary>
    /// Action to take for this status code. Possible values are "allow" or "deny".
    /// </summary>
    [ExpressionAllowed]
    public required string Action { get; init; }
}