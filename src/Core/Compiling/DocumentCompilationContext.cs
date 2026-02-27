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
        // Note: PendingPolicyId is intentionally NOT propagated to child contexts
    }

    public void AddPolicy(XNode element)
    {
        // If there's a pending policy ID and the element is an XElement, prepend the id attribute
        if (PendingPolicyId is not null && element is XElement xElement)
        {
            // Get existing attributes and prepend id
            var existingAttributes = xElement.Attributes().ToList();
            xElement.RemoveAttributes();
            xElement.Add(new XAttribute("id", PendingPolicyId));
            foreach (var attr in existingAttributes)
            {
                xElement.Add(attr);
            }

            PendingPolicyId = null;
        }

        CurrentElement.Add(element);
    }

    public void Report(Diagnostic diagnostic) => Diagnostics.Add(diagnostic);

    public Compilation Compilation { get; } = compilation;
    public SyntaxNode SyntaxRoot { get; } = syntaxRoot;
    public XElement RootElement { get; } = currentElement;
    public XElement CurrentElement { get; } = currentElement;
    public IList<Diagnostic> Diagnostics { get; } = new List<Diagnostic>();
    public string? PendingPolicyId { get; set; }

    public XElement Document => CurrentElement;
    public ImmutableArray<Diagnostic> Errors => [..Diagnostics];
}