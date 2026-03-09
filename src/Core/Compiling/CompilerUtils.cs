// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml.Linq;

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
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

        MethodDeclarationSyntax? expressionMethod = null;

        // Try semantic resolution first (works when all types are available)
        if (compilation.SyntaxTrees.Contains(syntax.SyntaxTree))
        {
            SemanticModel semanticModel = compilation.GetSemanticModel(syntax.SyntaxTree);
            var symbolInfo = semanticModel.GetSymbolInfo(syntax.Expression);
            var symbol = symbolInfo.Symbol ?? symbolInfo.CandidateSymbols.SingleOrDefault(s => s is IMethodSymbol);

            if (symbol is IMethodSymbol methodSymbol)
            {
                expressionMethod = methodSymbol.DeclaringSyntaxReferences
                    .Select(r => r.GetSyntax())
                    .OfType<MethodDeclarationSyntax>()
                    .FirstOrDefault();
            }
        }

        // Fall back to syntax-based lookup when semantic resolution fails
        // (e.g., expression methods reference types not in the compilation)
        if (expressionMethod is null)
        {
            var methodName = syntax.Expression switch
            {
                IdentifierNameSyntax id => id.Identifier.ValueText,
                MemberAccessExpressionSyntax ma => ma.Name.Identifier.ValueText,
                _ => null
            };

            if (methodName != null)
            {
                expressionMethod = context.SyntaxRoot
                    .DescendantNodes()
                    .OfType<MethodDeclarationSyntax>()
                    .FirstOrDefault(m => m.Identifier.ValueText == methodName);
            }
        }

        if (expressionMethod is null)
        {
            context.Report(Diagnostic.Create(
                CompilationErrors.InvalidExpression,
                syntax.GetLocation()
            ));
            return "";
        }

        // Check for [NamedValue("name")] attribute → emit {{name}} token
        var namedValueAttr = expressionMethod.AttributeLists
            .SelectMany(al => al.Attributes)
            .FirstOrDefault(a => a.Name.ToString() is "NamedValue" or "NamedValueAttribute");
        if (namedValueAttr?.ArgumentList?.Arguments.Count > 0)
        {
            var nameArg = namedValueAttr.ArgumentList.Arguments[0].Expression.ToString().Trim('"');
            return $"{{{{{nameArg}}}}}";
        }

        // Check for [NamedValueTemplate("{{token}}/path")] → emit template as literal attribute value
        var templateAttr = expressionMethod.AttributeLists
            .SelectMany(al => al.Attributes)
            .FirstOrDefault(a => a.Name.ToString() is "NamedValueTemplate" or "NamedValueTemplateAttribute");
        if (templateAttr?.ArgumentList?.Arguments.Count > 0)
        {
            var arg = templateAttr.ArgumentList.Arguments[0].Expression;
            if (arg is LiteralExpressionSyntax templateLiteral)
            {
                return templateLiteral.Token.ValueText;
            }
            return arg.ToString().Trim('"');
        }

        expressionMethod = Normalize(expressionMethod);

        if (expressionMethod.Body != null)
        {
            return ReplaceNamedValueCalls($"@{expressionMethod.Body.ToFullString().Trim()}");
        }
        else if (expressionMethod.ExpressionBody != null)
        {
            return ReplaceNamedValueCalls(
                $"@({expressionMethod.ExpressionBody.Expression.ToFullString().Trim()})");
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

    /// <summary>
    /// Adds an XML attribute from config parameters, but skips emission when the value
    /// matches the APIM default declared via <see cref="ApimDefaultValueAttribute"/> on the config property.
    /// </summary>
    public static bool AddAttributeSkipDefault<TConfig>(this XElement element,
        IReadOnlyDictionary<string, InitializerValue> parameters, string key, string attName)
    {
        if (parameters.TryGetValue(key, out var value))
        {
            if (!IsApimDefault<TConfig>(key, value.Value?.ToString()))
                element.Add(new XAttribute(attName, value.Value!));
            return true;
        }

        return false;
    }

    /// <summary>
    /// Adds an XML attribute only if the value does not match the APIM default
    /// declared via <see cref="ApimDefaultValueAttribute"/> on the specified config property.
    /// Used by method-based compilers where the value is known at compile time.
    /// </summary>
    public static void AddAttributeIfNotDefault<TConfig>(this XElement element,
        string attName, string value, string configPropertyName)
    {
        if (!IsApimDefault<TConfig>(configPropertyName, value))
            element.Add(new XAttribute(attName, value));
    }

    /// <summary>
    /// Checks whether the given value matches the APIM default for a config property.
    /// </summary>
    public static bool IsApimDefault<TConfig>(string propertyName, string? value)
    {
        var attr = typeof(TConfig).GetProperty(propertyName)?
            .GetCustomAttribute<ApimDefaultValueAttribute>();
        return attr != null && string.Equals(value, attr.Value, StringComparison.OrdinalIgnoreCase);
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
        return unformatted.NormalizeWhitespace("", "\n");
    }

    private static readonly Regex NamedValueCallPattern = new(
        @"context\s*(?:\.\s*ExpressionContext\s*)?\.\s*NamedValue\s*\(\s*""([^""]*)""\s*\)",
        RegexOptions.Compiled);

    /// <summary>
    /// Replaces context.NamedValue("name") calls in expression code with {{name}} tokens.
    /// Then cleans up concatenation artifacts from the decompiler's string-breaking approach.
    /// </summary>
    internal static string ReplaceNamedValueCalls(string expressionCode)
    {
        var result = NamedValueCallPattern.Replace(expressionCode, m => $"{{{{{m.Groups[1].Value}}}}}");
        // Clean up decompiler artifacts: "prefix" + {{token}} + "suffix" → "prefix{{token}}suffix"
        // Matches the closing " of one string, the + {{token}} +, and the opening " of the next string
        // Handles all string types: regular "", interpolated $"", verbatim @"", interpolated verbatim $@""/@$""
        while (Regex.IsMatch(result, @"""\s*\+\s*\{\{[^}]+\}\}\s*\+\s*(?:\$@?|@\$?)?"""))
        {
            result = Regex.Replace(result, @"""\s*\+\s*(\{\{[^}]+\}\})\s*\+\s*(?:\$@?|@\$?)?""", "$1");
        }
        return result;
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