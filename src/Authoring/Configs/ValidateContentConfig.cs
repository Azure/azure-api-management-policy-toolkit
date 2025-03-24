// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.ApiManagement.PolicyToolkit.Authoring;

public record ValidateContentConfig
{
    public required string UnspecifiedContentTypeAction { get; init; }
    public required int MaxSize { get; init; }
    public required string SizeExceededAction { get; init; }
    public string? ErrorsVariableName { get; init; }

    public ContentTypeMapConfig? ContentTypeMap { get; init; }
    public ValidateContent[]? Contents { get; init; }
}

public record ContentTypeMapConfig
{
    public string? AnyContentTypeValue { get; init; }
    public string? MissingContentTypeValue { get; init; }
    public ContentTypeMap[]? Types { get; init; }
}

public record ContentTypeMap
{
    public required string To { get; init; }
    public string? From { get; init; }
    public bool? When { get; init; }
}

public record ValidateContent
{
    public required string ValidateAs { get; init; }
    public required string Action { get; init; }
    public string? Type { get; init; }
    public string? SchemaId { get; init; }
    public string? SchemaRef { get; init; }
    public bool? AllowAdditionalProperties { get; init; }
    public bool? CaseInsensitivePropertyNames { get; init; }
}