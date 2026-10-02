// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling;

internal sealed partial class PolicyExpressionCompiler
{
    private IReadOnlyDictionary<IParameterSymbol, ExpressionSyntax>? CreateBindings(
        InvocationExpressionSyntax invocation,
        IMethodSymbol method,
        SemanticModel callerModel,
        IReadOnlyDictionary<IParameterSymbol, ExpressionSyntax> callerBindings,
        ImmutableArray<IMethodSymbol> stack,
        IMethodSymbol? callerMethod,
        IReadOnlyDictionary<ISymbol, string>? callerRenames,
        bool conditionalReceiver = false)
    {
        var operation = callerModel.GetOperation(invocation) as IInvocationOperation;
        if (!TryGetMethodDeclaration(method, out var declaration))
        {
            ReportUnsupportedHelper(invocation, method);
            return null;
        }

        var declarationModel = ModelFor(declaration.SyntaxTree)!;
        if (declarationModel.GetDeclaredSymbol(declaration) is not IMethodSymbol declarationMethod)
        {
            ReportUnsupportedHelper(invocation, method);
            return null;
        }

        var result = ImmutableDictionary.CreateBuilder<IParameterSymbol, ExpressionSyntax>(
            SymbolEqualityComparer.Default);
        // At most one argument may be more than a simple value, so evaluation order can't change.
        var complexArguments = 0;
        var declarationOffset = method.MethodKind == MethodKind.ReducedExtension ? 1 : 0;
        if (declarationOffset == 1 && conditionalReceiver)
        {
            // The receiver of recv?.Helper() stays in the conditional access; the body refers to it by a sentinel.
            result[declarationMethod.Parameters[0]] = SyntaxFactory.IdentifierName(ConditionalReceiver);
        }
        else if (declarationOffset == 1)
        {
            if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess ||
                !TryBindArgument(
                    declarationMethod.Parameters[0],
                    method.ReceiverType!,
                    memberAccess.Expression,
                    callerModel,
                    callerBindings,
                    stack,
                    callerMethod,
                    callerRenames,
                    IsEvaluatedOnce(declaration, declarationMethod.Parameters[0]),
                    result,
                    ref complexArguments))
            {
                ReportUnsupportedHelper(invocation, method);
                return null;
            }
        }

        foreach (var parameter in method.Parameters)
        {
            var declarationParameter = declarationMethod.Parameters[parameter.Ordinal + declarationOffset];
            // params collections (C# 13) other than arrays have no C# 7 equivalent.
            if (declarationParameter.RefKind is not (RefKind.None or RefKind.In) ||
                declarationParameter.IsParams && declarationParameter.Type is not IArrayTypeSymbol)
            {
                ReportUnsupportedHelper(invocation, method);
                return null;
            }

            // The operation's target is the unreduced method, whose parameter ordinals match the declaration's.
            var argument = operation?.Arguments.FirstOrDefault(candidate =>
                candidate.Parameter?.Ordinal == declarationParameter.Ordinal);

            // params arguments in expanded form become an array, which is evaluated once like any complex argument.
            // A dynamically bound call, e.g. with a named value argument, has no operation, so its expanded form is
            // taken from the syntax: anything other than a single array argument.
            var dynamicElements = operation is null && declarationParameter.IsParams
                ? invocation.ArgumentList.Arguments.Where(candidate => candidate.NameColon is null)
                    .Skip(parameter.Ordinal).Select(candidate => candidate.Expression).ToArray()
                : null;
            IReadOnlyList<ExpressionSyntax?>? paramsElements =
                argument is { ArgumentKind: ArgumentKind.ParamArray, Value: IArrayCreationOperation array }
                    ? (array.Initializer?.ElementValues ?? []).Select(element => element.Syntax as ExpressionSyntax)
                    .ToArray()
                    : dynamicElements is not null &&
                      !(dynamicElements is [var single] &&
                        callerModel.GetTypeInfo(single).Type is IArrayTypeSymbol or null)
                        ? dynamicElements
                        : null;
            if (paramsElements is not null)
            {
                if (!TryBindParamsArray(
                        declaration,
                        declarationParameter,
                        parameter.Type,
                        paramsElements,
                        callerModel,
                        callerBindings,
                        stack,
                        callerMethod,
                        callerRenames,
                        result,
                        ref complexArguments))
                {
                    ReportUnsupportedHelper(invocation, method);
                    return null;
                }

                continue;
            }

            if (declarationParameter.IsParams && argument is null && dynamicElements is null)
            {
                ReportUnsupportedHelper(invocation, method);
                return null;
            }
            var argumentExpression = argument is { IsImplicit: false, Value.Syntax: ExpressionSyntax operationExpression }
                ? operationExpression
                : FindArgumentExpression(invocation, parameter);
            if (argumentExpression is null)
            {
                if (!declarationParameter.HasExplicitDefaultValue ||
                    !TryCreateDefaultValue(declarationParameter, parameter.Type, out var defaultValue))
                {
                    ReportUnsupportedHelper(invocation, method);
                    return null;
                }

                // null and nullable defaults need the parameter type, e.g. int? x = null emits ((int?)null).
                result[declarationParameter] =
                    (defaultValue.IsKind(SyntaxKind.NullLiteralExpression) ||
                     parameter.Type.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T) &&
                    !IsAuthoringType(parameter.Type) && IsEmittableType(parameter.Type)
                        ? CastTo(parameter.Type, defaultValue)
                        : ParenthesizeIfNeeded(defaultValue);
                continue;
            }

            if (!TryBindArgument(
                    declarationParameter,
                    parameter.Type,
                    argumentExpression,
                    callerModel,
                    callerBindings,
                    stack,
                    callerMethod,
                    callerRenames,
                    IsEvaluatedOnce(declaration, declarationParameter),
                    result,
                    ref complexArguments))
            {
                ReportUnsupportedHelper(invocation, method);
                return null;
            }
        }

