// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Globalization;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling;

internal static class ConstantExpressionFactory
{
    public static bool TryCreate(object? value, ITypeSymbol type, out ExpressionSyntax expression)
    {
        if (value is null)
        {
            expression = SyntaxFactory.LiteralExpression(SyntaxKind.NullLiteralExpression);
            return type.IsReferenceType || type.NullableAnnotation == NullableAnnotation.Annotated;
        }

        if (type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } nullable)
        {
            type = nullable.TypeArguments[0];
        }

        // Enum values are constant integers; emit the member (or a cast) so the expression keeps the enum type.
        if (type is INamedTypeSymbol { TypeKind: TypeKind.Enum } enumType)
        {
            if (!enumType.DeclaringSyntaxReferences.IsDefaultOrEmpty ||
                !TryCreate(value, enumType.EnumUnderlyingType!, out var underlying))
            {
                expression = null!;
                return false;
            }

            var typeName = enumType.ToDisplayString();
            var member = enumType.GetMembers().OfType<IFieldSymbol>()
                .FirstOrDefault(field => field.HasConstantValue && Equals(field.ConstantValue, value));
            expression = member is not null
                ? Numeric($"{typeName}.{member.Name}")
                : SyntaxFactory.CastExpression(
                    SyntaxFactory.ParseTypeName(typeName),
                    SyntaxFactory.ParenthesizedExpression(underlying));
            return true;
        }

        expression = value switch
        {
            string item => SyntaxFactory.LiteralExpression(
                SyntaxKind.StringLiteralExpression, SyntaxFactory.Literal(item)),
            char item => SyntaxFactory.LiteralExpression(
                SyntaxKind.CharacterLiteralExpression, SyntaxFactory.Literal(item)),
            bool item => SyntaxFactory.LiteralExpression(
                item ? SyntaxKind.TrueLiteralExpression : SyntaxKind.FalseLiteralExpression),
            sbyte item => Numeric($"(sbyte){item.ToString(CultureInfo.InvariantCulture)}"),
            byte item => Numeric($"(byte){item.ToString(CultureInfo.InvariantCulture)}"),
            short item => Numeric($"(short){item.ToString(CultureInfo.InvariantCulture)}"),
            ushort item => Numeric($"(ushort){item.ToString(CultureInfo.InvariantCulture)}"),
            int item => Numeric(item.ToString(CultureInfo.InvariantCulture)),
            uint item => Numeric($"{item.ToString(CultureInfo.InvariantCulture)}U"),
            long item => Numeric($"{item.ToString(CultureInfo.InvariantCulture)}L"),
            ulong item => Numeric($"{item.ToString(CultureInfo.InvariantCulture)}UL"),
            float item when float.IsNaN(item) => Numeric("float.NaN"),
            float item when float.IsInfinity(item) =>
                Numeric(item > 0 ? "float.PositiveInfinity" : "float.NegativeInfinity"),
            float item => Numeric($"{item.ToString("R", CultureInfo.InvariantCulture)}F"),
            double item when double.IsNaN(item) => Numeric("double.NaN"),
            double item when double.IsInfinity(item) =>
                Numeric(item > 0 ? "double.PositiveInfinity" : "double.NegativeInfinity"),
            double item => Numeric($"{item.ToString("R", CultureInfo.InvariantCulture)}D"),
            decimal item => Numeric($"{item.ToString(CultureInfo.InvariantCulture)}M"),
            _ => null!
        };
        return expression is not null;
    }

    private static ExpressionSyntax Numeric(string value) => SyntaxFactory.ParseExpression(value);
}