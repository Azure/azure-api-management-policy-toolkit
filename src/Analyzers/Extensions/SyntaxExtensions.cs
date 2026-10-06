// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Analyzers;

public static class SyntaxExtensions
{
    public static bool ContainsAttributeOfType(this SyntaxList<AttributeListSyntax> syntax, SemanticModel model,
        string type)
    {
        return syntax
            .SelectMany(a => a.Attributes)
            .Any(attribute =>
            {
                var attributeType = model.GetSymbolInfo(attribute).Symbol?.ContainingType;
                return attributeType?.ToFullyQualifiedString() == type;
            });
    }

    private const string ExpressionAttribute =
        "Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring.ExpressionAttribute";

    public static bool ContainsExpressionAttribute(this SyntaxList<AttributeListSyntax> syntax, SemanticModel model)
    {
        return syntax.ContainsAttributeOfType(model, ExpressionAttribute);
    }

    public static bool HasExpressionAttribute(this ISymbol symbol)
    {
        return symbol.GetAttributes()
            .Any(attribute => attribute.AttributeClass?.ToFullyQualifiedString() == ExpressionAttribute);
    }

    // A node inside an [Expression] method, inside any method of an expression helper library (by symbol, so
    // every part of a partial class is included), or inside a helper of a policy document or fragment that the
    // compiler turns into a policy expression without the attribute.
    public static bool IsPartOfPolicyExpressionMethod(this SyntaxNode syntax, SemanticModel model)
    {
        return syntax.Ancestors()
            .OfType<MethodDeclarationSyntax>()
            .Any(method => IsMarkedExpressionMethod(method, model) ||
                           model.GetDeclaredSymbol(method) is IMethodSymbol symbol &&
                           IsDocumentExpressionHelper(symbol, model.Compilation));
    }

    private static readonly ConditionalWeakTable<Compilation, ConcurrentDictionary<INamedTypeSymbol, HashSet<IMethodSymbol>>>
        DocumentHelpers = new ConditionalWeakTable<Compilation, ConcurrentDictionary<INamedTypeSymbol, HashSet<IMethodSymbol>>>();

    // The compiler expands a helper of a document only where an expression calls it, so a method of a document is
    // expression code when it takes the expression context or is called, directly or through other helpers, from
    // a section, a configuration factory or such a method. A method nothing of the document calls, such as a
    // ToString() override, never becomes a policy expression.
    private static bool IsDocumentExpressionHelper(IMethodSymbol method, Compilation compilation)
    {
        if (!method.IsDocumentMember() || IsSectionOrConfigurationFactory(method))
        {
            return false;
        }

        var document = method.ContainingType;
        for (var type = document; type is not null; type = type.ContainingType)
        {
            if (type.AllInterfaces.Any(implemented => implemented.ToFullyQualifiedString() is Document or Fragment))
            {
                document = type;
            }
        }

        return DocumentHelpers
            .GetValue(compilation, _ => new ConcurrentDictionary<INamedTypeSymbol, HashSet<IMethodSymbol>>(SymbolEqualityComparer.Default))
            .GetOrAdd(document, type => FindCalledMethods(type, compilation))
            .Contains(method);
    }

