// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Xml.Linq;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Serialization;

[TestClass]
public class RazorCodeFormatterLexicalTests
{
    [TestMethod]
    public void ShouldNotCountBracketsInStringAndCharLiterals()
    {
        var code = "<element>@{var s = \"}\"; var t = '{'; return s + t + \"{{\";}</element>";

        var result = RazorCodeFormatter.Format(code);

        result.Should().Be(
            """
            <element>@{
            var s = "}";
            var t = '{';
            return s + t + "{{";
            }</element>
            """.ReplaceLineEndings());
    }

    [TestMethod]
    [DataRow("@(\")\" + context.RequestId)", "@(\")\" + context.RequestId)")]
    [DataRow("@(@\"\"\")\" + context.RequestId)", "@(@\"\"\")\" + context.RequestId)")]
    [DataRow("@(')'.ToString()+'\\''.ToString())", "@(')'.ToString() + '\\''.ToString())")]
    [DataRow("@(\"\\\")\"+context.RequestId)", "@(\"\\\")\" + context.RequestId)")]
    [DataRow("@($\"){context.RequestId}(\"+1)", "@($\"){context.RequestId}(\" + 1)")]
    [DataRow("@(context.RequestId /* ) */+1)", "@(context.RequestId /* ) */ + 1)")]
    public void ShouldFindClosingBracketOfOneLineCode(string expression, string formatted)
    {
        var result = RazorCodeFormatter.Format($"<element att1=\"{expression}\" att2=\"@(1+1)\" />");

        result.Should().Be($"<element att1=\"{formatted}\" att2=\"@(1 + 1)\" />");
    }

    [TestMethod]
    public void ShouldNotCountBracketsInComments()
    {
        var code =
            """
            <element>@{
            // comment with } and )
            /* another } one */
            return 1;
            }</element>
            """.ReplaceLineEndings();

        var result = RazorCodeFormatter.Format(code);

        result.Should().Be(code);
    }

    [TestMethod]
    [DataRow("<element>@{ return '{'; </element>")]
    [DataRow("<element att1=\"@(context.Method(\" />")]
    [DataRow("<element>@{ return \"unterminated; }</element>")]
    [DataRow("<element>@(</element>")]
    [DataRow("@{")]
    public void ShouldLeaveUnbalancedCodeAsIs(string code)
    {
        var result = RazorCodeFormatter.Format(code);

        result.Should().Be(code);
        RazorCodeFormatter.ToCleanXml(code, out var markerToCode).Should().Be(code);
        markerToCode.Should().BeEmpty();
    }

    [TestMethod]
    public void ShouldFormatBalancedCodeBeforeUnbalancedCode()
    {
        var result = RazorCodeFormatter.Format("<a b=\"@(1+1)\">@{ return '{'; </a>");

        result.Should().Be("<a b=\"@(1 + 1)\">@{ return '{'; </a>");
    }

    [TestMethod]
    public void ShouldNotTreatExpressionStartInsideExpressionAsNewExpression()
    {
        var result = RazorCodeFormatter.Format("<a>@(\"@(\"+1)</a><b>@(2+2)</b>");

        result.Should().Be("<a>@(\"@(\" + 1)</a><b>@(2 + 2)</b>");
    }

    [TestMethod]
    public void ShouldReplaceCodeWithBracketInStringWithOneMarker()
    {
        var result = RazorCodeFormatter.ToCleanXml("<c>@{ return \"}\"; }</c>", out var markerToCode);

        markerToCode.Should().ContainSingle();
        markerToCode.Single().Value.Should().Be("@{return \"}\";}");
        result.Should().Be($"<c>{markerToCode.Single().Key}</c>");
    }

    [TestMethod]
    public void ShouldFormatExpressionsOfMixedContentAndKeepSiblings()
    {
        var element = XElement.Parse("<foo>@(1+1)<b>@(2+2)</b><!-- c -->@(3+3)</foo>");

        RazorCodeFormatter.FormatExpressions(element);

        element.ToString(SaveOptions.DisableFormatting).Should()
            .Be("<foo>@(1 + 1)<b>@(2 + 2)</b><!-- c -->@(3 + 3)</foo>");
    }
}
