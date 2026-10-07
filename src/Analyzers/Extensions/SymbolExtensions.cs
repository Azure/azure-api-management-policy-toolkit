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

    private readonly static SymbolDisplayFormat FormatWithoutTypeParameters =
        Format.WithGenericsOptions(SymbolDisplayGenericsOptions.None);

    public static string ToFullyQualifiedString(this ISymbol symbol) => symbol.ToDisplayString(Format);

    public static string ToFullyQualifiedStringWithoutTypeParameters(this ISymbol symbol) =>
        symbol.ToDisplayString(FormatWithoutTypeParameters);
}