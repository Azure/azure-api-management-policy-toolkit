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

    public static bool IsPartOfPolicyExpressionMethod(this SyntaxNode syntax, SemanticModel model)
    {
        return syntax.Ancestors()
            .OfType<MethodDeclarationSyntax>()
            .Any(c => c.AttributeLists.ContainsExpressionAttribute(model));
    }

    public static bool IsPartOfPolicyExpressionDelegate(this SyntaxNode syntaxNode, SemanticModel model)
    {
        return syntaxNode.Ancestors()
            .OfType<LambdaExpressionSyntax>()
            .Any(l => l.IsExpressionLambda(model));
    }

    private static readonly Regex ExpressionDelegateTypeMatcher =
        new Regex(
            @"Mielek\.Azure\.ApiManagement\.PolicyToolkit\.Authoring\.Expression<.*?>",
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