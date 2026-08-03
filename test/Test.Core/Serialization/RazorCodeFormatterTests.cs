// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Serialization;

[TestClass]
public class RazorCodeFormatterTests
{
    [TestMethod]
    [DataRow(
        "<element att1=\"@(var a=1;)\" />",
        "<element att1=\"@(var a = 1;)\" />")]
    [DataRow(
        "<element>@(var a=1;)</element>",
        "<element>@(var a = 1;)</element>")]
    [DataRow(
        "<element att1=\"@(context.Request.IpAddress.StartsWith(\"10.0.0.\")?\"a\".ToString():\"b\")\" />",
        "<element att1=\"@(context.Request.IpAddress.StartsWith(\"10.0.0.\") ? \"a\".ToString() : \"b\")\" />")]
    [DataRow(
        "<element>@(context.Request.IpAddress.StartsWith(\"10.0.0.\")?\"a\".ToString():\"b\")</element>",
        "<element>@(context.Request.IpAddress.StartsWith(\"10.0.0.\") ? \"a\".ToString() : \"b\")</element>")]
    public void ShouldFormatOneLineCode(string notFormatted, string formatted)
    {
        var result = RazorCodeFormatter.Format(notFormatted);
        result.Should().Be(formatted);
    }

    [TestMethod]
    [DataRow("<element>@{var a=1;}</element>",
        """
        <element>@{
        var a = 1;
        }</element>
        """)]
    [DataRow("<element>@{return context.Request.IpAddress.StartsWith(\"10.0.0.\")?\"a\":\"b\";}</element>",
        """
        <element>@{
        return context.Request.IpAddress.StartsWith("10.0.0.") ? "a" : "b";
        }</element>
        """)]
    [DataRow(
        """
        <element>@{
        if(context.Request.IpAddress.StartsWith("10.0.0."))
        { return "a"; }
        else
        { return "b"; }
        }</element>
        """,
        """
        <element>@{
        if (context.Request.IpAddress.StartsWith("10.0.0."))
        {
            return "a";
        }
        else
        {
            return "b";
        }
        }</element>
        """)]
    [DataRow(
        """
        <element att1="@{
        if(context.Request.IpAddress.StartsWith("10.0.0."))
        { return "a"; }
        else
        { return "b"; }
        }" />
        """,
        """
        <element att1="@{
        if (context.Request.IpAddress.StartsWith("10.0.0."))
        {
            return "a";
        }
        else
        {
            return "b";
        }
        }" />
        """)]
    public void ShouldFormatMultiLineCode(string notFormatted, string formatted)
    {
        var result = RazorCodeFormatter.Format(notFormatted.ReplaceLineEndings());
        result.Should().Be(formatted.ReplaceLineEndings());
    }

    [TestMethod]
    [DataRow(
        "<element att1=\"@(var a=1;)\" />",
        "@(var a = 1;)",
        "<element att1=\"{0}\" />")]
    [DataRow(
        "<element att1=\"@{var a=1;}\" />",
        "@{var a = 1;}",
        "<element att1=\"{0}\" />")]
    [DataRow(
        "<element>@(var a=1;)</element>",
        "@(var a = 1;)",
        "<element>{0}</element>")]
    [DataRow(
        "<element>@{var a=1;}</element>",
        "@{var a = 1;}",
        "<element>{0}</element>")]
    public void ShouldReplaceCodeWithMarkers(string code, string expression, string expected)
    {
        var result = RazorCodeFormatter.ToCleanXml(code, out var markerToCode);
        markerToCode.Should().HaveCount(1);
        var entry = markerToCode.First();
        entry.Value.Should().Be(expression);
        result.Should().NotBe(code).And.Be(string.Format(expected, entry.Key));
    }

    [TestMethod]
    public void ShouldReplaceAllExpressionsInCode()
    {
        var code =
            """
                <element att1="@(var a=1;)">
                    <v>@{
                    if (context.Request.IpAddress.StartsWith("10.0.0."))
                    {
                        return "a";
                    }
                    else
                    {
                        return "b";
                    }
                    }</v>
                <element>
                """.ReplaceLineEndings();
        var result = RazorCodeFormatter.ToCleanXml(code, out var markerToCode);

        markerToCode.Should().HaveCount(2);
        markerToCode.Should().ContainValue("@(var a = 1;)");
        markerToCode.Should()
            .ContainValue("@{if (context.Request.IpAddress.StartsWith(\"10.0.0.\"))\n{\nreturn \"a\";\n}\nelse\n{\nreturn \"b\";\n}}");
        result.Should().Match("""
                              <element att1="*">
                                  <v>*</v>
                              <element>
                              """.ReplaceLineEndings());
    }

    [TestMethod]
    public void ShouldFormatExpressionsOnElementValueBeforeEncoding()
    {
        var element = new XElement("element", "@(context.Request.Body.As<string>()  ??  \"x\")");

        RazorCodeFormatter.FormatExpressions(element);
        var result = SerializeXml(element);

        result.Should().Be("<element>@(context.Request.Body.As&lt;string&gt;() ?? \"x\")</element>");
    }

    [TestMethod]
    public void ShouldFormatExpressionsInAttributeBeforeEncoding()
    {
        var element = new XElement("element",
            new XAttribute("condition", "@(context.Request.Url.Path.StartsWith( \"/v1/\" ))"));

        RazorCodeFormatter.FormatExpressions(element);
        var result = SerializeXml(element);

        result.Should()
            .Be("<element condition=\"@(context.Request.Url.Path.StartsWith(&quot;/v1/&quot;))\" />");
    }

    private static string SerializeXml(XElement element)
    {
        var settings = new XmlWriterSettings
        {
            OmitXmlDeclaration = true, ConformanceLevel = ConformanceLevel.Fragment,
        };
        var builder = new StringBuilder();
        using (var writer = CustomXmlWriter.Create(builder, settings, rawExpressions: false))
        {
            writer.Write(element);
        }

        return builder.ToString();
    }
}