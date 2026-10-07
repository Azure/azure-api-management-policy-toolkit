// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Xml.Linq;

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling.Diagnostics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling.Policy;

public class SetVariableCompiler : IMethodPolicyHandler
{
    public string MethodName => nameof(IInboundContext.SetVariable);

    public void Handle(IDocumentCompilationContext context, InvocationExpressionSyntax node)
    {
        if (node.ArgumentList.Arguments.Count != 2)
        {
            context.Report(Diagnostic.Create(
                CompilationErrors.ArgumentCountMissMatchForPolicy,
                node.ArgumentList.GetLocation(),
                "set-variable"));
            return;
        }

        var name = node.ArgumentList.Arguments[0].Expression.ProcessParameter(context);
        var valueExpression = node.ArgumentList.Arguments[1].Expression;
        if (FindRejectedValueType(context, valueExpression) is { } rejected)
        {
            context.Report(Diagnostic.Create(
                CompilationErrors.ValueTypeNotAccepted,
                valueExpression.GetLocation(),
                "set-variable",
                rejected.ToDisplayString()));
            return;
        }

        var value = TypedConstant(context, valueExpression) ?? valueExpression.ProcessParameter(context);
        context.AddPolicy(new XElement("set-variable", new XAttribute("name", name), new XAttribute("value", value)));
    }

    // A literal value attribute is a string in the gateway: value="5" stores "5", and a policy expression
    // that reads the variable as an int fails when the policy runs. A constant that isn't a string is
    // written as an expression, value="@(5)", which keeps the type it has in C#.
    private static string? TypedConstant(IDocumentCompilationContext context, ExpressionSyntax expression)
    {
        if (!context.Compilation.ContainsSyntaxTree(expression.SyntaxTree))
        {
            return null;
        }

        var model = CompilerUtils.CachedModel(context.Compilation, expression.SyntaxTree);
        var constant = model.GetConstantValue(expression);
        if (!constant.HasValue || constant.Value is null or string ||
            model.GetTypeInfo(expression).Type is not { TypeKind: not TypeKind.Enum } type)
        {
            return null;
        }

        // NaN and the infinities have no literal: they stay the plain text they were
        if (constant.Value is double.NaN or double.PositiveInfinity or double.NegativeInfinity or
            float.NaN or float.PositiveInfinity or float.NegativeInfinity)
        {
            return null;
        }

        var text = SymbolDisplay.FormatPrimitive(constant.Value, quoteStrings: true, useHexadecimalNumbers: false);
        var literal = type.SpecialType switch
        {
            SpecialType.System_Boolean or SpecialType.System_Int32 or SpecialType.System_Char => text,
            SpecialType.System_Int64 => text + "L",
            SpecialType.System_UInt32 => text + "u",
            SpecialType.System_UInt64 => text + "UL",
            SpecialType.System_Single => text + "f",
            SpecialType.System_Decimal => text + "m",
            SpecialType.System_Double => text.Contains('.') || text.Contains('E') ? text : text + "d",
            SpecialType.System_Byte or SpecialType.System_SByte or SpecialType.System_Int16 or
                SpecialType.System_UInt16 => $"({type.ToDisplayString()}){text}",
            _ => null
        };
        return literal is null ? null : $"@({literal})";
    }

    // The gateway types the expression itself, not the helper that holds it, and rejects these as the value
    // of a variable: Uri, enums, anonymous types, tuples, dictionaries and arrays other than string[] and
    // byte[]. It rejects object too, but an expression typed object in C# is often a call to another helper
    // whose own expression has an accepted type, so that isn't reported: the helper it calls is looked at instead.
    private static ITypeSymbol? FindRejectedValueType(
        IDocumentCompilationContext context,
        ExpressionSyntax value,
        HashSet<ISymbol>? visited = null)
    {
        if (!context.Compilation.ContainsSyntaxTree(value.SyntaxTree))
        {
            return null;
        }

        var model = CompilerUtils.CachedModel(context.Compilation, value.SyntaxTree);
        switch (value)
        {
            case ParenthesizedExpressionSyntax parenthesized:
                return FindRejectedValueType(context, parenthesized.Expression, visited);
            // a cast to object keeps the value it is given
            case CastExpressionSyntax cast when model.GetTypeInfo(cast).Type is { SpecialType: SpecialType.System_Object }:
                return FindRejectedValueType(context, cast.Expression, visited);
        }

        if (value is not InvocationExpressionSyntax invocation ||
            model.GetSymbolInfo(invocation).Symbol is not IMethodSymbol method ||
            method.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax() is not MethodDeclarationSyntax declaration ||
            !context.Compilation.ContainsSyntaxTree(declaration.SyntaxTree))
        {
            // A value written in place, such as DayOfWeek.Monday, would otherwise be emitted as plain text.
            return model.GetTypeInfo(value).Type is { } type && IsRejectedValueType(type) ? type : null;
        }

        if (method.GetAttributes().Any(CompilerUtils.IsNamedValueAttribute) ||
            !(visited ??= new HashSet<ISymbol>(SymbolEqualityComparer.Default)).Add(method))
        {
            return null;
        }

        // a helper declared with a rejected type returns one whatever it returns: DayOfWeek? Day() => null
        if (IsRejectedValueType(method.ReturnType))
        {
            return method.ReturnType;
        }

        var returned = declaration.ExpressionBody is { } arrow
            ? [arrow.Expression]
            : declaration.Body?.DescendantNodes(node => node is not (LambdaExpressionSyntax or LocalFunctionStatementSyntax))
                .OfType<ReturnStatementSyntax>()
                .Select(statement => statement.Expression)
                .OfType<ExpressionSyntax>() ?? [];
        return returned
            .Select(expression => FindRejectedValueType(context, expression, visited))
            .FirstOrDefault(type => type is not null);
    }

    private static bool IsRejectedValueType(ITypeSymbol? type) => type switch
    {
        null or IDynamicTypeSymbol or IErrorTypeSymbol => false,
        { TypeKind: TypeKind.Enum } => true,
        INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } nullable =>
            IsRejectedValueType(nullable.TypeArguments[0]),
        IArrayTypeSymbol array => array.Rank != 1 || array.ElementType.SpecialType is not
            (SpecialType.System_String or SpecialType.System_Byte),
        INamedTypeSymbol named => named.IsAnonymousType || named.IsTupleType ||
                                  named.ToDisplayString() == "System.Uri" ||
                                  named.OriginalDefinition.ToDisplayString() is
                                      "System.Collections.Generic.Dictionary<TKey, TValue>" or
                                      "System.Collections.Generic.IDictionary<TKey, TValue>" or
                                      "System.Collections.Generic.IReadOnlyDictionary<TKey, TValue>" ||
                                  named.OriginalDefinition.ToDisplayString().StartsWith("System.Tuple<"),
        _ => false
    };
}
