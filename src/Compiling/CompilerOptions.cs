// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Xml;

using Microsoft.Extensions.Configuration;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling;

public class CompilerOptions
{
    public string SourcePath { get; }
    public string OutputPath { get; }
    public bool Format { get; }
    public string FileExtension { get; }

    public XmlWriterSettings XmlWriterSettings => new()
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

    public bool IsProjectSource =>
        Path.GetExtension(SourcePath)?.Equals(".csproj", StringComparison.OrdinalIgnoreCase) == true;

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
        ProjectPath = SourcePath,
        OutputFolder = OutputPath,
        FormatCode = Format,
        FileExtension = FileExtension,
        XmlWriterSettings = XmlWriterSettings,
    };
}