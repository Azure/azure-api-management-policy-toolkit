// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Xml.Linq;

using Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling.Diagnostics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling;

public static class CompilerUtils
{
    public static string ProcessParameter(this ExpressionSyntax expression, IDocumentCompilationContext context)
    {
        switch (expression)
        {
            case LiteralExpressionSyntax syntax:
                return syntax.Token.ValueText;
            case InvocationExpressionSyntax syntax:
                return FindCode(syntax, context);
            case MemberAccessExpressionSyntax syntax:
                return FindCode(syntax, context);
            // case InterpolatedStringExpressionSyntax syntax:
            //     var interpolationParts = syntax.Contents.Select(c => c switch
            //     {
            //         InterpolatedStringTextSyntax text => text.TextToken.ValueText,
            //         InterpolationSyntax interpolation =>
            //             $"{{context.Variables[\"{interpolation.Expression.ToString()}\"]}}",
            //         _ => ""
            //     });
            //     var interpolationExpression = CSharpSyntaxTree
            //         .ParseText($"context => $\"{string.Join("", interpolationParts)}\"").GetRoot();
            //     var lambda = interpolationExpression.DescendantNodesAndSelf().OfType<LambdaExpressionSyntax>()
            //         .FirstOrDefault();
            //     lambda = Normalize(lambda!);
            //     return $"@({lambda.ExpressionBody})";
            default:
                context.Report(Diagnostic.Create(
                    CompilationErrors.NotSupportedParameter,
                    expression.GetLocation()
                ));
                return "";
        }
    }

    public static string FindCode(this InvocationExpressionSyntax syntax, IDocumentCompilationContext context)
    {
        Compilation compilation = context.Compilation;
        SemanticModel semanticModel = compilation.GetSemanticModel(syntax.SyntaxTree);
        var symbolInfo = semanticModel.GetSymbolInfo(syntax.Expression);
        var symbol = symbolInfo.Symbol ?? symbolInfo.CandidateSymbols.SingleOrDefault(s => s is IMethodSymbol);

        if (symbol is not IMethodSymbol methodSymbol)
        {
            context.Report(Diagnostic.Create(
                CompilationErrors.InvalidExpression,
                syntax.GetLocation()
            ));
            return "";
        }

        var expressionMethod = methodSymbol.DeclaringSyntaxReferences
            .Select(r => r.GetSyntax())
            .OfType<MethodDeclarationSyntax>()
            .FirstOrDefault();

        if (expressionMethod is null)
        {
            context.Report(Diagnostic.Create(
                CompilationErrors.CannotFindMethodCode,
                syntax.GetLocation(),
                methodSymbol.Name
            ));
            return "";
        }

        expressionMethod = Normalize(expressionMethod);

        if (expressionMethod.Body != null)
        {
            return $"@{expressionMethod.Body.ToFullString().TrimEnd()}";
        }
        else if (expressionMethod.ExpressionBody != null)
        {
            return $"@({expressionMethod.ExpressionBody.Expression.ToFullString().TrimEnd()})";
        }
        else
        {
            throw new InvalidOperationException("Invalid expression");
        }
    }

    public static string FindCode(this MemberAccessExpressionSyntax syntax, IDocumentCompilationContext context)
    {
        Compilation compilation = context.Compilation;
        SemanticModel semanticModel = compilation.GetSemanticModel(syntax.SyntaxTree);
        var symbolInfo = semanticModel.GetSymbolInfo(syntax);
        var symbol = symbolInfo.Symbol ?? symbolInfo.CandidateSymbols.SingleOrDefault(s => s is IFieldSymbol);

        if (symbol is not IFieldSymbol fieldSymbol)
        {
            context.Report(Diagnostic.Create(
                CompilationErrors.InvalidConstantReference,
                syntax.GetLocation()
            ));
            return "";
        }

        if (!fieldSymbol.IsConst)
        {
            context.Report(Diagnostic.Create(
                CompilationErrors.InvalidExpression,
                syntax.GetLocation()
            ));
            return "";
        }

        var value = fieldSymbol.ConstantValue?.ToString();
        if (value is null)
        {
            context.Report(Diagnostic.Create(
                CompilationErrors.IsNotAConstant,
                syntax.GetLocation(),
                fieldSymbol.Name
            ));
            value = "";
        }

        return value;
    }

    public static InitializerValue Process(
        this ObjectCreationExpressionSyntax creationSyntax,
        IDocumentCompilationContext context)
    {
        var result = new Dictionary<string, InitializerValue>();
        if (creationSyntax.Initializer is null)
        {
            context.Report(Diagnostic.Create(
                CompilationErrors.PolicyObjectCreationDoesNotContainInitializerSection,
                creationSyntax.GetLocation()
            ));
        }

        foreach (var expression in creationSyntax.Initializer?.Expressions ?? [])
        {
            if (expression is not AssignmentExpressionSyntax assignment)
            {
                context.Report(Diagnostic.Create(
                    CompilationErrors.ObjectInitializerContainsNotAnAssigmentExpression,
                    expression.GetLocation()
                ));
                continue;
            }

            var name = assignment.Left.ToString();
            result[name] = assignment.Right.ProcessExpression(context);
        }

        return new InitializerValue
        {
            Type = (creationSyntax.Type as IdentifierNameSyntax)?.Identifier.ValueText,
            NamedValues = result,
            Node = creationSyntax,
        };
    }

