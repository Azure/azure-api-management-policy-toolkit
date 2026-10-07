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

    private static readonly ConditionalWeakTable<Compilation, Lazy<HashSet<IMethodSymbol>>> ReachedMethods =
        new ConditionalWeakTable<Compilation, Lazy<HashSet<IMethodSymbol>>>();

    // The compiler expands a helper of a document only where an expression calls it, so a method of a document is
    // expression code when it takes the expression context or is called, directly or through other helpers, from
    // a section, a configuration factory or such a method, of its own document or of another. A method nothing
    // calls, such as a ToString() override, never becomes a policy expression.
    private static bool IsDocumentExpressionHelper(IMethodSymbol method, Compilation compilation) =>
        method.IsDocumentMember() && !IsSectionOrConfigurationFactory(method) &&
        ReachedMethods.GetValue(compilation, c => new Lazy<HashSet<IMethodSymbol>>(() => FindReachedMethods(c)))
            .Value.Contains(method);

    // A document with the classes nested in it and the documents derived from it, or an expression helper library:
    // the methods a bare call from one of them can mean, and the names a qualified call to one of them is written
    // with.
    private sealed class Family
    {
        public List<IMethodSymbol> Methods { get; } = new List<IMethodSymbol>();

        // Nested, Document, Derived: the last name of a qualified receiver
        public HashSet<string> Receivers { get; } = new HashSet<string>();

        // those, the namespace and the classes the document is nested in: the other names of the receiver
        public HashSet<string> Qualifiers { get; } = new HashSet<string>();
    }

    // The methods of every document and expression helper library of the compilation that are reached from a
    // root, in one pass. Calls are matched by name (Helper(), this.Helper(), Nested.Helper(), Shared.Helper()),
    // which needs no semantic model for other files; an overload that isn't the one called is included with it.
    private static HashSet<IMethodSymbol> FindReachedMethods(Compilation compilation)
    {
        var types = compilation.GetSymbolsWithName(_ => true, SymbolFilter.Type)
            .OfType<INamedTypeSymbol>()
            .Where(type => !type.DeclaringSyntaxReferences.IsDefaultOrEmpty)
            .ToList();
        var families = new List<Family>();
        var familiesOf = new Dictionary<IMethodSymbol, List<Family>>(SymbolEqualityComparer.Default);
        foreach (var type in types.Where(type => type.ContainingType is null || !IsFamilyMember(type.ContainingType)))
        {
            // a document, an expression helper library, or a class with an [Expression] method that may call a document
            if (!type.IsDocument() && !type.HasExpressionAttribute() &&
                !type.GetMembers().OfType<IMethodSymbol>().Any(HasExpressionAttribute))
            {
                continue;
            }

            var family = new Family();
            var members = new Stack<INamedTypeSymbol>();
            members.Push(type);
            // a helper of a base document is expanded where a derived document calls it
            foreach (var derived in types.Where(candidate => Derives(candidate, type)))
            {
                members.Push(derived);
            }

            while (members.Count > 0)
            {
                var member = members.Pop();
                family.Receivers.Add(member.Name);
                // the namespace and the classes each is nested in, which differ for a derived document:
                // Other.Derived.Helper(), Outer.Document.Nested.Helper()
                for (var space = member.ContainingNamespace; space is { IsGlobalNamespace: false }; space = space.ContainingNamespace)
                {
                    family.Qualifiers.Add(space.Name);
                }

                for (var outer = member.ContainingType; outer is not null; outer = outer.ContainingType)
                {
                    family.Qualifiers.Add(outer.Name);
                }

                // a section may implement IDocument explicitly: void IDocument.Inbound(IInboundContext context)
                foreach (var method in member.GetMembers().OfType<IMethodSymbol>().Where(method =>
                             method.MethodKind is MethodKind.Ordinary or MethodKind.ExplicitInterfaceImplementation))
                {
                    family.Methods.Add(method);
                    if (!familiesOf.TryGetValue(method, out var owners))
                    {
                        familiesOf[method] = owners = new List<Family>();
                    }

                    owners.Add(family);
                }

                foreach (var nested in member.GetTypeMembers())
                {
                    members.Push(nested);
                }
            }

            family.Qualifiers.UnionWith(family.Receivers);
            families.Add(family);
        }

        var reached = new HashSet<IMethodSymbol>(SymbolEqualityComparer.Default);
        var pending = new Queue<IMethodSymbol>();
        foreach (var root in familiesOf.Keys.Where(IsRoot))
        {
            if (reached.Add(root))
            {
                pending.Enqueue(root);
            }
        }

        var aliasesByTree = new Dictionary<SyntaxTree, Dictionary<string, string>>();
        while (pending.Count > 0)
        {
            var caller = pending.Dequeue();
            var own = familiesOf[caller];
            foreach (var reference in caller.DeclaringSyntaxReferences)
            {
                var aliases = AliasesOf(reference.SyntaxTree, aliasesByTree);
                foreach (var invocation in reference.GetSyntax().DescendantNodes().OfType<InvocationExpressionSyntax>())
                {
                    IEnumerable<IMethodSymbol> candidates;
                    string name;
                    switch (invocation.Expression)
                    {
                        // Helper(), this.Helper(), base.Helper(): a method of the caller's own document
                        case SimpleNameSyntax simple:
                            name = simple.Identifier.ValueText;
                            candidates = own.SelectMany(family => family.Methods);
                            break;
                        case MemberAccessExpressionSyntax access when Unparenthesized(access.Expression) is
                            ThisExpressionSyntax or BaseExpressionSyntax:
                            name = access.Name.Identifier.ValueText;
                            candidates = own.SelectMany(family => family.Methods);
                            break;
                        // Nested.Helper(), Shared.Helper(), Ns.Shared.Nested.Helper(): the families the receiver names;
                        // value.ToString() is a call on something else
                        case MemberAccessExpressionSyntax access when Segments(access.Expression, aliases) is { } segments:
                            name = access.Name.Identifier.ValueText;
                            candidates = families.Where(family => IsReceiver(segments, family)).SelectMany(family => family.Methods);
                            break;
                        default:
                            continue;
                    }

                    foreach (var method in candidates.Where(method => method.Name == name && reached.Add(method)))
                    {
                        pending.Enqueue(method);
                    }
                }
            }
        }

        return reached;

        static bool IsFamilyMember(INamedTypeSymbol type)
        {
            for (var outer = type; outer is not null; outer = outer.ContainingType)
            {
                if (outer.IsDocument() || outer.HasExpressionAttribute() ||
                    outer.GetMembers().OfType<IMethodSymbol>().Any(HasExpressionAttribute))
                {
                    return true;
                }
            }

            return false;
        }

        static bool Derives(INamedTypeSymbol candidate, INamedTypeSymbol type)
        {
            for (var baseType = candidate.BaseType; baseType is not null; baseType = baseType.BaseType)
            {
                // Derived : Base<int> has the constructed type as its base
                if (SymbolEqualityComparer.Default.Equals(baseType.OriginalDefinition, type.OriginalDefinition))
                {
                    return true;
                }
            }

            return false;
        }

        // The methods the compiler starts from: the sections, the configuration factories, the expression methods
        // and the helpers that take a section context or the expression context. A factory is a root wherever it is
        // called from. A void method that isn't a section, Audit(IInboundContext context), is never compiled, so
        // nothing is compiled from it either.
        static bool IsRoot(IMethodSymbol method) =>
            IsSection(method) || IsConfigurationFactory(method) || method.HasExpressionAttribute() ||
            method.IsExpressionLibraryMember() ||
            !method.ReturnsVoid && (TakesSectionContext(method) || method.Parameters.Any(parameter =>
                parameter.Type.ToFullyQualifiedString() == ExpressionContext));
    }

    // A section as the compiler finds it: a void method of the document class itself, named for a section of its
    // kind of document, taking one section context. It may implement the interface explicitly.
    private static bool IsSection(IMethodSymbol method)
    {
        if (!method.ReturnsVoid || !TakesOneSectionContext(method))
        {
            return false;
        }

        var name = SectionName(method);
        var interfaces = method.ContainingType.AllInterfaces.Select(implemented => implemented.ToFullyQualifiedString()).ToList();
        if (interfaces.Contains(Document) && name is "Inbound" or "Outbound" or "Backend" or "OnError")
        {
            return true;
        }

        // the compiler compiles the first Fragment method taking a section context of the class declaration it
        // compiles; an overload taking another is left. Members of a partial class come in file order, so the first
        // of each part is taken.
        var part = Part(method);
        return interfaces.Contains(Fragment) && name == "Fragment" &&
               SymbolEqualityComparer.Default.Equals(method, method.ContainingType.GetMembers().OfType<IMethodSymbol>()
                   .First(candidate => SectionName(candidate) == "Fragment" && TakesOneSectionContext(candidate) &&
                                       Part(candidate) == part));

        static string SectionName(IMethodSymbol method) =>
            method.ExplicitInterfaceImplementations.FirstOrDefault()?.Name ?? method.Name;

        static SyntaxNode? Part(IMethodSymbol method) =>
            method.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax().Parent;

        // IHaveExpressionContext is what the section contexts share, not a section's own
        static bool TakesOneSectionContext(IMethodSymbol method) =>
            method.Parameters.Length == 1 && method.Parameters[0].Type.IsSectionContext() &&
            method.Parameters[0].Type.ToFullyQualifiedString() != SectionContext;
    }

    // using S = Some.Namespace.Shared; gives the name S the meaning of Shared in the file
    private static Dictionary<string, string> AliasesOf(SyntaxTree tree, Dictionary<SyntaxTree, Dictionary<string, string>> cache)
    {
        if (!cache.TryGetValue(tree, out var aliases))
        {
            cache[tree] = aliases = tree.GetRoot().DescendantNodes(node => node is not TypeDeclarationSyntax)
                .OfType<UsingDirectiveSyntax>()
                .Where(directive => directive.Alias is not null && directive.Name is not null)
                .GroupBy(directive => directive.Alias!.Name.Identifier.ValueText)
                .ToDictionary(group => group.Key, group => LastName(group.First().Name!));
        }

        return aliases;
    }

    private static string LastName(NameSyntax name) => name switch
    {
        QualifiedNameSyntax qualified => qualified.Right.Identifier.ValueText,
        AliasQualifiedNameSyntax aliased => aliased.Name.Identifier.ValueText,
        SimpleNameSyntax simple => simple.Identifier.ValueText,
        _ => name.ToString()
    };

    // The names of a receiver made of names only, Ns.Shared.Nested or global::Shared, with an alias replaced by
    // what it stands for; null for a receiver with anything else in it, such as a call or an index. A value's
    // members, context.Request.Headers, are names too and match no family.
    private static List<string>? Segments(ExpressionSyntax receiver, Dictionary<string, string> aliases)
    {
        var segments = new List<string>();
        while (true)
        {
            switch (receiver)
            {
                case MemberAccessExpressionSyntax access:
                    segments.Add(access.Name.Identifier.ValueText);
                    receiver = access.Expression;
                    continue;
                case AliasQualifiedNameSyntax aliased:
                    segments.Add(aliased.Name.Identifier.ValueText);
                    break;
                case SimpleNameSyntax simple:
                    segments.Add(aliases.TryGetValue(simple.Identifier.ValueText, out var target) ? target : simple.Identifier.ValueText);
                    break;
                default:
                    return null;
            }

            segments.Reverse();
            return segments;
        }
    }

    // The last name is a type of the family, the others its namespace, the classes it is nested in or its types.
    private static bool IsReceiver(List<string> segments, Family family) =>
        family.Receivers.Contains(segments[segments.Count - 1]) &&
        segments.All(family.Qualifiers.Contains);

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

    // The methods of a document that hold policies rather than expression code, told apart by their return type,
    // which approximates how the compiler treats them: a section returns nothing, a policy configuration factory
    // returns a configuration. A helper returning anything else is expanded into an expression whatever it takes,
    // a section context included.
    private static bool IsSectionOrConfigurationFactory(IMethodSymbol method) =>
        method.ReturnsVoid || IsConfigurationFactory(method);

    // the configurations are the records of the authoring namespace; its interfaces and enums aren't ones
    private static bool IsConfigurationFactory(IMethodSymbol method) =>
        method.ReturnType is INamedTypeSymbol { TypeKind: TypeKind.Class } &&
        method.ReturnType.ContainingNamespace?.ToDisplayString() == Authoring;

    private static bool TakesSectionContext(IMethodSymbol method) =>
        method.Parameters.Any(parameter => parameter.Type.IsSectionContext());

    // The section context interfaces of the authoring library, IHaveExpressionContext itself included, as for the
    // compiler; a type of the user's that implements one is not one.
    public static bool IsSectionContext(this ITypeSymbol type) =>
        type is INamedTypeSymbol { TypeKind: TypeKind.Interface } &&
        type.ContainingNamespace?.ToDisplayString() == Authoring &&
        (type.ToFullyQualifiedString() == SectionContext ||
         type.AllInterfaces.Any(implemented => implemented.ToFullyQualifiedString() == SectionContext));

    private const string Document = Authoring + ".IDocument";
    private const string Fragment = Authoring + ".IFragment";

    public static bool IsDocument(this INamedTypeSymbol type) =>
        type.AllInterfaces.Any(implemented => implemented.ToFullyQualifiedString() is Document or Fragment);

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