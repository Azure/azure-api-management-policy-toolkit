// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling;

/// <summary>
/// Removes whitespace trivia from a syntax tree while preserving comments.
/// </summary>
public class TriviaRemoverRewriter : CSharpSyntaxRewriter
{
    public override SyntaxTriviaList VisitList(SyntaxTriviaList list)
    {
        var kept = list.Where(t =>
            t.IsKind(SyntaxKind.SingleLineCommentTrivia) ||
            t.IsKind(SyntaxKind.MultiLineCommentTrivia)).ToArray();
        return SyntaxFactory.TriviaList(kept);
    }
}