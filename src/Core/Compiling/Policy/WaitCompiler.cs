// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Xml.Linq;

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling.Diagnostics;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling.Syntax;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling.Policy;

public class WaitCompiler : IMethodPolicyHandler
{
    private readonly Lazy<BlockCompiler> _blockCompiler;
    private readonly Lazy<IEnumerable<IMethodPolicyHandler>> _policyHandlers;

    public WaitCompiler(Lazy<BlockCompiler> blockCompiler, Lazy<IEnumerable<IMethodPolicyHandler>> policyHandlers)
    {
        _blockCompiler = blockCompiler;
        _policyHandlers = policyHandlers;
    }

    public string MethodName => nameof(IInboundContext.Wait);

    public void Handle(IDocumentCompilationContext context, InvocationExpressionSyntax node)
    {
        var semanticModel = context.Compilation.GetSemanticModel(node.SyntaxTree);
        var method = GetMethodSymbol(semanticModel, node);
        var branchScoped = method is not null
            ? IsBranchScopedMethod(method)
            : IsBranchScopedSyntax(semanticModel, node);

        if (branchScoped)
        {
            HandleBranches(context, node, semanticModel,
                method ?? GetMethodSymbol(semanticModel, node, branchScopedOnly: true));
        }
        else
        {
            HandleLegacy(context, node);
        }
    }

    private void HandleLegacy(IDocumentCompilationContext context, InvocationExpressionSyntax node)
    {
        if (node.ArgumentList.Arguments.Count is > 2 or < 1)
        {
            context.Report(Diagnostic.Create(
                CompilationErrors.ArgumentCountMissMatchForPolicy,
                node.ArgumentList.GetLocation(),
                "wait"));
            return;
        }

        ExpressionSyntax childPoliciesLambdaExpression = node.ArgumentList.Arguments[0].Expression;
        if (childPoliciesLambdaExpression is not LambdaExpressionSyntax lambda)
        {
            context.Report(Diagnostic.Create(
                CompilationErrors.ValueShouldBe,
                childPoliciesLambdaExpression.GetLocation(),
                "wait",
                nameof(LambdaExpressionSyntax)));
            return;
        }

        if (lambda.Block is null)
        {
            context.Report(Diagnostic.Create(
                CompilationErrors.NotSupportedStatement,
                lambda.GetLocation(),
                childPoliciesLambdaExpression.GetType().FullName
            ));
            return;
        }

        XElement element = new("wait");

        if (node.ArgumentList.Arguments.Count == 2)
        {
            string value = node.ArgumentList.Arguments[1].Expression.ProcessParameter(context);
            element.Add(new XAttribute("for", value));
        }

        var subContext = new DocumentCompilationContext(context, element);
        _blockCompiler.Value.Compile(subContext, lambda.Block);

        context.AddPolicy(element);
    }

    private void HandleBranches(IDocumentCompilationContext context, InvocationExpressionSyntax node,
        SemanticModel semanticModel, IMethodSymbol? method)
    {
        ArgumentSyntax? waitFor = null;
        var branches = new List<ArgumentSyntax>();
        foreach (var argument in node.ArgumentList.Arguments)
        {
            var name = argument.NameColon?.Name.Identifier.ValueText;
            if (!argument.RefKindKeyword.IsKind(SyntaxKind.None) ||
                name is not null and not ("waitFor" or "branches"))
            {
                ReportInvalidBranch(context, argument, "Use waitFor and individual lambdas without ref, out, or in arguments.");
                return;
            }

            if (name == "waitFor" || name is null && argument == node.ArgumentList.Arguments[0])
            {
                if (waitFor is not null)
                {
                    context.Report(Diagnostic.Create(
                        CompilationErrors.ArgumentCountMissMatchForPolicy,
                        argument.GetLocation(),
                        "wait"));
                    return;
                }

                waitFor = argument;
            }
            else
            {
                branches.Add(argument);
            }
        }

        if (waitFor is null)
        {
            context.Report(Diagnostic.Create(
                CompilationErrors.RequiredParameterNotDefined,
                node.ArgumentList.GetLocation(),
                "wait",
                "waitFor"));
            return;
        }

        if (branches.Count == 0)
        {
            context.Report(Diagnostic.Create(
                CompilationErrors.RequiredParameterIsEmpty,
                node.ArgumentList.GetLocation(),
                "wait",
                "branches"));
            return;
        }

        var diagnosticStart = context.Diagnostics.Count;
        var element = new XElement("wait");
        var constant = semanticModel.GetConstantValue(waitFor.Expression);
        if (!constant.HasValue || constant.Value is not null)
        {
            element.Add(new XAttribute("for", waitFor.Expression.ProcessParameter(context)));
        }

        var branchContextType = GetBranchContextType(method);
        foreach (var branch in branches)
        {
            if (branch.Expression is not LambdaExpressionSyntax lambda)
            {
                ReportInvalidBranch(context, branch,
                    "Branches must be supplied as individual lambdas, not arrays or other expressions.");
                continue;
            }

            CompileBranch(context, element, lambda, semanticModel, branchContextType);
        }

        if (!HasErrorsSince(context, diagnosticStart))
        {
            context.AddPolicy(element);
        }
    }

