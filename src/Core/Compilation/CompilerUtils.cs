// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Xml.Linq;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Azure.ApiManagement.PolicyToolkit.Compilation;

public static class CompilerUtils
{
    public static string ProcessParameter(this ExpressionSyntax expression, ICompilationContext context)
    {
        switch (expression)
        {
            case LiteralExpressionSyntax syntax:
                return syntax.Token.ValueText;
            case InterpolatedStringExpressionSyntax syntax:
                var interpolationParts = syntax.Contents.Select(c => c switch
                {
                    InterpolatedStringTextSyntax text => text.TextToken.ValueText,
                    InterpolationSyntax interpolation =>
                        $"{{context.Variables[\"{interpolation.Expression.ToString()}\"]}}",
                    _ => ""
                });
                var interpolationExpression = CSharpSyntaxTree.ParseText($"context => $\"{string.Join("", interpolationParts)}\"").GetRoot();
                var lambda = interpolationExpression.DescendantNodesAndSelf().OfType<LambdaExpressionSyntax>().FirstOrDefault();
                lambda = Normalize(lambda!);
                return $"@({lambda.ExpressionBody})";
            case InvocationExpressionSyntax syntax:
                return FindCode(syntax, context);
            default:
                return "";
        }
    }

    public static string FindCode(this InvocationExpressionSyntax syntax, ICompilationContext context)
    {
        if (syntax.Expression is not IdentifierNameSyntax identifierSyntax)
        {
            context.ReportError(
                $"Invalid expression. It should be IdentifierNameSyntax but was {syntax.Expression.GetType()}. {syntax.GetLocation()}");
            return "";
        }

        var methodIdentifier = identifierSyntax.Identifier.ValueText;
        var expressionMethod = context.SyntaxRoot.DescendantNodes()
            .OfType<MethodDeclarationSyntax>()
            .First(m => m.Identifier.ValueText == methodIdentifier);

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

    public static InitializerValue Process(
        this ObjectCreationExpressionSyntax creationSyntax,
        ICompilationContext context)
    {
        var result = new Dictionary<string, InitializerValue>();
        foreach (var expression in creationSyntax.Initializer?.Expressions ?? [])
        {
            if (expression is not AssignmentExpressionSyntax assignment)
            {
                context.ReportError($"Is not AssignmentExpressionSyntax. {expression.GetLocation()}");
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
        ICompilationContext context)
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
        ICompilationContext context)
    {
        var result = collectionSyntax.Elements
            .OfType<ExpressionElementSyntax>()
            .Select(e => e.Expression)
            .Select(expression => expression.ProcessExpression(context)).ToList();

        return new InitializerValue { UnnamedValues = result, Node = collectionSyntax };
    }

    public static InitializerValue Process(
        this ImplicitArrayCreationExpressionSyntax creationSyntax,
        ICompilationContext context)
    {
        var result = creationSyntax.Initializer.Expressions
            .Select(expression => expression.ProcessExpression(context))
            .ToList();

        return new InitializerValue { UnnamedValues = result, Node = creationSyntax };
    }

    public static InitializerValue ProcessExpression(
        this ExpressionSyntax expression,
        ICompilationContext context)
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
        ICompilationContext context,
        string policy,
        [NotNullWhen(true)] out IReadOnlyDictionary<string, InitializerValue>? values)
    {
        values = null;

        if (node.ArgumentList.Arguments.Count != 1)
        {
            context.ReportError($"Wrong argument count for {policy} policy. {node.GetLocation()}");
            return false;
        }

        return node.ArgumentList.Arguments[0].Expression.TryExtractingConfig<T>(context, policy, out values);
    }

    public static bool TryExtractingConfig<T>(this ExpressionSyntax syntax,
        ICompilationContext context,
        string policy,
        [NotNullWhen(true)] out IReadOnlyDictionary<string, InitializerValue>? values)
    {
        values = null;
        if (syntax is not ObjectCreationExpressionSyntax config)
        {
            context.ReportError(
                $"{policy} policy argument must be an object creation expression. {syntax.GetLocation()}");
            return false;
        }

        var initializer = config.Process(context);
        if (!initializer.TryGetValues<T>(out var result))
        {
            context.ReportError($"{policy} policy argument must be of type {typeof(T).Name}. {syntax.GetLocation()}");
            return false;
        }

        values = result;
        return true;
    }

    public static string ExtractDocumentFileName(this ClassDeclarationSyntax document)
    {
        var attributeSyntax = document.AttributeLists.GetFirstAttributeOfType("Document");
        var attributeArgumentExpression =
            attributeSyntax?.ArgumentList?.Arguments.FirstOrDefault()?.Expression as LiteralExpressionSyntax;
        return attributeArgumentExpression?.Token.ValueText ?? document.Identifier.ValueText;
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