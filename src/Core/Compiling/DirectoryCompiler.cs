// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.RegularExpressions;
using System.Xml.Linq;

using Azure.ApiManagement.PolicyToolkit.Authoring;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Azure.ApiManagement.PolicyToolkit.Compiling;

public partial class DirectoryCompiler(DocumentCompiler compiler)
{
    private static readonly IEnumerable<MetadataReference> References =
    [
        MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
        MetadataReference.CreateFromFile(typeof(XElement).Assembly.Location),
        MetadataReference.CreateFromFile(typeof(IDocument).Assembly.Location)
    ];

    public Task<DirectoryCompilerResult> Compile(DirectoryCompilerOptions options)
    {
        var files = Directory.GetFiles(options.SourceFolder, "*.cs", SearchOption.AllDirectories)
            .Where(p => !InObjectOrBinFolder().IsMatch(p));

        DirectoryCompilerResult result = new();
        foreach (var file in files)
        {
            Console.Out.WriteLine($"File '{file}' Processing");
            var code = File.ReadAllText(file);
            var syntax = CSharpSyntaxTree.ParseText(code, path: file);
            var compilation = CSharpCompilation.Create(
                file,
                syntaxTrees: [syntax],
                references: References);

            var documents = syntax.GetRoot()
                .GetDocumentAttributedClasses();

            foreach (var document in documents)
            {
                var policyFileName = document.ExtractDocumentFileName(options.FileExtension);
                IDocumentCompilationResult documentResult = compiler.Compile(compilation, document);
                result.DocumentResults.Add(documentResult);

                foreach (var error in documentResult.Diagnostics)
                {
                    Console.Error.WriteLine(error.ToString());
                }

                var targetFile = FileUtils.WriteToFile(new FileUtils.Data()
                {
                    Element = documentResult.Document,
                    SourceFolder = options.SourceFolder,
                    SourceFilePath = file,
                    OutputFolder = options.OutputFolder,
                    OutputFilePath = policyFileName,
                    FormatCode = options.FormatCode,
                    XmlWriterSettings = options.XmlWriterSettings,
                });
                Console.Out.WriteLine($"File '{targetFile}' created");
            }

            Console.Out.WriteLine($"File '{file}' processed");
        }

        return Task.FromResult(result);
    }

    [GeneratedRegex(@".*[\\/](obj|bin)[\\/].*")]
    private static partial Regex InObjectOrBinFolder();
}