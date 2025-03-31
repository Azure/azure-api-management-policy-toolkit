// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.ApiManagement.PolicyToolkit.Authoring;

/// <summary>
/// Configuration for the validate-headers policy.<br/>
/// Specifies the validation rules for headers, including actions for specified and unspecified headers.
/// </summary>
public record ValidateHeadersConfig
{
    /// <summary>
    /// Action to take for specified headers. Possible values are "allow" or "deny".
    /// </summary>
    public required string SpecifiedHeaderAction { get; init; }

    /// <summary>
    /// Action to take for unspecified headers. Possible values are "allow" or "deny".
    /// </summary>
    public required string UnspecifiedHeaderAction { get; init; }

    /// <summary>
    /// Optional variable name to store validation errors.
    /// </summary>
    public string? ErrorsVariableName { get; init; }

    /// <summary>
    /// List of headers to validate.
    /// </summary>
    public ValidateHeader[]? Headers { get; init; }
}

/// <summary>
/// Represents a header to validate.
/// </summary>
public record ValidateHeader
{
    /// <summary>
    /// Name of the header to validate.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Action to take for this header. Possible values are "allow" or "deny".
    /// </summary>
    public required string Action { get; init; }
}