    // The methods of a document, of the classes nested in it and of the documents derived from it, that are
    // reached from their sections, factories and expression methods. Calls are matched by name (Helper(),
    // this.Helper(), Nested.Helper()), which needs no semantic model for the other files of a partial class; an
    // overload that isn't the one called is included with it.
    private static HashSet<IMethodSymbol> FindCalledMethods(INamedTypeSymbol document, Compilation compilation)
    {
        var methods = new List<IMethodSymbol>();
        var types = new Stack<INamedTypeSymbol>();
        types.Push(document);
        // a helper of a base document is expanded where a derived document calls it
        foreach (var derived in compilation.GetSymbolsWithName(_ => true, SymbolFilter.Type).OfType<INamedTypeSymbol>())
        {
            for (var type = derived.BaseType; type is not null; type = type.BaseType)
            {
                // Derived : Base<int> has the constructed type as its base
                if (SymbolEqualityComparer.Default.Equals(type.OriginalDefinition, document.OriginalDefinition))
                {
                    types.Push(derived);
                    break;
                }
            }
        }

        var receivers = new HashSet<string>();
        while (types.Count > 0)
        {
            var type = types.Pop();
            receivers.Add(type.Name);
            methods.AddRange(type.GetMembers().OfType<IMethodSymbol>()
                .Where(member => member.MethodKind == MethodKind.Ordinary));
            foreach (var nested in type.GetTypeMembers())
            {
                types.Push(nested);
            }
        }

        // A receiver is one of those types, written with or without its namespace, or a using alias.
        var qualifiers = new HashSet<string>(receivers);
        for (var space = document.ContainingNamespace; space is { IsGlobalNamespace: false }; space = space.ContainingNamespace)
        {
            qualifiers.Add(space.Name);
        }

        foreach (var alias in methods.SelectMany(method => method.DeclaringSyntaxReferences)
                     .Select(reference => reference.SyntaxTree).Distinct()
                     .SelectMany(tree => tree.GetRoot().DescendantNodes(node => node is not TypeDeclarationSyntax))
                     .OfType<UsingDirectiveSyntax>()
                     .Where(directive => directive.Alias is not null))
        {
            receivers.Add(alias.Alias!.Name.Identifier.ValueText);
        }

        // a document nested in another class: Outer.Document.Nested.Helper()
        for (var outer = document.ContainingType; outer is not null; outer = outer.ContainingType)
        {
            qualifiers.Add(outer.Name);
        }

        qualifiers.UnionWith(receivers);
        var reached = new HashSet<IMethodSymbol>(SymbolEqualityComparer.Default);
        var pending = new Queue<IMethodSymbol>(methods.Where(method =>
            IsSectionOrConfigurationFactory(method) || method.HasExpressionAttribute() ||
            method.IsExpressionLibraryMember() ||
            method.Parameters.Any(parameter => parameter.Type.ToFullyQualifiedString() == ExpressionContext)));
        foreach (var root in pending)
        {
            reached.Add(root);
        }

        while (pending.Count > 0)
        {
            var called = pending.Dequeue().DeclaringSyntaxReferences
                .SelectMany(reference => reference.GetSyntax().DescendantNodes())
                .OfType<InvocationExpressionSyntax>()
                .Select(invocation => invocation.Expression switch
                {
                    // Helper(), this.Helper(), base.Helper() or Nested.Helper(); value.ToString() is a call on
                    // something else
                    MemberAccessExpressionSyntax access when Unparenthesized(access.Expression) is
                        ThisExpressionSyntax or BaseExpressionSyntax => access.Name,
                    MemberAccessExpressionSyntax access when IsReceiver(access.Expression, receivers, qualifiers) =>
                        access.Name,
                    _ => invocation.Expression as SimpleNameSyntax
                })
                .Where(name => name is not null)
                .Select(name => name!.Identifier.ValueText)
                .ToImmutableHashSet();
            foreach (var method in methods.Where(method => called.Contains(method.Name) && reached.Add(method)))
            {
                pending.Enqueue(method);
            }
        }

        return reached;
    }

    private static ExpressionSyntax Unparenthesized(ExpressionSyntax expression)
    {
        // (this) and ((Document)this)
        while (true)
        {
            switch (expression)
            {
                case ParenthesizedExpressionSyntax parenthesized:
                    expression = parenthesized.Expression;
                    continue;
                case CastExpressionSyntax cast:
                    expression = cast.Expression;
                    continue;
                default:
                    return expression;
            }
        }
    }

