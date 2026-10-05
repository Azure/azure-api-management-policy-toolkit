// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Xml;

using Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling;
using Microsoft.Azure.ApiManagement.PolicyToolkit.IO;
using Microsoft.Azure.ApiManagement.PolicyToolkit.IoC;
using Microsoft.Extensions.DependencyInjection;

namespace Test.Core.IO;

[TestClass]
public class PathUtilsRootTests
{
    private static readonly char Sep = Path.DirectorySeparatorChar;

    private const string Document =
        """
        using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;

        [Document("NAME")]
        public class Valid : IDocument
        {
            public void Inbound(IInboundContext context)
            {
                context.SetHeader("X-Test", "value");
            }
        }
        """;

    private string _testFolder = null!;

    [TestInitialize]
    public void Initialize()
    {
        _testFolder = Path.Combine(Path.GetTempPath(), $"policy-toolkit-path-{Guid.NewGuid():N}");
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
    [DynamicData(nameof(IsNotInObjOrBinFolder_ChecksOnlyFoldersBelowRoot_Data))]
    public void IsNotInObjOrBinFolder_ChecksOnlyFoldersBelowRoot(string path, string root, bool expected)
    {
        PathUtils.IsNotInObjOrBinFolder(path, root).Should().Be(expected);
    }

    public static IEnumerable<object[]> IsNotInObjOrBinFolder_ChecksOnlyFoldersBelowRoot_Data()
    {
        var root = Path.GetFullPath($"{Sep}home{Sep}u{Sep}bin{Sep}obj{Sep}proj");
        yield return [$"{root}{Sep}UserFile.cs", root, true];
        yield return [$"{root}{Sep}a{Sep}UserFile.cs", root, true];
        yield return [$"{root}{Sep}a{Sep}UserFile.cs", root + Sep, true];
        yield return [$"{root}{Sep}bin{Sep}NotUserFile.cs", root, false];
        yield return [$"{root}{Sep}obj{Sep}Debug{Sep}NotUserFile.cs", root, false];
        yield return [$"{root}{Sep}a{Sep}OBJ{Sep}NotUserFile.cs", root, false];
        yield return [$"a{Sep}UserFile.cs", ".", true];
        yield return [$"obj{Sep}NotUserFile.cs", ".", false];
    }

    [TestMethod]
    [DynamicData(nameof(PrepareOutputPath_ShouldUnrootPathWithExtension_Data))]
    public void PrepareOutputPath_ShouldUnrootPathWithExtension(string path, string expected)
    {
        var actual = PathUtils.PrepareOutputPath(path, "xml");

        Path.IsPathRooted(actual).Should().BeFalse();
        actual.Should().Be(expected);
    }

    public static IEnumerable<object[]> PrepareOutputPath_ShouldUnrootPathWithExtension_Data()
    {
        yield return ["/Folder/P.xml", $"Folder{Sep}P.xml"];
        yield return ["/P.cshtml", "P.cshtml"];
        yield return [$"{Sep}Folder{Sep}P.xml", $"Folder{Sep}P.xml"];
        yield return ["Folder/P.xml", $"Folder{Sep}P.xml"];
        yield return ["/Folder/P", $"Folder{Sep}P.xml"];
    }

    [TestMethod]
    public async Task DirectoryCompiler_ShouldCompileSourcesLocatedUnderBinFolder()
    {
        var sourceFolder = Path.Combine(_testFolder, "bin", "obj", "source");
        var outputFolder = Path.Combine(_testFolder, "output");
        Directory.CreateDirectory(Path.Combine(sourceFolder, "obj"));
        File.WriteAllText(Path.Combine(sourceFolder, "Valid.cs"), Document.Replace("NAME", "valid"));
        File.WriteAllText(Path.Combine(sourceFolder, "obj", "Generated.cs"), Document.Replace("NAME", "generated"));

        var result = await Compile(sourceFolder, outputFolder);

        result.DocumentResults.Should().ContainSingle();
        File.Exists(Path.Combine(outputFolder, "valid.xml")).Should().BeTrue();
        File.Exists(Path.Combine(outputFolder, "obj", "generated.xml")).Should().BeFalse();
    }

    [TestMethod]
    public async Task DirectoryCompiler_ShouldWriteRootedDocumentNameIntoOutputFolder()
    {
        var sourceFolder = Path.Combine(_testFolder, "source");
        var outputFolder = Path.Combine(_testFolder, "output");
        Directory.CreateDirectory(sourceFolder);
        var name = $"/policy-toolkit-{Guid.NewGuid():N}/P.xml";
        File.WriteAllText(Path.Combine(sourceFolder, "Valid.cs"), Document.Replace("NAME", name));

        var result = await Compile(sourceFolder, outputFolder);

        result.DocumentResults.Should().ContainSingle();
        File.Exists(Path.Combine(outputFolder, name.TrimStart('/'))).Should().BeTrue();
        Directory.Exists(Path.GetDirectoryName(name)).Should().BeFalse();
    }

    private static async Task<DirectoryCompilerResult> Compile(string sourceFolder, string outputFolder)
    {
        using var serviceProvider = new ServiceCollection().SetupCompiler().BuildServiceProvider();
        var compiler = serviceProvider.GetRequiredService<DirectoryCompiler>();
        return await compiler.Compile(new DirectoryCompilerOptions
        {
            SourceFolder = sourceFolder,
            OutputFolder = outputFolder,
            FileExtension = "xml",
            FormatCode = true,
            RawXml = true,
            XmlWriterSettings = new XmlWriterSettings
            {
                OmitXmlDeclaration = true, ConformanceLevel = ConformanceLevel.Fragment, Indent = true,
            },
        });
    }
}
