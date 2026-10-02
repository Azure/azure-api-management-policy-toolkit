// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Immutable;

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring.Expressions;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling.Diagnostics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling;

internal sealed partial class PolicyExpressionCompiler(IDocumentCompilationContext context)
{
    private const int MaxExpansionDepth = 32;
    private const int MaxExpandedNodes = 10_000;

    // Helper body nodes processed across all expansions, including ones whose result is dropped.
    private const int MaxExpansionWork = 50_000;

    // Type names as C# 7 source: special type keywords, no global:: prefix and no nullable reference annotations.
    private static readonly SymbolDisplayFormat EmittedTypeFormat = new(
        globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Omitted,
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
        miscellaneousOptions: SymbolDisplayMiscellaneousOptions.UseSpecialTypes);

    private static readonly string AuthoringAssemblyName = typeof(IExpressionContext).Assembly.GetName().Name!;
    private readonly Dictionary<string, string> _namedValuePlaceholders = new();
    private readonly HashSet<string> _stringNamedValuePlaceholders = new();
    private readonly List<SyntaxNode> _emittedSources = new();
    private int _expandedNodeCount;
    private int _expansionWork;
    private readonly DeclarationRenamer _renamer = new();
    private bool _expansionLimitReported;

    public string CompileInvocation(InvocationExpressionSyntax invocation)
    {
        var model = CompilerUtils.CachedModel(context.Compilation, invocation.SyntaxTree);
        if (!TryResolveMethod(invocation, model, out var method))
        {
            ReportCannotFindMethod(invocation);
            return string.Empty;
        }

        if (TryGetNamedValue(method, out var namedValue))
        {
            return namedValue;
        }

        if (!TryGetMethodDeclaration(method, out var declaration))
        {
            ReportCannotFindMethod(invocation);
            return string.Empty;
        }

        if (!IsSafeSourceHelper(invocation, model, method))
        {
            ReportUnsupportedHelper(invocation, method);
            return string.Empty;
        }

        var stack = ImmutableArray.Create(method.OriginalDefinition);
        var bindings = CreateBindings(
            invocation,
            method,
            model,
            ImmutableDictionary<IParameterSymbol, ExpressionSyntax>.Empty,
            [],
            callerMethod: null,
            callerRenames: null);
        if (bindings is null)
        {
            return string.Empty;
        }

        // A root helper is emitted as written, except that a declaration named context would hide API Management's,
        // unless its declarations clash with ones in the arguments; then it's compiled again with them renamed.
        var declarationModel = ModelFor(declaration.SyntaxTree)!;
        var body = (SyntaxNode?)declaration.ExpressionBody?.Expression ?? declaration.Body;
        if (body is null)
        {
            ReportUnsupportedHelper(declaration, method);
            return string.Empty;
        }

        var diagnostics = context.Diagnostics.Count;
        var (expandedNodeCount, expansionWork) = (_expandedNodeCount, _expansionWork);
        var lowered = CompileRootBody(renameAll: false);
        if (lowered is not null && context.Diagnostics.Count == diagnostics && DeclarationRenamer.HasConflictingDeclaration(lowered))
        {
            (_expandedNodeCount, _expansionWork) = (expandedNodeCount, expansionWork);
            lowered = CompileRootBody(renameAll: true);
        }

        if (lowered is null)
        {
            return string.Empty;
        }

        if (lowered is IdentifierNameSyntax identifier &&
            _namedValuePlaceholders.TryGetValue(identifier.Identifier.ValueText, out var nestedNamedValue))
        {
            return nestedNamedValue;
        }

        return FinalizeCode(lowered);

        SyntaxNode? CompileRootBody(bool renameAll)
        {
            var rewriter = new HelperInliningRewriter(
                this, declarationModel, bindings, stack, method, _renamer.CreateRenames(body, declarationModel, renameAll));
            var visited = rewriter.Visit(body);
            return rewriter.HasUnsupportedWrite ? null : visited;
        }
    }

