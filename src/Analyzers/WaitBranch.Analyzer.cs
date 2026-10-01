// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class WaitBranchAnalyzer : DiagnosticAnalyzer
{
    private const string AuthoringNamespace = "Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring";
    private const string ExpressionNamespace = AuthoringNamespace + ".Expressions";
    private static readonly HashSet<string> s_sectionNames =
    [
        "IInboundContext", "IBackendContext", "IOutboundContext", "IOnErrorContext", "IFragmentContext"
    ];

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => Rules.WaitBranch.All;

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze |
                                               GeneratedCodeAnalysisFlags.ReportDiagnostics);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeInvocation, SyntaxKind.InvocationExpression);
    }

    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
    {
        if (context.Node is not InvocationExpressionSyntax invocation ||
            context.SemanticModel.GetSymbolInfo(invocation).Symbol is not IMethodSymbol method ||
            method.Name != "Wait" ||
            !IsSectionContext(method.ContainingType) ||
            method.Parameters is not [_, { IsParams: true, Type: IArrayTypeSymbol array }] ||
            array.ElementType is not INamedTypeSymbol { Name: "Action", Arity: 1 } action ||
            action.ContainingNamespace.ToDisplayString() != "System" ||
            !SymbolEqualityComparer.Default.Equals(action.TypeArguments[0], method.ContainingType))
        {
            return;
        }

        var branches = invocation.ArgumentList.Arguments.Skip(1).ToArray();
        if (branches.Length == 0)
        {
            context.ReportDiagnostic(Diagnostic.Create(Rules.WaitBranch.InvalidChild, invocation.GetLocation()));
        }

        foreach (var branch in branches)
        {
            if (branch.Expression is not LambdaExpressionSyntax lambda ||
                GetBranchParameter(lambda, context.SemanticModel) is not { } parameter ||
                !HasSingleImmediateChild(lambda, context.SemanticModel))
            {
                context.ReportDiagnostic(Diagnostic.Create(Rules.WaitBranch.InvalidChild,
                    branch.Expression.GetLocation()));
                continue;
            }

            foreach (var name in lambda.Body.DescendantNodesAndSelf(
                         node => node is not LambdaExpressionSyntax).OfType<IdentifierNameSyntax>())
            {
                var symbol = context.SemanticModel.GetSymbolInfo(name).Symbol;
                if (symbol is null || SymbolEqualityComparer.Default.Equals(symbol, parameter))
                {
                    continue;
                }

                if (name.Ancestors().OfType<InvocationExpressionSyntax>()
                    .Any(invocation => context.SemanticModel.GetOperation(invocation) is INameOfOperation))
                {
                    continue;
                }

                if (symbol is IPropertySymbol { Name: "ExpressionContext" } expressionProperty &&
                    IsBaseContext(expressionProperty.ContainingType))
                {
                    continue;
                }

                var type = symbol switch
                {
                    IParameterSymbol p => p.Type,
                    ILocalSymbol local => local.Type,
                    IFieldSymbol field => field.Type,
                    IPropertySymbol property => property.Type,
                    _ => null
                };
                if (type is INamedTypeSymbol section && IsWaitContextType(section) &&
                    !symbol.DeclaringSyntaxReferences.Any(reference =>
                        reference.SyntaxTree == lambda.SyntaxTree && lambda.Body.Span.Contains(reference.Span)))
                {
                    context.ReportDiagnostic(Diagnostic.Create(Rules.WaitBranch.CapturedContext,
                        name.GetLocation()));
                }
            }
        }
    }

    private static IParameterSymbol? GetBranchParameter(LambdaExpressionSyntax lambda, SemanticModel model)
    {
        var parameter = lambda switch
        {
            SimpleLambdaExpressionSyntax simple => simple.Parameter,
            ParenthesizedLambdaExpressionSyntax { ParameterList.Parameters.Count: 1 } parenthesized =>
                parenthesized.ParameterList.Parameters[0],
            _ => null
        };
        return parameter is null ? null : model.GetDeclaredSymbol(parameter) as IParameterSymbol;
    }

    private static bool HasSingleImmediateChild(LambdaExpressionSyntax lambda, SemanticModel model)
    {
        if (lambda.Body is InvocationExpressionSyntax expression)
        {
            return IsSupportedDirectChild(expression, model);
        }

        if (lambda.Body is not BlockSyntax { Statements.Count: 1 } block)
        {
            return false;
        }

        return block.Statements[0] switch
        {
            ExpressionStatementSyntax { Expression: InvocationExpressionSyntax invocation } =>
                IsSupportedDirectChild(invocation, model),
            IfStatementSyntax conditional => HasSupportedChoose(conditional),
            _ => false
        };
    }

    private static bool IsSupportedDirectChild(InvocationExpressionSyntax invocation, SemanticModel model)
    {
        return model.GetSymbolInfo(invocation).Symbol is IMethodSymbol method &&
               IsSectionContext(method.ContainingType) &&
               method.Name is "SendRequest" or "CacheLookupValue";
    }

    private static bool HasSupportedChoose(IfStatementSyntax conditional)
    {
        for (var current = conditional; ; )
        {
            if (current.Condition is not InvocationExpressionSyntax ||
                current.Statement is not BlockSyntax)
            {
                return false;
            }

            if (current.Else?.Statement is IfStatementSyntax next)
            {
                current = next;
                continue;
            }

            return current.Else is null || current.Else.Statement is BlockSyntax;
        }
    }

    private static bool IsSectionContext(INamedTypeSymbol type) =>
        type.ContainingNamespace.ToDisplayString() == AuthoringNamespace &&
        s_sectionNames.Contains(type.Name);

    private static bool IsWaitContextType(INamedTypeSymbol type) =>
        IsSectionContext(type) ||
        IsBaseContext(type) ||
        IsExpressionContext(type) ||
        type.AllInterfaces.Any(section => IsSectionContext(section) || IsBaseContext(section) ||
            IsExpressionContext(section));

    private static bool IsBaseContext(INamedTypeSymbol type) =>
        type.ContainingNamespace.ToDisplayString() == AuthoringNamespace &&
        type.Name == "IHaveExpressionContext";

    private static bool IsExpressionContext(INamedTypeSymbol type) =>
        type.ContainingNamespace.ToDisplayString() == ExpressionNamespace &&
        type.Name == "IExpressionContext";
}
