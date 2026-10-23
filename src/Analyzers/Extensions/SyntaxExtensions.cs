// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.RegularExpressions;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Analyzers;

public static class SyntaxExtensions
{
    public static bool ContainsAttributeOfType(this SyntaxList<AttributeListSyntax> syntax, SemanticModel model,
        string type)
    {
        return syntax
            .SelectMany(a => a.Attributes)
            .Any(attribute =>
            {
                var attributeType = model.GetSymbolInfo(attribute).Symbol?.ContainingType;
                return attributeType?.ToFullyQualifiedString() == type;
            });
    }

    private const string ExpressionAttribute =
        "Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring.ExpressionAttribute";

    public static bool ContainsExpressionAttribute(this SyntaxList<AttributeListSyntax> syntax, SemanticModel model)
    {
        return syntax.ContainsAttributeOfType(model, ExpressionAttribute);
    }

    public static bool HasExpressionAttribute(this ISymbol symbol)
    {
        return symbol.GetAttributes()
            .Any(attribute => attribute.AttributeClass?.ToFullyQualifiedString() == ExpressionAttribute);
    }

    // A node inside an [Expression] method, or inside any method of an expression helper library (by symbol, so
    // every part of a partial class is included).
    public static bool IsPartOfPolicyExpressionMethod(this SyntaxNode syntax, SemanticModel model)
    {
        return syntax.Ancestors()
            .OfType<MethodDeclarationSyntax>()
            .Any(method => method.AttributeLists.ContainsExpressionAttribute(model) ||
                           model.GetDeclaredSymbol(method)?.IsExpressionLibraryMember() == true);
    }

    // A member of a class marked [Expression] (an expression helper library), from source or metadata.
    public static bool IsExpressionLibraryMember(this ISymbol symbol)
    {
        for (var type = symbol.ContainingType; type is not null; type = type.ContainingType)
        {
            if (type.HasExpressionAttribute())
            {
                return true;
            }
        }

        return false;
    }

    public static bool IsPartOfPolicyExpressionDelegate(this SyntaxNode syntaxNode, SemanticModel model)
    {
        return syntaxNode.Ancestors()
            .OfType<LambdaExpressionSyntax>()
            .Any(l => l.IsExpressionLambda(model));
    }

    private static readonly Regex ExpressionDelegateTypeMatcher =
        new Regex(
            @"Microsoft\.Azure\.ApiManagement\.PolicyToolkit\.Authoring\.Expression<.*?>",
            RegexOptions.Compiled);

    public static bool IsExpressionLambda(this LambdaExpressionSyntax syntaxNode, SemanticModel model)
    {
        var syntax = syntaxNode.Ancestors()
            .OfType<InvocationExpressionSyntax>()
            .FirstOrDefault();
        if (syntax == null)
        {
            return false;
        }

        var symbol = model.GetSymbolInfo(syntax).Symbol;
        if (symbol is not IMethodSymbol methodSymbol)
        {
            return false;
        }

        var parameter = methodSymbol.Parameters.FirstOrDefault();
        if (parameter == null)
        {
            return false;
        }

        var displayName = parameter.OriginalDefinition.Type.ToDisplayString();
        return ExpressionDelegateTypeMatcher.IsMatch(displayName);
    }
}