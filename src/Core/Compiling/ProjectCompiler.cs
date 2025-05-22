// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling;

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

        await Console.Out.WriteLineAsync($"Compiling project '{options.ProjectPath}'");
        var compilation = await project.GetCompilationAsync(cancellationToken);
        if (compilation is null)
        {
            throw new NullReferenceException("Compilation is null");
        }

        var emitResult = compilation.Emit(Stream.Null, cancellationToken: cancellationToken);
        if (!emitResult.Success)
        {
            result.CompilerDiagnostics =
            [
                ..emitResult.Diagnostics.Where(d =>
                    !d.IsSuppressed &&
                    d is { Severity: DiagnosticSeverity.Error } or
                        { Severity: DiagnosticSeverity.Warning, IsWarningAsError: true })
            ];
            foreach (var diag in result.CompilerDiagnostics)
            {
                await Console.Error.WriteLineAsync(diag.ToString());
            }

            return result;
        }

        var onlyUserSyntaxTrees = compilation.SyntaxTrees.Where(t => !FileUtils.InObjOrBinFolder.IsMatch(t.FilePath));

        foreach (var syntaxTree in onlyUserSyntaxTrees)
        {
            await Console.Out.WriteLineAsync($"File '{syntaxTree.FilePath}' processing");
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

            await Console.Out.WriteLineAsync($"File '{syntaxTree.FilePath}' processed");
        }

        return result;
    }
}