    public string CompileCondition(ExpressionSyntax condition)
    {
        var model = CompilerUtils.CachedModel(context.Compilation, condition.SyntaxTree);
        if (condition is InvocationExpressionSyntax namedValueInvocation &&
            TryResolveMethod(namedValueInvocation, model, out var namedValueMethod) &&
            TryGetNamedValue(namedValueMethod, out var namedValue))
        {
            return namedValue;
        }

        if (condition is InvocationExpressionSyntax invocation &&
            TryResolveMethod(invocation, model, out var method) &&
            method.ReturnType.SpecialType == SpecialType.System_Boolean &&
            TryGetMethodDeclaration(method, out var declaration) &&
            declaration.Body is { } body &&
            body.Statements is not [ReturnStatementSyntax { Expression: not null }])
        {
            if (!IsSafeSourceHelper(invocation, model, method))
            {
                ReportUnsupportedHelper(invocation, method);
                return string.Empty;
            }

            var stack = ImmutableArray.Create(method.OriginalDefinition);
            var bindings = CreateBindings(
                invocation,
                method,
                model,
                ImmutableDictionary<IParameterSymbol, ExpressionSyntax>.Empty,
                [],
                callerMethod: null,
                callerRenames: null);
            if (bindings is null)
            {
                return string.Empty;
            }

            var declarationModel = ModelFor(declaration.SyntaxTree)!;
            var rewriter = new HelperInliningRewriter(
                this,
                declarationModel,
                bindings,
                stack,
                method,
                _renamer.CreateRenames(body, declarationModel, renameAll: false));
            var loweredBody = (BlockSyntax?)rewriter.Visit(body);
            return loweredBody is null || rewriter.HasUnsupportedWrite
                ? string.Empty
                : FinalizeCode(loweredBody);
        }

        // Helpers called directly in the condition keep their names, unless pattern or out variables from different
        // helpers clash in the condition's shared scope; then the condition is compiled again with them renamed.
        var diagnostics = context.Diagnostics.Count;
        var (expandedNodeCount, expansionWork) = (_expandedNodeCount, _expansionWork);
        var lowered = CompileConditionExpression(
            condition,
            model,
            ImmutableDictionary<IParameterSymbol, ExpressionSyntax>.Empty,
            [],
            renameDeclarations: false);
        if (lowered is not null && context.Diagnostics.Count == diagnostics && DeclarationRenamer.HasConflictingDeclaration(lowered))
        {
            (_expandedNodeCount, _expansionWork) = (expandedNodeCount, expansionWork);
            lowered = CompileConditionExpression(
                condition,
                model,
                ImmutableDictionary<IParameterSymbol, ExpressionSyntax>.Empty,
                [],
                renameDeclarations: true);
        }

        if (lowered is ParenthesizedExpressionSyntax && condition is InvocationExpressionSyntax)
        {
            lowered = ((ParenthesizedExpressionSyntax)lowered).Expression;
        }

        return lowered is null
            ? string.Empty
            : FinalizeCode(lowered);
    }

