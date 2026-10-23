// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling;

// Replaces named-value placeholders and context.NamedValue("name") calls with {{name}} on the syntax tree. A named
// value concatenated directly with a string literal becomes part of that literal, and a raw token inside an
// interpolation is parenthesized so it can't merge with the interpolation braces.
internal sealed class NamedValueRewriter(
    IReadOnlyDictionary<string, string> placeholders,
    IReadOnlySet<string> stringPlaceholders) : CSharpSyntaxRewriter
{
    // Marks an emitted + operand that is a string in the source, so a named value can be merged after it.
    internal const string StringOperandAnnotationKind = "StringOperand";

    private const string NamedValueAnnotationKind = "NamedValue";
    private const string NamedValueOnlyAnnotationKind = "NamedValueOnly";

    public override SyntaxNode? VisitIdentifierName(IdentifierNameSyntax node)
    {
        var name = node.Identifier.ValueText;
        if (!placeholders.TryGetValue(name, out var value))
        {
            return node;
        }

        // A non-string named value keeps its own value; merging it into a string would skip its ToString(). A
        // string named value is verbatim, so a backslash in the value isn't an escape sequence, and takes the
        // kind of any string it's merged into.
        return stringPlaceholders.Contains(name)
            ? StringContent(value, verbatim: true)
                .WithAdditionalAnnotations(new SyntaxAnnotation(NamedValueOnlyAnnotationKind))
                .WithTriviaFrom(node)
            : Token(node, value, mergeable: false);
    }

    // A parenthesized call, (context.NamedValue("x")), stays a raw token even next to a string literal; the
    // decompiler writes named values used as code this way.
    public override SyntaxNode? VisitInvocationExpression(InvocationExpressionSyntax node) =>
        IsNamedValueCall(node, out var name)
            ? Token(node, $"{{{{{name}}}}}", mergeable: node.Parent is not ParenthesizedExpressionSyntax)
            : base.VisitInvocationExpression(node);

    public override SyntaxNode? VisitBinaryExpression(BinaryExpressionSyntax node)
    {
        var visited = (BinaryExpressionSyntax)base.VisitBinaryExpression(node)!;
        return visited.IsKind(SyntaxKind.AddExpression) && Merge(visited.Left, visited.Right) is { } merged
            ? merged.WithTriviaFrom(visited)
            : visited;
    }

    // A merged string is a primary expression, so parentheses around it (from the decompiler) aren't needed.
    public override SyntaxNode? VisitParenthesizedExpression(ParenthesizedExpressionSyntax node)
    {
        var visited = (ParenthesizedExpressionSyntax)base.VisitParenthesizedExpression(node)!;
        var isMergedString = HasNamedValue(visited.Expression) &&
                             visited.Expression is LiteralExpressionSyntax or InterpolatedStringExpressionSyntax;
        var isRawToken = node.Expression is InvocationExpressionSyntax call && IsNamedValueCall(call, out _) &&
                         visited.Expression is IdentifierNameSyntax &&
                         !IsAtHoleEdge(node);
        return isMergedString || isRawToken ? visited.Expression.WithTriviaFrom(visited) : visited;
    }

    // A named value is merged into the preceding string of a + chain once the chain is a string, or into a
    // directly following string; merging into an earlier operand could change which + runs first.
    private static ExpressionSyntax? Merge(ExpressionSyntax left, ExpressionSyntax right) =>
        left is BinaryExpressionSyntax binary && binary.IsKind(SyntaxKind.AddExpression) && HasNamedValue(right)
            ? (IsStringOperand(binary.Right) || HasStringOperand(binary.Left)) &&
              Merge(binary.Right, right) is { } merged
                ? binary.WithRight(merged)
                : null
            : Concat(left, right)?.WithTriviaFrom(left);

    private static bool IsStringOperand(ExpressionSyntax operand) =>
        operand.HasAnnotations(StringOperandAnnotationKind) ||
        operand.Unparenthesized() is InterpolatedStringExpressionSyntax ||
        operand.Unparenthesized() is LiteralExpressionSyntax literal && IsString(literal);

    private static bool HasStringOperand(ExpressionSyntax chain) =>
        IsStringOperand(chain) ||
        chain is BinaryExpressionSyntax binary && binary.IsKind(SyntaxKind.AddExpression) &&
        (HasStringOperand(binary.Left) || IsStringOperand(binary.Right));

    private static ExpressionSyntax? Concat(ExpressionSyntax left, ExpressionSyntax right)
    {
        if (!HasNamedValue(left) && !HasNamedValue(right))
        {
            return null;
        }

        return (left, right) switch
        {
            (LiteralExpressionSyntax first, LiteralExpressionSyntax second)
                when IsString(first) && IsString(second) &&
                     (IsVerbatim(first) == IsVerbatim(second) || IsNamedValueOnly(first) || IsNamedValueOnly(second)) =>
                StringContent(
                    first.Token.ValueText + second.Token.ValueText,
                    IsNamedValueOnly(first) ? IsVerbatim(second) : IsVerbatim(first)),
            (LiteralExpressionSyntax first, IdentifierNameSyntax token) when IsString(first) && HasNamedValue(token) =>
                StringContent(first.Token.ValueText + token.Identifier.Text, IsVerbatim(first)),
            (IdentifierNameSyntax token, LiteralExpressionSyntax second) when IsString(second) && HasNamedValue(token) =>
                StringContent(token.Identifier.Text + second.Token.ValueText, IsVerbatim(second)),
            (InterpolatedStringExpressionSyntax first, InterpolatedStringExpressionSyntax second)
                when first.StringStartToken.IsKind(second.StringStartToken.Kind()) =>
                Annotate(first.WithContents(first.Contents.AddRange(second.Contents))),
            (InterpolatedStringExpressionSyntax first, IdentifierNameSyntax token) when HasNamedValue(token) =>
                Annotate(first.AddContents(InterpolatedText(token.Identifier.Text))),
            (IdentifierNameSyntax token, InterpolatedStringExpressionSyntax second) when HasNamedValue(token) =>
                Annotate(second.WithContents(second.Contents.Insert(0, InterpolatedText(token.Identifier.Text)))),
            _ => null
        };
    }

    private static bool IsString(LiteralExpressionSyntax literal) => literal.IsKind(SyntaxKind.StringLiteralExpression);

    private static bool IsNamedValueOnly(SyntaxNode node) => node.HasAnnotations(NamedValueOnlyAnnotationKind);

    // API Management substitutes the named value's text before compiling, so a verbatim literal must stay
    // verbatim: re-escaping it would turn backslashes in the value into escape sequences.
    private static bool IsVerbatim(LiteralExpressionSyntax literal) => literal.Token.Text.StartsWith('@');

    private static bool HasNamedValue(SyntaxNode node) => node.HasAnnotations(NamedValueAnnotationKind);

    private static T Annotate<T>(T node) where T : SyntaxNode =>
        node.WithAdditionalAnnotations(new SyntaxAnnotation(NamedValueAnnotationKind));

    // API Management substitutes {{name}} as text before compiling, so it's kept as-is inside the string.
    private static InterpolatedStringTextSyntax InterpolatedText(string text) =>
        SyntaxFactory.InterpolatedStringText(
            SyntaxFactory.Token(default, SyntaxKind.InterpolatedStringTextToken, text, text, default));

    private static ExpressionSyntax StringContent(string value, bool verbatim = false) =>
        Annotate(SyntaxFactory.LiteralExpression(
            SyntaxKind.StringLiteralExpression,
            verbatim
                ? SyntaxFactory.Literal($"@\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\"", value)
                : SyntaxFactory.Literal(value)));

    // At either end of an interpolation hole, parentheses keep {{x}} apart from the hole's braces.
    private static bool IsAtHoleEdge(SyntaxNode node) =>
        node.Ancestors().OfType<InterpolationSyntax>().FirstOrDefault()?.Expression is { } hole &&
        (hole.GetFirstToken() == node.GetFirstToken() || hole.GetLastToken() == node.GetLastToken());

    private static ExpressionSyntax Token(ExpressionSyntax original, string text, bool mergeable = true)
    {
        var token = SyntaxFactory.IdentifierName(SyntaxFactory.Identifier(text)).WithTriviaFrom(original);
        return IsAtHoleEdge(original) ? SyntaxFactory.ParenthesizedExpression(token)
            : mergeable ? Annotate(token)
            : token;
    }

    internal static bool IsNamedValueCall(InvocationExpressionSyntax node, out string name)
    {
        if (node is
            {
                Expression: MemberAccessExpressionSyntax
                {
                    Name.Identifier.ValueText: "NamedValue",
                    Expression: IdentifierNameSyntax { Identifier.ValueText: "context" } or
                    MemberAccessExpressionSyntax
                    {
                        Expression: IdentifierNameSyntax { Identifier.ValueText: "context" },
                        Name.Identifier.ValueText: "ExpressionContext"
                    }
                },
                ArgumentList.Arguments: [{ NameColon: null, Expression: LiteralExpressionSyntax literal }]
            } &&
            literal.IsKind(SyntaxKind.StringLiteralExpression))
        {
            name = literal.Token.ValueText;
            return true;
        }

        name = string.Empty;
        return false;
    }
}