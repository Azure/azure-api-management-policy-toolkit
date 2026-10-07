// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Xml.Linq;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling;

[TestClass]
public class InlinePolicyExpressionTests
{
    private const string RawPolicy =
        """<my-custom-policy cond="@(context.Request.Method == "GET" && context.Request.Headers.Count < 3)"><thing>@(context.Request.Body.As<JObject>()["a"].ToString())</thing></my-custom-policy>""";

    private const string Condition = """@(context.Request.Method == "GET" && context.Request.Headers.Count < 3)""";
    private const string Thing = """@(context.Request.Body.As<JObject>()["a"].ToString())""";

    [TestMethod]
    public void ShouldCompileRawExpressions()
    {
        var policy = CompileInlinePolicy(RawPolicy);

        policy.Attribute("cond")!.Value.Should().Be(Condition);
        policy.Element("thing")!.Value.Should().Be(Thing);
    }

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
    [DataRow("""<c>@{ return "}"; }</c>""", "@{return \"}\";}")]
    [DataRow("""<c>@(")".Length < 2)</c>""", "@(\")\".Length < 2)")]
    [DataRow("""<c>prefix @("a" + "b") suffix</c>""", "prefix @(\"a\" + \"b\") suffix")]
    public void ShouldRestoreExpressionsInsideElementValue(string policy, string expected)
    {
        var element = CompileInlinePolicy(policy);

        element.Value.Should().Be(expected);
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

    [TestMethod]
    public void ShouldKeepLineBreaksOfMultiLineExpressionInAttribute()
    {
        var element = CompileInlinePolicy("<a b=\"@{\n    var x = 1; // comment\n    return x;\n}\" />");

        element.Attribute("b")!.Value.Should().Be("@{var x = 1; // comment\nreturn x;}");
    }

    [TestMethod]
    public void ShouldReportErrorForUnbalancedExpression()
    {
        var result = Compile("""<c d="@("a" + (1)" />""");

        result.Errors.Should().ContainSingle();
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
    [DataRow("""<a b='@("a&amp;b")' />""", DisplayName = "well-formed XML")]
    [DataRow("""<a b="@("a&amp;b")" />""", DisplayName = "not well-formed XML")]
    public void ShouldReadExpressionAsWrittenWhetherOrNotThePolicyIsWellFormedXml(string policy)
    {
        // the policy is rawxml: an entity inside an expression is C# text, it is not unescaped
        var element = CompileInlinePolicy(policy);

        element.Attribute("b")!.Value.Should().Be("@(\"a&amp;b\")");
    }

    [TestMethod]
    public void ShouldKeepLeadingCommentOfInlinePolicyExpression()
    {
        var code = CompilerTestInitialize.InboundDocument(
            """"
            context.InlinePolicy("""
                <set-variable name="x" value="@{
                    // leading comment
                    var a = 1;
                    /* block */ return a;
                }" />
                """);
            """");

        var result = code.CompileDocument();

        result.Should().BeSuccessful();
        result.Document.ToString().Should().Contain("// leading comment").And.Contain("/* block */");
    }

    [TestMethod]
    public void ShouldKeepCDataSectionWhenWritingADocument()
    {
        var result = CompilerTestInitialize.InboundDocument(
            """"
            context.InlinePolicy("""<set-body><![CDATA[<a> & b]]></set-body>""");
            """").CompileDocument();
        result.Should().BeSuccessful();

        var written = new System.Text.StringBuilder();
        using (var writer = Serialization.CustomXmlWriter.Create(
                   written, new System.Xml.XmlWriterSettings { Indent = true, OmitXmlDeclaration = true }))
        {
            writer.Write(result.Document);
        }

        written.ToString().Should().Contain("<set-body><![CDATA[<a> & b]]></set-body>");
    }
}
