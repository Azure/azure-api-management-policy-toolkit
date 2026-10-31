// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Xml;
using System.Xml.Linq;

using Microsoft.Azure.ApiManagement.PolicyToolkit.IO;

namespace Test.Core.IO;

[TestClass]
public class FileUtilsTests
{
    private string _testFolder = null!;
    private string _sourceFolder = null!;
    private string _outputFolder = null!;
    private string _sourceFile = null!;

    [TestInitialize]
    public void Initialize()
    {
        _testFolder = Path.Combine(Path.GetTempPath(), $"policy-toolkit-file-utils-{Guid.NewGuid():N}");
        _sourceFolder = Path.Combine(_testFolder, "source");
        _outputFolder = Path.Combine(_testFolder, "output");
        _sourceFile = Path.Combine(_sourceFolder, "Policy.cs");
        Directory.CreateDirectory(_sourceFolder);
        File.WriteAllText(_sourceFile, string.Empty);
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_testFolder))
        {
            Directory.Delete(_testFolder, true);
        }
    }

    [TestMethod]
    public void WriteToFile_WithNestedOutputPath_ShouldWriteInsideOutputFolder()
    {
        var targetFile = FileUtils.WriteToFile(CreateData(Path.Combine("nested", "policy.xml")));

        targetFile.Should().Be(Path.Combine(_outputFolder, "nested", "policy.xml"));
        File.Exists(targetFile).Should().BeTrue();
    }

    [TestMethod]
    [DataRow("../escaped.xml")]
    [DataRow("../output-sibling/escaped.xml")]
    [DataRow(@"..\escaped.xml")]
    public void WriteToFile_WithOutputPathOutsideOutputFolder_ShouldThrow(string outputFilePath)
    {
        var action = () => FileUtils.WriteToFile(CreateData(outputFilePath));

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*outside the output folder*");
        Directory.Exists(_outputFolder).Should().BeFalse();
        File.Exists(Path.Combine(_testFolder, "escaped.xml")).Should().BeFalse();
        File.Exists(Path.Combine(_testFolder, "output-sibling", "escaped.xml")).Should().BeFalse();
    }

    [TestMethod]
    public void WriteToFile_WithSourceOutsideSourceFolder_ShouldThrow()
    {
        var outsideSourceFile = Path.Combine(_testFolder, "outside", "Policy.cs");
        Directory.CreateDirectory(Path.GetDirectoryName(outsideSourceFile)!);
        File.WriteAllText(outsideSourceFile, string.Empty);
        var data = CreateData("policy.xml");
        data.SourceFilePath = outsideSourceFile;

        var action = () => FileUtils.WriteToFile(data);

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*outside the output folder*");
        Directory.Exists(_outputFolder).Should().BeFalse();
    }

    [TestMethod]
    public void WriteToFile_WithLinkOutsideOutputFolder_ShouldThrow()
    {
        var outsideFolder = Path.Combine(_testFolder, "outside");
        var linkPath = Path.Combine(_outputFolder, "linked");
        Directory.CreateDirectory(_outputFolder);
        Directory.CreateDirectory(outsideFolder);

        try
        {
            Directory.CreateSymbolicLink(linkPath, outsideFolder);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or PlatformNotSupportedException)
        {
            Assert.Inconclusive($"Symbolic links are not supported in this test environment: {exception.Message}");
        }

        var action = () => FileUtils.WriteToFile(CreateData(Path.Combine("linked", "escaped.xml")));

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*outside the output folder*");
        File.Exists(Path.Combine(outsideFolder, "escaped.xml")).Should().BeFalse();
    }

    [TestMethod]
    public void WriteToFile_ShouldFormatExpressionsWithBracketsInLiteralsAndComments()
    {
        var element = new XElement("policies",
            new XElement("inbound",
                new XElement("set-variable",
                    new XAttribute("name", "a"),
                    new XAttribute("value", "@{var s = \"}\"; var t = '{'; return s + t + \"{{\";}")),
                new XElement("set-body", "@{\n// comment with } and )\nreturn context.Request.Body.As<string>();}"),
                new XElement("set-header",
                    new XAttribute("name", "x @(\"a\"+\"b\") y"),
                    new XElement("value", "@(\")\"+1)"))));

        var data = CreateData("policy.xml");
        data.Element = element;
        data.FormatCode = true;
        data.RawXml = true;
        data.XmlWriterSettings = new XmlWriterSettings
        {
            OmitXmlDeclaration = true,
            ConformanceLevel = ConformanceLevel.Fragment,
            Indent = true,
            IndentChars = "    ",
            NewLineChars = Environment.NewLine,
        };

        var lines = File.ReadAllLines(FileUtils.WriteToFile(data));

        lines[0].Should().StartWith("<!--");
        lines[1].Should().StartWith("<!--");
        string.Join(Environment.NewLine, lines.Skip(2)).Should().Be(
            """
            <policies>
                <inbound>
                    <set-variable name="a" value="@{
            var s = "}";
            var t = '{';
            return s + t + "{{";
            }" />
                    <set-body>@{
            // comment with } and )
            return context.Request.Body.As<string>();
            }</set-body>
                    <set-header name="x @(&quot;a&quot;+&quot;b&quot;) y">
                        <value>@(")" + 1)</value>
                    </set-header>
                </inbound>
            </policies>
            """.ReplaceLineEndings());
    }

    private FileUtils.Data CreateData(string outputFilePath)
    {
        return new FileUtils.Data
        {
            Element = new XElement("policies"),
            XmlWriterSettings = new XmlWriterSettings
            {
                OmitXmlDeclaration = true,
                ConformanceLevel = ConformanceLevel.Fragment,
            },
            FormatCode = false,
            SourceFilePath = _sourceFile,
            SourceFolder = _sourceFolder,
            OutputFilePath = outputFilePath,
            OutputFolder = _outputFolder,
        };
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void WriteToFile_ShouldLeaveTheGivenDocumentAsItIs(bool rawXml)
    {
        var element = new XElement("policies", new XAttribute("a", "@(1+1)"));
        var data = CreateData("policy.xml");
        data.Element = element;
        data.FormatCode = true;
        data.RawXml = rawXml;

        FileUtils.WriteToFile(data);

        element.Attribute("a")!.Value.Should().Be("@(1+1)");
    }

    [TestMethod]
    [DataRow("""<a>@(1 + 1) &amp; b</a>""")]
    [DataRow("""<a>@(1 + 1) &lt; b</a>""")]
    [DataRow("""<a b="@(1 + 1) &amp; c" />""")]
    [DataRow("""<a b="@(1 + 1)@(2 + 2) &lt; c" />""")]
    public void WriteToFile_ShouldEscapeLiteralTextThatFollowsAnExpression(string xml)
    {
        var data = CreateData("policy.xml");
        data.Element = XElement.Parse(xml);
        data.FormatCode = true;
        data.RawXml = true;

        var written = File.ReadAllLines(FileUtils.WriteToFile(data));

        written.Last().Should().EndWith(xml);
    }
}
