// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Serialization;

[TestClass]
public class CustomXmlWriterMixedContentTests
{
    [TestMethod]
    [DataRow("<foo>text <b>bold</b> tail<!-- c --></foo>")]
    [DataRow("<foo><!-- c --><b>bold</b></foo>")]
    [DataRow("<foo>text<!-- c --></foo>")]
    [DataRow("<foo><!-- c --></foo>")]
    [DataRow("<foo><b /><![CDATA[a < b]]><c /></foo>")]
    [DataRow("<foo>a &lt; b<b /></foo>")]
    public void ShouldWriteAllNodesInOrder(string elementString)
    {
        var element = XElement.Parse(elementString);

        var result = Serialize(element);

        result.Should().Be(elementString);
    }

    [TestMethod]
    public void ShouldWriteExpressionNextToElement()
    {
        var element = new XElement("foo", new XText("@(\"a\" + \"<\")"), new XElement("b"));

        Serialize(element).Should().Be("<foo>@(\"a\" + \"<\")<b /></foo>");
        Serialize(element, rawExpressions: false).Should().Be("<foo>@(\"a\" + \"&lt;\")<b /></foo>");
    }

    [TestMethod]
    public void ShouldKeepIndentationOfElementOnlyContent()
    {
        var element = XElement.Parse("<a>\n  <b>\n    <c>text</c>\n  </b>\n  <d />\n</a>",
            LoadOptions.PreserveWhitespace);

        var result = Serialize(element, indent: true);

        result.Should().Be(
            """
            <a>
                <b>
                    <c>text</c>
                </b>
                <d />
            </a>
            """.ReplaceLineEndings());
    }

    [TestMethod]
    public void ShouldIndentCommentBetweenElements()
    {
        var element = XElement.Parse("<a><b /><!-- c --><d /></a>");

        var result = Serialize(element, indent: true);

        result.Should().Be(
            """
            <a>
                <b />
                <!-- c -->
                <d />
            </a>
            """.ReplaceLineEndings());
    }

    private static string Serialize(XElement element, bool rawExpressions = true, bool indent = false)
    {
        var settings = new XmlWriterSettings
        {
            OmitXmlDeclaration = true,
            ConformanceLevel = ConformanceLevel.Fragment,
            Indent = indent,
            IndentChars = "    ",
            NewLineChars = Environment.NewLine,
        };
        var builder = new StringBuilder();
        using (var writer = CustomXmlWriter.Create(builder, settings, rawExpressions))
        {
            writer.Write(element);
        }

        return builder.ToString();
    }
}
