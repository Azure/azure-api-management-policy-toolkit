// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Immutable;

using Microsoft.CodeAnalysis;

namespace Azure.ApiManagement.PolicyToolkit.Compiling;

public class ProjectCompilerResult
{
    public ImmutableArray<Diagnostic> CompilerDiagnostics { get; set; }
    public IList<IDocumentCompilationResult> DocumentResults { get; } = new List<IDocumentCompilationResult>();
}