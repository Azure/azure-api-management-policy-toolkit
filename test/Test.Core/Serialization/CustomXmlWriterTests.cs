// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Serialization;

[TestClass]
public class CustomXmlWriterTests
{
    private static XmlWriterSettings TestSettings = new XmlWriterSettings()
    {
        OmitXmlDeclaration = true, ConformanceLevel = ConformanceLevel.Fragment,
    };

    [TestMethod]
    [DataRow("<empty />")]
    [DataRow("<with-attributes att1=\"some-text\" att2=\"123\" />")]
    [DataRow("<with-value>some text&lt;&gt;</with-value>")]
    [DataRow("<with-elements><el1 att1=\"text\">txet</el1><el2 /></with-elements>")]
    public void ShouldWriteSimpleElement(string elementString)
    {
        var element = XElement.Parse(elementString);

        var result = Serialize(element);

        result.Should().Be(elementString);
    }

    [TestMethod]
    [DataRow("@( context.Request.IpAddress.StartsWith(\"10.0.0.\") )")]
    [DataRow("@{ return context.Request.IpAddress.StartsWith(\"10.0.0.\"); }")]
    [DataRow("""
             @{ 
                return context.Request.IpAddress.StartsWith("10.0.0.");
             }
             """)]
    public void ShouldSerializeExpressionInAttribute(string expression)
    {
        expression = expression.ReplaceLineEndings();
        var element = new XElement("element", new XAttribute("att1", expression));

        var result = Serialize(element);

        result.Should().Be($"<element att1=\"{expression}\" />");
    }

    [TestMethod]
    [DataRow("@( context.Request.IpAddress.StartsWith(\"10.0.0.\") )")]
    [DataRow("@{ return context.Request.IpAddress.StartsWith(\"10.0.0.\"); }")]
    [DataRow("""
             @{ 
                return context.Request.IpAddress.StartsWith("10.0.0.");
             }
             """)]
    public void ShouldSerializeExpressionInElementValue(string expression)
    {
        expression = expression.ReplaceLineEndings();
        var element = new XElement("element", expression);

        var result = Serialize(element);

        result.Should().Be($"<element>{expression}</element>");
    }

    private string Serialize(XElement element)
    {
        var builder = new StringBuilder();
        using (var writer = CustomXmlWriter.Create(builder, TestSettings))
        {
            writer.Write(element);
        }

        return builder.ToString();
    }
}