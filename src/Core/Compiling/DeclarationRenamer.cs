// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling;

// Renames the declarations in emitted helper code so they can't clash with the code around them.
internal sealed class DeclarationRenamer
{
    private int _count;

    // Maps the variables, lambda parameters and range variables declared in an emitted helper body to new names:
    // all of them for an inlined helper, or only those named context, which would hide API Management's context.
    public Dictionary<ISymbol, string> CreateRenames(SyntaxNode? body, SemanticModel model, bool renameAll)
    {
        var renames = new Dictionary<ISymbol, string>(SymbolEqualityComparer.Default);
        foreach (var node in body?.DescendantNodesAndSelf() ?? [])
        {
            if (DeclaredName(node) is not null &&
                model.GetDeclaredSymbol(node) is { } symbol &&
                (renameAll || symbol.Name == "context"))
            {
                renames[symbol] = $"{symbol.Name}__{++_count}";
            }
        }

        return renames;
    }

    private static string? DeclaredName(SyntaxNode node) => node switch
    {
        SingleVariableDesignationSyntax designation => designation.Identifier.ValueText,
        ParameterSyntax parameter => parameter.Identifier.ValueText,
        VariableDeclaratorSyntax declarator => declarator.Identifier.ValueText,
        FromClauseSyntax from => from.Identifier.ValueText,
        LetClauseSyntax let => let.Identifier.ValueText,
        JoinClauseSyntax join => join.Identifier.ValueText,
        JoinIntoClauseSyntax into => into.Identifier.ValueText,
        QueryContinuationSyntax continuation => continuation.Identifier.ValueText,
        ForEachStatementSyntax forEach => forEach.Identifier.ValueText,
        CatchDeclarationSyntax { Identifier.ValueText: { Length: > 0 } name } => name,
        LocalFunctionStatementSyntax localFunction => localFunction.Identifier.ValueText,
        _ => null
    };

    // Two declarations of a name clash when one's scope (the nearest enclosing lambda, block or loop, or the whole
    // expression) contains the other's; declarations in sibling lambdas or blocks don't clash.
    public static bool HasConflictingDeclaration(SyntaxNode expression)
    {
        var declarations = expression.DescendantNodesAndSelf()
            .Select(node => (Name: DeclaredName(node), Scope: Scope(node)))
            .Where(declaration => declaration.Name is not null)
            .ToArray();
        return declarations
            .SelectMany((first, index) => declarations.Skip(index + 1).Select(second => (first, second)))
            .Any(pair => pair.first.Name == pair.second.Name &&
                         (pair.first.Scope!.Contains(pair.second.Scope) || pair.second.Scope!.Contains(pair.first.Scope)));

        SyntaxNode Scope(SyntaxNode node) => node switch
        {
            ForEachStatementSyntax => node,
            CatchDeclarationSyntax => node.Parent!,
            _ => node.Ancestors()
                .TakeWhile(ancestor => ancestor != expression)
                .FirstOrDefault(ancestor => ancestor is AnonymousFunctionExpressionSyntax or BlockSyntax
                    or ForStatementSyntax) ?? expression
        };
    }
}