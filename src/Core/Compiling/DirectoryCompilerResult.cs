// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling;

public class DirectoryCompilerResult
{
    public IList<IDocumentCompilationResult> DocumentResults { get; } = new List<IDocumentCompilationResult>();
}