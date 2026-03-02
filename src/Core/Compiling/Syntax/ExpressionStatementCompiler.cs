// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling.Diagnostics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling.Syntax;

public class ExpressionStatementCompiler : ISyntaxCompiler
{
    private readonly IReadOnlyDictionary<string, IMethodPolicyHandler> _handlers;

    public ExpressionStatementCompiler(IEnumerable<IMethodPolicyHandler> handlers)
    {
        _handlers = handlers.ToDictionary(h => h.MethodName);
    }

    public SyntaxKind Syntax => SyntaxKind.ExpressionStatement;

    public void Compile(IDocumentCompilationContext context, SyntaxNode node)
    {
        var statement = node as ExpressionStatementSyntax ?? throw new NullReferenceException(nameof(node));
        if (statement.Expression is not InvocationExpressionSyntax invocation)
        {
            context.Report(Diagnostic.Create(
                CompilationErrors.ExpressionNotSupported,
                statement.Expression.GetLocation(),
                statement.Expression.GetType().Name,
                nameof(InvocationExpressionSyntax)
            ));
            return;
        }

        // Unwrap WithId() chains and extract the policy id (last one wins)
        invocation = UnwrapWithIdChain(context, invocation);

        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
        {
            context.Report(Diagnostic.Create(
                CompilationErrors.ExpressionNotSupported,
                invocation.Expression.GetLocation(),
                invocation.Expression.GetType().Name,
                nameof(MemberAccessExpressionSyntax)
            ));
            return;
        }

        var name = memberAccess.Name.ToString();
        if (_handlers.TryGetValue(name, out var handler))
        {
            handler.Handle(context, invocation);
        }
        else
        {
            context.Report(Diagnostic.Create(
                CompilationErrors.MethodNotSupported,
                memberAccess.GetLocation(),
                name
            ));
        }
    }

    /// <summary>
    /// Unwraps chained WithId() calls from an invocation expression.
    /// For example: context.WithId("a").WithId("b").SetHeader(...) 
    /// Returns the SetHeader invocation and sets context.PendingPolicyId to "b" (last wins).
    /// </summary>
    private static InvocationExpressionSyntax UnwrapWithIdChain(
        IDocumentCompilationContext context,
        InvocationExpressionSyntax invocation)
    {
        // Check if this is a method call on the result of WithId()
        // Pattern: something.WithId("id").Method(...)
        // The invocation.Expression would be: something.WithId("id").Method
        // We need to check if "something.WithId("id")" is itself an InvocationExpression of WithId

        // Track if we've set the id (first WithId encountered is the outermost/last in chain)
        var idSet = false;

        while (invocation.Expression is MemberAccessExpressionSyntax memberAccess &&
               memberAccess.Expression is InvocationExpressionSyntax innerInvocation &&
               innerInvocation.Expression is MemberAccessExpressionSyntax innerMemberAccess &&
               innerMemberAccess.Name.ToString() == "WithId")
        {
            // Extract the id from WithId("id") call - only take the first (outermost) one
            if (!idSet && innerInvocation.ArgumentList.Arguments.Count == 1)
            {
                var argExpression = innerInvocation.ArgumentList.Arguments[0].Expression;
                var idValue = ExtractConstantStringValue(context, argExpression);
                if (idValue is not null)
                {
                    context.PendingPolicyId = idValue;
                    idSet = true;
                }
            }

            // Reconstruct the invocation without the WithId in the chain
            // context.WithId("id").Method(args) -> context.Method(args)
            var newMemberAccess = SyntaxFactory.MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                innerMemberAccess.Expression,
                memberAccess.Name);

            invocation = invocation.WithExpression(newMemberAccess);
        }

        return invocation;
    }

    /// <summary>
    /// Extracts a constant string value from an expression, handling both string literals and const field references.
    /// </summary>
    private static string? ExtractConstantStringValue(IDocumentCompilationContext context, ExpressionSyntax expression)
    {
        // Handle direct string literals
        if (expression is LiteralExpressionSyntax literal && literal.IsKind(SyntaxKind.StringLiteralExpression))
        {
            return literal.Token.ValueText;
        }

        // Handle constant references (const fields, etc.) using semantic model
        var semanticModel = context.Compilation.GetSemanticModel(expression.SyntaxTree);
        var constantValue = semanticModel.GetConstantValue(expression);

        if (constantValue.HasValue && constantValue.Value is string stringValue)
        {
            return stringValue;
        }

        return null;
    }
}