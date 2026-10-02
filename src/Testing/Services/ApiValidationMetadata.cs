// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Services;

/// <summary>
/// Explicit schema metadata for the operation under test. Register this instance with
/// <c>test.Context.Services.Register(metadata)</c>. The emulator does not import OpenAPI documents
/// or infer definitions from policy overrides, request values, or response values.
/// </summary>
public sealed record ApiValidationMetadata
{
    /// <summary>Concrete request media types and their body schemas. Comparisons ignore case and parameters; wildcard selection is unsupported.</summary>
    public IReadOnlyDictionary<string, ContentValidationSchema> RequestContent { get; init; } =
        new Dictionary<string, ContentValidationSchema>();

    /// <summary>Request header definitions. Header names are compared case-insensitively.</summary>
    public IReadOnlyDictionary<string, ApiParameterValidationMetadata> RequestHeaders { get; init; } =
        new Dictionary<string, ApiParameterValidationMetadata>();

    /// <summary>Query parameter definitions. Names are compared ordinally.</summary>
    public IReadOnlyDictionary<string, ApiParameterValidationMetadata> QueryParameters { get; init; } =
        new Dictionary<string, ApiParameterValidationMetadata>();

    /// <summary>Matched path parameter definitions. Names are compared ordinally.</summary>
    public IReadOnlyDictionary<string, ApiParameterValidationMetadata> PathParameters { get; init; } =
        new Dictionary<string, ApiParameterValidationMetadata>();

    /// <summary>Responses explicitly declared by the API, indexed by HTTP status code.</summary>
    public IReadOnlyDictionary<int, ApiResponseValidationMetadata> Responses { get; init; } =
        new Dictionary<int, ApiResponseValidationMetadata>();

    /// <summary>The OpenAPI default response, if present. It also permits otherwise unspecified status codes.</summary>
    public ApiResponseValidationMetadata? DefaultResponse { get; init; }
}

/// <summary>Body and header definitions for one API response.</summary>
public sealed record ApiResponseValidationMetadata
{
    /// <summary>Response media types and their body schemas.</summary>
    public IReadOnlyDictionary<string, ContentValidationSchema> Content { get; init; } =
        new Dictionary<string, ContentValidationSchema>();

    /// <summary>Response header definitions. Header names are compared case-insensitively.</summary>
    public IReadOnlyDictionary<string, ApiParameterValidationMetadata> Headers { get; init; } =
        new Dictionary<string, ApiParameterValidationMetadata>();
}

/// <summary>
/// A parameter's JSON schema and requiredness. Scalars require exactly one wire value; arrays
/// use separate wire values and an item schema. Complex object/style/explode serialization is unsupported.
/// </summary>
public sealed record ApiParameterValidationMetadata
{
    /// <summary>The scalar or array JSON schema for the parameter.</summary>
    public required string JsonSchema { get; init; }

    /// <summary>Whether the parameter must be present.</summary>
    public bool Required { get; init; }
}

/// <summary>
/// An inline API schema, or a named schema registered with
/// <c>test.Context.Services.Register("schema-id", schema)</c>.
/// A named schema bound to a concrete policy content type remains authoritative when unrelated
/// operation metadata is registered. API content definitions are used when SchemaId is omitted.
/// JSON validation supports local references, types, properties, required, additionalProperties,
/// enum/const, scalar bounds/formats, homogeneous arrays, and allOf/anyOf/oneOf/not.
/// Unsupported keywords/formats fail explicitly, including in unvisited properties.
/// Numbers must be exactly representable as decimal; schema evaluation is limited to 64 levels.
/// External references, tuple items, and reference assertion siblings are unsupported.
/// XML uses actual UTF-8 message bytes and a self-contained XSD with DTDs and external resolution disabled.
/// Document roots must be declared; lax wildcard warnings are not validation errors.
/// SOAP payload extraction retains all in-scope namespace bindings, including prefixes used only in QName values.
/// </summary>
/// <param name="Format">The schema format: json or xml. SOAP payloads use an xml schema.</param>
/// <param name="Definition">The JSON schema or XSD text.</param>
public sealed record ContentValidationSchema(string Format, string Definition);

/// <summary>Observable validation failure details stored in a policy's configured error variable.</summary>
/// <param name="Name">The media type, parameter/header name, or status code.</param>
/// <param name="Type">The validated entity, such as RequestBody, ResponseHeader, or QueryParameter.</param>
/// <param name="ValidationRule">The failed rule, such as SizeLimit, Unspecified, or IncorrectMessage.</param>
/// <param name="Details">Diagnostic details for test assertions; not copied into public response bodies.</param>
/// <param name="Action">The effective detect or prevent action.</param>
public sealed record SchemaValidationError(
    string Name, string Type, string ValidationRule, string Details, string Action);