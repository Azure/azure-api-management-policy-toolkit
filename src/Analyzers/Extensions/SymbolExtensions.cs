// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.CodeAnalysis;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Analyzers;

public static class SymbolExtensions
{
    private readonly static SymbolDisplayFormat Format = new SymbolDisplayFormat(
        globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.OmittedAsContaining,
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
        miscellaneousOptions: SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers
    );

    public static string ToFullyQualifiedString(this ISymbol symbol) => symbol.ToDisplayString(Format);
}