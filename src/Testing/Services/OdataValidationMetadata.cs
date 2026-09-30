// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Services;

/// <summary>
/// Explicit OData model for the operation under test, registered through
/// <c>test.Context.Services.Register(metadata)</c>. The built-in validator supports OData 4.0/4.01,
/// entity collections, single scalar keys, JSON payload schemas, and $top/$skip/$count/$select/$orderby/$format.
/// The built-in model permits only json/application/json $format values and quoted string/numeric/boolean key literals.
/// Encoded resource segments are separated before percent decoding, so encoded slashes remain key data.
/// Empty structural segments are rejected; actual navigation separators remain unsupported.
/// Navigation, functions, batches, and expression-based queries require an <see cref="IOdataRequestValidator"/>.
/// It never imports EDM/CSDL metadata or infers a model from an incoming request.
/// </summary>
public sealed record OdataValidationMetadata
{
    /// <summary>The absolute service root URI, including the API base path.</summary>
    public required Uri ServiceRoot { get; init; }

    /// <summary>Entity sets, using case-sensitive OData identifiers.</summary>
    public required IReadOnlyDictionary<string, OdataEntitySetMetadata> EntitySets { get; init; }
}

/// <summary>Explicit structural metadata for an OData entity set.</summary>
public sealed record OdataEntitySetMetadata
{
    /// <summary>The name of the single scalar key property.</summary>
    public required string KeyProperty { get; init; }

    /// <summary>Property definitions, including the key, using JSON scalar schemas.</summary>
    public required IReadOnlyDictionary<string, ApiParameterValidationMetadata> Properties { get; init; }

    /// <summary>Methods permitted on the entity collection.</summary>
    public IReadOnlyCollection<string> CollectionMethods { get; init; } = ["GET", "POST"];

    /// <summary>Methods permitted on an entity selected by key.</summary>
    public IReadOnlyCollection<string> EntityMethods { get; init; } = ["GET", "PUT", "PATCH", "DELETE"];

    /// <summary>The JSON body schema for write requests. A write cannot be validated without this schema.</summary>
    public ContentValidationSchema? PayloadSchema { get; init; }

    /// <summary>Explicitly permitted custom query parameters, in addition to supported system options.</summary>
    public IReadOnlyDictionary<string, ApiParameterValidationMetadata> QueryParameters { get; init; } =
        new Dictionary<string, ApiParameterValidationMetadata>();
}

/// <summary>
/// Extension point for full EDM/CSDL-aware validation. Register with
/// <c>test.Context.Services.Register&lt;IOdataRequestValidator&gt;(validator)</c>.
/// Implementations must perform model validation and return diagnostic failures, not a success-shaped
/// placeholder. Exceptions and null results are surfaced; the emulator performs size/version checks first.
/// </summary>
public interface IOdataRequestValidator
{
    /// <summary>Validates a resolved OData request, returning an empty list only for a conforming request.</summary>
    IReadOnlyList<string> Validate(OdataValidationRequest request);
}

/// <summary>Read-only inputs supplied to an injected OData validator.</summary>
/// <param name="Url">The actual request URL.</param>
/// <param name="Method">The actual HTTP method.</param>
/// <param name="Version">The resolved OData version, including the configured default.</param>
/// <param name="Headers">Request headers.</param>
/// <param name="Query">Decoded query parameter values.</param>
/// <param name="Body">The UTF-8 text payload represented by the emulator.</param>
public sealed record OdataValidationRequest(
    Uri Url, string Method, string Version,
    IReadOnlyDictionary<string, string[]> Headers,
    IReadOnlyDictionary<string, string[]> Query, string Body);