// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Immutable;

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring.Expressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling;

internal sealed partial class PolicyExpressionCompiler
{
    private sealed class HelperInliningRewriter(
        PolicyExpressionCompiler compiler,
        SemanticModel model,
        IReadOnlyDictionary<IParameterSymbol, ExpressionSyntax> bindings,
        ImmutableArray<IMethodSymbol> stack,
        IMethodSymbol? method,
        IReadOnlyDictionary<ISymbol, string>? renames = null) : CSharpSyntaxRewriter
    {
        private bool _rootVisited;

        public bool HasUnsupportedWrite { get; private set; }

        public override SyntaxNode? Visit(SyntaxNode? node)
        {
            if (!_rootVisited && node is not null)
            {
                _rootVisited = true;
                compiler._emittedSources.Add(node);
            }

            var visited = base.Visit(node);
            return node is ExpressionSyntax operand &&
                   node.Parent is BinaryExpressionSyntax parent && parent.IsKind(SyntaxKind.AddExpression) &&
                   visited is not null &&
                   model.GetTypeInfo(operand).Type?.SpecialType == SpecialType.System_String
                ? visited.WithAdditionalAnnotations(new SyntaxAnnotation(NamedValueRewriter.StringOperandAnnotationKind))
                : visited;
        }

        public override SyntaxNode? VisitIdentifierName(IdentifierNameSyntax node)
        {
            var symbol = model.GetSymbolInfo(node).Symbol;
            if (symbol is IParameterSymbol parameter && bindings.TryGetValue(parameter, out var replacement))
            {
                if (IsWriteTarget(node) || IsSectionContextMemberUse(node, parameter))
                {
                    compiler.ReportUnsupportedHelper(node, method!);
                    HasUnsupportedWrite = true;
                    return node;
                }

                return replacement.WithTriviaFrom(node);
            }

            if (symbol is not null && renames?.TryGetValue(symbol, out var renamed) == true)
            {
                return SyntaxFactory.IdentifierName(renamed).WithTriviaFrom(node);
            }

            if (symbol is ITypeParameterSymbol typeParameter)
            {
                return SubstituteTypeParameter(node, typeParameter);
            }

            if (TryFoldConstant(node, symbol, out var folded))
            {
                return folded;
            }

            if (ImportedName(node, symbol) is { } qualified)
            {
                return qualified.WithTriviaFrom(node);
            }

            // The name of a member access is reported with the member access itself.
            if (node.Parent is not MemberAccessExpressionSyntax memberAccess || memberAccess.Name != node)
            {
                ReportSourceMember(node, symbol);
            }

            return base.VisitIdentifierName(node);
        }

        // Whether an expression is a chain continuing a conditional access, e.g. .Email or .Headers["a"].Trim().
        private static bool StartsWithBinding(ExpressionSyntax expression)
        {
            while (true)
            {
                switch (expression)
                {
                    case MemberAccessExpressionSyntax memberAccess:
                        expression = memberAccess.Expression;
                        break;
                    case InvocationExpressionSyntax call:
                        expression = call.Expression;
                        break;
                    case ElementAccessExpressionSyntax elementAccess:
                        expression = elementAccess.Expression;
                        break;
                    default:
                        return expression is MemberBindingExpressionSyntax or ElementBindingExpressionSyntax;
                }
            }
        }

        // L::X with a namespace alias L is written out in full; global::X stays as written.
        public override SyntaxNode? VisitAliasQualifiedName(AliasQualifiedNameSyntax node) =>
            node.Alias.Identifier.ValueText != "global" &&
            model.GetAliasInfo(node.Alias) is { Target: INamespaceSymbol aliasedNamespace }
                ? SyntaxFactory.QualifiedName(
                        SyntaxFactory.ParseName(aliasedNamespace.ToDisplayString()),
                        (SimpleNameSyntax)Visit(node.Name)!)
                    .WithTriviaFrom(node)
                : base.VisitAliasQualifiedName(node);

        public override SyntaxNode? VisitSingleVariableDesignation(SingleVariableDesignationSyntax node) =>
            node.WithIdentifier(Renamed(node, node.Identifier));

        public override SyntaxNode? VisitParameter(ParameterSyntax node) =>
            ((ParameterSyntax)base.VisitParameter(node)!).WithIdentifier(Renamed(node, node.Identifier));

        public override SyntaxNode? VisitForEachStatement(ForEachStatementSyntax node) =>
            ((ForEachStatementSyntax)base.VisitForEachStatement(node)!).WithIdentifier(Renamed(node, node.Identifier));

        public override SyntaxNode? VisitCatchDeclaration(CatchDeclarationSyntax node) =>
            ((CatchDeclarationSyntax)base.VisitCatchDeclaration(node)!).WithIdentifier(Renamed(node, node.Identifier));

        public override SyntaxNode? VisitLocalFunctionStatement(LocalFunctionStatementSyntax node) =>
            ((LocalFunctionStatementSyntax)base.VisitLocalFunctionStatement(node)!)
            .WithIdentifier(Renamed(node, node.Identifier));

        public override SyntaxNode? VisitVariableDeclarator(VariableDeclaratorSyntax node) =>
            ((VariableDeclaratorSyntax)base.VisitVariableDeclarator(node)!).WithIdentifier(Renamed(node, node.Identifier));

        public override SyntaxNode? VisitFromClause(FromClauseSyntax node) =>
            ((FromClauseSyntax)base.VisitFromClause(node)!).WithIdentifier(Renamed(node, node.Identifier));

        public override SyntaxNode? VisitLetClause(LetClauseSyntax node) =>
            ((LetClauseSyntax)base.VisitLetClause(node)!).WithIdentifier(Renamed(node, node.Identifier));

        public override SyntaxNode? VisitJoinClause(JoinClauseSyntax node) =>
            ((JoinClauseSyntax)base.VisitJoinClause(node)!).WithIdentifier(Renamed(node, node.Identifier));

        public override SyntaxNode? VisitJoinIntoClause(JoinIntoClauseSyntax node) =>
            node.WithIdentifier(Renamed(node, node.Identifier));

        public override SyntaxNode? VisitQueryContinuation(QueryContinuationSyntax node) =>
            ((QueryContinuationSyntax)base.VisitQueryContinuation(node)!).WithIdentifier(Renamed(node, node.Identifier));

        private SyntaxToken Renamed(SyntaxNode declaration, SyntaxToken identifier) =>
            model.GetDeclaredSymbol(declaration) is { } symbol && renames?.TryGetValue(symbol, out var renamed) == true
                ? SyntaxFactory.Identifier(identifier.LeadingTrivia, renamed, identifier.TrailingTrivia)
                : identifier;

        public override SyntaxNode? VisitGenericName(GenericNameSyntax node)
        {
            if (node.Parent is not MemberAccessExpressionSyntax memberAccess || memberAccess.Name != node)
            {
                ReportSourceMember(node, model.GetSymbolInfo(node).Symbol);
            }

            return base.VisitGenericName(node);
        }

        public override SyntaxNode? VisitMemberAccessExpression(MemberAccessExpressionSyntax node)
        {
            // Section context parameters are bound to the API Management context, which is their ExpressionContext.
            if (node.Name.Identifier.ValueText == "ExpressionContext" &&
                model.GetTypeInfo(node.Expression).Type is { } receiverType &&
                IsAuthoringSectionContext(receiverType))
            {
                return SyntaxFactory.IdentifierName("context").WithTriviaFrom(node);
            }

            var symbol = model.GetSymbolInfo(node).Symbol;
            if (TryFoldConstant(node, symbol, out var folded))
            {
                return folded;
            }

            if (ReportSourceMember(node, symbol))
            {
                return node;
            }

            var rewritten = (MemberAccessExpressionSyntax?)base.VisitMemberAccessExpression(node);
            return rewritten?.Expression is PrefixUnaryExpressionSyntax or CastExpressionSyntax
                ? rewritten.WithExpression(SyntaxFactory.ParenthesizedExpression(rewritten.Expression))
                : rewritten;
        }

        // An explicit cast of a named value, directly or from a helper or argument, converts all of its text like
        // an implicit conversion: to string it's the string "{{x}}", to another type (int)({{x}}). A cast of the
        // parenthesized call, as the decompiler writes a token used as code, keeps the raw token.
        public override SyntaxNode? VisitCastExpression(CastExpressionSyntax node)
        {
            var visited = (CastExpressionSyntax)base.VisitCastExpression(node)!;
            var toString = visited.Type is PredefinedTypeSyntax predefined &&
                           predefined.Keyword.IsKind(SyntaxKind.StringKeyword) ||
                           model.GetTypeInfo(node.Type).Type?.SpecialType == SpecialType.System_String;
            if (visited.Expression is InvocationExpressionSyntax call &&
                NamedValueRewriter.IsNamedValueCall(call, out var name))
            {
                return toString
                    ? compiler.CreateNamedValuePlaceholder($"{{{{{name}}}}}", isString: true).WithTriviaFrom(node)
                    : visited.WithExpression(
                        SyntaxFactory.ParenthesizedExpression(SyntaxFactory.ParenthesizedExpression(call)));
            }

            // A [NamedValue] helper that doesn't return string gives a raw placeholder.
            if (visited.Expression is IdentifierNameSyntax placeholder &&
                compiler._namedValuePlaceholders.TryGetValue(placeholder.Identifier.ValueText, out var value) &&
                !compiler._stringNamedValuePlaceholders.Contains(placeholder.Identifier.ValueText))
            {
                return toString
                    ? compiler.CreateNamedValuePlaceholder(value, isString: true).WithTriviaFrom(node)
                    : visited.WithExpression(SyntaxFactory.ParenthesizedExpression(placeholder));
            }

            return visited;
        }

        // context?.NamedValue("x") is context.NamedValue("x"): API Management's context is never null.
        public override SyntaxNode? VisitConditionalAccessExpression(ConditionalAccessExpressionSyntax node)
        {
            if (node.WhenNotNull is InvocationExpressionSyntax { Expression: MemberBindingExpressionSyntax } binding &&
                TryGetNamedValueName(binding, out var name))
            {
                return NamedValueExpression(node, name);
            }

            // context?.NamedValue("x").Length and context?.NamedValue("x")?.ToString() continue from the named value,
            // also when the name is a helper argument, which is a literal once substituted.
            var visited = base.VisitConditionalAccessExpression(node);
            if (visited is ConditionalAccessExpressionSyntax
                {
                    Expression: IdentifierNameSyntax { Identifier.ValueText: "context" }
                } conditional &&
                LeadingBindingCall(conditional.WhenNotNull) is
                {
                    Expression: MemberBindingExpressionSyntax { Name.Identifier.ValueText: "NamedValue" },
                    ArgumentList.Arguments: [{ NameColon: null, Expression: LiteralExpressionSyntax literal }]
                } leading &&
                literal.IsKind(SyntaxKind.StringLiteralExpression) &&
                model.GetTypeInfo(node.Expression).Type is { } receiverType && IsExpressionContext(receiverType))
            {
                return leading == conditional.WhenNotNull
                    ? NamedValueExpression(node, literal.Token.ValueText)
                    : conditional.WhenNotNull
                        .ReplaceNode(
                            leading,
                            SyntaxFactory.ParseExpression(
                                $"context.NamedValue({SymbolDisplay.FormatLiteral(literal.Token.ValueText, true)})"))
                        .WithTriviaFrom(node);
            }

            return visited;

            static InvocationExpressionSyntax? LeadingBindingCall(ExpressionSyntax expression) => expression switch
            {
                InvocationExpressionSyntax { Expression: MemberBindingExpressionSyntax } call => call,
                InvocationExpressionSyntax call => LeadingBindingCall(call.Expression),
                MemberAccessExpressionSyntax access => LeadingBindingCall(access.Expression),
                ElementAccessExpressionSyntax element => LeadingBindingCall(element.Expression),
                ConditionalAccessExpressionSyntax conditional => LeadingBindingCall(conditional.Expression),
                _ => null
            };
        }

        // context.NamedValue("x") implicitly converted to string (e.g. returned from a string helper) is used as
        // text, so it becomes a string named value, unless it's parenthesized. Any other named value call is written
        // as context.NamedValue("x"), which the named value rewriter turns into {{x}}.
        private ExpressionSyntax NamedValueExpression(ExpressionSyntax node, string name) =>
            node.Parent is not ParenthesizedExpressionSyntax &&
            model.GetTypeInfo(node) is { Type.TypeKind: TypeKind.Dynamic, ConvertedType.SpecialType: SpecialType.System_String }
                ? compiler.CreateNamedValuePlaceholder($"{{{{{name}}}}}", isString: true).WithTriviaFrom(node)
                : SyntaxFactory.ParseExpression($"context.NamedValue({SymbolDisplay.FormatLiteral(name, true)})")
                    .WithTriviaFrom(node);

        private bool TryGetNamedValueName(InvocationExpressionSyntax node, out string name)
        {
            if (TryResolveMethod(node, model, out var namedValueCall) &&
                namedValueCall is { Name: nameof(IExpressionContext.NamedValue) } &&
                IsExpressionContext(namedValueCall.ContainingType) &&
                node.ArgumentList.Arguments is [{ Expression: var nameArgument }] &&
                model.GetConstantValue(nameArgument) is { HasValue: true, Value: string constant })
            {
                name = constant;
                return true;
            }

            name = string.Empty;
            return false;
        }

        public override SyntaxNode? VisitInvocationExpression(InvocationExpressionSyntax node)
        {
            // nameof is a compile-time string; its operand must not be substituted or folded.
            if (node.Expression is IdentifierNameSyntax { Identifier.ValueText: "nameof" } &&
                model.GetConstantValue(node) is { HasValue: true, Value: string name })
            {
                return SyntaxFactory.LiteralExpression(SyntaxKind.StringLiteralExpression, SyntaxFactory.Literal(name))
                    .WithTriviaFrom(node);
            }

            if (node.Expression is not MemberBindingExpressionSyntax && TryGetNamedValueName(node, out var namedValueName))
            {
                // Inside a conditional access (context?.Self().NamedValue("x")) the receiver can't be replaced.
                if (node.Expression is MemberAccessExpressionSyntax { Expression: var receiver } &&
                    StartsWithBinding(receiver) &&
                    TryResolveMethod(node, model, out var namedValueMethod))
                {
                    compiler.ReportUnsupportedHelper(node, namedValueMethod);
                    return node;
                }

                return NamedValueExpression(node, namedValueName);
            }

            if (node.Expression is MemberBindingExpressionSyntax &&
                TryResolveMethod(node, model, out var conditionalExtension) &&
                conditionalExtension.MethodKind == MethodKind.ReducedExtension &&
                IsUserSource(conditionalExtension))
            {
                return compiler.InlineConditionalExtension(
                    node, WithCallerTypeArguments(conditionalExtension), model, bindings, stack, method, renames) ?? node;
            }

            if (node.Expression is MemberAccessExpressionSyntax { Expression: var bindingReceiver } &&
                StartsWithBinding(bindingReceiver) &&
                TryResolveMethod(node, model, out var chainedExtension) &&
                chainedExtension.MethodKind == MethodKind.ReducedExtension &&
                IsUserSource(chainedExtension))
            {
                return compiler.InlineConditionalExtension(
                    node,
                    WithCallerTypeArguments(chainedExtension),
                    model,
                    bindings,
                    stack,
                    method,
                    renames,
                    (ExpressionSyntax)Visit(bindingReceiver)!) ?? node;
            }

            // A helper from a compiled [Expression] library has no source to inline.
            if (TryResolveMethod(node, model, out var library) && !IsUserSource(library) &&
                IsExpressionLibraryMember(library))
            {
                compiler.ReportUnsupportedHelper(node, library);
                return node;
            }

            // Local functions and delegate invocations stay in the emitted helper body.
            if (!TryResolveMethod(node, model, out var invoked) ||
                invoked.MethodKind is not (MethodKind.Ordinary or MethodKind.ReducedExtension) ||
                !IsUserSource(invoked))
            {
                return base.VisitInvocationExpression(node);
            }

            return compiler.InlineSourceInvocation(
                           node, WithCallerTypeArguments(invoked), model, bindings, stack, method, renames)
                       ?.WithTriviaFrom(node) ??
                   node;
        }

        public override SyntaxNode? VisitAnonymousObjectMemberDeclarator(AnonymousObjectMemberDeclaratorSyntax node)
        {
            var visited = (AnonymousObjectMemberDeclaratorSyntax)base.VisitAnonymousObjectMemberDeclarator(node)!;

            // A substituted projection such as new { value } loses the member name it was inferred from.
            return node.NameEquals is null && LostInferredName(node.Expression, visited.Expression) is { } name
                ? visited.WithNameEquals(SyntaxFactory.NameEquals(name))
                : visited;
        }

        public override SyntaxNode? VisitTupleExpression(TupleExpressionSyntax node)
        {
            var visited = (TupleExpressionSyntax)base.VisitTupleExpression(node)!;

            // Likewise a substituted tuple element such as (value, 1) loses its inferred element name.
            var arguments = visited.Arguments.Select((argument, index) =>
                node.Arguments[index].NameColon is null &&
                LostInferredName(node.Arguments[index].Expression, argument.Expression) is { } name
                    ? argument.WithNameColon(SyntaxFactory.NameColon(name))
                    : argument);
            return visited.WithArguments(SyntaxFactory.SeparatedList(arguments, visited.Arguments.GetSeparators()));
        }

        private static string? LostInferredName(ExpressionSyntax original, ExpressionSyntax rewritten) =>
            rewritten.ToString() == original.ToString()
                ? null
                : original switch
                {
                    IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
                    MemberAccessExpressionSyntax memberAccess => memberAccess.Name.Identifier.ValueText,
                    _ => null
                };

        // Handles source constants: emitted as literals, or reported when the value can't be emitted.
        private bool TryFoldConstant(ExpressionSyntax node, ISymbol? symbol, out SyntaxNode result)
        {
            result = node;
            if (symbol is not IFieldSymbol { IsConst: true, ContainingType.TypeKind: not TypeKind.Enum } field ||
                !IsUserSource(field) && !IsExpressionLibraryMember(field))
            {
                return false;
            }

            if (ConstantExpressionFactory.TryCreate(field.ConstantValue, field.Type, out var literal))
            {
                // A negative literal after a unary operator or cast would otherwise read as "--1" or a subtraction.
                result = literal is PrefixUnaryExpressionSyntax &&
                         node.Parent is PrefixUnaryExpressionSyntax or CastExpressionSyntax
                    ? SyntaxFactory.ParenthesizedExpression(literal).WithTriviaFrom(node)
                    : literal.WithTriviaFrom(node);
                return true;
            }

            compiler.ReportUnsupportedConstant(node, field);
            return true;
        }

        // Names imported by the helper file's using static or alias directives don't exist in API Management, so
        // they're written out in full.
        private ExpressionSyntax? ImportedName(IdentifierNameSyntax node, ISymbol? symbol)
        {
            // global::X and other alias-qualified names are left as written.
            if (node.Parent is MemberAccessExpressionSyntax memberAccess && memberAccess.Name == node ||
                node.Parent is AliasQualifiedNameSyntax)
            {
                return null;
            }

            if (model.GetAliasInfo(node) is not null && symbol is INamedTypeSymbol aliasedType &&
                compiler.IsEmittableType(aliasedType))
            {
                return SyntaxFactory.ParseTypeName(aliasedType.ToDisplayString(EmittedTypeFormat));
            }

            if (model.GetAliasInfo(node) is { Target: INamespaceSymbol aliasedNamespace })
            {
                return SyntaxFactory.ParseName(aliasedNamespace.ToDisplayString());
            }

            var enclosingType = model.GetEnclosingSymbol(node.SpanStart)?.ContainingType;
            return symbol is { IsStatic: true, ContainingType: { } containingType } &&
                   symbol is IMethodSymbol { MethodKind: MethodKind.Ordinary } or IFieldSymbol or IPropertySymbol &&
                   !IsUserSource(symbol) &&
                   compiler.IsEmittableType(containingType) &&
                   !InheritsFrom(enclosingType, containingType)
                ? SyntaxFactory.ParseExpression($"{containingType.ToDisplayString(EmittedTypeFormat)}.{symbol.Name}")
                : null;

            static bool InheritsFrom(INamedTypeSymbol? type, INamedTypeSymbol baseType)
            {
                for (var current = type; current is not null; current = current.BaseType)
                {
                    if (SymbolEqualityComparer.Default.Equals(current.OriginalDefinition, baseType.OriginalDefinition))
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        // A generic helper called with this helper's own type parameters is constructed with this helper's arguments.
        private IMethodSymbol WithCallerTypeArguments(IMethodSymbol invoked)
        {
            if (method is null || invoked.MethodKind != MethodKind.Ordinary || !invoked.IsGenericMethod ||
                !invoked.TypeArguments.Any(IsCallerTypeParameter))
            {
                return invoked;
            }

            return invoked.ConstructedFrom.Construct(invoked.TypeArguments
                .Select(argument => IsCallerTypeParameter(argument)
                    ? method.TypeArguments[((ITypeParameterSymbol)argument).Ordinal]
                    : argument)
                .ToArray());

            bool IsCallerTypeParameter(ITypeSymbol argument) =>
                argument is ITypeParameterSymbol typeParameter &&
                method.ReducedFrom is null &&
                SymbolEqualityComparer.Default.Equals(
                    typeParameter.DeclaringMethod?.OriginalDefinition,
                    method.OriginalDefinition);
        }

        // A type parameter of the inlined method becomes its type argument when that type exists in API Management.
        private SyntaxNode SubstituteTypeParameter(IdentifierNameSyntax node, ITypeParameterSymbol typeParameter)
        {
            var definition = method?.ReducedFrom ?? method;
            var typeArgument = definition is not null &&
                               SymbolEqualityComparer.Default.Equals(
                                   typeParameter.DeclaringMethod?.OriginalDefinition,
                                   definition.OriginalDefinition)
                ? method!.ReducedFrom is null
                    ? method.TypeArguments[typeParameter.Ordinal]
                    : method.GetTypeInferredDuringReduction(typeParameter) ??
                      method.TypeArguments.ElementAtOrDefault(
                          method.TypeParameters.IndexOf(
                              method.TypeParameters.FirstOrDefault(candidate => candidate.Name == typeParameter.Name)!))
                : null;
            if (typeArgument is INamedTypeSymbol { IsGenericType: false } authoringType && IsAuthoringType(authoringType))
            {
                // API Management exposes these runtime types by their simple names, e.g. (IResponse).
                return SyntaxFactory.IdentifierName(authoringType.Name).WithTriviaFrom(node);
            }

            if (typeArgument is not null && compiler.IsEmittableType(typeArgument))
            {
                return SyntaxFactory.ParseTypeName(typeArgument.ToDisplayString(EmittedTypeFormat)).WithTriviaFrom(node);
            }

            compiler.ReportUnsupportedSourceReference(node, typeParameter);
            return node;
        }

        // Source types, fields, properties and method groups don't exist in API Management; invoked methods are inlined.
        // Neither do those of a compiled [Expression] library.
        private bool ReportSourceMember(ExpressionSyntax node, ISymbol? symbol)
        {
            var isSourceMember = symbol switch
            {
                INamedTypeSymbol type => !type.IsAnonymousType && !type.IsTupleType,
                IFieldSymbol or IPropertySymbol => symbol.ContainingType is not { IsAnonymousType: true } and
                    not { IsTupleType: true },
                IMethodSymbol { MethodKind: MethodKind.Ordinary or MethodKind.ReducedExtension } =>
                    !IsInvokedExpression(node),
                _ => false
            };
            if (!isSourceMember || !IsUserSource(symbol!) && !IsExpressionLibraryMember(symbol!))
            {
                return false;
            }

            compiler.ReportUnsupportedSourceReference(node, symbol!);
            return true;
        }

        // A section context maps to the API Management context only through ExpressionContext (handled by
        // VisitMemberAccessExpression). Its own members, such as SetHeader, and bare uses don't exist at runtime;
        // unresolved member accesses are left as they are for existing code that relies on them.
        private bool IsSectionContextMemberUse(IdentifierNameSyntax node, IParameterSymbol parameter) =>
            IsAuthoringSectionContext(parameter.Type) &&
            (node.Parent is not MemberAccessExpressionSyntax memberAccess ||
             memberAccess.Expression != node ||
             model.GetSymbolInfo(memberAccess) is not { Symbol: null, CandidateSymbols.IsEmpty: true });

        private static bool IsInvokedExpression(ExpressionSyntax node) =>
            node.Parent is InvocationExpressionSyntax invocation && invocation.Expression == node ||
            node.Parent is MemberAccessExpressionSyntax memberAccess && memberAccess.Name == node &&
            memberAccess.Parent is InvocationExpressionSyntax memberInvocation && memberInvocation.Expression == memberAccess;

        private static bool IsWriteTarget(IdentifierNameSyntax node)
        {
            // An element of a deconstruction assignment such as (value, other) = ... is written too.
            SyntaxNode target = node;
            while (target.Parent is ArgumentSyntax { Parent: TupleExpressionSyntax tuple })
            {
                target = tuple;
            }

            if (target != node)
            {
                return target.Parent is AssignmentExpressionSyntax deconstruction && deconstruction.Left == target;
            }

            return node.Parent switch
            {
                AssignmentExpressionSyntax assignment when assignment.Left == node => true,
                PrefixUnaryExpressionSyntax prefix
                    when prefix.IsKind(SyntaxKind.PreIncrementExpression) ||
                         prefix.IsKind(SyntaxKind.PreDecrementExpression) => true,
                PostfixUnaryExpressionSyntax postfix
                    when postfix.IsKind(SyntaxKind.PostIncrementExpression) ||
                         postfix.IsKind(SyntaxKind.PostDecrementExpression) => true,
                ArgumentSyntax { RefKindKeyword.RawKind: not 0 } => true,
                _ => false
            };
        }
    }
}