    private void CompileBranch(IDocumentCompilationContext context, XElement wait, LambdaExpressionSyntax lambda,
        SemanticModel semanticModel, ITypeSymbol? branchContextType)
    {
        var parameter = lambda switch
        {
            SimpleLambdaExpressionSyntax simple => simple.Parameter,
            ParenthesizedLambdaExpressionSyntax parenthesized when parenthesized.ParameterList.Parameters.Count == 1 =>
                parenthesized.ParameterList.Parameters[0],
            _ => null
        };
        if (parameter is null)
        {
            ReportInvalidBranch(context, lambda, "Each branch lambda must have exactly one context parameter.");
            return;
        }

        if (lambda.AsyncKeyword.IsKind(SyntaxKind.AsyncKeyword))
        {
            ReportInvalidBranch(context, lambda, "Branch lambdas must be synchronous.");
            return;
        }

        if (parameter.Modifiers.Count != 0)
        {
            ReportInvalidBranch(context, parameter, "The branch context parameter cannot have ref, out, in, or params modifiers.");
            return;
        }

        if (parameter.Type is not null && branchContextType is not null)
        {
            var parameterType = semanticModel.GetTypeInfo(parameter.Type).Type;
            if (parameterType is { TypeKind: not TypeKind.Error } &&
                !SymbolEqualityComparer.Default.Equals(parameterType, branchContextType))
            {
                ReportInvalidBranch(context, parameter,
                    "The branch parameter must have the same context type as the Wait overload.");
                return;
            }
        }

        if (lambda.Block is { Statements.Count: not 1 })
        {
            ReportInvalidBranch(context, lambda, "Each branch must contain exactly one top-level statement.");
            return;
        }

        SyntaxNode? child = lambda.Block is { } block ? block.Statements[0] : lambda.ExpressionBody;
        var invocation = child switch
        {
            ExpressionStatementSyntax { Expression: InvocationExpressionSyntax call } => call,
            InvocationExpressionSyntax call => call,
            _ => null
        };
        if (child is not IfStatementSyntax &&
            (invocation?.Expression is not MemberAccessExpressionSyntax member ||
             member.Name.Identifier.ValueText is not (nameof(IInboundContext.SendRequest) or nameof(IInboundContext.CacheLookupValue))))
        {
            ReportInvalidBranch(context, child ?? lambda,
                "The immediate child must be send-request, cache-lookup-value, or choose.");
            return;
        }

        if (!ValidateReceivers(context, lambda, parameter, semanticModel, invocation, out var policyId))
        {
            return;
        }

        var diagnosticStart = context.Diagnostics.Count;
        var container = new XElement("branch");
        var branchContext = new DocumentCompilationContext(context, container)
        {
            PendingPolicyId = policyId
        };
        if (child is IfStatementSyntax)
        {
            _blockCompiler.Value.Compile(branchContext, lambda.Block!);
        }
        else
        {
            var name = ((MemberAccessExpressionSyntax)invocation!.Expression).Name.Identifier.ValueText;
            var handler = _policyHandlers.Value.FirstOrDefault(handler => handler.MethodName == name);
            if (handler is null)
            {
                context.Report(Diagnostic.Create(
                    CompilationErrors.MethodNotSupported,
                    invocation.GetLocation(),
                    name));
                return;
            }

            // Keep the original invocation attached to its semantic model for policy expressions.
            handler.Handle(branchContext, invocation);
        }

        if (HasErrorsSince(context, diagnosticStart))
        {
            return;
        }

        var children = container.Nodes().ToArray();
        if (children is not [XElement policy] || policy.Name.Namespace != XNamespace.None ||
            policy.Name.LocalName is not ("send-request" or "cache-lookup-value" or "choose"))
        {
            ReportInvalidBranch(context, lambda,
                "Each branch must compile to exactly one send-request, cache-lookup-value, or choose element.");
            return;
        }

        wait.Add(policy);
    }

