// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;

namespace Azure.ApiManagement.PolicyToolkit.Compiling;

public class ProjectCompiler(DocumentCompiler documentCompiler)
{
    public async Task<ProjectCompilerResult> Compile(ProjectCompilerOptions options,
        CancellationToken cancellationToken = default(CancellationToken))
    {
        var result = new ProjectCompilerResult();
        var workspace = MSBuildWorkspace.Create();
        await Console.Out.WriteLineAsync($"Opening project '{options.ProjectPath}'");
        var project = await workspace.OpenProjectAsync(options.ProjectPath, cancellationToken: cancellationToken);
        if (!project.SupportsCompilation)
        {
            throw new Exception("Cannot compile project which does not support compilation");
        }

        var compilation = await project.GetCompilationAsync(cancellationToken);
        if (compilation is null)
        {
            throw new NullReferenceException("Compilation is null");
        }

        result.CompilerDiagnostics =
        [
            ..compilation.GetDiagnostics(cancellationToken).Where(d =>
                !d.IsSuppressed &&
                d is { Severity: DiagnosticSeverity.Error } or
                    { Severity: DiagnosticSeverity.Warning, IsWarningAsError: true })
        ];
        foreach (var diag in result.CompilerDiagnostics)
        {
            await Console.Error.WriteLineAsync(diag.ToString());
        }

        if (result.CompilerDiagnostics.Any())
        {
            return result;
        }

        foreach (var syntaxTree in compilation.SyntaxTrees)
        {
            await Console.Out.WriteLineAsync($"File '{syntaxTree.FilePath}' Processing");
            var root = await syntaxTree.GetRootAsync(cancellationToken);
            var documents = root.GetDocumentAttributedClasses();
            foreach (var document in documents)
            {
                var policyFileName = document.ExtractDocumentFileName(options.FileExtension);
                var documentResult = documentCompiler.Compile(compilation, document);
                result.DocumentResults.Add(documentResult);

                foreach (var error in documentResult.Diagnostics)
                {
                    await Console.Error.WriteLineAsync(error.ToString());
                }

                var targetFile = FileUtils.WriteToFile(new FileUtils.Data()
                {
                    Element = documentResult.Document,
                    SourceFolder = Path.GetDirectoryName(options.ProjectPath)!,
                    SourceFilePath = syntaxTree.FilePath,
                    OutputFolder = options.OutputFolder,
                    OutputFilePath = policyFileName,
                    FormatCode = options.FormatCode,
                    XmlWriterSettings = options.XmlWriterSettings,
                });
                await Console.Out.WriteLineAsync($"File '{targetFile}' created");
            }
        }

        return result;
    }
}