    private ExpressionSyntax? CompileConditionExpression(
        ExpressionSyntax expression,
        SemanticModel model,
        IReadOnlyDictionary<IParameterSymbol, ExpressionSyntax> bindings,
        ImmutableArray<IMethodSymbol> stack,
        bool renameDeclarations)
    {
        switch (expression)
        {
            case InvocationExpressionSyntax invocation:
                if (!TryResolveMethod(invocation, model, out var method) ||
                    method.ReturnType.SpecialType != SpecialType.System_Boolean ||
                    !TryGetMethodDeclaration(method, out _))
                {
                    ReportUnsupportedCondition(expression);
                    return null;
                }

                return InlineSourceInvocation(
                    invocation, method, model, bindings, stack, callerMethod: null, callerRenames: null,
                    emittedOnce: !renameDeclarations, conditionOperand: true);
            case PrefixUnaryExpressionSyntax prefix when prefix.IsKind(SyntaxKind.LogicalNotExpression):
                var operand = CompileConditionExpression(prefix.Operand, model, bindings, stack, renameDeclarations);
                return operand is null ? null : prefix.WithOperand(ParenthesizeIfNeeded(operand));
            case BinaryExpressionSyntax binary when binary.IsKind(SyntaxKind.LogicalAndExpression) ||
                                                    binary.IsKind(SyntaxKind.LogicalOrExpression):
                var left = CompileConditionExpression(binary.Left, model, bindings, stack, renameDeclarations);
                var right = CompileConditionExpression(binary.Right, model, bindings, stack, renameDeclarations);
                return left is null || right is null
                    ? null
                    : binary.WithLeft(left).WithRight(right);
            case ParenthesizedExpressionSyntax parenthesized:
                var nested = CompileConditionExpression(parenthesized.Expression, model, bindings, stack, renameDeclarations);
                return nested is null ? null : parenthesized.WithExpression(nested);
            default:
                ReportUnsupportedCondition(expression);
                return null;
        }
    }

    private ExpressionSyntax? CompileExpression(
        ExpressionSyntax expression,
        SemanticModel model,
        IReadOnlyDictionary<IParameterSymbol, ExpressionSyntax> bindings,
        ImmutableArray<IMethodSymbol> stack,
        IMethodSymbol? method,
        IReadOnlyDictionary<ISymbol, string>? renames = null)
    {
        var rewriter = new HelperInliningRewriter(this, model, bindings, stack, method, renames);
        return (ExpressionSyntax?)rewriter.Visit(expression);
    }

