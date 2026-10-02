// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Xml;
using System.Xml.Linq;

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;

using Newtonsoft.Json;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Policies;

[
    Section(nameof(IInboundContext)),
    Section(nameof(IBackendContext)),
    Section(nameof(IOutboundContext)),
    Section(nameof(IOnErrorContext))
]
internal class JsonToXmlHandle : PolicyHandler<JsonToXmlConfig>
{
    public override string PolicyName => nameof(IInboundContext.JsonToXml);

    protected override void Handle(GatewayContext context, JsonToXmlConfig config)
    {
        BodyConversionUtilities.ValidateApply(config.Apply, "json");
        if (config.NamespacePrefix is not null && string.IsNullOrWhiteSpace(config.NamespacePrefix)
            || config.AttributeBlockName is not null && string.IsNullOrWhiteSpace(config.AttributeBlockName)
            || config.NamespaceSeparator is { } separator && (char.IsWhiteSpace(separator) || char.IsControl(separator)))
        {
            throw new ArgumentException("Namespace and attribute options must not be empty, whitespace, or control characters.", nameof(config));
        }

        var message = BodyConversionUtilities.Message(context, this);
        if (!BodyConversionUtilities.ShouldApply(
            context, message, config.Apply, config.ConsiderAcceptHeader, "json", "xml"))
        {
            return;
        }

        using var document = JsonDocument.Parse(BodyConversionUtilities.Body(message, PolicyName));
        if (document.RootElement.ValueKind != JsonValueKind.Object)
        {
            throw new ArgumentException("JSON-to-XML conversion requires an object containing one root element.");
        }

        var properties = Properties(document.RootElement);
        var roots = properties.Where(property => property.Name != "?xml").ToArray();
        if (roots.Length != 1)
        {
            throw new ArgumentException("JSON-to-XML conversion requires exactly one root element.");
        }

        var value = roots[0].Value;
        if (value.ValueKind == JsonValueKind.Array)
        {
            if (value.GetArrayLength() != 1)
            {
                throw new ArgumentException("A root array must contain exactly one element.");
            }

            value = value[0];
        }

        var namespaces = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["xml"] = XNamespace.Xml.NamespaceName,
        };
        var root = Element(roots[0].Name, value, config, namespaces);
        var declaration = document.RootElement.TryGetProperty("?xml", out var xml)
            ? Declaration(xml).ToString()
            : string.Empty;
        var converted = declaration + root.ToString(SaveOptions.DisableFormatting);
        BodyConversionUtilities.ReplaceBody(message, converted, "application/xml");
    }

    private static XElement Element(
        string name,
        JsonElement value,
        JsonToXmlConfig config,
        Dictionary<string, string> inheritedNamespaces)
    {
        if (value.ValueKind == JsonValueKind.Array)
        {
            throw new ArgumentException("Nested JSON arrays cannot be represented as XML elements.");
        }

        var properties = value.ValueKind == JsonValueKind.Object ? Properties(value) : [];
        var attributes = properties
            .Where(property => property.Name == config.AttributeBlockName)
            .SelectMany(property =>
            {
                if (property.Value.ValueKind != JsonValueKind.Object)
                {
                    throw new ArgumentException("The attribute block must be a JSON object.");
                }

                return Properties(property.Value);
            })
            .ToArray();
        var namespaces = new Dictionary<string, string>(inheritedNamespaces, StringComparer.Ordinal);
        var declarations = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var property in properties.Where(property => property.Name != config.AttributeBlockName).Concat(attributes))
        {
            var prefix = NamespacePrefix(property.Name, config);
            if (prefix is null)
            {
                continue;
            }

            if (property.Value.ValueKind != JsonValueKind.String)
            {
                throw new ArgumentException("Namespace declarations must contain string values.");
            }

            var uri = property.Value.GetString()!;
            XmlConvert.VerifyXmlChars(uri);
            if (prefix.Length > 0)
            {
                XmlConvert.VerifyNCName(prefix);
            }

            if (prefix.Length > 0 && uri.Length == 0 || prefix == "xmlns"
                || uri == XNamespace.Xmlns.NamespaceName
                || (prefix == "xml") != (uri == XNamespace.Xml.NamespaceName))
            {
                throw new ArgumentException($"Invalid namespace declaration for prefix '{prefix}'.");
            }

            if (!declarations.TryAdd(prefix, uri))
            {
                throw new ArgumentException($"Duplicate namespace declaration for prefix '{prefix}'.");
            }

            namespaces[prefix] = uri;
        }

        var element = new XElement(Name(name, config, namespaces, false));
        foreach (var (prefix, uri) in declarations)
        {
            var attributeName = prefix.Length == 0 ? XName.Get("xmlns") : XNamespace.Xmlns + prefix;
            element.Add(new XAttribute(attributeName, uri));
        }

        foreach (var property in properties.Where(property => property.Name.StartsWith('@')).Concat(attributes))
        {
            if (NamespacePrefix(property.Name, config) is not null || property.Name == config.AttributeBlockName)
            {
                continue;
            }

            var attributeName = property.Name.StartsWith('@') ? property.Name[1..] : property.Name;
            element.Add(new XAttribute(Name(attributeName, config, namespaces, true), Scalar(property.Value, config)));
        }

        if (value.ValueKind != JsonValueKind.Object)
        {
            if (value.ValueKind != JsonValueKind.Null)
            {
                element.Add(new XText(Scalar(value, config)));
            }

            return element;
        }

        foreach (var property in properties)
        {
            if (property.Name == config.AttributeBlockName || property.Name.StartsWith('@')
                || NamespacePrefix(property.Name, config) is not null)
            {
                continue;
            }

            if (property.Value.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in property.Value.EnumerateArray())
                {
                    AddContent(element, property.Name, item, config, namespaces);
                }
            }
            else
            {
                AddContent(element, property.Name, property.Value, config, namespaces);
            }
        }

        return element;
    }

    private static void AddContent(
        XElement element,
        string name,
        JsonElement value,
        JsonToXmlConfig config,
        Dictionary<string, string> namespaces)
    {
        switch (name)
        {
            case "#text":
            case "#whitespace":
            case "#significant-whitespace":
                element.Add(new XText(Scalar(value, config)));
                break;
            case "#cdata-section":
                element.Add(new XCData(Scalar(value, config)));
                break;
            case "#comment":
                element.Add(new XComment(Scalar(value, config)));
                break;
            default:
                if (name.StartsWith('?'))
                {
                    element.Add(new XProcessingInstruction(name[1..], Scalar(value, config)));
                }
                else
                {
                    element.Add(Element(name, value, config, namespaces));
                }

                break;
        }
    }

    private static XName Name(
        string name,
        JsonToXmlConfig config,
        Dictionary<string, string> namespaces,
        bool attribute)
    {
        var index = name.IndexOf(':');
        if (index >= 0)
        {
            var prefix = name[..index];
            if (!namespaces.TryGetValue(prefix, out var uri) || prefix.Length == 0)
            {
                throw new ArgumentException($"Undeclared namespace prefix '{prefix}'.");
            }

            return XName.Get(XmlConvert.VerifyNCName(name[(index + 1)..]), uri);
        }

        var separator = config.NamespaceSeparator ?? '_';
        var namespacePrefix = namespaces.Keys
            .Where(prefix => prefix.Length > 0 && name.StartsWith(prefix + separator, StringComparison.Ordinal))
            .OrderByDescending(prefix => prefix.Length)
            .FirstOrDefault();
        if (namespacePrefix is not null)
        {
            return XName.Get(XmlConvert.VerifyNCName(name[(namespacePrefix.Length + 1)..]), namespaces[namespacePrefix]);
        }

        var defaultNamespace = attribute ? string.Empty : namespaces.GetValueOrDefault(string.Empty, string.Empty);
        return XName.Get(XmlConvert.VerifyNCName(name), defaultNamespace);
    }

    private static string? NamespacePrefix(string name, JsonToXmlConfig config)
    {
        if (name == "@xmlns")
        {
            return string.Empty;
        }

        if (name.StartsWith("@xmlns:", StringComparison.Ordinal))
        {
            if (name.Length == 7)
            {
                throw new ArgumentException($"Namespace declaration '{name}' has an empty prefix.");
            }

            return name[7..];
        }

        if (config.NamespacePrefix is not { } prefix || !name.StartsWith(prefix, StringComparison.Ordinal))
        {
            return null;
        }

        var suffix = name[prefix.Length..];
        if (suffix.Length > 0 && (suffix[0] == (config.NamespaceSeparator ?? '_') || suffix[0] == ':'))
        {
            suffix = suffix[1..];
            if (suffix.Length == 0)
            {
                throw new ArgumentException($"Namespace declaration '{name}' has an empty prefix.");
            }
        }

        return suffix;
    }

    private static string Scalar(JsonElement value, JsonToXmlConfig config)
    {
        if (value.ValueKind == JsonValueKind.String)
        {
            var text = value.GetString()!;
            if (config.ParseDate != false)
            {
                using var input = new StringReader(value.GetRawText());
                using var reader = new JsonTextReader(input)
                {
                    DateParseHandling = DateParseHandling.DateTimeOffset,
                    Culture = CultureInfo.InvariantCulture,
                };
                reader.Read();
                if (reader.Value is DateTimeOffset date)
                {
                    text = date.Offset == TimeSpan.Zero
                        ? XmlConvert.ToString(date.UtcDateTime, XmlDateTimeSerializationMode.RoundtripKind)
                        : XmlConvert.ToString(date);
                }
            }

            return XmlConvert.VerifyXmlChars(text);
        }

        return value.ValueKind switch
        {
            JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False => value.GetRawText(),
            JsonValueKind.Null => string.Empty,
            _ => throw new ArgumentException("XML attributes and text nodes must contain scalar JSON values."),
        };
    }

    private static JsonProperty[] Properties(JsonElement value)
    {
        var properties = value.EnumerateObject().ToArray();
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in properties)
        {
            if (!names.Add(property.Name))
            {
                throw new ArgumentException($"Duplicate JSON property '{property.Name}'.");
            }
        }

        return properties;
    }

    private static XDeclaration Declaration(JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Object)
        {
            throw new ArgumentException("The XML declaration must be a JSON object.");
        }

        string? encoding = null;
        string? standalone = null;
        foreach (var property in Properties(value))
        {
            if (property.Value.ValueKind != JsonValueKind.String)
            {
                throw new ArgumentException("XML declaration properties must be strings.");
            }

            var text = property.Value.GetString()!;
            switch (property.Name)
            {
                case "@version" when text == "1.0": break;
                case "@encoding": encoding = text; break;
                case "@standalone" when text is "yes" or "no": standalone = text; break;
                default: throw new ArgumentException($"Unsupported XML declaration property '{property.Name}'.");
            }
        }

        if (encoding is not null)
        {
            if (encoding.Length == 0 || !char.IsAsciiLetter(encoding[0])
                || encoding.Any(character => !char.IsAsciiLetterOrDigit(character) && character is not ('.' or '_' or '-')))
            {
                throw new ArgumentException("Invalid XML declaration encoding.");
            }

            if (Encoding.GetEncoding(encoding).CodePage != Encoding.UTF8.CodePage)
            {
                throw new ArgumentException("The XML declaration must use UTF-8 to match the emulator body encoding.");
            }
        }

        return new XDeclaration("1.0", encoding, standalone);
    }
}