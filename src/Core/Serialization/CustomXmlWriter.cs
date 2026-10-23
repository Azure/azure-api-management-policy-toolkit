// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Serialization;

public sealed class CustomXmlWriter : IDisposable
{
    private readonly XmlWriter _xmlWriter;
    private readonly bool _rawExpressions;

    public static CustomXmlWriter Create(StringBuilder stringBuilder, XmlWriterSettings? options = null,
        bool rawExpressions = true) =>
        new CustomXmlWriter(XmlWriter.Create(stringBuilder, options), rawExpressions);

    public static CustomXmlWriter Create(string outputFileName, XmlWriterSettings? options = null,
        bool rawExpressions = true) =>
        new CustomXmlWriter(XmlWriter.Create(outputFileName, options), rawExpressions);

    CustomXmlWriter(XmlWriter xmlWriter, bool rawExpressions)
    {
        _xmlWriter = xmlWriter;
        _rawExpressions = rawExpressions;
    }

    public void Flush() => _xmlWriter.Flush();

    public void Dispose() => _xmlWriter.Dispose();

    public void Write(XComment comment) => comment.WriteTo(_xmlWriter);

    public void Write(XElement element)
    {
        _xmlWriter.WriteStartElement(element.GetPrefixOfNamespace(element.Name.Namespace), element.Name.LocalName,
            element.Name.NamespaceName);

        if (element.HasAttributes)
        {
            WriteAttributes(element.Attributes());
        }

        if (element.HasElements)
        {
            WriteElements(element.Elements());
        }
        else if (!string.IsNullOrEmpty(element.Value))
        {
            WriteValue(element.Value);
        }

        _xmlWriter.WriteEndElement();
    }

    private void WriteElements(IEnumerable<XElement> elements)
    {
        foreach (var element in elements)
        {
            Write(element);
        }
    }

    private void WriteAttributes(IEnumerable<XAttribute> attributes)
    {
        foreach (var attribute in attributes)
        {
            _xmlWriter.WriteStartAttribute(attribute.Parent?.GetPrefixOfNamespace(attribute.Name.Namespace),
                attribute.Name.LocalName, attribute.Name.NamespaceName);
            WriteValue(attribute.Value);
            _xmlWriter.WriteEndAttribute();
        }
    }

    private void WriteValue(string value)
    {
        var trimmed = value.TrimStart();
        if (_rawExpressions && (trimmed.StartsWith("@(") || trimmed.StartsWith("@{")))
        {
            // rawxml format: policy expressions are written verbatim, keeping any
            // characters that are otherwise reserved in XML (e.g. unescaped quotes
            // and angle brackets in generic type arguments like As<JObject>()).
            // This mirrors the Razor-like format Azure API Management accepts and
            // returns for the 'rawxml' policy content type.
            _xmlWriter.WriteRaw(value);
        }
        else
        {
            // xml format: everything is XML-encoded, so expressions become valid XML
            // (e.g. As<JObject>() -> As&lt;JObject&gt;(), "x" -> &quot;x&quot;). This
            // matches the 'xml' policy content type Azure API Management persists.
            _xmlWriter.WriteString(value);
        }
    }
}