    private ExpressionSyntax? InlineSourceInvocation(
        InvocationExpressionSyntax invocation,
        IMethodSymbol method,
        SemanticModel callerModel,
        IReadOnlyDictionary<IParameterSymbol, ExpressionSyntax> callerBindings,
        ImmutableArray<IMethodSymbol> stack,
        IMethodSymbol? callerMethod,
        IReadOnlyDictionary<ISymbol, string>? callerRenames,
        bool emittedOnce = false,
        bool conditionOperand = false)
    {
        if (_expansionLimitReported)
        {
            return null;
        }

        var countedBefore = _expandedNodeCount;
        if (stack.Any(candidate => SymbolEqualityComparer.Default.Equals(candidate, method.OriginalDefinition)))
        {
            context.Report(Diagnostic.Create(
                CompilationErrors.RecursiveExpressionHelper,
                invocation.GetLocation(),
                string.Join(" -> ", stack.Select(item => item.Name).Append(method.Name))));
            return null;
        }

        if (stack.Length >= MaxExpansionDepth)
        {
            context.Report(Diagnostic.Create(
                CompilationErrors.ExpressionExpansionLimitExceeded,
                invocation.GetLocation(),
                method.Name));
            return null;
        }

        if (!IsSafeSourceHelper(invocation, callerModel, method) ||
            !TryGetMethodDeclaration(method, out var declaration))
        {
            ReportUnsupportedHelper(invocation, method);
            return null;
        }

        if (TryGetNamedValue(method, out var namedValue))
        {
            // Like context.NamedValue("x"), a dynamic [NamedValue] helper implicitly converted to string is a string.
            return CreateNamedValuePlaceholder(
                namedValue,
                method.ReturnType.SpecialType == SpecialType.System_String ||
                invocation.Parent is not ParenthesizedExpressionSyntax &&
                callerModel.GetTypeInfo(invocation) is
                {
                    Type.TypeKind: TypeKind.Dynamic, ConvertedType.SpecialType: SpecialType.System_String
                });
        }

        var nextStack = stack.Add(method.OriginalDefinition);
        var bindings = CreateBindings(invocation, method, callerModel, callerBindings, stack, callerMethod, callerRenames);
        if (bindings is null)
        {
            return null;
        }

        var returnExpression = ReturnExpression(declaration);
        if (returnExpression is null)
        {
            ReportUnsupportedHelper(declaration, method);
            return null;
        }

        if (ExceedsExpansionWork(returnExpression, invocation, method))
        {
            return null;
        }

        // An inlined helper can appear more than once and next to any caller code, so the names it declares get
        // unique names. A helper called directly in a condition is emitted once and keeps its names.
        var declarationModel = ModelFor(declaration.SyntaxTree)!;
        var renames = _renamer.CreateRenames(returnExpression, declarationModel, renameAll: !emittedOnce);
        var lowered = CompileExpression(returnExpression, declarationModel, bindings, nextStack, method, renames);
        if (lowered is null || ExceedsExpansionBudget(lowered, countedBefore, invocation, method))
        {
            return null;
        }

        // Keep the helper's implicit conversion of its result to its return type, e.g. double Two() => 2, except for
        // a dynamic helper called directly in a condition, which converts it anyway; a raw named value there is
        // still kept together by parentheses, ({{flag}}).
        var resultType = declarationModel.GetTypeInfo(returnExpression).Type;
        if (resultType is { TypeKind: TypeKind.Dynamic } && conditionOperand &&
            method.ReturnType.SpecialType == SpecialType.System_Boolean)
        {
            return lowered switch
            {
                InvocationExpressionSyntax conditionCall when NamedValueRewriter.IsNamedValueCall(conditionCall, out _) =>
                    SyntaxFactory.ParenthesizedExpression(SyntaxFactory.ParenthesizedExpression(conditionCall)),
                IdentifierNameSyntax token when _namedValuePlaceholders.ContainsKey(token.Identifier.ValueText) =>
                    SyntaxFactory.ParenthesizedExpression(token),
                _ => ParenthesizeIfNeeded(lowered)
            };
        }

        return resultType is IErrorTypeSymbol or ITypeParameterSymbol ||
               lowered.Unparenthesized() is IdentifierNameSyntax placeholder &&
               _stringNamedValuePlaceholders.Contains(placeholder.Identifier.ValueText) ||
               IsSameType(resultType, method.ReturnType) ||
               IsAuthoringType(method.ReturnType) ||
               !IsEmittableType(method.ReturnType)
            ? ParenthesizeIfNeeded(lowered)
            : ConvertRawNamedValue(method.ReturnType, lowered) ?? CastTo(method.ReturnType, lowered);
    }

    // A raw named value is replaced by text: converted to string it's the string "{{x}}", and a cast to another
    // type applies to all of the text, (int)({{n}}).
    private ExpressionSyntax? ConvertRawNamedValue(ITypeSymbol type, ExpressionSyntax lowered)
    {
        // A [NamedValue] helper that doesn't return string gives a raw placeholder.
        if (lowered.Unparenthesized() is IdentifierNameSyntax placeholder &&
            _namedValuePlaceholders.ContainsKey(placeholder.Identifier.ValueText) &&
            !_stringNamedValuePlaceholders.Contains(placeholder.Identifier.ValueText))
        {
            return type.SpecialType == SpecialType.System_String
                ? CreateNamedValuePlaceholder(_namedValuePlaceholders[placeholder.Identifier.ValueText], isString: true)
                : CastTo(type, SyntaxFactory.ParenthesizedExpression(placeholder));
        }

        // A parenthesized call, (context.NamedValue("x")), stays a raw token.
        if (lowered is not InvocationExpressionSyntax call || !NamedValueRewriter.IsNamedValueCall(call, out var name))
        {
            return null;
        }

        return type.SpecialType == SpecialType.System_String
            ? CreateNamedValuePlaceholder($"{{{{{name}}}}}", isString: true)
            : CastTo(type, SyntaxFactory.ParenthesizedExpression(SyntaxFactory.ParenthesizedExpression(call)));
    }

    private const string ConditionalReceiver = "__apim_conditional_receiver__";