    public static InitializerValue Process(
        this ArrayCreationExpressionSyntax creationSyntax,
        IDocumentCompilationContext context)
    {
        var expressions = creationSyntax.Initializer?.Expressions ?? [];
        var result = expressions
            .Select(expression => expression.ProcessExpression(context))
            .ToList();

        return new InitializerValue
        {
            Type = (creationSyntax.Type.ElementType as IdentifierNameSyntax)?.Identifier.ValueText,
            UnnamedValues = result,
            Node = creationSyntax,
        };
    }

    public static InitializerValue Process(
        this CollectionExpressionSyntax collectionSyntax,
        IDocumentCompilationContext context)
    {
        var result = collectionSyntax.Elements
            .OfType<ExpressionElementSyntax>()
            .Select(e => e.Expression)
            .Select(expression => expression.ProcessExpression(context)).ToList();

        return new InitializerValue { UnnamedValues = result, Node = collectionSyntax };
    }

    public static InitializerValue Process(
        this ImplicitArrayCreationExpressionSyntax creationSyntax,
        IDocumentCompilationContext context)
    {
        var result = creationSyntax.Initializer.Expressions
            .Select(expression => expression.ProcessExpression(context))
            .ToList();

        return new InitializerValue { UnnamedValues = result, Node = creationSyntax };
    }

    public static InitializerValue ProcessExpression(
        this ExpressionSyntax expression,
        IDocumentCompilationContext context)
    {
        return expression switch
        {
            ObjectCreationExpressionSyntax config => config.Process(context),
            ArrayCreationExpressionSyntax array => array.Process(context),
            ImplicitArrayCreationExpressionSyntax array => array.Process(context),
            CollectionExpressionSyntax collection => collection.Process(context),
            _ => new InitializerValue { Value = expression.ProcessParameter(context), Node = expression }
        };
    }

    public static bool AddAttribute(this XElement element, IReadOnlyDictionary<string, InitializerValue> parameters,
        string key, string attName)
    {
        if (parameters.TryGetValue(key, out var value))
        {
            element.Add(new XAttribute(attName, value.Value!));
            return true;
        }

        return false;
    }

    public static bool TryExtractingConfigParameter<T>(
        this InvocationExpressionSyntax node,
        IDocumentCompilationContext context,
        string policy,
        [NotNullWhen(true)] out IReadOnlyDictionary<string, InitializerValue>? values)
    {
        values = null;

        if (node.ArgumentList.Arguments.Count != 1)
        {
            context.Report(Diagnostic.Create(
                CompilationErrors.ArgumentCountMissMatchForPolicy,
                node.ArgumentList.GetLocation(),
                policy));
            return false;
        }

        return node.ArgumentList.Arguments[0].Expression.TryExtractingConfig<T>(context, policy, out values);
    }

    public static bool TryExtractingConfig<T>(this ExpressionSyntax syntax,
        IDocumentCompilationContext context,
        string policy,
        [NotNullWhen(true)] out IReadOnlyDictionary<string, InitializerValue>? values)
    {
        values = null;
        if (syntax is not ObjectCreationExpressionSyntax config)
        {
            context.Report(Diagnostic.Create(
                CompilationErrors.PolicyArgumentIsNotAnObjectCreation,
                syntax.GetLocation(),
                policy,
                typeof(T).Name
            ));
            return false;
        }

        var initializer = config.Process(context);
        if (!initializer.TryGetValues<T>(out var result))
        {
            context.Report(Diagnostic.Create(
                CompilationErrors.PolicyArgumentIsNotOfRequiredType,
                syntax.GetLocation(),
                policy,
                typeof(T).Name
            ));
            return false;
        }

        values = result;
        return true;
    }

    public static T Normalize<T>(T node) where T : SyntaxNode
    {
        var unformatted = (T)new TriviaRemoverRewriter().Visit(node);
        return unformatted.NormalizeWhitespace("", "");
    }
}

public class InitializerValue
{
    public string? Name { get; init; }
    public string? Value { get; init; }
    public string? Type { get; init; }
    public IReadOnlyCollection<InitializerValue>? UnnamedValues { get; init; }
    public IReadOnlyDictionary<string, InitializerValue>? NamedValues { get; init; }

    public required SyntaxNode Node { get; init; }

    public bool TryGetValues<T>([NotNullWhen(true)] out IReadOnlyDictionary<string, InitializerValue>? namedValues)
    {
        if (Type == typeof(T).Name && NamedValues is not null)
        {
            namedValues = NamedValues;
            return true;
        }

        namedValues = null;
        return false;
    }
}