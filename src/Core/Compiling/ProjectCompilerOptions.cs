// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Xml;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling;

public class ProjectCompilerOptions
{
    public required string ProjectPath { get; init; }
    public required string FileExtension { get; init; }
    public required string OutputFolder { get; init; }
    public required bool FormatCode { get; init; }
    public required XmlWriterSettings XmlWriterSettings { get; init; }
}