    // Inlines an extension helper called as recv?.Helper(...) when its body is a member chain on the receiver used
    // once (s => s.Trim().ToUpper()), so the chain can continue the conditional access: recv?.Trim().ToUpper().
    private ExpressionSyntax? InlineConditionalExtension(
        InvocationExpressionSyntax invocation,
        IMethodSymbol method,
        SemanticModel callerModel,
        IReadOnlyDictionary<IParameterSymbol, ExpressionSyntax> callerBindings,
        ImmutableArray<IMethodSymbol> stack,
        IMethodSymbol? callerMethod,
        IReadOnlyDictionary<ISymbol, string>? callerRenames,
        ExpressionSyntax? receiverChain = null)
    {
        if (_expansionLimitReported)
        {
            return null;
        }

        var countedBefore = _expandedNodeCount;
        if (stack.Any(candidate => SymbolEqualityComparer.Default.Equals(candidate, method.OriginalDefinition)) ||
            stack.Length >= MaxExpansionDepth ||
            !TryGetMethodDeclaration(method, out var declaration) ||
            ReturnExpression(declaration) is not { } returnExpression ||
            CreateBindings(
                invocation, method, callerModel, callerBindings, stack, callerMethod, callerRenames, conditionalReceiver: true)
                is not { } bindings)
        {
            ReportUnsupportedHelper(invocation, method);
            return null;
        }

        // The receiver can't be converted to the this parameter's type inside the conditional access.
        var thisArgument = (callerModel.GetOperation(invocation) as IInvocationOperation)?.Arguments
            .FirstOrDefault(argument => argument.Parameter?.Ordinal == 0)?.Value;
        var receiverType = (thisArgument is IConversionOperation conversion ? conversion.Operand : thisArgument)?.Type;
        if (receiverType is not null and not (IErrorTypeSymbol or ITypeParameterSymbol) &&
            method.ReceiverType is { } thisType && !IsSameType(receiverType, thisType))
        {
            ReportUnsupportedHelper(invocation, method);
            return null;
        }

        if (ExceedsExpansionWork(returnExpression, invocation, method))
        {
            return null;
        }

        var declarationModel = ModelFor(declaration.SyntaxTree)!;
        var lowered = CompileExpression(
            returnExpression,
            declarationModel,
            bindings,
            stack.Add(method.OriginalDefinition),
            method,
            _renamer.CreateRenames(returnExpression, declarationModel, renameAll: true));
        if (lowered is null || ExceedsExpansionBudget(lowered, countedBefore, invocation, method))
        {
            return null;
        }

        // A return conversion can't be kept inside a conditional access, e.g. double Len(this string s) => s.Length.
        var resultType = declarationModel.GetTypeInfo(returnExpression).Type;
        var receivers = lowered.DescendantNodesAndSelf()
            .OfType<IdentifierNameSyntax>()
            .Where(identifier => identifier.Identifier.ValueText == ConditionalReceiver)
            .ToArray();
        if (receivers is not [var receiver] || !IsChainStart(receiver, lowered) ||
            resultType is not (IErrorTypeSymbol or ITypeParameterSymbol) && !IsSameType(resultType, method.ReturnType))
        {
            ReportUnsupportedHelper(invocation, method);
            return null;
        }

        // Inside a conditional access the receiver itself starts with a binding (recv?.Member.Helper()), so the
        // chain continues from it; otherwise the helper call was the binding (recv?.Helper()).
        if (receiverChain is not null)
        {
            return lowered.ReplaceNode(receiver, receiverChain);
        }

        ExpressionSyntax binding = receiver.Parent switch
        {
            MemberAccessExpressionSyntax memberAccess => SyntaxFactory.MemberBindingExpression(memberAccess.Name),
            ElementAccessExpressionSyntax elementAccess =>
                SyntaxFactory.ElementBindingExpression(elementAccess.ArgumentList),
            _ => null!
        };
        return lowered.ReplaceNode(receiver.Parent!, binding);

        // The receiver is the leftmost operand of a chain of member accesses, calls and indexers forming the body.
        static bool IsChainStart(SyntaxNode receiver, SyntaxNode body)
        {
            if (receiver.Parent is not (MemberAccessExpressionSyntax or ElementAccessExpressionSyntax))
            {
                return false;
            }

            for (var node = receiver; node != body; node = node.Parent!)
            {
                var continuesChain = node.Parent switch
                {
                    MemberAccessExpressionSyntax memberAccess => memberAccess.Expression == node,
                    ElementAccessExpressionSyntax elementAccess => elementAccess.Expression == node,
                    InvocationExpressionSyntax call => call.Expression == node,
                    _ => false
                };
                if (!continuesChain)
                {
                    return false;
                }
            }

            return true;
        }
    }

