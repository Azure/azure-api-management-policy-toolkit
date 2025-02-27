// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.ApiManagement.PolicyToolkit.Compiling.Diagnostics;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Azure.ApiManagement.PolicyToolkit.Compiling.Syntax;

public class ExpressionStatementCompiler : ISyntaxCompiler
{
    private readonly IReadOnlyDictionary<string, IMethodPolicyHandler> _handlers;

    public ExpressionStatementCompiler(IEnumerable<IMethodPolicyHandler> handlers)
    {
        _handlers = handlers.ToDictionary(h => h.MethodName);
    }

    public SyntaxKind Syntax => SyntaxKind.ExpressionStatement;

    public void Compile(ICompilationContext context, SyntaxNode node)
    {
        var statement = node as ExpressionStatementSyntax ?? throw new NullReferenceException(nameof(node));
        var invocation = statement.Expression as InvocationExpressionSyntax;
        if (invocation == null)
        {
            context.Report(Diagnostic.Create(
                CompilationErrors.ExpressionNotSupported,
                statement.Expression.GetLocation(),
                statement.Expression.GetType().Name,
                nameof(InvocationExpressionSyntax)
            ));
            return;
        }

        var memberAccess = invocation.Expression as MemberAccessExpressionSyntax;
        if (memberAccess == null)
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
}