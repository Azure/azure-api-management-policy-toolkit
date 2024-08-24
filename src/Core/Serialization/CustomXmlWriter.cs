
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace Mielek.Azure.ApiManagement.PolicyToolkit.Serialization;

public sealed class CustomXmlWriter : IDisposable
{
    private readonly XmlWriter _xmlWriter;

    public static CustomXmlWriter Create(StringBuilder stringBuilder, XmlWriterSettings? options = null) => new CustomXmlWriter(XmlWriter.Create(stringBuilder, options));
    public static CustomXmlWriter Create(string outputFileName, XmlWriterSettings? options = null) => new CustomXmlWriter(XmlWriter.Create(outputFileName, options));

    CustomXmlWriter(XmlWriter xmlWriter)
    {
        _xmlWriter = xmlWriter;
    }

    public void Flush() => _xmlWriter.Flush();

    public void Dispose() => _xmlWriter.Dispose();

    public void Write(XElement element)
    {
        _xmlWriter.WriteStartElement(element.Name.LocalName);

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
            _xmlWriter.WriteStartAttribute(attribute.Name.LocalName);
            WriteValue(attribute.Value);
            _xmlWriter.WriteEndAttribute();
        }
    }

    private void WriteValue(string value)
    {
        var trimmed = value.TrimStart();
        if (trimmed.StartsWith("@(") || trimmed.StartsWith("@{"))
        {
            _xmlWriter.WriteRaw(value);
        }
        else
        {
            _xmlWriter.WriteString(value);
        }
    }
}