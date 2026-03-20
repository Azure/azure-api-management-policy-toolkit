// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Xml.Linq;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Decompiling.Policy;

public class ValidateContentDecompiler : IPolicyDecompiler
{
    public string PolicyName => "validate-content";

    public void Decompile(CodeWriter writer, XElement element, string contextVar, PolicyDecompilerContext context)
    {
        var prefix = PolicyDecompilerContext.GetContextPrefix(element, contextVar);
        var props = new List<string>();

        context.AddRequiredExprStringProp(props, element, "unspecified-content-type-action",
            "UnspecifiedContentTypeAction");
        context.AddRequiredIntProp(props, element, "max-size", "MaxSize");
        context.AddRequiredExprStringProp(props, element, "size-exceeded-action", "SizeExceededAction");
        context.AddOptionalStringProp(props, element, "errors-variable-name", "ErrorsVariableName");

        var contentTypeMapElement = element.Element("content-type-map");
        if (contentTypeMapElement != null)
        {
            var mapProps = new List<string>();
            var anyContentType = contentTypeMapElement.Attribute("any-content-type-value")?.Value;
            if (anyContentType != null)
            {
                mapProps.Add($"AnyContentTypeValue = {PolicyDecompilerContext.Literal(anyContentType)}");
            }

            var missingContentType = contentTypeMapElement.Attribute("missing-content-type-value")?.Value;
            if (missingContentType != null)
            {
                mapProps.Add($"MissingContentTypeValue = {PolicyDecompilerContext.Literal(missingContentType)}");
            }

            var types = contentTypeMapElement.Elements("type").ToList();
            if (types.Count > 0)
            {
                var typeConfigs = types.Select(t =>
                {
                    var typeProps = new List<string>();
                    var to = t.Attribute("to")?.Value;
                    if (to != null)
                    {
                        typeProps.Add($"To = {PolicyDecompilerContext.Literal(to)}");
                    }

                    var from = t.Attribute("from")?.Value;
                    if (from != null)
                    {
                        typeProps.Add($"From = {PolicyDecompilerContext.Literal(from)}");
                    }

                    var when = t.Attribute("when")?.Value;
                    if (when != null)
                    {
                        typeProps.Add($"When = {when.ToLower()}");
                    }

                    return $"new ContentTypeMap {{ {string.Join(", ", typeProps)} }}";
                });
                mapProps.Add($"Types = new ContentTypeMap[] {{ {string.Join(", ", typeConfigs)} }}");
            }

            props.Add($"ContentTypeMap = new ContentTypeMapConfig {{ {string.Join(", ", mapProps)} }}");
        }

        var contents = element.Elements("content").ToList();
        if (contents.Count > 0)
        {
            var contentConfigs = contents.Select(c =>
            {
                var contentProps = new List<string>();
                var validateAs = c.Attribute("validate-as")?.Value;
                if (validateAs != null)
                {
                    contentProps.Add($"ValidateAs = {PolicyDecompilerContext.Literal(validateAs)}");
                }

                var action = c.Attribute("action")?.Value;
                if (action != null)
                {
                    contentProps.Add($"Action = {PolicyDecompilerContext.Literal(action)}");
                }

                var type = c.Attribute("type")?.Value;
                if (type != null)
                {
                    contentProps.Add($"Type = {PolicyDecompilerContext.Literal(type)}");
                }

                var schemaId = c.Attribute("schema-id")?.Value;
                if (schemaId != null)
                {
                    contentProps.Add($"SchemaId = {PolicyDecompilerContext.Literal(schemaId)}");
                }

                var schemaRef = c.Attribute("schema-ref")?.Value;
                if (schemaRef != null)
                {
                    contentProps.Add($"SchemaRef = {PolicyDecompilerContext.Literal(schemaRef)}");
                }

                var allowAdditional = c.Attribute("allow-additional-properties")?.Value;
                if (allowAdditional != null)
                {
                    contentProps.Add($"AllowAdditionalProperties = {allowAdditional.ToLower()}");
                }

                var caseInsensitive = c.Attribute("case-insensitive-property-names")?.Value;
                if (caseInsensitive != null)
                {
                    contentProps.Add($"CaseInsensitivePropertyNames = {caseInsensitive.ToLower()}");
                }

                return $"new ValidateContent {{ {string.Join(", ", contentProps)} }}";
            });
            props.Add($"Contents = new ValidateContent[] {{ {string.Join(", ", contentConfigs)} }}");
        }

        PolicyDecompilerContext.EmitConfigCall(writer, prefix, "ValidateContent", "ValidateContentConfig", props);
    }
}
