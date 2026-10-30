// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling;

internal static class NullExpression
{
    // null, or a constant that is null: default or default(string) for a reference type, (string?)null, a null
    // const, and default for a nullable value type. default for a value type is zero or false and is a value like
    // any other.
    internal static bool IsNull(ExpressionSyntax expression, IDocumentCompilationContext context)
    {
        if (expression.IsKind(SyntaxKind.NullLiteralExpression))
        {
            return true;
        }

        // (bool?)null is not a constant either
        switch (expression)
        {
            case CastExpressionSyntax cast:
                return IsNull(cast.Expression, context);
            case ParenthesizedExpressionSyntax parenthesized:
                return IsNull(parenthesized.Expression, context);
        }

        if (!context.Compilation.ContainsSyntaxTree(expression.SyntaxTree))
        {
            return false;
        }

        var model = CompilerUtils.CachedModel(context.Compilation, expression.SyntaxTree);
        if (model.GetConstantValue(expression) is { HasValue: true, Value: null })
        {
            return true;
        }

        // default for a nullable value type, such as bool?, is null without being a constant. A directory is compiled
        // without a reference to System.Runtime, so there Nullable<T> doesn't resolve and default has no type to be a
        // value of.
        return expression is LiteralExpressionSyntax or DefaultExpressionSyntax &&
               expression.Kind() is SyntaxKind.DefaultLiteralExpression or SyntaxKind.DefaultExpression &&
               model.GetTypeInfo(expression).ConvertedType is { } type &&
               (type.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T ||
                type.TypeKind == TypeKind.Error);
    }
}