    private bool ValidateReceivers(IDocumentCompilationContext context, LambdaExpressionSyntax lambda,
        ParameterSyntax parameter, SemanticModel semanticModel, InvocationExpressionSyntax? immediateInvocation,
        out string? policyId)
    {
        policyId = null;
        var parameterSymbol = semanticModel.GetDeclaredSymbol(parameter);
        if (!ValidateCapturedContexts(context, lambda, parameterSymbol, semanticModel))
        {
            return false;
        }

        var invocations = lambda.Body.DescendantNodesAndSelf(node => node is not LambdaExpressionSyntax nested ||
                nested is ParenthesizedLambdaExpressionSyntax { ParameterList.Parameters.Count: 0 })
            .OfType<InvocationExpressionSyntax>();
        foreach (var invocation in invocations)
        {
            if (invocation.Expression is not MemberAccessExpressionSyntax member)
            {
                continue;
            }

            var method = GetMethodSymbol(semanticModel, invocation);
            var policyMethod = method is not null && IsAuthoringContext(method.ContainingType);
            var policyStatement = invocation.Parent is ExpressionStatementSyntax or LambdaExpressionSyntax &&
                                  _policyHandlers.Value.Any(handler => handler.MethodName == member.Name.Identifier.ValueText);
            if (!policyMethod && !policyStatement)
            {
                continue;
            }

            if (!TryUnwrapWithId(context, member.Expression, semanticModel, out var receiverExpression, out var id))
            {
                return false;
            }

            var receiverSymbol = semanticModel.GetSymbolInfo(receiverExpression).Symbol;
            if (receiverExpression is not IdentifierNameSyntax receiver ||
                (parameterSymbol is not null && receiverSymbol is not null
                    ? !SymbolEqualityComparer.Default.Equals(parameterSymbol, receiverSymbol)
                    : receiver.Identifier.ValueText != parameter.Identifier.ValueText))
            {
                ReportInvalidBranch(context, invocation,
                    "Child policies must be invoked directly on the branch context parameter.");
                return false;
            }

            if (method is not null && !policyMethod)
            {
                ReportInvalidBranch(context, invocation, "Child invocations must be authoring context policy methods.");
                return false;
            }

            if (invocation == immediateInvocation)
            {
                policyId = id;
            }
        }

        return true;
    }

    private static bool TryUnwrapWithId(IDocumentCompilationContext context, ExpressionSyntax expression,
        SemanticModel semanticModel, out ExpressionSyntax receiver, out string? policyId)
    {
        receiver = expression;
        policyId = null;
        while (receiver is InvocationExpressionSyntax
               {
                   Expression: MemberAccessExpressionSyntax { Name.Identifier.ValueText: nameof(IInboundContext.WithId) } member
               } metadata)
        {
            var method = GetMethodSymbol(semanticModel, metadata);
            if (method is not null && !IsAuthoringContext(method.ContainingType))
            {
                ReportInvalidBranch(context, metadata, "WithId chaining must remain on the branch context parameter.");
                return false;
            }

            if (metadata.ArgumentList.Arguments.Count != 1)
            {
                ReportInvalidBranch(context, metadata, "WithId requires exactly one constant string ID.");
                return false;
            }

            var constant = semanticModel.GetConstantValue(metadata.ArgumentList.Arguments[0].Expression);
            if (!constant.HasValue || constant.Value is not string id)
            {
                ReportInvalidBranch(context, metadata, "WithId requires a constant string ID.");
                return false;
            }

            // Match ExpressionStatementCompiler: the outermost (last) WithId wins.
            policyId ??= id;
            receiver = member.Expression;
        }

        return true;
    }

