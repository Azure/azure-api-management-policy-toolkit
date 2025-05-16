// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

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
            .ContainValue("""@{if (context.Request.IpAddress.StartsWith("10.0.0.")){return "a";}else{return "b";}}""");
        result.Should().Match("""
                              <element att1="*">
                                  <v>*</v>
                              <element>
                              """.ReplaceLineEndings());
    }
}