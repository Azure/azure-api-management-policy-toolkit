// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.RegularExpressions;

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Services;

internal static class OdataRequestValidation
{
    private static readonly HashSet<string> s_edmQueries =
        ["$filter", "$expand", "$search", "$apply", "$compute", "$skiptoken", "$deltatoken", "$index"];

    internal static IReadOnlyList<string> Validate(OdataValidationRequest request, OdataValidationMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(metadata.ServiceRoot);
        ArgumentNullException.ThrowIfNull(metadata.EntitySets);
        if (!metadata.ServiceRoot.IsAbsoluteUri || metadata.ServiceRoot.Scheme is not ("http" or "https") ||
            metadata.ServiceRoot.Query.Length != 0 || metadata.ServiceRoot.Fragment.Length != 0)
        {
            throw new ArgumentException("OData ServiceRoot must be an absolute HTTP(S) URI without a query or fragment.");
        }
        var entitySets = ApiSchemaValidation.Definitions(metadata.EntitySets, StringComparer.Ordinal);
        foreach (var entity in entitySets.Values)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(entity.KeyProperty);
            var properties = ApiSchemaValidation.Definitions(entity.Properties, StringComparer.Ordinal);
            if (!properties.ContainsKey(entity.KeyProperty))
            {
                throw new ArgumentException($"OData key property '{entity.KeyProperty}' is missing from the model.");
            }
            ArgumentNullException.ThrowIfNull(entity.CollectionMethods);
            ArgumentNullException.ThrowIfNull(entity.EntityMethods);
            if (entity.CollectionMethods.Concat(entity.EntityMethods).Any(string.IsNullOrWhiteSpace))
            {
                throw new ArgumentException("OData permitted methods must be nonempty HTTP method names.");
            }
        }

        var root = metadata.ServiceRoot.AbsolutePath.TrimEnd('/');
        var path = request.Url.AbsolutePath;
        if (request.Url.Scheme != metadata.ServiceRoot.Scheme ||
            !string.Equals(request.Url.Host, metadata.ServiceRoot.Host, StringComparison.OrdinalIgnoreCase) ||
            request.Url.Port != metadata.ServiceRoot.Port ||
            path != root && !path.StartsWith(root + "/", StringComparison.Ordinal))
        {
            return ["The request URL is outside the configured OData service root."];
        }
        var encodedRelative = path == root ? string.Empty : path[(root.Length + 1)..];
        var segments = encodedRelative.Split('/');
        if (segments.Length > 1)
        {
            if (segments.Any(string.IsNullOrEmpty))
            {
                return ["The OData resource path contains an empty segment."];
            }
            throw Unsupported("navigation/property paths");
        }
        var relative = Uri.UnescapeDataString(segments[0]);
        if (relative is "" or "$metadata")
        {
            return request.Method == "GET" && request.Query.Count == 0 && request.Body.Length == 0
                ? [] : ["The OData service document and $metadata endpoints require GET without query options or a payload."];
        }
        if (relative == "$batch")
        {
            throw Unsupported("batch requests");
        }
        var errors = new List<string>();
        var open = relative.IndexOf('(');
        var name = open < 0 ? relative : relative[..open];
        var keyed = open >= 0;
        if (!entitySets.TryGetValue(name, out var entitySet))
        {
            return [$"OData entity set '{name}' is not declared in the model."];
        }
        if (keyed)
        {
            if (!relative.EndsWith(')'))
            {
                return ["The OData entity key URL is malformed."];
            }
            var key = relative[(open + 1)..^1];
            if (!key.StartsWith('\'') && (key.Contains('=') || key.Contains(',')))
            {
                throw Unsupported("named/composite keys");
            }
            using var validator = new JsonSchemaValidator(entitySet.Properties[entitySet.KeyProperty].JsonSchema);
            validator.EnsureParameterSupport();
            if (validator.ParameterType is "array" or "object")
            {
                throw Unsupported("non-scalar keys");
            }
            if (validator.ParameterType == "string")
            {
                if (!Regex.IsMatch(key, "^'(?:[^']|'')*'$", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(250)))
                {
                    errors.Add("An OData string key must be a single quoted string literal.");
                }
                else
                {
                    errors.AddRange(validator.ValidateParameter([key[1..^1].Replace("''", "'")]));
                }
            }
            else
            {
                errors.AddRange(validator.ValidateParameter([key]));
            }
        }
        var methods = keyed ? entitySet.EntityMethods : entitySet.CollectionMethods;
        if (!methods.Contains(request.Method, StringComparer.Ordinal))
        {
            errors.Add($"HTTP method '{request.Method}' is not allowed on this OData resource.");
        }
        ValidateQuery(request.Query, entitySet, keyed, errors);

