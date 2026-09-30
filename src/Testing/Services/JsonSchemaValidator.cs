// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Globalization;
using System.Net;
using System.Net.Mail;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Services;

internal sealed class JsonSchemaValidator : IDisposable
{
    private static readonly HashSet<string> s_types =
        ["object", "array", "string", "number", "integer", "boolean", "null"];
    private static readonly HashSet<string> s_annotations =
        ["$schema", "$id", "id", "$comment", "title", "description", "default", "examples", "example",
            "deprecated", "readOnly", "writeOnly"];
    private static readonly HashSet<string> s_keywords =
        ["$ref", "$defs", "definitions", "type", "nullable", "properties", "required", "additionalProperties",
            "items", "minItems", "maxItems", "uniqueItems", "minProperties", "maxProperties",
            "minLength", "maxLength", "pattern", "format", "minimum", "maximum",
            "exclusiveMinimum", "exclusiveMaximum", "multipleOf", "enum", "const", "allOf", "anyOf", "oneOf", "not"];
    private static readonly HashSet<string> s_formats =
        ["date-time", "date", "time", "uuid", "email", "hostname", "ipv4", "ipv6", "uri", "uri-reference",
            "int32", "int64", "float", "double"];
    private static readonly string[] s_counts =
        ["minItems", "maxItems", "minProperties", "maxProperties", "minLength", "maxLength"];
    private readonly JsonDocument _document;
    private readonly JsonElement _schema;

    internal JsonSchemaValidator(string definition, string? reference = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(definition);
        try
        {
            _document = JsonDocument.Parse(definition);
        }
        catch (JsonException error)
        {
            throw new ArgumentException("The configured JSON schema is not valid JSON.", nameof(definition), error);
        }

        var initialized = false;
        try
        {
            _schema = reference is null ? _document.RootElement : Resolve(reference);
            Inspect(_schema, new HashSet<string>(StringComparer.Ordinal), 0);
            initialized = true;
        }
        finally
        {
            if (!initialized) _document.Dispose();
        }
    }

    public void Dispose() => _document.Dispose();

    internal IReadOnlyList<string> Validate(
        string content, bool? allowAdditionalProperties = null, bool caseInsensitivePropertyNames = false)
    {
        JsonDocument payload;
        try
        {
            payload = JsonDocument.Parse(content);
        }
        catch (JsonException error)
        {
            return [$"Invalid JSON payload: {error.Message}"];
        }

        using (payload)
        {
            var errors = new List<string>();
            var comparer = caseInsensitivePropertyNames ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
            CheckDuplicateProperties(payload.RootElement, "$", comparer, errors);
            Validate(_schema, payload.RootElement, "$", allowAdditionalProperties, comparer, errors, 0);
            return errors;
        }
    }

    internal string ParameterType => WireType(Effective(_schema));

    internal void EnsureParameterSupport()
    {
        var schema = Effective(_schema);
        var type = WireType(schema);
        if (type == "array")
        {
            if (!schema.TryGetProperty("items", out var items))
            {
                throw new NotSupportedException("Array parameter serialization requires an explicit primitive items schema.");
            }
            var itemType = WireType(Effective(items));
            if (itemType is "array" or "object")
            {
                throw new NotSupportedException("Nested array/object parameter serialization is not supported.");
            }
        }
        else if (type == "object")
        {
            throw new NotSupportedException(
                "Object parameter serialization (OpenAPI style/explode/deepObject) requires a dedicated validator.");
        }
    }

    internal IReadOnlyList<string> ValidateParameter(string[] values)
    {
        ArgumentNullException.ThrowIfNull(values);
        EnsureParameterSupport();
        var schema = Effective(_schema);
        var type = WireType(schema);
        if (type != "array" && values.Length != 1)
        {
            return ["A scalar parameter must contain exactly one value."];
        }

        var valueType = type == "array" ? WireType(Effective(schema.GetProperty("items"))) : type;
        var literals = new List<string>();
        foreach (var value in values)
        {
            ArgumentNullException.ThrowIfNull(value);
            if (!TryWireLiteral(value, valueType, out var literal))
            {
                return [$"A wire value cannot be parsed as {valueType}."];
            }
            literals.Add(literal);
        }
        return Validate(type == "array" ? $"[{string.Join(',', literals)}]" : literals[0]);
    }

