// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.ApiManagement.PolicyToolkit.Compiling.Diagnostics;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Azure.ApiManagement.PolicyToolkit.Compiling.Syntax;

public class BlockCompiler : ISyntaxCompiler
{
    private readonly Dictionary<SyntaxKind, ISyntaxCompiler> _compilers = new();

    public BlockCompiler(IEnumerable<ISyntaxCompiler> compilers)
    {
        foreach (ISyntaxCompiler compiler in compilers)
        {
            _compilers.Add(compiler.Syntax, compiler);
        }
    }

    public SyntaxKind Syntax => SyntaxKind.Block;

    public void Compile(ICompilationContext context, SyntaxNode node)
    {
        var block = node as BlockSyntax ?? throw new NullReferenceException();

        foreach (var statement in block.Statements)
        {
            if (_compilers.TryGetValue(statement.Kind(), out var compiler))
            {
                compiler.Compile(context, statement);
            }
            else
            {
                context.Report(Diagnostic.Create(
                    CompilationErrors.NotSupportedStatement,
                    statement.GetLocation(),
                    statement.Kind().ToString()
                ));
            }
        }
    }
}