        var writing = request.Method is "POST" or "PUT" or "PATCH";
        if (!writing)
        {
            if (request.Body.Length != 0) errors.Add("This OData request method does not permit a payload.");
            return errors;
        }
        var schema = entitySet.PayloadSchema ?? throw Unsupported("write requests without an explicit payload schema");
        if (schema.Format != "json")
        {
            throw Unsupported("non-JSON write payload schemas");
        }
        var (contentType, _, typeError) = SchemaValidationSession.ContentType(request.Headers);
        if (typeError is not null || contentType != "application/json" && !contentType.EndsWith("+json", StringComparison.Ordinal))
        {
            errors.Add("An OData JSON write payload requires a valid JSON Content-Type.");
        }
        if (request.Body.Length == 0)
        {
            errors.Add("An OData write request requires a payload.");
        }
        else
        {
            errors.AddRange(ContentSchemaValidator.Validate(schema,
                new ValidateContent { ValidateAs = "json", Action = "prevent" }, request.Body));
        }
        return errors;
    }

    private static void ValidateQuery(
        IReadOnlyDictionary<string, string[]> query, OdataEntitySetMetadata entitySet, bool keyed, List<string> errors)
    {
        var parameters = ApiSchemaValidation.Definitions(entitySet.QueryParameters, StringComparer.Ordinal);
        var actual = ApiSchemaValidation.Values(query, StringComparer.Ordinal);
        var properties = ApiSchemaValidation.Definitions(entitySet.Properties, StringComparer.Ordinal);
        foreach (var (name, values) in actual)
        {
            if (s_edmQueries.Contains(name)) throw Unsupported($"query option {name}");
            if (!name.StartsWith('$'))
            {
                if (!parameters.ContainsKey(name)) errors.Add($"OData custom query parameter '{name}' is not declared.");
                continue;
            }
            if (values.Length != 1)
            {
                errors.Add($"OData query option '{name}' requires exactly one value.");
                continue;
            }
            var value = values[0];
            switch (name)
            {
                case "$top":
                case "$skip":
                    if (keyed || value.Length == 0 || value.Any(character => character is < '0' or > '9'))
                    {
                        errors.Add($"OData query option '{name}' requires a nonnegative integer on a collection.");
                    }
                    break;
                case "$count":
                    if (keyed || value is not ("true" or "false"))
                    {
                        errors.Add("$count requires true or false on an OData collection.");
                    }
                    break;
                case "$select":
                    var selected = value.Split(',').Select(item => item.Trim()).ToArray();
                    if (selected.Any(item => item != "*" && !properties.ContainsKey(item)) ||
                        selected.Distinct(StringComparer.Ordinal).Count() != selected.Length)
                    {
                        errors.Add("$select must name distinct declared OData properties or '*'.");
                    }
                    break;
                case "$orderby":
                    var ordered = new HashSet<string>(StringComparer.Ordinal);
                    foreach (var term in value.Split(','))
                    {
                        var parts = term.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
                        if (keyed || parts.Length is < 1 or > 2 || !properties.ContainsKey(parts[0]) ||
                            !ordered.Add(parts[0]) || parts.Length == 2 && parts[1] is not ("asc" or "desc"))
                        {
                            errors.Add("$orderby requires declared collection properties with optional asc/desc.");
                        }
                    }
                    break;
                case "$format":
                    if (value != "json" && !string.Equals(value, "application/json", StringComparison.OrdinalIgnoreCase))
                    {
                        errors.Add("The injected JSON OData model permits only json/application/json $format values.");
                    }
                    break;
                default:
                    errors.Add($"OData system query option '{name}' is not defined.");
                    break;
            }
        }
        foreach (var (name, parameter) in parameters)
        {
            using var validator = new JsonSchemaValidator(parameter.JsonSchema);
            validator.EnsureParameterSupport();
            if (actual.TryGetValue(name, out var values))
            {
                errors.AddRange(validator.ValidateParameter(values).Select(error => $"{name}: {error}"));
            }
            else if (parameter.Required)
            {
                errors.Add($"Required OData custom query parameter '{name}' is missing.");
            }
        }
    }

    private static NotSupportedException Unsupported(string feature) => new(
        $"Built-in OData validation does not support {feature}. Register IOdataRequestValidator with full EDM/CSDL validation.");
}