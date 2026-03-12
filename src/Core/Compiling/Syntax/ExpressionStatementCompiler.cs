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
    /// Extracts the policy id and sets it on the context, but returns
    /// the ORIGINAL invocation unchanged to preserve the SyntaxTree reference.
    /// </summary>
    private static InvocationExpressionSyntax UnwrapWithIdChain(
        IDocumentCompilationContext context,
        InvocationExpressionSyntax invocation)
    {
        // Check if this is a method call on the result of WithId()
        // Pattern: something.WithId("id").Method(...)
        // The invocation.Expression would be: something.WithId("id").Method
        // We extract the id but return the original invocation to preserve
        // the SyntaxTree for semantic model lookups.

        if (invocation.Expression is MemberAccessExpressionSyntax memberAccess &&
            memberAccess.Expression is InvocationExpressionSyntax innerInvocation &&
            innerInvocation.Expression is MemberAccessExpressionSyntax innerMemberAccess &&
            innerMemberAccess.Name.ToString() == "WithId")
        {
            if (context.PendingPolicyId is null && innerInvocation.ArgumentList.Arguments.Count == 1)
            {
                var argExpression = innerInvocation.ArgumentList.Arguments[0].Expression;
                var idValue = ExtractConstantStringValue(context, argExpression);
                if (idValue is not null)
                {
                    context.PendingPolicyId = idValue;
                }
            }
        }

        // Return the original invocation unchanged - the caller extracts the
        // method name from invocation.Expression as MemberAccessExpressionSyntax.Name
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