    private static string WireType(JsonElement schema)
    {
        if (schema.ValueKind != JsonValueKind.Object ||
            !schema.TryGetProperty("type", out var type) || type.ValueKind != JsonValueKind.String)
        {
            throw new NotSupportedException(
                "Parameter serialization requires a single explicit primitive or array type; unions/inferred types are unsupported.");
        }
        return type.GetString()!;
    }

    private static bool TryWireLiteral(string value, string type, out string literal)
    {
        literal = string.Empty;
        switch (type)
        {
            case "string":
                literal = JsonSerializer.Serialize(value);
                return true;
            case "null":
                literal = "null";
                return value == "null";
            case "boolean":
                if (!bool.TryParse(value, out var boolean)) return false;
                literal = boolean ? "true" : "false";
                return true;
            case "integer":
            case "number":
                try
                {
                    using var parsed = JsonDocument.Parse(value);
                    if (parsed.RootElement.ValueKind != JsonValueKind.Number) return false;
                    Number(parsed.RootElement);
                    literal = parsed.RootElement.GetRawText();
                    return true;
                }
                catch (JsonException)
                {
                    return false;
                }
            default:
                throw new NotSupportedException($"Parameter serialization for '{type}' is not supported.");
        }
    }

    private JsonElement Effective(JsonElement schema)
    {
        for (var depth = 0; depth < 64; depth++)
        {
            if (schema.ValueKind != JsonValueKind.Object || !schema.TryGetProperty("$ref", out var reference))
            {
                return schema;
            }
            schema = Resolve(reference.GetString()!);
        }
        throw new NotSupportedException("A recursive JSON schema reference cannot determine a parameter's wire type.");
    }

    private JsonElement Resolve(string reference)
    {
        if (reference == "#") return _document.RootElement;
        if (!reference.StartsWith("#/", StringComparison.Ordinal))
        {
            throw new NotSupportedException($"Only local JSON schema references are supported: '{reference}'.");
        }

        var result = _document.RootElement;
        foreach (var encoded in Uri.UnescapeDataString(reference[2..]).Split('/'))
        {
            if (Regex.IsMatch(encoded, "~(?:[^01]|$)", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(250)))
            {
                throw new ArgumentException($"Invalid JSON schema reference '{reference}'.");
            }
            var segment = encoded.Replace("~1", "/").Replace("~0", "~");
            if (result.ValueKind == JsonValueKind.Object && result.TryGetProperty(segment, out var property))
            {
                result = property;
            }
            else if (result.ValueKind == JsonValueKind.Array &&
                     int.TryParse(segment, NumberStyles.None, CultureInfo.InvariantCulture, out var index) &&
                     index >= 0 && index < result.GetArrayLength())
            {
                result = result[index];
            }
            else
            {
                throw new ArgumentException($"JSON schema reference '{reference}' could not be resolved.");
            }
        }
        return result;
    }

    private void Inspect(JsonElement schema, HashSet<string> references, int depth)
    {
        CheckDepth(depth);
        if (schema.ValueKind is JsonValueKind.True or JsonValueKind.False) return;
        Require(schema.ValueKind == JsonValueKind.Object, "A JSON schema must be an object or boolean.");
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in schema.EnumerateObject())
        {
            Require(names.Add(property.Name), $"Duplicate JSON schema keyword '{property.Name}'.");
            if (!s_keywords.Contains(property.Name) && !s_annotations.Contains(property.Name))
            {
                throw new NotSupportedException($"JSON schema keyword '{property.Name}' is not supported by the emulator.");
            }
        }
        if (schema.TryGetProperty("$schema", out var dialect))
        {
            Require(dialect.ValueKind == JsonValueKind.String, "$schema must be a URI string.");
            var uri = dialect.GetString()!.TrimEnd('#', '/');
            if (uri is not ("http://json-schema.org/draft-04/schema" or "https://json-schema.org/draft-04/schema" or
                "http://json-schema.org/draft-06/schema" or "https://json-schema.org/draft-06/schema" or
                "http://json-schema.org/draft-07/schema" or "https://json-schema.org/draft-07/schema" or
                "https://json-schema.org/draft/2019-09/schema" or "https://json-schema.org/draft/2020-12/schema"))
            {
                throw new NotSupportedException($"JSON schema dialect '{dialect.GetString()}' is unsupported.");
            }
        }

