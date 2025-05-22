// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Xml.Linq;

using Microsoft.CodeAnalysis;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling;

public class SubCompilationContext : ICompilationContext
{
    private readonly ICompilationContext _parent;
    private readonly XElement _element;

    public SubCompilationContext(ICompilationContext parent, XElement element)
    {
        _parent = parent;
        _element = element;
    }

    public void AddPolicy(XNode element) => _element.Add(element);

    public void Report(Diagnostic diagnostic) => _parent.Report(diagnostic);

    public SyntaxNode SyntaxRoot => _parent.SyntaxRoot;
}