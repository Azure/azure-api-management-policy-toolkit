// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Xml.Linq;

using Microsoft.CodeAnalysis;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling;

public class CompilationContext : ICompilationContext, ICompilationResult
{
    private readonly IList<Diagnostic> _diagnostics = new List<Diagnostic>();
    private readonly XElement _rootElement;

    public CompilationContext(SyntaxNode syntaxRoot, XElement rootElement)
    {
        SyntaxRoot = syntaxRoot;
        _rootElement = rootElement;
    }

    public void AddPolicy(XNode element) => _rootElement.Add(element);
    public void Report(Diagnostic diagnostic) => _diagnostics.Add(diagnostic);

    public SyntaxNode SyntaxRoot { get; }

    public XElement Document => _rootElement;

    public IReadOnlyList<Diagnostic> Diagnostics => _diagnostics.AsReadOnly();
}