    private static bool ValidateCapturedContexts(IDocumentCompilationContext context, LambdaExpressionSyntax lambda,
        ISymbol? parameterSymbol, SemanticModel semanticModel)
    {
        foreach (var identifier in lambda.Body.DescendantNodesAndSelf().OfType<IdentifierNameSyntax>())
        {
            var symbol = semanticModel.GetSymbolInfo(identifier).Symbol;
            if (symbol is null)
            {
                continue;
            }

            var type = symbol switch
            {
                IParameterSymbol parameter => parameter.Type,
                ILocalSymbol local => local.Type,
                IFieldSymbol field => field.Type,
                IPropertySymbol property => property.Type,
                _ => null
            };
            if (!IsSectionContextType(type) ||
                SymbolEqualityComparer.Default.Equals(symbol, parameterSymbol) ||
                symbol.DeclaringSyntaxReferences.Any(reference =>
                    reference.SyntaxTree == lambda.SyntaxTree && lambda.Body.Span.Contains(reference.Span)) ||
                identifier.Ancestors().OfType<InvocationExpressionSyntax>()
                    .Any(invocation => semanticModel.GetOperation(invocation) is INameOfOperation))
            {
                continue;
            }

            ReportInvalidBranch(context, identifier,
                "References to an outer section context are not allowed; child policies, conditions, and configuration must use the branch context parameter.");
            return false;
        }

        return true;
    }

    private static bool IsSectionContextType(ITypeSymbol? type) =>
        type is INamedTypeSymbol named &&
        (IsAuthoringContext(named, includeBaseContext: true) ||
         named.AllInterfaces.Any(context => IsAuthoringContext(context, includeBaseContext: true)));

    private static IMethodSymbol? GetMethodSymbol(SemanticModel semanticModel, InvocationExpressionSyntax invocation,
        bool branchScopedOnly = false)
    {
        var symbols = semanticModel.GetSymbolInfo(invocation);
        if (symbols.Symbol is IMethodSymbol method && (!branchScopedOnly || IsBranchScopedMethod(method)))
        {
            return method;
        }

        var candidates = symbols.CandidateSymbols.OfType<IMethodSymbol>()
            .Where(candidate => !branchScopedOnly || IsBranchScopedMethod(candidate))
            .ToArray();
        return candidates.Length == 1 ? candidates[0] : null;
    }

    private static bool IsBranchScopedMethod(IMethodSymbol method) =>
        method.Parameters.Length == 2 && method.Parameters[0].Type.SpecialType == SpecialType.System_String &&
        method.Parameters[1].IsParams;

    private static bool IsBranchScopedSyntax(SemanticModel semanticModel, InvocationExpressionSyntax node)
    {
        var arguments = node.ArgumentList.Arguments;
        if (arguments.Any(argument => argument.NameColon?.Name.Identifier.ValueText == "section"))
        {
            return false;
        }

        if (arguments.Any(argument => argument.NameColon?.Name.Identifier.ValueText == "branches"))
        {
            return true;
        }

        if (arguments.Count == 0 || arguments[0].Expression is LambdaExpressionSyntax)
        {
            return false;
        }

        var type = semanticModel.GetTypeInfo(arguments[0].Expression).Type;
        return type is not INamedTypeSymbol { Name: "Action", Arity: 0 };
    }

    private static ITypeSymbol? GetBranchContextType(IMethodSymbol? method)
    {
        if (method?.Parameters.LastOrDefault()?.Type is IArrayTypeSymbol
            {
                ElementType: INamedTypeSymbol { TypeArguments.Length: 1 } action
            })
        {
            return action.TypeArguments[0];
        }

        return null;
    }

    private static bool IsAuthoringContext(INamedTypeSymbol type, bool includeBaseContext = false) =>
        type.ContainingNamespace.ToDisplayString() == typeof(IInboundContext).Namespace &&
        type.ContainingAssembly.Identity.Name == typeof(IInboundContext).Assembly.GetName().Name &&
        (type.Name is nameof(IInboundContext) or nameof(IOutboundContext) or nameof(IBackendContext)
             or nameof(IOnErrorContext) or nameof(IFragmentContext) ||
         includeBaseContext && type.Name == nameof(IHaveExpressionContext));

    private static bool HasErrorsSince(IDocumentCompilationContext context, int diagnosticStart) =>
        context.Diagnostics.Skip(diagnosticStart).Any(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);

    private static void ReportInvalidBranch(IDocumentCompilationContext context, SyntaxNode node, string reason) =>
        context.Report(Diagnostic.Create(CompilationErrors.InvalidWaitBranch, node.GetLocation(), reason));
}