// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Xml.Linq;

using Microsoft.CodeAnalysis;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling;

public interface ICompilationContext
{
    void AddPolicy(XNode element);
    void Report(Diagnostic diagnostic);

    SyntaxNode SyntaxRoot { get; }
}