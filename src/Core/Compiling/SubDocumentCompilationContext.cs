// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Xml.Linq;

using Microsoft.CodeAnalysis;

namespace Azure.ApiManagement.PolicyToolkit.Compiling;

public class SubDocumentCompilationContext(IDocumentCompilationContext parent, XElement element)
    : IDocumentCompilationContext
{
    public void AddPolicy(XNode element1) => element.Add(element1);

    public void Report(Diagnostic diagnostic) => parent.Report(diagnostic);

    public Compilation Compilation => parent.Compilation;

    public SyntaxNode SyntaxRoot => parent.SyntaxRoot;
}