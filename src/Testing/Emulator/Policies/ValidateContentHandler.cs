// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Services;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Policies;

[
    Section(nameof(IInboundContext)),
    Section(nameof(IOutboundContext)),
    Section(nameof(IOnErrorContext))
]
internal class ValidateContentHandler : PolicyHandler<ValidateContentConfig>
{
    public override string PolicyName => nameof(IInboundContext.ValidateContent);

    protected override void Handle(GatewayContext context, ValidateContentConfig config)
    {
        var unspecifiedAction = SchemaValidationSession.Action(
            config.UnspecifiedContentTypeAction, nameof(config.UnspecifiedContentTypeAction));
        var sizeAction = SchemaValidationSession.Action(config.SizeExceededAction, nameof(config.SizeExceededAction));
        SchemaValidationSession.ValidateSize(config.MaxSize, nameof(config.MaxSize));
        var rules = config.Contents ?? [];
        foreach (var rule in rules)
        {
            ArgumentNullException.ThrowIfNull(rule);
            SchemaValidationSession.Action(rule.Action, nameof(rule.Action));
            if (rule.ValidateAs is not ("json" or "xml" or "soap"))
            {
                throw new NotSupportedException($"Content validation engine '{rule.ValidateAs}' is not supported.");
            }
            if (!string.IsNullOrEmpty(rule.Type))
            {
                var mediaType = SchemaValidationSession.MediaType(rule.Type);
                if (rule.ValidateAs == "soap" && mediaType is not ("text/xml" or "application/soap+xml"))
                {
                    throw new ArgumentException("SOAP validation requires text/xml or application/soap+xml.");
                }
            }
            if (rule.SchemaId is not null) ArgumentException.ThrowIfNullOrWhiteSpace(rule.SchemaId);
            if (rule.SchemaRef is not null) ArgumentException.ThrowIfNullOrWhiteSpace(rule.SchemaRef);
        }
        ValidateMap(config.ContentTypeMap);
        var session = new SchemaValidationSession(context, "validate-content", config.ErrorsVariableName);
        session.CheckSize(config.MaxSize, sizeAction);
        if (unspecifiedAction == "ignore" && rules.All(rule => rule.Action == "ignore"))
        {
            session.Complete();
            return;
        }

        var (originalType, _, typeError) = SchemaValidationSession.ContentType(session.Message.Headers);
        if (typeError is not null)
        {
            var action = unspecifiedAction != "ignore" ? unspecifiedAction :
                rules.FirstOrDefault(rule => rule.Action != "ignore")?.Action ?? "ignore";
            session.Add(string.Empty, session.BodyType, "IncorrectMessage", typeError, action);
            session.Complete();
            return;
        }
        var contentType = Map(originalType, config.ContentTypeMap);
        var metadata = context.Services.Resolve<ApiValidationMetadata>();
        var namedTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var rule in rules.Where(rule => !string.IsNullOrEmpty(rule.Type) && rule.SchemaId is not null))
        {
            if (context.Services.Resolve<ContentValidationSchema>(rule.SchemaId) is null)
            {
                throw MissingSchema(rule.SchemaId!);
            }
            namedTypes.Add(SchemaValidationSession.MediaType(rule.Type!));
        }
        if (metadata is null)
        {
            if (unspecifiedAction != "ignore" && namedTypes.Count == 0 ||
                rules.Any(rule => string.IsNullOrEmpty(rule.Type) && rule.Action != "ignore"))
            {
                metadata = ApiSchemaValidation.RequireMetadata(context, PolicyName);
            }
        }
        var definitions = metadata is null ? new Dictionary<string, ContentValidationSchema>() :
            session.IsRequest ? metadata.RequestContent :
            ApiSchemaValidation.Response(metadata, context.Response.StatusCode)?.Content ??
            new Dictionary<string, ContentValidationSchema>();
        var schemas = ContentDefinitions(definitions);
        var specified = schemas.ContainsKey(contentType) || namedTypes.Contains(contentType) ||
                        unspecifiedAction == "ignore" && rules.Any(rule => !string.IsNullOrEmpty(rule.Type) &&
                                          SchemaValidationSession.MediaType(rule.Type) == contentType);
        if (!specified)
        {
            session.Add(contentType, session.BodyType, "Unspecified",
                $"Unspecified content type '{contentType}' is not allowed.", unspecifiedAction);
            session.Complete();
            return;
        }

        foreach (var rule in rules.Where(rule => rule.Action != "ignore" &&
                     (string.IsNullOrEmpty(rule.Type) || SchemaValidationSession.MediaType(rule.Type) == contentType)))
        {
            var schema = rule.SchemaId is not null
                ? context.Services.Resolve<ContentValidationSchema>(rule.SchemaId) ?? throw MissingSchema(rule.SchemaId)
                : schemas.TryGetValue(contentType, out var definition) ? definition :
                  throw new NotSupportedException(
                      $"No API content schema is available for '{contentType}'. Register explicit ApiValidationMetadata or a named schema.");
            if (rule.ValidateAs == "soap" && contentType is not ("text/xml" or "application/soap+xml"))
            {
                throw new ArgumentException("SOAP validation requires text/xml or application/soap+xml.");
            }
            SchemaValidationSession.EnsureUtf8(session.Message);
            session.AddErrors(contentType, session.BodyType, "IncorrectMessage",
                ContentSchemaValidator.Validate(schema, rule, session.Message.Body.Content ?? string.Empty), rule.Action);
        }
        session.Complete();
    }

    private static NotSupportedException MissingSchema(string schemaId) => new(
        $"Content schema '{schemaId}' is not registered. Register a keyed ContentValidationSchema with test.Context.Services.");

    private static Dictionary<string, ContentValidationSchema> ContentDefinitions(
        IReadOnlyDictionary<string, ContentValidationSchema> definitions)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        var result = new Dictionary<string, ContentValidationSchema>(StringComparer.OrdinalIgnoreCase);
        foreach (var (type, schema) in definitions)
        {
            ArgumentNullException.ThrowIfNull(schema);
            if (!result.TryAdd(SchemaValidationSession.MediaType(type), schema))
            {
                throw new ArgumentException($"Duplicate API content type '{type}'.");
            }
        }
        return result;
    }

    private static void ValidateMap(ContentTypeMapConfig? map)
    {
        if (map is null) return;
        if (map.AnyContentTypeValue is not null) SchemaValidationSession.MediaType(map.AnyContentTypeValue);
        if (map.MissingContentTypeValue is not null) SchemaValidationSession.MediaType(map.MissingContentTypeValue);
        foreach (var type in map.Types ?? [])
        {
            ArgumentNullException.ThrowIfNull(type);
            SchemaValidationSession.MediaType(type.To);
            if (type.From is not null) SchemaValidationSession.MediaType(type.From);
            if ((type.From is null) == (type.When is null))
            {
                throw new ArgumentException("A content type mapping requires exactly one of From or When.");
            }
        }
    }

    private static string Map(string original, ContentTypeMapConfig? map)
    {
        if (map is null) return original;
        foreach (var type in map.Types ?? [])
        {
            if (type.When == true || type.From is not null && SchemaValidationSession.MediaType(type.From) == original)
            {
                return SchemaValidationSession.MediaType(type.To);
            }
        }
        if (string.IsNullOrEmpty(original) && map.MissingContentTypeValue is not null)
        {
            return SchemaValidationSession.MediaType(map.MissingContentTypeValue);
        }
        return map.AnyContentTypeValue is not null ? SchemaValidationSession.MediaType(map.AnyContentTypeValue) : original;
    }
}