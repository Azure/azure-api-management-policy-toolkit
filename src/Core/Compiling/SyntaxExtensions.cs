// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling;

public static class SyntaxExtensions
{
    public static bool ContainsAttributeOfType(this SyntaxList<AttributeListSyntax> syntax, string type)
    {
        return syntax.GetFirstAttributeOfType(type) != null;
    }

    public static AttributeSyntax? GetFirstAttributeOfType(this SyntaxList<AttributeListSyntax> syntax, string type)
    {
        return syntax
            .SelectMany(a => a.Attributes)
            .FirstOrDefault(attribute => string.Equals(attribute.Name.ToString(), type, StringComparison.Ordinal));
    }

    public static IEnumerable<ClassDeclarationSyntax> GetDocumentAttributedClasses(this SyntaxNode syntax) =>
        syntax
            .DescendantNodes()
            .OfType<ClassDeclarationSyntax>()
            .Where(c => c.AttributeLists.ContainsAttributeOfType("Document"));

    public static IEnumerable<ClassDeclarationSyntax> GetDocumentAttributedClasses(this SyntaxNode syntax,
        SemanticModel semanticModel)
    {
        var documentAttributeSymbol =
            semanticModel.Compilation.GetTypeByMetadataName(typeof(DocumentAttribute).FullName!);
        return syntax
            .DescendantNodes()
            .OfType<ClassDeclarationSyntax>()
            .Where(c => c.AttributeLists
                .SelectMany(a => a.Attributes)
                .FirstOrDefault(attribute =>
                    SymbolEqualityComparer.Default.Equals(semanticModel.GetTypeInfo(attribute).Type,
                        documentAttributeSymbol)) != null
            );
    }
}