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
        // (bool?)default is not a constant
        switch (expression)
        {
            case CastExpressionSyntax cast:
                return IsNull(cast.Expression, context);
            case ParenthesizedExpressionSyntax parenthesized:
                return IsNull(parenthesized.Expression, context);
        }

        var model = CompilerUtils.CachedModel(context.Compilation, expression.SyntaxTree);
        if (model.GetConstantValue(expression) is { HasValue: true, Value: null })
        {
            return true;
        }

        // default for a nullable value type, such as bool?, is null without being a constant. A directory is compiled
        // without a reference to System.Runtime, so there Nullable<T> doesn't resolve and default has no type to be a
        // value of.
        // default(bool) given for a bool? is false: only a default of the literal form takes its type from the target.
        var typeInfo = model.GetTypeInfo(expression);
        return expression.Kind() is SyntaxKind.DefaultLiteralExpression or SyntaxKind.DefaultExpression &&
               (typeInfo.Type ?? typeInfo.ConvertedType) is { } type &&
               (type.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T ||
                type.TypeKind == TypeKind.Error);
    }
}
