// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling;

public class ConstFoldingRewriter : CSharpSyntaxRewriter
{
    private readonly SemanticModel _semanticModel;

    public ConstFoldingRewriter(SemanticModel semanticModel)
    {
        _semanticModel = semanticModel;
    }

    public override SyntaxNode? VisitMemberAccessExpression(MemberAccessExpressionSyntax node)
    {
        var symbolInfo = _semanticModel.GetSymbolInfo(node);
        var symbol = symbolInfo.Symbol ?? symbolInfo.CandidateSymbols.FirstOrDefault();

        if (symbol is IFieldSymbol { IsConst: true } field
            && field.ConstantValue is not null
            && IsDefinedInSource(field))
        {
            return field.ConstantValue switch
            {
                string s => SyntaxFactory.LiteralExpression(
                    SyntaxKind.StringLiteralExpression, SyntaxFactory.Literal(s)),
                int i => SyntaxFactory.LiteralExpression(
                    SyntaxKind.NumericLiteralExpression, SyntaxFactory.Literal(i)),
                bool b => SyntaxFactory.LiteralExpression(
                    b ? SyntaxKind.TrueLiteralExpression : SyntaxKind.FalseLiteralExpression),
                _ => base.VisitMemberAccessExpression(node)!
            };
        }

        return base.VisitMemberAccessExpression(node);
    }

    // Only fold constants declared in the user's source code. Constants and enum members
    // from referenced or runtime assemblies (e.g. System.*, Newtonsoft.*, Microsoft.*) must be
    // left intact, otherwise things like StringComparison.OrdinalIgnoreCase would fold to their
    // underlying value (5) and break the emitted policy expression.
    private static bool IsDefinedInSource(IFieldSymbol field) =>
        !field.DeclaringSyntaxReferences.IsDefaultOrEmpty;
}
