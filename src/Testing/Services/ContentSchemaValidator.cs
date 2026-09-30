// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Schema;

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Services;

internal static class ContentSchemaValidator
{
    internal static IReadOnlyList<string> Validate(ContentValidationSchema schema, ValidateContent rule, string content)
    {
        ArgumentNullException.ThrowIfNull(schema);
        ArgumentException.ThrowIfNullOrWhiteSpace(schema.Definition);
        if (rule.ValidateAs == "json")
        {
            if (schema.Format != "json")
            {
                throw new ArgumentException("JSON content validation requires a json schema.");
            }
            using var validator = new JsonSchemaValidator(schema.Definition, rule.SchemaRef);
            return validator.Validate(content, rule.AllowAdditionalProperties, rule.CaseInsensitivePropertyNames == true);
        }
        if (rule.ValidateAs is not ("xml" or "soap"))
        {
            throw new NotSupportedException($"Content validation engine '{rule.ValidateAs}' is not supported.");
        }
        if (schema.Format != "xml")
        {
            throw new ArgumentException("XML/SOAP content validation requires an xml schema.");
        }
        if (rule.SchemaRef is not null || rule.AllowAdditionalProperties is not null || rule.CaseInsensitivePropertyNames is not null)
        {
            throw new NotSupportedException("SchemaRef, AllowAdditionalProperties, and CaseInsensitivePropertyNames are JSON-only validation options.");
        }

        var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null };
        using var schemaText = new StringReader(schema.Definition);
        using var schemaReader = XmlReader.Create(schemaText, settings);
        var schemaDocument = XDocument.Load(schemaReader);
        XNamespace xsd = XmlSchema.Namespace;
        if (schemaDocument.Descendants().Any(element =>
                element.Name == xsd + "include" || element.Name == xsd + "import" || element.Name == xsd + "redefine" ||
                element.Attribute("schemaLocation") is not null))
        {
            throw new NotSupportedException(
                "External XSD include/import/redefine and schemaLocation resolution are unsupported. Supply a self-contained schema.");
        }
        var schemas = new XmlSchemaSet { XmlResolver = null };
        using (var reader = schemaDocument.CreateReader())
        {
            schemas.Add(null, reader);
        }
        schemas.Compile();

        XDocument payload;
        try
        {
            using var payloadBytes = new MemoryStream(Encoding.UTF8.GetBytes(content));
            using var reader = XmlReader.Create(payloadBytes, settings);
            payload = XDocument.Load(reader);
        }
        catch (XmlException error)
        {
            return [$"Invalid XML payload: {error.Message}"];
        }
        var declaredEncoding = payload.Declaration?.Encoding;
        if (!string.IsNullOrEmpty(declaredEncoding) &&
            !string.Equals(declaredEncoding, "utf-8", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(declaredEncoding, "utf8", StringComparison.OrdinalIgnoreCase))
        {
            return [$"XML encoding declaration '{declaredEncoding}' does not match the UTF-8 message representation."];
        }

        var root = payload.Root ?? throw new InvalidOperationException("XML parsing returned no document element.");
        if (rule.ValidateAs == "soap")
        {
            var envelopeNamespace = root.Name.NamespaceName;
            if (root.Name.LocalName != "Envelope" ||
                envelopeNamespace is not ("http://schemas.xmlsoap.org/soap/envelope/" or "http://www.w3.org/2003/05/soap-envelope"))
            {
                return ["SOAP content must contain a SOAP 1.1 or 1.2 Envelope."];
            }
            var bodies = root.Elements(root.Name.Namespace + "Body").ToArray();
            var headers = root.Elements(root.Name.Namespace + "Header").ToArray();
            if (bodies.Length != 1 || bodies[0].Elements().Count() != 1 ||
                bodies[0].Nodes().OfType<XText>().Any(text => !string.IsNullOrWhiteSpace(text.Value)) ||
                headers.Length > 1 || root.Elements().Any(element =>
                    element.Name != root.Name.Namespace + "Header" && element.Name != root.Name.Namespace + "Body") ||
                root.Nodes().OfType<XText>().Any(text => !string.IsNullOrWhiteSpace(text.Value)) ||
                root.Elements().Last().Name != root.Name.Namespace + "Body")
            {
                return ["A SOAP Envelope must contain an optional Header followed by one Body with exactly one payload element."];
            }
            root = CopyWithInScopeNamespaces(bodies[0].Elements().Single());
        }
        if (!schemas.GlobalElements.Contains(new XmlQualifiedName(root.Name.LocalName, root.Name.NamespaceName)))
        {
            return [$"XML document element '{root.Name}' is not declared in the configured schema."];
        }

        var errors = new List<string>();
        var validationSettings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            ValidationType = ValidationType.Schema,
            Schemas = schemas,
            ValidationFlags = XmlSchemaValidationFlags.ReportValidationWarnings |
                              XmlSchemaValidationFlags.ProcessIdentityConstraints,
        };
        validationSettings.ValidationEventHandler += (_, error) =>
        {
            if (error.Severity == XmlSeverityType.Error)
            {
                errors.Add(error.Message);
            }
        };
        using var rootText = new StringReader(root.ToString(SaveOptions.DisableFormatting));
        using var validationReader = XmlReader.Create(rootText, validationSettings);
        while (validationReader.Read()) { }
        return errors;
    }

    private static XElement CopyWithInScopeNamespaces(XElement element)
    {
        var copy = new XElement(element);
        var declarations = new HashSet<XName>(
            element.Attributes().Where(attribute => attribute.IsNamespaceDeclaration).Select(attribute => attribute.Name));
        foreach (var ancestor in element.Ancestors())
        {
            foreach (var declaration in ancestor.Attributes().Where(attribute => attribute.IsNamespaceDeclaration))
            {
                if (declarations.Add(declaration.Name))
                {
                    copy.Add(new XAttribute(declaration));
                }
            }
        }
        return copy;
    }
}