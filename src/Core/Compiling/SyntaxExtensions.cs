// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling;

public static class SyntaxExtensions
{
    public static bool ContainsAttributeOfType<T>(this SyntaxList<AttributeListSyntax> syntax, SemanticModel model)
        where T : Attribute
    {
        return syntax.GetFirstAttributeOfType<T>(model) != null;
    }

    public static AttributeSyntax? GetFirstAttributeOfType<T>(this SyntaxList<AttributeListSyntax> syntax,
        SemanticModel model) where T : Attribute
    {
        var attributeSymbol = model.Compilation.GetTypeByMetadataName(typeof(T).FullName!);
        return syntax
            .SelectMany(a => a.Attributes)
            .FirstOrDefault(attribute =>
                SymbolEqualityComparer.Default.Equals(model.GetTypeInfo(attribute).Type, attributeSymbol));
    }

    public static string ExtractDocumentFileName(this ClassDeclarationSyntax document, SemanticModel model)
    {
        var attributeSyntax = document.AttributeLists.GetFirstAttributeOfType<DocumentAttribute>(model);
        var attributeArgumentExpression =
            attributeSyntax?.ArgumentList?.Arguments.FirstOrDefault()?.Expression as LiteralExpressionSyntax;
        return attributeArgumentExpression?.Token.ValueText ?? document.Identifier.ValueText;
    }

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