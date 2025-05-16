// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Xml.Linq;

using Microsoft.CodeAnalysis;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling;

public interface ICompilationResult
{
    XElement Document { get; }
    IReadOnlyList<Diagnostic> Diagnostics { get; }
}