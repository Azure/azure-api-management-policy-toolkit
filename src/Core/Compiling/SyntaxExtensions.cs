// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling;

public static class SyntaxExtensions
{
    internal static ExpressionSyntax Unparenthesized(this ExpressionSyntax expression)
    {
        while (expression is ParenthesizedExpressionSyntax parenthesized)
        {
            expression = parenthesized.Expression;
        }

        return expression;
    }

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
        var name = TryExtractDocumentFileName(document, model);
        return name.IsValid ? name.Value! : document.Identifier.ValueText;
    }

    public static void ValidateDocumentName(
        this ClassDeclarationSyntax document,
        SemanticModel model,
        IDocumentCompilationContext context)
    {
        var name = TryExtractDocumentFileName(document, model);
        if (!name.IsValid)
        {
            context.Report(Diagnostic.Create(
                Diagnostics.CompilationErrors.InvalidDocumentName,
                name.Location));
        }
    }

    private static (bool IsValid, string? Value, Location Location) TryExtractDocumentFileName(
        ClassDeclarationSyntax document,
        SemanticModel model)
    {
        var attributeSyntax = document.AttributeLists.GetFirstAttributeOfType<DocumentAttribute>(model);
        var attributeArgumentExpression = attributeSyntax?.ArgumentList?.Arguments
            .FirstOrDefault(argument => argument.NameEquals is null)?.Expression;
        if (attributeArgumentExpression is null)
        {
            return (true, document.Identifier.ValueText, document.Identifier.GetLocation());
        }

        var constantValue = model.GetConstantValue(attributeArgumentExpression);
        if (constantValue is { HasValue: true, Value: null })
        {
            return (true, document.Identifier.ValueText, attributeArgumentExpression.GetLocation());
        }

        if (constantValue is { HasValue: true, Value: string { Length: > 0 } name })
        {
            return (true, name, attributeArgumentExpression.GetLocation());
        }

        return (false, null, attributeArgumentExpression.GetLocation());
    }

    public static DocumentType ExtractDocumentType(this ClassDeclarationSyntax document, SemanticModel model)
    {
        var attributeSyntax = document.AttributeLists.GetFirstAttributeOfType<DocumentAttribute>(model);
        var fragmentArgument = attributeSyntax?.ArgumentList?.Arguments
            .FirstOrDefault(arg => arg.Expression.ToString().Contains(nameof(DocumentType.Fragment)));
        return fragmentArgument != null ? DocumentType.Fragment : DocumentType.Policy;
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