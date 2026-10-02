// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Services;

internal static class ApiSchemaValidation
{
    internal static ApiValidationMetadata RequireMetadata(GatewayContext context, string policy) =>
        context.Services.Resolve<ApiValidationMetadata>() ?? throw new NotSupportedException(
            $"{policy} requires explicit ApiValidationMetadata. Register the operation's API definitions with " +
            "test.Context.Services.Register(metadata); policy overrides are not schema declarations.");

    internal static ApiResponseValidationMetadata? Response(ApiValidationMetadata metadata, int statusCode)
    {
        ArgumentNullException.ThrowIfNull(metadata.Responses);
        return metadata.Responses.TryGetValue(statusCode, out var response)
            ? response ?? throw new ArgumentException($"Response metadata for status {statusCode} is null.")
            : metadata.DefaultResponse;
    }

    internal static Dictionary<string, T> Definitions<T>(
        IReadOnlyDictionary<string, T> definitions, StringComparer comparer) where T : class
    {
        ArgumentNullException.ThrowIfNull(definitions);
        var result = new Dictionary<string, T>(comparer);
        foreach (var (name, definition) in definitions)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(name);
            ArgumentNullException.ThrowIfNull(definition);
            if (!result.TryAdd(name, definition))
            {
                throw new ArgumentException($"Duplicate API definition '{name}'.");
            }
        }
        return result;
    }

    internal static Dictionary<string, string> Overrides<T>(
        IEnumerable<T>? rules, Func<T, string> name, Func<T, string> action,
        StringComparer comparer) where T : class
    {
        var result = new Dictionary<string, string>(comparer);
        foreach (var rule in rules ?? [])
        {
            ArgumentNullException.ThrowIfNull(rule);
            var ruleName = name(rule);
            ArgumentException.ThrowIfNullOrWhiteSpace(ruleName);
            var ruleAction = SchemaValidationSession.Action(action(rule), "Action");
            if (!result.TryAdd(ruleName, ruleAction))
            {
                throw new ArgumentException($"Duplicate validation override '{ruleName}'.");
            }
        }
        return result;
    }

    internal static Dictionary<string, string[]> Values(
        IEnumerable<KeyValuePair<string, string[]>> values, StringComparer comparer)
    {
        ArgumentNullException.ThrowIfNull(values);
        var result = new Dictionary<string, string[]>(comparer);
        foreach (var (name, items) in values)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(name);
            ArgumentNullException.ThrowIfNull(items);
            if (items.Any(value => value is null))
            {
                throw new ArgumentException($"Null wire value for '{name}'.");
            }
            result[name] = result.TryGetValue(name, out var previous) ? [.. previous, .. items] : items;
        }
        return result;
    }

    internal static void Parameters(
        SchemaValidationSession session, IReadOnlyDictionary<string, ApiParameterValidationMetadata> definitions,
        IEnumerable<KeyValuePair<string, string[]>> values, string type, string specified, string unspecified,
        IReadOnlyDictionary<string, string> overrides, StringComparer comparer)
    {
        var schema = Definitions(definitions, comparer);
        var actual = Values(values, comparer);
        foreach (var (name, definition) in schema)
        {
            var action = overrides.TryGetValue(name, out var actionOverride) ? actionOverride : specified;
            if (action == "ignore")
            {
                continue;
            }

            using var validator = new JsonSchemaValidator(definition.JsonSchema);
            validator.EnsureParameterSupport();
            if (!actual.TryGetValue(name, out var items))
            {
                if (definition.Required)
                {
                    session.Add(name, type, "Required", $"Required {type} '{name}' is missing.", action);
                }
                continue;
            }

            session.AddErrors(name, type, "IncorrectMessage",
                validator.ValidateParameter(items).Select(error => $"{type} '{name}': {error}").ToArray(), action);
        }

        foreach (var (name, _) in actual.Where(item => !schema.ContainsKey(item.Key)))
        {
            var action = overrides.TryGetValue(name, out var actionOverride) ? actionOverride : unspecified;
            session.Add(name, type, "Unspecified", $"Unspecified {type} '{name}' is not allowed by the API schema.", action);
        }
    }
}