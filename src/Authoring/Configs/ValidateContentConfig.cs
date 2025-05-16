// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;

/// <summary>
/// Configuration for the validate-content policy.<br/>
/// Specifies the rules for validating the content of requests or responses.
/// </summary>
public record ValidateContentConfig
{
    /// <summary>
    /// Action to take when the content type is unspecified.
    /// </summary>
    [ExpressionAllowed]
    public required string UnspecifiedContentTypeAction { get; init; }

    /// <summary>
    /// Maximum allowed size of the content in bytes.
    /// </summary>
    [ExpressionAllowed]
    public required int MaxSize { get; init; }

    /// <summary>
    /// Action to take when the content size exceeds the maximum allowed size.
    /// </summary>
    [ExpressionAllowed]
    public required string SizeExceededAction { get; init; }

    /// <summary>
    /// Optional variable name to store validation errors.
    /// </summary>
    public string? ErrorsVariableName { get; init; }

    /// <summary>
    /// Optional mapping of content types to validation rules.
    /// </summary>
    public ContentTypeMapConfig? ContentTypeMap { get; init; }

    /// <summary>
    /// Optional array of content validation rules.
    /// </summary>
    public ValidateContent[]? Contents { get; init; }
}

/// <summary>
/// Configuration for mapping content types to validation rules.
/// </summary>
public record ContentTypeMapConfig
{
    /// <summary>
    /// Value to use when any content type is allowed.
    /// </summary>
    public string? AnyContentTypeValue { get; init; }

    /// <summary>
    /// Value to use when the content type is missing.
    /// </summary>
    public string? MissingContentTypeValue { get; init; }

    /// <summary>
    /// Array of content type mappings.
    /// </summary>
    public ContentTypeMap[]? Types { get; init; }
}

/// <summary>
/// Mapping of a content type to a validation rule.
/// </summary>
public record ContentTypeMap
{
    /// <summary>
    /// The content type to map from.
    /// </summary>
    public string? From { get; init; }

    /// <summary>
    /// The content type to map to.
    /// </summary>
    public required string To { get; init; }

    /// <summary>
    /// Optional condition for when the mapping should be applied.
    /// </summary>
    public bool? When { get; init; }
}

/// <summary>
/// Validation rule for a specific content type.
/// </summary>
public record ValidateContent
{
    /// <summary>
    /// The type of validation to perform.
    /// </summary>
    public required string ValidateAs { get; init; }

    /// <summary>
    /// Action to take when validation fails.
    /// </summary>
    public required string Action { get; init; }

    /// <summary>
    /// Optional content type to validate.
    /// </summary>
    public string? Type { get; init; }

    /// <summary>
    /// Optional schema ID for validation.
    /// </summary>
    public string? SchemaId { get; init; }

    /// <summary>
    /// Optional schema reference for validation.
    /// </summary>
    public string? SchemaRef { get; init; }

    /// <summary>
    /// Optional flag to allow additional properties.
    /// </summary>
    public bool? AllowAdditionalProperties { get; init; }

    /// <summary>
    /// Optional flag for case-insensitive property names.
    /// </summary>
    public bool? CaseInsensitivePropertyNames { get; init; }
}