    // Nested, Document.Nested, Namespace.Document or global::Document: names all the way, ending in a type of
    // the document. context.Request.Headers ends in a name that may match but starts with a value.
    private static bool IsReceiver(ExpressionSyntax receiver, HashSet<string> receivers, HashSet<string> qualifiers)
    {
        var last = true;
        while (true)
        {
            var name = receiver switch
            {
                MemberAccessExpressionSyntax access => access.Name,
                AliasQualifiedNameSyntax aliased => aliased.Name,
                _ => receiver as SimpleNameSyntax
            };
            if (name is null || !(last ? receivers : qualifiers).Contains(name.Identifier.ValueText))
            {
                return false;
            }

            if (receiver is not MemberAccessExpressionSyntax qualified)
            {
                return true;
            }

            receiver = qualified.Expression;
            last = false;
        }
    }

    private const string ExpressionContext =
        "Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring.Expressions.IExpressionContext";

    // A node inside a method that is expression code because it, or its class, is marked [Expression].
    public static bool IsPartOfMarkedPolicyExpressionMethod(this SyntaxNode syntax, SemanticModel model)
    {
        return syntax.Ancestors()
            .OfType<MethodDeclarationSyntax>()
            .Any(method => IsMarkedExpressionMethod(method, model));
    }

    private static bool IsMarkedExpressionMethod(MethodDeclarationSyntax method, SemanticModel model)
    {
        return method.AttributeLists.ContainsExpressionAttribute(model) ||
               model.GetDeclaredSymbol(method)?.IsExpressionLibraryMember() == true;
    }

    private const string Authoring = "Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring";
    private const string SectionContext = Authoring + ".IHaveExpressionContext";

    // The methods of a document that hold policies rather than expression code: sections and fragments return
    // nothing and take a section context, policy configuration factories return a configuration.
    private static bool IsSectionOrConfigurationFactory(IMethodSymbol method)
    {
        return method.ReturnsVoid ||
               method.ReturnType.ContainingNamespace?.ToDisplayString() == Authoring ||
               method.Parameters.Any(parameter =>
                   parameter.Type.AllInterfaces.Any(type => type.ToFullyQualifiedString() == SectionContext));
    }

    private const string Document = Authoring + ".IDocument";
    private const string Fragment = Authoring + ".IFragment";

    // A member declared in the source of a policy document or fragment class, or of a class nested in one.
    public static bool IsDocumentMember(this ISymbol symbol)
    {
        if (symbol.DeclaringSyntaxReferences.IsDefaultOrEmpty)
        {
            return false;
        }

        for (var type = symbol.ContainingType; type is not null; type = type.ContainingType)
        {
            if (type.AllInterfaces.Any(implemented => implemented.ToFullyQualifiedString() is Document or Fragment))
            {
                return true;
            }
        }

        return false;
    }

    // A member of a class marked [Expression] (an expression helper library), from source or metadata.
    public static bool IsExpressionLibraryMember(this ISymbol symbol)
    {
        for (var type = symbol.ContainingType; type is not null; type = type.ContainingType)
        {
            if (type.HasExpressionAttribute())
            {
                return true;
            }
        }

        return false;
    }

    public static bool IsPartOfPolicyExpressionDelegate(this SyntaxNode syntaxNode, SemanticModel model)
    {
        return syntaxNode.Ancestors()
            .OfType<LambdaExpressionSyntax>()
            .Any(l => l.IsExpressionLambda(model));
    }

    private static readonly Regex ExpressionDelegateTypeMatcher =
        new Regex(
            @"Microsoft\.Azure\.ApiManagement\.PolicyToolkit\.Authoring\.Expression<.*?>",
            RegexOptions.Compiled);

    public static bool IsExpressionLambda(this LambdaExpressionSyntax syntaxNode, SemanticModel model)
    {
        var syntax = syntaxNode.Ancestors()
            .OfType<InvocationExpressionSyntax>()
            .FirstOrDefault();
        if (syntax == null)
        {
            return false;
        }

        var symbol = model.GetSymbolInfo(syntax).Symbol;
        if (symbol is not IMethodSymbol methodSymbol)
        {
            return false;
        }

        var parameter = methodSymbol.Parameters.FirstOrDefault();
        if (parameter == null)
        {
            return false;
        }

        var displayName = parameter.OriginalDefinition.Type.ToDisplayString();
        return ExpressionDelegateTypeMatcher.IsMatch(displayName);
    }
}