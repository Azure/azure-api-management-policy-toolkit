// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.ApiManagement.PolicyToolkit.Compiling;

public class DirectoryCompilerResult
{
    public IList<IDocumentCompilationResult> DocumentResults { get; } = new List<IDocumentCompilationResult>();
}