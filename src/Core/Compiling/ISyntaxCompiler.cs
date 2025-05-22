// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling;

public interface ISyntaxCompiler
{
    SyntaxKind Syntax { get; }

    void Compile(IDocumentCompilationContext context, SyntaxNode node);
}