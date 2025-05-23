// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Xml.Linq;

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.IO;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling;

public class DirectoryCompiler(DocumentCompiler compiler)
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
            .Where(p => PathUtils.IsNotInObjOrBinFolder(Path.GetFullPath(p)));

        DirectoryCompilerResult result = new();
        foreach (var file in files)
        {
            Console.Out.WriteLine($"File '{file}' processing");
            var code = File.ReadAllText(file);
            var syntax = CSharpSyntaxTree.ParseText(code, path: file);
            var compilation = CSharpCompilation.Create(
                file,
                syntaxTrees: [syntax],
                references: References);

            var semantics = compilation.GetSemanticModel(syntax);
            var documents = syntax.GetRoot().GetDocumentAttributedClasses(semantics);

            foreach (var document in documents)
            {
                IDocumentCompilationResult documentResult = compiler.Compile(compilation, document);
                result.DocumentResults.Add(documentResult);

                foreach (var error in documentResult.Errors)
                {
                    Console.Error.WriteLine(error.ToString());
                }

                var policyFileName = document.ExtractDocumentFileName(semantics);
                var targetFile = FileUtils.WriteToFile(new FileUtils.Data()
                {
                    Element = documentResult.Document,
                    SourceFolder = options.SourceFolder,
                    SourceFilePath = file,
                    OutputFolder = options.OutputFolder,
                    OutputFilePath = PathUtils.PrepareOutputPath(policyFileName, options.FileExtension),
                    FormatCode = options.FormatCode,
                    XmlWriterSettings = options.XmlWriterSettings,
                });
                Console.Out.WriteLine($"File '{targetFile}' created");
            }

            Console.Out.WriteLine($"File '{file}' processed");
        }

        return Task.FromResult(result);
    }
}