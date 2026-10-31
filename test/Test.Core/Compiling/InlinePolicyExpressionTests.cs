// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Xml.Linq;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling;

[TestClass]
public class InlinePolicyExpressionTests
{
    [TestMethod]
    [DataRow("""<a b="x @(1) y" />""", "x @(1) y")]
    [DataRow("""<a b="x @("1") y" />""", "x @(\"1\") y")]
    [DataRow("""<a b="@("1")@("2")" />""", "@(\"1\")@(\"2\")")]
    public void ShouldRestoreExpressionsInsideAttributeValue(string policy, string expected)
    {
        var element = CompileInlinePolicy(policy);

        element.Attribute("b")!.Value.Should().Be(expected);
    }

    [TestMethod]
    public void ShouldRestoreExpressionsInMixedContent()
    {
        var element = CompileInlinePolicy("""<c>@("a" + "<")<d e="@("f")" /><!-- @("g") -->@("h")</c>""");

        element.ToString(SaveOptions.DisableFormatting).Should().NotContain("__expression__");
        element.Nodes().OfType<XText>().Select(t => t.Value).Should().Equal("@(\"a\" + \"<\")", "@(\"h\")");
        element.Element("d")!.Attribute("e")!.Value.Should().Be("@(\"f\")");
        element.Nodes().OfType<XComment>().Single().Value.Should().Be(" @(\"g\") ");
    }

    private static XElement CompileInlinePolicy(string policy)
    {
        var result = Compile(policy);
        result.Should().BeSuccessful();
        return result.Document.Element("inbound")!.Elements().Single();
    }

    private static IDocumentCompilationResult Compile(string policy)
    {
        var literal = Microsoft.CodeAnalysis.CSharp.SymbolDisplay.FormatLiteral(policy, quote: true);
        var code =
            $$"""
              [Document]
              public class PolicyDocument : IDocument
              {
                public void Inbound(IInboundContext context)
                {
                    context.InlinePolicy({{literal}});
                }
              }
              """;
        return code.CompileDocument();
    }

    [TestMethod]
    [DataRow("""<a><!-- @(i--) --><b /></a>""")]
    [DataRow("""<a><!-- @(i--) --></a>""")]
    public void ShouldReportACommentThatARestoredExpressionMakesInvalid(string policy)
    {
        Compile(policy).Errors.Should().ContainSingle(error => error.Id == "APIM2008");
    }

    [TestMethod]
    public void ShouldReportAProcessingInstructionThatARestoredExpressionMakesInvalid()
    {
        Compile("""<a><?note @("?>")?><b /></a>""").Errors.Should().ContainSingle(error => error.Id == "APIM2008");
    }

    [TestMethod]
    public void ShouldRestoreExpressionsInAProcessingInstruction()
    {
        var element = CompileInlinePolicy("""<a><?note @(1 + 1)?><b /></a>""");

        element.ToString(SaveOptions.DisableFormatting).Should().NotContain("__expression__").And.Contain("@(1 + 1)");
    }
}
