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

    [TestInitialize]
    public void Initialize()
    {
        _testFolder = Path.Combine(Path.GetTempPath(), $"policy-toolkit-file-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testFolder);
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

        var result = Write(element, rawXml: true);

        result.Should().Be(
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
                    <set-header name="x @(&quot;a&quot; + &quot;b&quot;) y">
                        <value>@(")" + 1)</value>
                    </set-header>
                </inbound>
            </policies>
            """.ReplaceLineEndings());
    }

    private string Write(XElement element, bool rawXml)
    {
        var file = FileUtils.WriteToFile(new FileUtils.Data
        {
            Element = element,
            SourceFolder = _testFolder,
            SourceFilePath = Path.Combine(_testFolder, "Policy.cs"),
            OutputFolder = Path.Combine(_testFolder, "output"),
            OutputFilePath = "policy.xml",
            FormatCode = true,
            RawXml = rawXml,
            XmlWriterSettings = new XmlWriterSettings
            {
                OmitXmlDeclaration = true,
                ConformanceLevel = ConformanceLevel.Fragment,
                Indent = true,
                IndentChars = "    ",
                NewLineChars = Environment.NewLine,
            },
        });
        var lines = File.ReadAllLines(file);
        lines[0].Should().StartWith("<!--");
        lines[1].Should().StartWith("<!--");
        return string.Join(Environment.NewLine, lines.Skip(2));
    }
}
