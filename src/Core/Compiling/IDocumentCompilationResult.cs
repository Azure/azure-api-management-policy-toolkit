// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Immutable;
using System.Xml.Linq;

using Microsoft.CodeAnalysis;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling;

public interface IDocumentCompilationResult
{
    XElement Document { get; }
    ImmutableArray<Diagnostic> Errors { get; }
}