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

            var capturedContext = false;
            foreach (var name in lambda.Body.DescendantNodesAndSelf(
                         node => node is not LambdaExpressionSyntax).OfType<IdentifierNameSyntax>())
            {
                if (name.Parent is NameColonSyntax)
                {
                    continue;
                }

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

                if (symbol is IMethodSymbol { Name: "WithId" } withId &&
                    IsSectionContext(withId.ContainingType))
                {
                    continue;
                }

                var type = symbol switch
                {
                    IParameterSymbol p => p.Type,
                    ILocalSymbol local => local.Type,
                    IFieldSymbol field => field.Type,
                    IPropertySymbol property => property.Type,
                    IMethodSymbol contextMethod => contextMethod.ReturnType,
                    _ => null
                };
                if (type is INamedTypeSymbol section && IsWaitContextType(section) &&
                    !IsBranchLocalSymbol(symbol, lambda, parameter) &&
                    !(IsExpressionContextType(section) &&
                      IsBranchExpressionContextProjection(name, symbol, lambda, parameter,
                          context.SemanticModel)) &&
                    !(symbol is IMethodSymbol &&
                      GetCalledInvocation(name, context.SemanticModel) is { } called &&
                      IsBranchLocalContextOrigin(called, context.SemanticModel, lambda, parameter,
                          new Dictionary<IParameterSymbol, bool>(SymbolEqualityComparer.Default),
                          new HashSet<IMethodSymbol>(SymbolEqualityComparer.Default))))
                {
                    context.ReportDiagnostic(Diagnostic.Create(Rules.WaitBranch.CapturedContext,
                        name.GetLocation()));
                    capturedContext = true;
                    break;
                }
            }

            if (capturedContext)
            {
                continue;
            }

            foreach (var call in lambda.Body.DescendantNodesAndSelf(
                         node => node is not LambdaExpressionSyntax).OfType<InvocationExpressionSyntax>())
            {
                if (context.SemanticModel.GetOperation(call) is not IInvocationOperation operation ||
                    operation.Type is not INamedTypeSymbol type || !IsWaitContextType(type) ||
                    IsBranchLocalContextOrigin(operation, context.SemanticModel, lambda, parameter,
                        new Dictionary<IParameterSymbol, bool>(SymbolEqualityComparer.Default),
                        new HashSet<IMethodSymbol>(SymbolEqualityComparer.Default)))
                {
                    continue;
                }

                context.ReportDiagnostic(Diagnostic.Create(Rules.WaitBranch.CapturedContext,
                    call.GetLocation()));
                break;
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
        IsExpressionContextType(type) ||
        type.AllInterfaces.Any(section => IsSectionContext(section) || IsBaseContext(section) ||
            IsExpressionContext(section));

    private static bool IsExpressionContextType(INamedTypeSymbol type) =>
        IsExpressionContext(type) || type.AllInterfaces.Any(IsExpressionContext);

    private static bool IsBranchLocalSymbol(ISymbol symbol, LambdaExpressionSyntax lambda, IParameterSymbol parameter) =>
        SymbolEqualityComparer.Default.Equals(symbol, parameter) ||
        symbol.DeclaringSyntaxReferences.Any(reference =>
            reference.SyntaxTree == lambda.SyntaxTree && lambda.Body.Span.Contains(reference.Span));

    private static bool IsBranchExpressionContextProjection(IdentifierNameSyntax name, ISymbol symbol,
        LambdaExpressionSyntax lambda, IParameterSymbol parameter, SemanticModel model)
    {
        if (symbol is not IPropertySymbol { Name: "ExpressionContext" } property ||
            !IsBaseContext(property.ContainingType) ||
            name.Parent is not MemberAccessExpressionSyntax member || member.Name != name ||
            model.GetOperation(member) is not IPropertyReferenceOperation projection)
        {
            return false;
        }

        return IsBranchLocalContextOrigin(projection, model, lambda, parameter,
            new Dictionary<IParameterSymbol, bool>(SymbolEqualityComparer.Default),
            new HashSet<IMethodSymbol>(SymbolEqualityComparer.Default));
    }

    private static IInvocationOperation? GetCalledInvocation(IdentifierNameSyntax name, SemanticModel model)
    {
        var invocation = name.Parent switch
        {
            InvocationExpressionSyntax direct when direct.Expression == name => direct,
            MemberAccessExpressionSyntax member when member.Name == name &&
                member.Parent is InvocationExpressionSyntax call && call.Expression == member => call,
            _ => null
        };
        return invocation is null ? null : model.GetOperation(invocation) as IInvocationOperation;
    }

    private static bool IsBranchLocalContextOrigin(
        IOperation operation, SemanticModel model, LambdaExpressionSyntax lambda, IParameterSymbol parameter,
        IReadOnlyDictionary<IParameterSymbol, bool> arguments, HashSet<IMethodSymbol> methods)
    {
        switch (operation)
        {
            case IConversionOperation conversion when conversion.OperatorMethod is null:
                return IsBranchLocalContextOrigin(conversion.Operand, model, lambda, parameter, arguments, methods);
            case IParameterReferenceOperation reference:
                return arguments.TryGetValue(reference.Parameter, out var proven)
                    ? proven
                    : IsBranchLocalSymbol(reference.Parameter, lambda, parameter);
            case ILocalReferenceOperation local:
                return IsBranchLocalSymbol(local.Local, lambda, parameter);
            case IPropertyReferenceOperation property when property.Property.Name == "ExpressionContext" &&
                                                            IsBaseContext(property.Property.ContainingType) &&
                                                            property.Instance is not null:
                return IsBranchLocalContextOrigin(property.Instance, model, lambda, parameter, arguments, methods);
            case IInvocationOperation call when call.TargetMethod.Name == "WithId" &&
                                               IsSectionContext(call.TargetMethod.ContainingType) &&
                                               call.Instance is not null:
                return IsBranchLocalContextOrigin(call.Instance, model, lambda, parameter, arguments, methods);
            case IInvocationOperation call:
                return IsBranchLocalContextHelper(call, model, lambda, parameter, arguments, methods);
            default:
                return false;
        }
    }

    private static bool IsBranchLocalContextHelper(
        IInvocationOperation invocation, SemanticModel model, LambdaExpressionSyntax lambda,
        IParameterSymbol parameter, IReadOnlyDictionary<IParameterSymbol, bool> arguments,
        HashSet<IMethodSymbol> methods)
    {
        var method = invocation.TargetMethod.OriginalDefinition;
        if (method.MethodKind is not (MethodKind.Ordinary or MethodKind.LocalFunction) ||
            method.IsVirtual || method.IsOverride || method.IsAbstract || method.IsExtern || method.IsAsync ||
            method.IsGenericMethod || method.Parameters.Any(argument => argument.RefKind != RefKind.None) ||
            method.DeclaringSyntaxReferences.Length != 1 || methods.Contains(method))
        {
            return false;
        }

        var declaration = method.DeclaringSyntaxReferences[0].GetSyntax();
        ExpressionSyntax? returnedExpression = declaration switch
        {
            MethodDeclarationSyntax { ExpressionBody: { } body } => body.Expression,
            MethodDeclarationSyntax { Body.Statements: [ReturnStatementSyntax { Expression: { } expression }] } => expression,
            LocalFunctionStatementSyntax { ExpressionBody: { } body } => body.Expression,
            LocalFunctionStatementSyntax { Body.Statements: [ReturnStatementSyntax { Expression: { } expression }] } => expression,
            _ => null
        };
        if (returnedExpression is null || !model.Compilation.SyntaxTrees.Contains(returnedExpression.SyntaxTree))
        {
            return false;
        }

        var boundArguments = new Dictionary<IParameterSymbol, bool>(SymbolEqualityComparer.Default);
        foreach (var argument in arguments)
        {
            boundArguments.Add(argument.Key, argument.Value);
        }

        foreach (var argument in invocation.Arguments)
        {
            if (argument.Parameter is null || argument.Parameter.Ordinal >= method.Parameters.Length)
            {
                return false;
            }

            boundArguments[method.Parameters[argument.Parameter.Ordinal]] = IsBranchLocalContextOrigin(
                argument.Value, model, lambda, parameter, arguments, methods);
        }

        var returnedModel = model;
        if (returnedExpression.SyntaxTree != model.SyntaxTree)
        {
#pragma warning disable RS1030 // Cross-file source helpers require their own semantic model.
            returnedModel = model.Compilation.GetSemanticModel(returnedExpression.SyntaxTree);
#pragma warning restore RS1030
        }
        var returnedOperation = returnedModel.GetOperation(returnedExpression);
        if (returnedOperation is null)
        {
            return false;
        }

        methods.Add(method);
        var proven = IsBranchLocalContextOrigin(
            returnedOperation, returnedModel, lambda, parameter, boundArguments, methods);
        methods.Remove(method);
        return proven;
    }

    private static bool IsBaseContext(INamedTypeSymbol type) =>
        type.ContainingNamespace.ToDisplayString() == AuthoringNamespace &&
        type.Name == "IHaveExpressionContext";

    private static bool IsExpressionContext(INamedTypeSymbol type) =>
        type.ContainingNamespace.ToDisplayString() == ExpressionNamespace &&
        type.Name == "IExpressionContext";
}
