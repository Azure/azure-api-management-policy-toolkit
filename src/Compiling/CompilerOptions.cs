// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Xml;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling;

public class CompilerOptions
{
    private string SourcePath { get; }
    private string OutputPath { get; }
    private bool Format { get; }
    private string FileExtension { get; }
    private bool RawXml { get; }

    private XmlWriterSettings XmlWriterSettings => new()
    {
        OmitXmlDeclaration = true, ConformanceLevel = ConformanceLevel.Fragment, Indent = Format
    };

    public CompilerOptions(string sourcePath, string outputPath, string fileExtension, bool format, string policyFormat)
    {
        SourcePath = Path.GetFullPath(sourcePath);
        OutputPath = Path.GetFullPath(outputPath);
        FileExtension = fileExtension;
        Format = format;
        RawXml = ParseRawXml(policyFormat);
    }

    private static bool ParseRawXml(string policyFormat)
    {
        if (policyFormat.Equals("rawxml", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (policyFormat.Equals("xml", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        throw new ArgumentException(
            $"Invalid policy format value '{policyFormat}'. Use 'rawxml' (default) or 'xml'.");
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
        RawXml = RawXml,
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
        RawXml = RawXml,
    };
}