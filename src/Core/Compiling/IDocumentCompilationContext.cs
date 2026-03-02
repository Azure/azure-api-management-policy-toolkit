// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Xml.Linq;

using Microsoft.CodeAnalysis;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling;

public interface IDocumentCompilationContext
{
    void AddPolicy(XNode element);
    void Report(Diagnostic diagnostic);

    Compilation Compilation { get; }
    SyntaxNode SyntaxRoot { get; }
    IList<Diagnostic> Diagnostics { get; }

    XElement RootElement { get; }
    XElement CurrentElement { get; }

    /// <summary>
    /// Gets or sets the pending policy ID to be added to the next policy element.
    /// This is set by WithId() calls and cleared after AddPolicy() uses it.
    /// </summary>
    string? PendingPolicyId { get; set; }
}