        return result.ToImmutable();
    }

    private bool TryBindParamsArray(
        MethodDeclarationSyntax declaration,
        IParameterSymbol parameter,
        ITypeSymbol parameterType,
        IReadOnlyList<ExpressionSyntax?> elementExpressions,
        SemanticModel callerModel,
        IReadOnlyDictionary<IParameterSymbol, ExpressionSyntax> callerBindings,
        ImmutableArray<IMethodSymbol> stack,
        IMethodSymbol? callerMethod,
        IReadOnlyDictionary<ISymbol, string>? callerRenames,
        ImmutableDictionary<IParameterSymbol, ExpressionSyntax>.Builder result,
        ref int complexArguments)
    {
        if (parameterType is not IArrayTypeSymbol arrayType || !IsEmittableType(arrayType) ||
            !IsEvaluatedOnce(declaration, parameter) || ++complexArguments > 1)
        {
            return false;
        }

        var elements = new List<ExpressionSyntax>();
        foreach (var elementSyntax in elementExpressions)
        {
            var countedBefore = _expandedNodeCount;
            if (elementSyntax is null ||
                CompileExpression(elementSyntax, callerModel, callerBindings, stack, callerMethod, callerRenames)
                    is not { } lowered ||
                ExceedsExpansionBudget(lowered, countedBefore, elementSyntax, parameter.ContainingSymbol))
            {
                return false;
            }

            // A named value element is converted to the element type like any named value.
            elements.Add(ConvertRawNamedValue(arrayType.ElementType, lowered) ?? lowered);
        }

        result[parameter] = SyntaxFactory.ArrayCreationExpression(
            (ArrayTypeSyntax)SyntaxFactory.ParseTypeName(arrayType.ToDisplayString(EmittedTypeFormat)),
            SyntaxFactory.InitializerExpression(
                SyntaxKind.ArrayInitializerExpression,
                SyntaxFactory.SeparatedList(elements)));
        return true;
    }

    private bool TryBindArgument(
        IParameterSymbol parameter,
        ITypeSymbol parameterType,
        ExpressionSyntax argumentExpression,
        SemanticModel callerModel,
        IReadOnlyDictionary<IParameterSymbol, ExpressionSyntax> callerBindings,
        ImmutableArray<IMethodSymbol> stack,
        IMethodSymbol? callerMethod,
        IReadOnlyDictionary<ISymbol, string>? callerRenames,
        bool evaluatedOnce,
        ImmutableDictionary<IParameterSymbol, ExpressionSyntax>.Builder result,
        ref int complexArguments)
    {
        if (IsContextParameter(parameter.Type) &&
            IsCanonicalExpressionContext(argumentExpression, callerModel, callerBindings))
        {
            result[parameter] = SyntaxFactory.IdentifierName("context");
            return true;
        }

        if (IsContextParameter(parameter.Type))
        {
            return false;
        }

        var countedBefore = _expandedNodeCount;
        var loweredArgument = CompileExpression(
            argumentExpression, callerModel, callerBindings, stack, callerMethod, callerRenames);
        if (loweredArgument is null ||
            ExceedsExpansionBudget(loweredArgument, countedBefore, argumentExpression, parameter.ContainingSymbol))
        {
            return false;
        }

        // A named value converted to string is the constant "{{x}}".
        if (parameterType.SpecialType == SpecialType.System_String &&
            ConvertRawNamedValue(parameterType, loweredArgument) is { } namedValueString)
        {
            result[parameter] = namedValueString;
            return true;
        }

        // A simple value may be copied or dropped; anything else must be evaluated exactly once, as in C#.
        if (!IsSafeSubstitutionArgument(loweredArgument) && (!evaluatedOnce || ++complexArguments > 1))
        {
            return false;
        }

        // Keep the argument's implicit conversion to the parameter type, which substitution would otherwise drop.
        // An unresolved argument type gives no conversion to preserve, and conversions to authoring types are
        // reference conversions between API Management runtime types.
        var argumentType = callerModel.GetTypeInfo(argumentExpression).Type;
        if (argumentType is IErrorTypeSymbol || IsAuthoringType(parameterType) || IsSameType(argumentType, parameterType))
        {
            result[parameter] = ParenthesizeIfNeeded(loweredArgument);
            return true;
        }

        if (!IsEmittableType(parameterType))
        {
            return false;
        }

        result[parameter] = ConvertRawNamedValue(parameterType, loweredArgument) ?? CastTo(parameterType, loweredArgument);
        return true;
    }

    // The parameter is used exactly once in the helper's returned expression, outside any branch, lambda or nameof.
    private bool IsEvaluatedOnce(MethodDeclarationSyntax declaration, IParameterSymbol parameter)
    {
        if (ReturnExpression(declaration) is not { } body)
        {
            return false;
        }

        var model = ModelFor(declaration.SyntaxTree)!;
        var uses = body.DescendantNodesAndSelf()
            .OfType<IdentifierNameSyntax>()
            .Where(identifier => SymbolEqualityComparer.Default.Equals(model.GetSymbolInfo(identifier).Symbol, parameter))
            .ToArray();
        // C# evaluates the argument before the helper runs, so no call in the helper may come before the use.
        return uses is [var use] &&
               !IsConditionallyEvaluated(use, body) &&
               !body.DescendantNodes().OfType<InvocationExpressionSyntax>().Any(call => call.Span.End <= use.SpanStart);
    }

    private static bool IsConditionallyEvaluated(SyntaxNode node, SyntaxNode root)
    {
        for (var child = node; child != root && child.Parent is { } parent; child = parent)
        {
            var conditional = parent switch
            {
                ConditionalExpressionSyntax expression => child != expression.Condition,
                BinaryExpressionSyntax binary => child == binary.Right &&
                                                 (binary.IsKind(SyntaxKind.LogicalAndExpression) ||
                                                  binary.IsKind(SyntaxKind.LogicalOrExpression) ||
                                                  binary.IsKind(SyntaxKind.CoalesceExpression)),
                ConditionalAccessExpressionSyntax access => child == access.WhenNotNull,
                AnonymousFunctionExpressionSyntax or QueryBodySyntax => true,
                InvocationExpressionSyntax { Expression: IdentifierNameSyntax { Identifier.ValueText: "nameof" } } => true,
                _ => false
            };
            if (conditional)
            {
                return true;
            }
        }

        return false;
    }

    private bool TryCreateDefaultValue(IParameterSymbol declarationParameter, ITypeSymbol type, out ExpressionSyntax value)
    {
        // A struct parameter defaulting to default has no constant value; emit default(T).
        if (declarationParameter.ExplicitDefaultValue is null &&
            type is { IsValueType: true, OriginalDefinition.SpecialType: not SpecialType.System_Nullable_T } &&
            IsEmittableType(type))
        {
            value = SyntaxFactory.ParseExpression($"default({type.ToDisplayString(EmittedTypeFormat)})");
            return true;
        }

        return ConstantExpressionFactory.TryCreate(declarationParameter.ExplicitDefaultValue, declarationParameter.Type, out value);
    }

    private static bool IsSafeSubstitutionArgument(ExpressionSyntax expression) =>
        expression switch
        {
            LiteralExpressionSyntax => true,
            IdentifierNameSyntax => true,
            MemberAccessExpressionSyntax member => IsSafeSubstitutionArgument(member.Expression),
            ElementAccessExpressionSyntax element =>
                IsSafeSubstitutionArgument(element.Expression) &&
                element.ArgumentList.Arguments.All(argument => IsSafeSubstitutionArgument(argument.Expression)),
            ParenthesizedExpressionSyntax parenthesized => IsSafeSubstitutionArgument(parenthesized.Expression),
            CastExpressionSyntax cast => IsSafeSubstitutionArgument(cast.Expression),
            PrefixUnaryExpressionSyntax prefix =>
                !prefix.IsKind(SyntaxKind.PreIncrementExpression) &&
                !prefix.IsKind(SyntaxKind.PreDecrementExpression) &&
                IsSafeSubstitutionArgument(prefix.Operand),
            // The right of as and is is a type. Division and modulo can throw, so copying or dropping them changes
            // what happens.
            BinaryExpressionSyntax binary when binary.IsKind(SyntaxKind.AsExpression) ||
                                               binary.IsKind(SyntaxKind.IsExpression) =>
                IsSafeSubstitutionArgument(binary.Left),
            BinaryExpressionSyntax binary =>
                !binary.IsKind(SyntaxKind.DivideExpression) &&
                !binary.IsKind(SyntaxKind.ModuloExpression) &&
                IsSafeSubstitutionArgument(binary.Left) && IsSafeSubstitutionArgument(binary.Right),
            ConditionalExpressionSyntax conditional =>
                IsSafeSubstitutionArgument(conditional.Condition) &&
                IsSafeSubstitutionArgument(conditional.WhenTrue) &&
                IsSafeSubstitutionArgument(conditional.WhenFalse),
            _ => false
        };

    private static ExpressionSyntax? FindArgumentExpression(
        InvocationExpressionSyntax invocation,
        IParameterSymbol parameter)
    {
        var namedArgument = invocation.ArgumentList.Arguments.FirstOrDefault(argument =>
            argument.NameColon?.Name.Identifier.ValueText == parameter.Name);
        if (namedArgument is not null)
        {
            return namedArgument.Expression;
        }

        var positionalArguments = invocation.ArgumentList.Arguments
            .Where(argument => argument.NameColon is null)
            .ToArray();
        return parameter.Ordinal < positionalArguments.Length
            ? positionalArguments[parameter.Ordinal].Expression
            : null;
    }

    private bool IsCanonicalExpressionContext(
        SyntaxNode argument,
        SemanticModel model,
        IReadOnlyDictionary<IParameterSymbol, ExpressionSyntax> bindings)
    {
        if (argument is MemberAccessExpressionSyntax memberAccess &&
            memberAccess.Name.Identifier.ValueText is "ExpressionContext" or "ExecutionContext")
        {
            if (model.GetTypeInfo(memberAccess.Expression).Type is not { } receiverType ||
                !IsAuthoringSectionContext(receiverType))
            {
                return false;
            }

            var memberSymbol = model.GetSymbolInfo(memberAccess).Symbol;
            if (memberSymbol is IPropertySymbol property && IsExpressionContext(property.Type))
            {
                return true;
            }

            return memberSymbol is null &&
                   memberAccess.Name.Identifier.ValueText == "ExecutionContext";
        }

        if (argument is not IdentifierNameSyntax identifier ||
            model.GetSymbolInfo(identifier).Symbol is not IParameterSymbol parameter)
        {
            return false;
        }

        if (bindings.TryGetValue(parameter, out var replacement))
        {
            return replacement is IdentifierNameSyntax { Identifier.ValueText: "context" };
        }

        return IsExpressionContext(parameter.Type) || IsAuthoringSectionContext(parameter.Type);
    }
}