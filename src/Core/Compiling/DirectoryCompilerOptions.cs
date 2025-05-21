// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Xml;

namespace Azure.ApiManagement.PolicyToolkit.Compiling;

public record DirectoryCompilerOptions()
{
    public required string SourceFolder { get; init; }
    public required string OutputFolder { get; init; }
    public required XmlWriterSettings XmlWriterSettings { get; init; }
    public required bool CodeFormat { get; init; }
    public required string FileExtension { get; init; }
}