// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Xml.Linq;

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling.Diagnostics;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Serialization;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling;

public static class CompilerUtils
{
    public static string ProcessParameter(this ExpressionSyntax expression, IDocumentCompilationContext context)
    {
        // null has no policy representation; it used to be written out as the text "null"
        if (IsNull(expression, context))
        {
            context.Report(Diagnostic.Create(
                CompilationErrors.NotSupportedParameter,
                expression.GetLocation()
            ));
            return "";
        }

        var semanticModel = context.Compilation.ContainsSyntaxTree(expression.SyntaxTree)
            ? CachedModel(context.Compilation, expression.SyntaxTree)
            : null;
        var constantValue = semanticModel?.GetConstantValue(expression) ?? default;
        if (semanticModel is not null && constantValue.HasValue &&
            semanticModel.GetTypeInfo(expression).Type is { TypeKind: not TypeKind.Enum })
        {
            return constantValue.Value switch
            {
                bool value => value ? "true" : "false",
                IFormattable value => value.ToString(null, CultureInfo.InvariantCulture),
                _ => constantValue.Value?.ToString() ?? string.Empty
            };
        }

        switch (expression)
        {
            case LiteralExpressionSyntax syntax:
                return syntax.Token.ValueText;
            case InvocationExpressionSyntax syntax:
                return WithConversionToTarget(FindCode(syntax, context), syntax, semanticModel);
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

    // C# converts an int helper to the long of the property it is assigned to, such as TokenQuota, but the
    // emitted expression is still typed int, which API Management rejects there. The conversion is written out.
    private static string WithConversionToTarget(string code, ExpressionSyntax helperCall, SemanticModel? model)
    {
        if (model is null ||
            !PolicyExpressionCompiler.TryGetImplicitNumericConversion(model, helperCall, out var target))
        {
            return code;
        }

        var type = target.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
        if (code.StartsWith("@(", StringComparison.Ordinal) && code.EndsWith(')'))
        {
            return $"@(({type})({code[2..^1]}))";
        }

        // In a multi-statement expression every value it returns is converted. A raw named value, {{name}},
        // isn't C# and is kept out of the way while the code is read.
        if (code.StartsWith("@{", StringComparison.Ordinal) && code.EndsWith('}'))
        {
            return "@" + RazorCodeFormatter.WithNamedValuesProtected(code[1..], body =>
                SyntaxFactory.ParseStatement(body) is BlockSyntax block && !block.ContainsDiagnostics
                    ? new ReturnConversionRewriter(type).Visit(block).ToFullString()
                    : body);
        }

        return code;
    }

    private sealed class ReturnConversionRewriter(string type) : CSharpSyntaxRewriter
    {
        public override SyntaxNode? VisitReturnStatement(ReturnStatementSyntax node) =>
            node.Expression is { } returned
                ? node.WithExpression(SyntaxFactory.ParseExpression($"({type})({returned.WithoutTrivia().ToFullString()})")
                    .WithTriviaFrom(returned))
                : node;

        // a lambda or a local function returns its own value
        public override SyntaxNode? VisitParenthesizedLambdaExpression(ParenthesizedLambdaExpressionSyntax node) => node;

        public override SyntaxNode? VisitSimpleLambdaExpression(SimpleLambdaExpressionSyntax node) => node;

        public override SyntaxNode? VisitAnonymousMethodExpression(AnonymousMethodExpressionSyntax node) => node;

        public override SyntaxNode? VisitLocalFunctionStatement(LocalFunctionStatementSyntax node) => node;
    }

    public static string FindCode(this InvocationExpressionSyntax syntax, IDocumentCompilationContext context)
    {
        return new PolicyExpressionCompiler(context).CompileInvocation(syntax);
    }

    public static string FindCode(this MemberAccessExpressionSyntax syntax, IDocumentCompilationContext context)
    {
        Compilation compilation = context.Compilation;
        SemanticModel semanticModel = CachedModel(compilation, syntax.SyntaxTree);
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

            // Prop = null is the same as not setting the property
            if (IsNull(assignment.Right, context))
            {
                continue;
            }

            // A callback, such as RetryConfig.ConditionEvaluator, is only run by the emulator and isn't part of
            // the policy.
            if (assignment.Right is AnonymousFunctionExpressionSyntax || IsDelegateProperty(context, assignment.Left))
            {
                continue;
            }

            var name = assignment.Left.ToString();
            var value = assignment.Right.ProcessExpression(context);
            if (value.Value is null && IsScalarProperty(context, assignment.Left))
            {
                // an object or a collection where a single value is expected would otherwise be dropped silently
                context.Report(Diagnostic.Create(
                    CompilationErrors.NotSupportedParameter,
                    assignment.Right.GetLocation()
                ));
                continue;
            }

            if (value.UnnamedValues is null && IsCollectionProperty(context, assignment.Left))
            {
                // a collection that isn't written in place, such as a method call, has no items to emit
                context.Report(Diagnostic.Create(
                    CompilationErrors.NotSupportedParameter,
                    assignment.Right.GetLocation()
                ));
                continue;
            }

            result[name] = value;
        }

        return new InitializerValue
        {
            Type = (creationSyntax.Type as IdentifierNameSyntax)?.Identifier.ValueText,
            NamedValues = result,
            Node = creationSyntax,
        };
    }

    // null, or a constant that is null: default or default(string) for a reference type, (string?)null, a null
    // const, and default for a nullable value type. default for a value type is zero or false and is a value like
    // any other.
    internal static bool IsNull(ExpressionSyntax expression, IDocumentCompilationContext context)
    {
        if (expression.IsKind(SyntaxKind.NullLiteralExpression))
        {
            return true;
        }

        // (bool?)null is not a constant either
        switch (expression)
        {
            case CastExpressionSyntax cast:
                return IsNull(cast.Expression, context);
            case ParenthesizedExpressionSyntax parenthesized:
                return IsNull(parenthesized.Expression, context);
        }

        if (!context.Compilation.ContainsSyntaxTree(expression.SyntaxTree))
        {
            return false;
        }

        var model = CachedModel(context.Compilation, expression.SyntaxTree);
        if (model.GetConstantValue(expression) is { HasValue: true, Value: null })
        {
            return true;
        }

        // default for a nullable value type, such as bool?, is null without being a constant
        return expression is LiteralExpressionSyntax or DefaultExpressionSyntax &&
               expression.Kind() is SyntaxKind.DefaultLiteralExpression or SyntaxKind.DefaultExpression &&
               model.GetTypeInfo(expression).ConvertedType is INamedTypeSymbol
               {
                   OriginalDefinition.SpecialType: SpecialType.System_Nullable_T
               };
    }

    private static bool IsDelegateProperty(IDocumentCompilationContext context, ExpressionSyntax property)
    {
        return context.Compilation.ContainsSyntaxTree(property.SyntaxTree) &&
               CachedModel(context.Compilation, property.SyntaxTree).GetTypeInfo(property).Type is
                   { TypeKind: TypeKind.Delegate };
    }

    // Collections of the policy configurations are arrays; byte[] is a single binary value.
    private static bool IsCollectionProperty(IDocumentCompilationContext context, ExpressionSyntax property)
    {
        return context.Compilation.ContainsSyntaxTree(property.SyntaxTree) &&
               CachedModel(context.Compilation, property.SyntaxTree).GetTypeInfo(property).Type is
                   IArrayTypeSymbol { ElementType.SpecialType: not SpecialType.System_Byte };
    }

    private static bool IsScalarProperty(IDocumentCompilationContext context, ExpressionSyntax property)
    {
        if (!context.Compilation.ContainsSyntaxTree(property.SyntaxTree))
        {
            return false;
        }

        var type = CachedModel(context.Compilation, property.SyntaxTree).GetTypeInfo(property).Type;
        if (type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } nullable)
        {
            type = nullable.TypeArguments[0];
        }

        return type switch
        {
            IArrayTypeSymbol array => array.ElementType.SpecialType == SpecialType.System_Byte,
            { SpecialType: SpecialType.System_Object } => false,
            { TypeKind: TypeKind.Enum } => true,
            not null => type.SpecialType != SpecialType.None,
            _ => false
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
        if (parameters.TryGetValue(key, out var value) && value.Value is not null)
        {
            element.Add(new XAttribute(attName, value.Value));
            return true;
        }

        return false;
    }

    /// <summary>
    /// Reports a literal value outside the range the gateway accepts. A value given by an expression
    /// can't be checked.
    /// </summary>
    public static bool ReportIfOutOfRange(
        this IDocumentCompilationContext context,
        IReadOnlyDictionary<string, InitializerValue> parameters,
        string key,
        string policy,
        int minimum,
        int maximum = int.MaxValue)
    {
        if (!parameters.TryGetValue(key, out var parameter) ||
            !int.TryParse(parameter.Value, out var value) ||
            (value >= minimum && value <= maximum))
        {
            return false;
        }

        context.Report(Diagnostic.Create(
            CompilationErrors.ValueOutOfRange,
            parameter.Node.GetLocation(),
            policy,
            key,
            value,
            minimum,
            maximum));
        return true;
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
        if (syntax is InvocationExpressionSyntax invocation &&
            TryResolveConfigFactory(invocation, context, out var factoryConfig, out var factoryContext))
        {
            syntax = factoryConfig;
            context = factoryContext;
        }

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

    // A config factory is a source method returning a single object creation expression,
    // whose parameters (if any) are policy section contexts. Its body is compiled in place.
    private static bool TryResolveConfigFactory(
        InvocationExpressionSyntax invocation,
        IDocumentCompilationContext context,
        [NotNullWhen(true)] out ObjectCreationExpressionSyntax? config,
        out IDocumentCompilationContext factoryContext)
    {
        var model = CachedModel(context.Compilation, invocation.SyntaxTree);
        var declaration = model.GetSymbolInfo(invocation).Symbol is IMethodSymbol method &&
                          method.Parameters.All(parameter => PolicyExpressionCompiler.IsAuthoringSectionContext(parameter.Type))
            ? method.DeclaringSyntaxReferences
                .Select(reference => reference.GetSyntax())
                .OfType<MethodDeclarationSyntax>()
                .FirstOrDefault()
            : null;

        // A factory in a referenced project is bound with that project's compilation.
        factoryContext = context;
        if (declaration is not null && !context.Compilation.ContainsSyntaxTree(declaration.SyntaxTree))
        {
            var owner = FindCompilation(context.Compilation, declaration.SyntaxTree);
            factoryContext = owner is null ? context : new ReferencedCompilationContext(context, owner);
            declaration = owner is null ? null : declaration;
        }

        config = declaration switch
        {
            { ExpressionBody.Expression: ObjectCreationExpressionSyntax expression } => expression,
            { Body.Statements: [ReturnStatementSyntax { Expression: ObjectCreationExpressionSyntax expression }] } =>
                expression,
            _ => null
        };
        return config is not null;
    }

    public static T Normalize<T>(T node) where T : SyntaxNode
    {
        var unformatted = (T)new TriviaRemoverRewriter().Visit(node);
        return unformatted.NormalizeWhitespace("", "\n");
    }

    // Semantic models are reused across the expressions of a compilation so binding work isn't repeated.
    private static readonly ConditionalWeakTable<Compilation, ConcurrentDictionary<SyntaxTree, SemanticModel>>
        SemanticModels = new();

    internal static SemanticModel CachedModel(Compilation compilation, SyntaxTree tree) =>
        SemanticModels.GetOrCreateValue(compilation).GetOrAdd(tree, key => compilation.GetSemanticModel(key));

    // A syntax tree from a referenced project belongs to that project's compilation.
    internal static Compilation? FindCompilation(Compilation compilation, SyntaxTree tree) =>
        FindCompilation(compilation, tree, new HashSet<Compilation>());

    private static Compilation? FindCompilation(Compilation compilation, SyntaxTree tree, HashSet<Compilation> visited)
    {
        if (!visited.Add(compilation))
        {
            return null;
        }

        if (compilation.ContainsSyntaxTree(tree))
        {
            return compilation;
        }

        return compilation.References
            .OfType<CompilationReference>()
            .Select(reference => FindCompilation(reference.Compilation, tree, visited))
            .FirstOrDefault(found => found is not null);
    }
}

// A compilation context whose semantic questions are answered by the compilation that declares a config factory.
internal sealed class ReferencedCompilationContext(IDocumentCompilationContext inner, Compilation compilation)
    : IDocumentCompilationContext
{
    public void AddPolicy(XNode element) => inner.AddPolicy(element);
    public void Report(Diagnostic diagnostic) => inner.Report(diagnostic);
    public Compilation Compilation => compilation;
    public SyntaxNode SyntaxRoot => inner.SyntaxRoot;
    public IList<Diagnostic> Diagnostics => inner.Diagnostics;
    public XElement RootElement => inner.RootElement;
    public XElement CurrentElement => inner.CurrentElement;

    public string? PendingPolicyId
    {
        get => inner.PendingPolicyId;
        set => inner.PendingPolicyId = value;
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