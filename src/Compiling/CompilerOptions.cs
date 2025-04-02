// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Xml;

using Microsoft.Extensions.Configuration;

namespace Azure.ApiManagement.PolicyToolkit.Compiling;

public class CompilerOptions
{
    public string SourceFolder { get; }
    public string OutputFolder { get; }
    public bool Format { get; }
    public string FileExtension { get; }

    public XmlWriterSettings XmlWriterSettings => new XmlWriterSettings()
    {
        OmitXmlDeclaration = true, ConformanceLevel = ConformanceLevel.Fragment, Indent = Format
    };

    public CompilerOptions(IConfigurationRoot configuration)
    {
        SourceFolder = configuration["s"] ??
                       configuration["source"] ??
                       throw new NullReferenceException("Source folder not provided");
        SourceFolder = Path.GetFullPath(SourceFolder);
        OutputFolder = configuration["o"] ??
                       configuration["out"] ??
                       throw new NullReferenceException("Output folder not provided");
        OutputFolder = Path.GetFullPath(OutputFolder);

        FileExtension = configuration["ext"] ?? "xml";
        Format = bool.TryParse(configuration["format"] ?? "true", out var fmt) && fmt;
    }
}