    private static bool IsSameType(ITypeSymbol? first, ITypeSymbol second) =>
        first is not null &&
        (SymbolEqualityComparer.Default.Equals(first, second) ||
         first.ToDisplayString(EmittedTypeFormat) == second.ToDisplayString(EmittedTypeFormat));

    private static ExpressionSyntax CastTo(ITypeSymbol type, ExpressionSyntax expression) =>
        SyntaxFactory.ParenthesizedExpression(SyntaxFactory.CastExpression(
            SyntaxFactory.ParseTypeName(type.ToDisplayString(EmittedTypeFormat)),
            ParenthesizeIfNeeded(expression)));

    private static ExpressionSyntax? ReturnExpression(MethodDeclarationSyntax declaration) =>
        declaration.ExpressionBody?.Expression ??
        (declaration.Body?.Statements is [ReturnStatementSyntax { Expression: { } expression }] ? expression : null);

    // An [Expression] method or a member of a class marked [Expression], an expression helper library.
    private static bool IsExpressionLibraryMember(ISymbol symbol)
    {
        for (var marked = symbol is IMethodSymbol or INamedTypeSymbol ? symbol : symbol.ContainingType;
             marked is not null;
             marked = marked.ContainingType)
        {
            if (marked.GetAttributes().Any(attribute =>
                    attribute.AttributeClass is { Name: nameof(ExpressionAttribute) } attributeClass &&
                    IsAuthoringAssembly(attributeClass.ContainingAssembly)))
            {
                return true;
            }
        }

        return false;
    }

    // Authoring library symbols are the API Management expression surface, even when the library is
    // referenced as a project and its declarations are available as source.
    private static bool IsUserSource(ISymbol symbol) =>
        !symbol.DeclaringSyntaxReferences.IsDefaultOrEmpty && !IsAuthoringAssembly(symbol.ContainingAssembly);

    // Symbols from different compilations (such as a referenced project's) aren't identical, so the authoring
    // library is recognised by its assembly name.
    private static bool IsAuthoringAssembly(IAssemblySymbol? assembly) => assembly?.Identity.Name == AuthoringAssemblyName;

    // A type that exists in API Management: not declared in user source, not a type parameter and not anonymous.
    private bool IsEmittableType(ITypeSymbol type) =>
        type switch
        {
            ITypeParameterSymbol or IErrorTypeSymbol => false,
            IArrayTypeSymbol array => IsEmittableType(array.ElementType),
            INamedTypeSymbol named => !named.IsAnonymousType &&
                                      !IsAuthoringType(named) &&
                                      (named.IsTupleType || !IsUserSource(named)) &&
                                      named.TypeArguments.All(IsEmittableType),
            _ => true
        };

    // Authoring library types model the API Management runtime types but their names and namespaces differ.
    private static bool IsAuthoringType(ITypeSymbol type) => IsAuthoringAssembly(type.ContainingAssembly);

    // Counts an expansion once: what was counted since countedBefore (nested helpers and arguments) is part of it.
    private bool ExceedsExpansionBudget(SyntaxNode expanded, int countedBefore, SyntaxNode location, ISymbol method)
    {
        _expandedNodeCount = countedBefore + expanded.DescendantNodesAndSelf().Count();
        return _expandedNodeCount > MaxExpandedNodes && ReportExpansionLimit(location, method);
    }

