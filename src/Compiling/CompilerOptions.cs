// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Xml;

using Microsoft.Extensions.Configuration;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling;

public class CompilerOptions
{
    private string SourcePath { get; }
    private string OutputPath { get; }
    private bool Format { get; }
    private string FileExtension { get; }

    private XmlWriterSettings XmlWriterSettings => new()
    {
        OmitXmlDeclaration = true, ConformanceLevel = ConformanceLevel.Fragment, Indent = Format
    };

    public CompilerOptions(IConfigurationRoot configuration)
    {
        SourcePath = configuration["s"] ??
                     configuration["source"] ??
                     throw new NullReferenceException("Source path not provided");
        SourcePath = Path.GetFullPath(SourcePath);
        OutputPath = configuration["o"] ??
                     configuration["out"] ??
                     throw new NullReferenceException("Output path not provided");
        OutputPath = Path.GetFullPath(OutputPath);

        FileExtension = configuration["ext"] ?? "xml";
        Format = bool.TryParse(configuration["format"] ?? "true", out var fmt) && fmt;
    }

    public bool IsProjectSource
    {
        get
        {
            return TryGetProjectPath(out _);
        }
    }

    bool TryGetProjectPath([NotNullWhen(true)] out string? projectPath)
    {
        if (Path.GetExtension(SourcePath).Equals(".csproj", StringComparison.OrdinalIgnoreCase))
        {
            projectPath = SourcePath;
            return true;
        }

        var paths = Directory.GetFiles(SourcePath, "*.csproj", SearchOption.TopDirectoryOnly);
        if (paths.Length != 0)
        {
            return !string.IsNullOrEmpty(projectPath = paths.SingleOrDefault());
        }

        projectPath = null;
        return false;
    }

    public DirectoryCompilerOptions ToDirectoryCompilerOptions() => new()
    {
        SourceFolder = SourcePath,
        OutputFolder = OutputPath,
        FormatCode = Format,
        FileExtension = FileExtension,
        XmlWriterSettings = XmlWriterSettings,
    };

    public ProjectCompilerOptions ToProjectCompilerOptions() => new()
    {
        ProjectPath =
            TryGetProjectPath(out var projectPath)
                ? projectPath
                : throw new InvalidOperationException("Project path not found"),
        OutputFolder = OutputPath,
        FormatCode = Format,
        FileExtension = FileExtension,
        XmlWriterSettings = XmlWriterSettings,
    };
}