        if (schema.TryGetProperty("$ref", out var reference))
        {
            Require(reference.ValueKind == JsonValueKind.String, "$ref must be a string.");
            if (names.Any(name => name != "$ref" && name is not ("definitions" or "$defs") && !s_annotations.Contains(name)))
            {
                throw new NotSupportedException("JSON schema references with assertion siblings are not supported.");
            }
            var target = Resolve(reference.GetString()!);
            if (references.Add(reference.GetString()!)) Inspect(target, references, depth + 1);
        }
        if (schema.TryGetProperty("type", out var type))
        {
            var types = type.ValueKind == JsonValueKind.Array ? type.EnumerateArray().ToArray() : [type];
            Require(types.Length > 0 && types.All(item => item.ValueKind == JsonValueKind.String && s_types.Contains(item.GetString()!)),
                "JSON schema type must contain recognized type names.");
            Require(types.Select(item => item.GetString()).Distinct().Count() == types.Length, "Duplicate JSON schema types.");
        }
        foreach (var name in new[] { "nullable", "uniqueItems" })
        {
            if (schema.TryGetProperty(name, out var flag))
            {
                Require(flag.ValueKind is JsonValueKind.True or JsonValueKind.False, $"{name} must be boolean.");
            }
        }
        foreach (var name in new[] { "properties", "definitions", "$defs" })
        {
            if (!schema.TryGetProperty(name, out var dictionary)) continue;
            Require(dictionary.ValueKind == JsonValueKind.Object, $"{name} must be an object.");
            var propertyNames = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in dictionary.EnumerateObject())
            {
                Require(propertyNames.Add(property.Name), $"Duplicate schema property '{property.Name}'.");
                Inspect(property.Value, references, depth + 1);
            }
        }
        if (schema.TryGetProperty("required", out var required))
        {
            Require(required.ValueKind == JsonValueKind.Array && required.EnumerateArray().All(item => item.ValueKind == JsonValueKind.String),
                "required must be an array of property names.");
            Require(required.EnumerateArray().Select(item => item.GetString()).Distinct().Count() == required.GetArrayLength(),
                "Duplicate required property names.");
        }
        foreach (var name in new[] { "additionalProperties", "items", "not" })
        {
            if (!schema.TryGetProperty(name, out var child)) continue;
            if (name == "items" && child.ValueKind == JsonValueKind.Array)
            {
                throw new NotSupportedException("Tuple JSON schema items are not supported.");
            }
            Inspect(child, references, depth + 1);
        }
        foreach (var name in new[] { "allOf", "anyOf", "oneOf" })
        {
            if (!schema.TryGetProperty(name, out var children)) continue;
            Require(children.ValueKind == JsonValueKind.Array && children.GetArrayLength() > 0,
                $"{name} must be a nonempty array of schemas.");
            foreach (var child in children.EnumerateArray()) Inspect(child, references, depth + 1);
        }
        foreach (var name in s_counts)
        {
            if (!schema.TryGetProperty(name, out var count)) continue;
            Require(count.ValueKind == JsonValueKind.Number && count.TryGetInt32(out var number) && number >= 0,
                $"{name} must be a nonnegative integer.");
        }
        foreach (var name in new[] { "minimum", "maximum", "multipleOf", "exclusiveMinimum", "exclusiveMaximum" })
        {
            if (!schema.TryGetProperty(name, out var bound)) continue;
            if (name.StartsWith("exclusive", StringComparison.Ordinal) && bound.ValueKind is JsonValueKind.True or JsonValueKind.False)
            {
                Require(bound.ValueKind == JsonValueKind.False || schema.TryGetProperty(
                    name == "exclusiveMinimum" ? "minimum" : "maximum", out _), $"{name} requires a bound.");
                continue;
            }
            Require(bound.ValueKind == JsonValueKind.Number, $"{name} must be numeric.");
            var number = Number(bound);
            Require(name != "multipleOf" || number > 0, "multipleOf must be positive.");
        }
        if (schema.TryGetProperty("enum", out var enumeration))
        {
            Require(enumeration.ValueKind == JsonValueKind.Array && enumeration.GetArrayLength() > 0, "enum must be a nonempty array.");
        }
        if (schema.TryGetProperty("pattern", out var pattern))
        {
            Require(pattern.ValueKind == JsonValueKind.String, "pattern must be a string.");
            _ = new Regex(pattern.GetString()!, RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(250));
        }
        if (schema.TryGetProperty("format", out var format))
        {
            Require(format.ValueKind == JsonValueKind.String, "format must be a string.");
            if (!s_formats.Contains(format.GetString()!))
            {
                throw new NotSupportedException($"JSON schema format '{format.GetString()}' is not supported.");
            }
        }
    }

    private void Validate(JsonElement schema, JsonElement value, string path, bool? allowAdditional,
        StringComparer comparer, List<string> errors, int depth)
    {
        CheckDepth(depth);
        if (schema.ValueKind == JsonValueKind.True) return;
        if (schema.ValueKind == JsonValueKind.False)
        {
            errors.Add($"Value at '{path}' is not allowed by the schema.");
            return;
        }
        if (schema.TryGetProperty("$ref", out var reference))
        {
            Validate(Resolve(reference.GetString()!), value, path, allowAdditional, comparer, errors, depth + 1);
            return;
        }
        if (schema.TryGetProperty("type", out var type))
        {
            var types = type.ValueKind == JsonValueKind.Array ? type.EnumerateArray().ToArray() : [type];
            var nullable = value.ValueKind == JsonValueKind.Null &&
                           schema.TryGetProperty("nullable", out var flag) && flag.GetBoolean();
            if (!nullable && !types.Any(item => Matches(item.GetString()!, value)))
            {
                errors.Add($"Value at '{path}' does not match the declared type.");
                return;
            }
        }
        if (schema.TryGetProperty("enum", out var enumeration) && !enumeration.EnumerateArray().Any(item => Equal(item, value)))
        {
            errors.Add($"Value at '{path}' is not one of the declared enum values.");
        }
        if (schema.TryGetProperty("const", out var constant) && !Equal(constant, value))
        {
            errors.Add($"Value at '{path}' does not equal the declared constant.");
        }
        if (schema.TryGetProperty("allOf", out var all))
        {
            foreach (var child in all.EnumerateArray()) Validate(child, value, path, allowAdditional, comparer, errors, depth + 1);
        }
        foreach (var name in new[] { "anyOf", "oneOf" })
        {
            if (!schema.TryGetProperty(name, out var children)) continue;
            var matches = children.EnumerateArray().Count(child => Conforms(child, value, path, allowAdditional, comparer, depth + 1));
            if (matches == 0 || name == "oneOf" && matches != 1)
            {
                errors.Add($"Value at '{path}' does not satisfy {name}.");
            }
        }
        if (schema.TryGetProperty("not", out var forbidden) && Conforms(forbidden, value, path, allowAdditional, comparer, depth + 1))
        {
            errors.Add($"Value at '{path}' matches a forbidden schema.");
        }

        switch (value.ValueKind)
        {
            case JsonValueKind.Object:
                ValidateObject(schema, value, path, allowAdditional, comparer, errors, depth);
                break;
            case JsonValueKind.Array:
                ValidateArray(schema, value, path, allowAdditional, comparer, errors, depth);
                break;
            case JsonValueKind.String:
                ValidateString(schema, value.GetString()!, path, errors);
                break;
            case JsonValueKind.Number:
                ValidateNumber(schema, Number(value), path, errors);
                break;
        }
    }

    private bool Conforms(JsonElement schema, JsonElement value, string path, bool? allowAdditional,
        StringComparer comparer, int depth)
    {
        var errors = new List<string>();
        Validate(schema, value, path, allowAdditional, comparer, errors, depth);
        return errors.Count == 0;
    }

    private void ValidateObject(JsonElement schema, JsonElement value, string path, bool? allowAdditional,
        StringComparer comparer, List<string> errors, int depth)
    {
        var actual = value.EnumerateObject().ToArray();
        CountBounds(schema, actual.Length, "minProperties", "maxProperties", path, errors);
        if (schema.TryGetProperty("required", out var required))
        {
            foreach (var property in required.EnumerateArray())
            {
                if (!actual.Any(item => comparer.Equals(item.Name, property.GetString())))
                {
                    errors.Add($"Required property '{property.GetString()}' is missing at '{path}'.");
                }
            }
        }
        var properties = new Dictionary<string, JsonElement>(comparer);
        if (schema.TryGetProperty("properties", out var declared))
        {
            foreach (var property in declared.EnumerateObject())
            {
                Require(properties.TryAdd(property.Name, property.Value),
                    $"Schema properties are ambiguous under the configured property-name comparison at '{path}'.");
            }
        }
        foreach (var property in actual)
        {
            if (properties.TryGetValue(property.Name, out var definition))
            {
                Validate(definition, property.Value, $"{path}.{property.Name}", allowAdditional, comparer, errors, depth + 1);
            }
            else if (allowAdditional == false ||
                     allowAdditional is null && schema.TryGetProperty("additionalProperties", out var additional) &&
                     additional.ValueKind == JsonValueKind.False)
            {
                errors.Add($"Additional property '{property.Name}' is not allowed at '{path}'.");
            }
            else if (allowAdditional is null && schema.TryGetProperty("additionalProperties", out var additionalSchema) &&
                     additionalSchema.ValueKind == JsonValueKind.Object)
            {
                Validate(additionalSchema, property.Value, $"{path}.{property.Name}", null, comparer, errors, depth + 1);
            }
        }
    }

    private void ValidateArray(JsonElement schema, JsonElement value, string path, bool? allowAdditional,
        StringComparer comparer, List<string> errors, int depth)
    {
        var items = value.EnumerateArray().ToArray();
        CountBounds(schema, items.Length, "minItems", "maxItems", path, errors);
        if (schema.TryGetProperty("uniqueItems", out var unique) && unique.GetBoolean())
        {
            for (var index = 0; index < items.Length; index++)
            {
                if (items.Take(index).Any(previous => Equal(previous, items[index])))
                {
                    errors.Add($"Duplicate array item at '{path}[{index}]'.");
                }
            }
        }
        if (schema.TryGetProperty("items", out var itemSchema))
        {
            for (var index = 0; index < items.Length; index++)
            {
                Validate(itemSchema, items[index], $"{path}[{index}]", allowAdditional, comparer, errors, depth + 1);
            }
        }
    }

    private static void ValidateString(JsonElement schema, string value, string path, List<string> errors)
    {
        var length = 0;
        foreach (var _ in value.EnumerateRunes()) length++;
        CountBounds(schema, length, "minLength", "maxLength", path, errors);
        if (schema.TryGetProperty("pattern", out var pattern) &&
            !Regex.IsMatch(value, pattern.GetString()!, RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(250)))
        {
            errors.Add($"String at '{path}' does not match its pattern.");
        }
        if (!schema.TryGetProperty("format", out var format)) return;
        var valid = format.GetString() switch
        {
            "uuid" => Guid.TryParseExact(value, "D", out _),
            "date" => DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _),
            "time" => Regex.IsMatch(value, @"^\d{2}:\d{2}:\d{2}(?:\.\d+)?(?:Z|[+-]\d{2}:\d{2})$",
                RegexOptions.CultureInvariant | RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(250)) &&
                DateTimeOffset.TryParse("2000-01-01T" + value, CultureInfo.InvariantCulture, DateTimeStyles.None, out _),
            "date-time" => Regex.IsMatch(value, @"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(?:\.\d+)?(?:Z|[+-]\d{2}:\d{2})$",
                RegexOptions.CultureInvariant | RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(250)) &&
                DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out _),
            "email" => MailAddress.TryCreate(value, out var address) && address.Address == value &&
                       value.Contains('@') && !value.Any(char.IsWhiteSpace),
            "hostname" => Uri.CheckHostName(value) == UriHostNameType.Dns,
            "ipv4" => value.Split('.') is { Length: 4 } octets &&
                      octets.All(octet => octet.Length > 0 && (octet.Length == 1 || octet[0] != '0') &&
                          byte.TryParse(octet, NumberStyles.None, CultureInfo.InvariantCulture, out _)),
            "ipv6" => !value.Contains('%') && IPAddress.TryParse(value, out var ipv6) &&
                      ipv6.AddressFamily == AddressFamily.InterNetworkV6,
            "uri" => Uri.IsWellFormedUriString(value, UriKind.Absolute),
            "uri-reference" => Uri.IsWellFormedUriString(value, UriKind.RelativeOrAbsolute),
            _ => true,
        };
        if (!valid) errors.Add($"String at '{path}' does not conform to format '{format.GetString()}'.");
    }

    private static void ValidateNumber(JsonElement schema, decimal value, string path, List<string> errors)
    {
        foreach (var name in new[] { "minimum", "maximum" })
        {
            if (!schema.TryGetProperty(name, out var bound)) continue;
            var limit = Number(bound);
            var exclusive = schema.TryGetProperty(name == "minimum" ? "exclusiveMinimum" : "exclusiveMaximum", out var flag) &&
                            flag.ValueKind == JsonValueKind.True;
            var invalid = name == "minimum"
                ? value < limit || exclusive && value == limit
                : value > limit || exclusive && value == limit;
            if (invalid) errors.Add($"Number at '{path}' does not satisfy {name}.");
        }
        foreach (var name in new[] { "exclusiveMinimum", "exclusiveMaximum" })
        {
            if (!schema.TryGetProperty(name, out var bound) || bound.ValueKind != JsonValueKind.Number) continue;
            if (name == "exclusiveMinimum" ? value <= Number(bound) : value >= Number(bound))
            {
                errors.Add($"Number at '{path}' does not satisfy {name}.");
            }
        }
        if (schema.TryGetProperty("multipleOf", out var multiple) && value % Number(multiple) != 0)
        {
            errors.Add($"Number at '{path}' is not a multipleOf its declared divisor.");
        }
        if (!schema.TryGetProperty("format", out var format)) return;
        var valid = format.GetString() switch
        {
            "int32" => decimal.Truncate(value) == value && value is >= int.MinValue and <= int.MaxValue,
            "int64" => decimal.Truncate(value) == value && value is >= long.MinValue and <= long.MaxValue,
            "float" => float.IsFinite((float)value),
            "double" => double.IsFinite((double)value),
            _ => true,
        };
        if (!valid) errors.Add($"Number at '{path}' does not conform to format '{format.GetString()}'.");
    }

    private static void CountBounds(JsonElement schema, int count, string minimum, string maximum,
        string path, List<string> errors)
    {
        if (schema.TryGetProperty(minimum, out var min) && count < min.GetInt32())
        {
            errors.Add($"Value at '{path}' does not satisfy {minimum}.");
        }
        if (schema.TryGetProperty(maximum, out var max) && count > max.GetInt32())
        {
            errors.Add($"Value at '{path}' does not satisfy {maximum}.");
        }
    }

    private static bool Matches(string type, JsonElement value) => type switch
    {
        "object" => value.ValueKind == JsonValueKind.Object,
        "array" => value.ValueKind == JsonValueKind.Array,
        "string" => value.ValueKind == JsonValueKind.String,
        "boolean" => value.ValueKind is JsonValueKind.True or JsonValueKind.False,
        "null" => value.ValueKind == JsonValueKind.Null,
        "number" => value.ValueKind == JsonValueKind.Number,
        "integer" => value.ValueKind == JsonValueKind.Number && decimal.Truncate(Number(value)) == Number(value),
        _ => throw new ArgumentException($"Invalid JSON schema type '{type}'."),
    };

    private static decimal Number(JsonElement value)
    {
        if (!value.TryGetDecimal(out var number) ||
            NormalizeNumber(value.GetRawText()) != NormalizeNumber(number.ToString("G29", CultureInfo.InvariantCulture)))
        {
            throw new NotSupportedException("JSON numeric values outside the emulator's exact decimal range/precision are unsupported.");
        }
        return number;
    }

    private static (string Digits, int Power, bool Negative) NormalizeNumber(string value)
    {
        if (value.Length > 256)
        {
            throw new NotSupportedException("JSON numeric literals longer than 256 characters are unsupported.");
        }
        var exponentIndex = value.IndexOfAny(['e', 'E']);
        var exponent = 0;
        if (exponentIndex >= 0 &&
            (!int.TryParse(value[(exponentIndex + 1)..], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out exponent) ||
             exponent is < -10000 or > 10000))
        {
            throw new NotSupportedException("JSON numeric exponents outside the supported range are unsupported.");
        }
        var mantissa = exponentIndex < 0 ? value : value[..exponentIndex];
        var negative = mantissa.StartsWith('-');
        var decimalPoint = mantissa.IndexOf('.');
        if (decimalPoint >= 0) exponent -= mantissa.Length - decimalPoint - 1;
        var digits = mantissa.TrimStart('-').Replace(".", string.Empty).TrimStart('0');
        if (digits.Length == 0) return ("0", 0, false);
        var trimmed = digits.TrimEnd('0');
        exponent += digits.Length - trimmed.Length;
        return (trimmed, exponent, negative);
    }

    private static bool Equal(JsonElement left, JsonElement right)
    {
        if (left.ValueKind != right.ValueKind) return false;
        return left.ValueKind switch
        {
            JsonValueKind.Number => Number(left) == Number(right),
            JsonValueKind.String => left.GetString() == right.GetString(),
            JsonValueKind.Array => left.GetArrayLength() == right.GetArrayLength() &&
                                   left.EnumerateArray().Zip(right.EnumerateArray()).All(pair => Equal(pair.First, pair.Second)),
            JsonValueKind.Object => left.EnumerateObject().Count() == right.EnumerateObject().Count() &&
                                    left.EnumerateObject().All(property => right.TryGetProperty(property.Name, out var value) &&
                                                                         Equal(property.Value, value)),
            _ => true,
        };
    }

    private static void CheckDuplicateProperties(JsonElement value, string path, StringComparer comparer, List<string> errors)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            var seen = new HashSet<string>(comparer);
            foreach (var property in value.EnumerateObject())
            {
                if (!seen.Add(property.Name)) errors.Add($"Duplicate or ambiguous property '{property.Name}' at '{path}'.");
                CheckDuplicateProperties(property.Value, $"{path}.{property.Name}", comparer, errors);
            }
        }
        else if (value.ValueKind == JsonValueKind.Array)
        {
            var index = 0;
            foreach (var item in value.EnumerateArray()) CheckDuplicateProperties(item, $"{path}[{index++}]", comparer, errors);
        }
    }

    private static void CheckDepth(int depth)
    {
        if (depth > 64) throw new NotSupportedException("JSON schema evaluation deeper than 64 levels is unsupported.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new ArgumentException(message);
    }
}