    private bool ExceedsExpansionWork(SyntaxNode body, SyntaxNode location, ISymbol method)
    {
        _expansionWork += body.DescendantNodesAndSelf().Count();
        return _expansionWork > MaxExpansionWork && ReportExpansionLimit(location, method);
    }

    private bool ReportExpansionLimit(SyntaxNode location, ISymbol method)
    {
        if (!_expansionLimitReported)
        {
            _expansionLimitReported = true;
            context.Report(Diagnostic.Create(
                CompilationErrors.ExpressionExpansionLimitExceeded,
                location.GetLocation(),
                method.Name));
        }

        return true;
    }

    private static bool IsExpressionContext(ITypeSymbol type) =>
        type is INamedTypeSymbol { Name: nameof(IExpressionContext) } && IsAuthoringType(type);

    private bool IsContextParameter(ITypeSymbol type) =>
        IsExpressionContext(type) ||
        type is IErrorTypeSymbol { Name: "ExpressionContext" } ||
        IsAuthoringSectionContext(type);

    internal static bool IsAuthoringSectionContext(ITypeSymbol type) =>
        type is INamedTypeSymbol
        {
            TypeKind: TypeKind.Interface,
            Name: { } name,
            ContainingNamespace: { } containingNamespace
        } &&
        name is (nameof(IHaveExpressionContext)
            or nameof(IInboundContext)
            or nameof(IOutboundContext)
            or nameof(IBackendContext)
            or nameof(IOnErrorContext)
            or nameof(IFragmentContext)) &&
        containingNamespace.ToDisplayString() ==
        "Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring";

    private static bool IsSafeSourceHelper(
        InvocationExpressionSyntax invocation,
        SemanticModel model,
        IMethodSymbol method)
    {
        if (method.IsAbstract || method.IsVirtual || method.IsOverride || method.DeclaringSyntaxReferences.IsDefaultOrEmpty)
        {
            return false;
        }

        if (method.IsStatic)
        {
            return true;
        }

        if (invocation.Expression is IdentifierNameSyntax ||
            invocation.Expression is MemberAccessExpressionSyntax
            {
                Expression: ThisExpressionSyntax
            })
        {
            return true;
        }

        return model.GetOperation(invocation) is IInvocationOperation
        {
            Instance: null or IInstanceReferenceOperation
        };
    }

    private static bool TryResolveMethod(
        InvocationExpressionSyntax invocation,
        SemanticModel model,
        out IMethodSymbol method)
    {
        var symbolInfo = model.GetSymbolInfo(invocation);
        var candidates = symbolInfo.CandidateSymbols.OfType<IMethodSymbol>().ToArray();
        method = (symbolInfo.Symbol as IMethodSymbol ??
                  (candidates.Length == 1 ? candidates[0] : null))!;
        return method is not null;
    }

    private bool TryGetMethodDeclaration(
        IMethodSymbol method,
        out MethodDeclarationSyntax declaration)
    {
        declaration = method.DeclaringSyntaxReferences
            .Select(reference => reference.GetSyntax())
            .OfType<MethodDeclarationSyntax>()
            .FirstOrDefault()!;
        return declaration is not null && ModelFor(declaration.SyntaxTree) is not null;
    }

    // A helper can be declared in a referenced project, whose syntax tree belongs to that project's compilation.
    private SemanticModel? ModelFor(SyntaxTree tree) =>
        CompilerUtils.FindCompilation(context.Compilation, tree) is { } compilation
            ? CompilerUtils.CachedModel(compilation, tree)
            : null;

