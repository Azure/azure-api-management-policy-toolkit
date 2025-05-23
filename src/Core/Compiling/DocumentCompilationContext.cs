// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Immutable;
using System.Xml.Linq;

using Microsoft.CodeAnalysis;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling;

public class DocumentCompilationContext(Compilation compilation, SyntaxNode syntaxRoot, XElement currentElement)
    : IDocumentCompilationContext, IDocumentCompilationResult
{
    public DocumentCompilationContext(IDocumentCompilationContext parent, XElement currentElement)
        : this(parent.Compilation, parent.SyntaxRoot, currentElement)
    {
        RootElement = parent.RootElement;
        Diagnostics = parent.Diagnostics;
    }

    public void AddPolicy(XNode element) => CurrentElement.Add(element);
    public void Report(Diagnostic diagnostic) => Diagnostics.Add(diagnostic);

    public Compilation Compilation { get; } = compilation;
    public SyntaxNode SyntaxRoot { get; } = syntaxRoot;
    public XElement RootElement { get; } = currentElement;
    public XElement CurrentElement { get; } = currentElement;
    public IList<Diagnostic> Diagnostics { get; } = new List<Diagnostic>();

    public XElement Document => CurrentElement;
    public ImmutableArray<Diagnostic> Errors => [..Diagnostics];
}