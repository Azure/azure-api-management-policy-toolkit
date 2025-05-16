// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Xml.Linq;

using Microsoft.CodeAnalysis;

namespace Azure.ApiManagement.PolicyToolkit.Compiling;

public class DocumentCompilationContext(Compilation compilation, SyntaxNode syntaxRoot, XElement rootElement)
    : IDocumentCompilationContext, IDocumentCompilationResult
{
    private readonly IList<Diagnostic> _diagnostics = new List<Diagnostic>();

    public DocumentCompilationContext(DocumentCompilationContext parent, XElement rootElement) : this(
        parent.Compilation, parent.SyntaxRoot, rootElement)
    {
        _diagnostics = parent._diagnostics;
    }

    public void AddPolicy(XNode element) => rootElement.Add(element);
    public void Report(Diagnostic diagnostic) => _diagnostics.Add(diagnostic);

    public Compilation Compilation { get; } = compilation;
    public SyntaxNode SyntaxRoot { get; } = syntaxRoot;

    public XElement Document => rootElement;
    public IReadOnlyList<Diagnostic> Diagnostics => _diagnostics.AsReadOnly();
}