    private bool TryGetNamedValue(IMethodSymbol method, out string value)
    {
        var attribute = method.GetAttributes().FirstOrDefault(candidate =>
            candidate.AttributeClass?.Name == "NamedValueAttribute");
        if (attribute?.ConstructorArguments is
            [{ Value: string configuredValue }])
        {
            value = configuredValue.Contains("{{", StringComparison.Ordinal)
                ? configuredValue
                : $"{{{{{configuredValue}}}}}";
            return true;
        }

        if (TryGetMethodDeclaration(method, out var declaration))
        {
            var attributeSyntax = declaration.AttributeLists
                .SelectMany(list => list.Attributes)
                .FirstOrDefault(candidate => candidate.Name.ToString() is "NamedValue" or "NamedValueAttribute");
            var argument = attributeSyntax?.ArgumentList?.Arguments.FirstOrDefault()?.Expression;
            if (argument is not null)
            {
                var constant = ModelFor(argument.SyntaxTree)?.GetConstantValue(argument);
                if (constant is { HasValue: true, Value: string syntaxValue })
                {
                    value = syntaxValue.Contains("{{", StringComparison.Ordinal)
                        ? syntaxValue
                        : $"{{{{{syntaxValue}}}}}";
                    return true;
                }
            }
        }

        value = string.Empty;
        return false;
    }

    private void ReportCannotFindMethod(InvocationExpressionSyntax invocation)
    {
        var name = invocation.Expression switch
        {
            IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
            MemberAccessExpressionSyntax memberAccess => memberAccess.Name.Identifier.ValueText,
            _ => invocation.Expression.ToString()
        };
        context.Report(Diagnostic.Create(
            CompilationErrors.CannotFindMethodCode,
            invocation.GetLocation(),
            name));
    }

    private void ReportUnsupportedHelper(SyntaxNode node, IMethodSymbol method)
    {
        context.Report(Diagnostic.Create(
            CompilationErrors.UnsupportedExpressionHelper,
            node.GetLocation(),
            method.ToDisplayString()));
    }

    private void ReportUnsupportedCondition(ExpressionSyntax expression)
    {
        context.Report(Diagnostic.Create(
            CompilationErrors.UnsupportedConditionExpression,
            expression.GetLocation(),
            expression.Kind()));
    }

    private static ExpressionSyntax ParenthesizeIfNeeded(ExpressionSyntax expression) =>
        expression is IdentifierNameSyntax or PredefinedTypeSyntax or LiteralExpressionSyntax or MemberAccessExpressionSyntax
            or InvocationExpressionSyntax or ElementAccessExpressionSyntax or ParenthesizedExpressionSyntax
            or DefaultExpressionSyntax
            ? expression
            : SyntaxFactory.ParenthesizedExpression(expression.WithoutTrivia());

    private void ReportUnsupportedConstant(SyntaxNode node, IFieldSymbol field)
    {
        context.Report(Diagnostic.Create(
            CompilationErrors.UnsupportedSourceConstant,
            node.GetLocation(),
            field.ToDisplayString()));
    }

    private void ReportUnsupportedSourceReference(SyntaxNode node, ISymbol symbol)
    {
        context.Report(Diagnostic.Create(
            CompilationErrors.UnsupportedSourceReference,
            node.GetLocation(),
            symbol.ToDisplayString()));
    }

    private IdentifierNameSyntax CreateNamedValuePlaceholder(string namedValue, bool isString)
    {
        var placeholder = $"__APIM_NAMED_VALUE_{_namedValuePlaceholders.Count}__";
        _namedValuePlaceholders.Add(placeholder, namedValue);
        if (isString)
        {
            _stringNamedValuePlaceholders.Add(placeholder);
        }

        return SyntaxFactory.IdentifierName(placeholder);
    }

    private string FinalizeCode(SyntaxNode lowered)
    {
        LanguageVersionCheck.ReportNewerLanguageFeatures(context, _emittedSources);
        var code = CompilerUtils.Normalize(
            new NamedValueRewriter(_namedValuePlaceholders, _stringNamedValuePlaceholders).Visit(lowered)!);
        return lowered is BlockSyntax ? $"@{code}" : $"@({code})";
    }
}