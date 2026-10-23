// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Xml;

using Microsoft.Azure.ApiManagement.PolicyToolkit.IoC;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling;

[TestClass]
public class DirectoryCompilerTests
{
    private string _testFolder = null!;
    private string _sourceFolder = null!;
    private string _outputFolder = null!;
    private ServiceProvider _serviceProvider = null!;

    [TestInitialize]
    public void Initialize()
    {
        _testFolder = Path.Combine(Path.GetTempPath(), $"policy-toolkit-directory-{Guid.NewGuid():N}");
        _sourceFolder = Path.Combine(_testFolder, "source");
        _outputFolder = Path.Combine(_testFolder, "output");
        Directory.CreateDirectory(_sourceFolder);
        _serviceProvider = new ServiceCollection().SetupCompiler().BuildServiceProvider();
    }

    [TestCleanup]
    public void Cleanup()
    {
        _serviceProvider.Dispose();
        if (Directory.Exists(_testFolder))
        {
            Directory.Delete(_testFolder, true);
        }
    }

    [TestMethod]
    public async Task Compile_ShouldWriteDocumentWithoutErrors()
    {
        File.WriteAllText(
            Path.Combine(_sourceFolder, "Valid.cs"),
            """
            using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;

            [Document("valid")]
            public class Valid : IDocument
            {
                public void Inbound(IInboundContext context)
                {
                    context.SetHeader("X-Test", "value");
                }
            }
            """);

        var result = await Compile();

        result.DocumentResults.Should().ContainSingle();
        result.DocumentResults.Single().Should().BeSuccessful();
        File.Exists(Path.Combine(_outputFolder, "valid.xml")).Should().BeTrue();
    }

    [TestMethod]
    public async Task Compile_ShouldNotWriteDocumentWithErrors()
    {
        File.WriteAllText(
            Path.Combine(_sourceFolder, "Invalid.cs"),
            """
            using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;

            [Document("invalid")]
            public class Invalid : IDocument
            {
                public void Inbound(IInboundContext context)
                {
                    var enabled = true;
                    if (enabled)
                    {
                        context.SetHeader("X-Test", "value");
                    }
                }
            }
            """);

        var result = await Compile();

        result.DocumentResults.Should().ContainSingle();
        result.DocumentResults.Single().Errors.Should().NotBeEmpty();
        File.Exists(Path.Combine(_outputFolder, "invalid.xml")).Should().BeFalse();
    }

    private Task<DirectoryCompilerResult> Compile()
    {
        var compiler = _serviceProvider.GetRequiredService<DirectoryCompiler>();
        return compiler.Compile(new DirectoryCompilerOptions
        {
            SourceFolder = _sourceFolder,
            OutputFolder = _outputFolder,
            FileExtension = "xml",
            FormatCode = true,
            RawXml = true,
            XmlWriterSettings = new XmlWriterSettings
            {
                OmitXmlDeclaration = true,
                ConformanceLevel = ConformanceLevel.Fragment,
                Indent = true,
            },
        });
    }
}
