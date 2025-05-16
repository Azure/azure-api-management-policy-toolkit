// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling;

public class TriviaRemoverRewriter : CSharpSyntaxRewriter
{
    public override SyntaxTriviaList VisitList(SyntaxTriviaList list)
    {
        return SyntaxFactory.TriviaList();
    }
}