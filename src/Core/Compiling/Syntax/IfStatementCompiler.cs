// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Xml.Linq;

using Azure.ApiManagement.PolicyToolkit.Compiling.Diagnostics;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Azure.ApiManagement.PolicyToolkit.Compiling.Syntax;

public class IfStatementCompiler : ISyntaxCompiler
{
    ISyntaxCompiler _blockCompiler;

    public IfStatementCompiler(ISyntaxCompiler blockCompiler)
    {
        this._blockCompiler = blockCompiler;
    }

    public SyntaxKind Syntax => SyntaxKind.IfStatement;

    public void Compile(ICompilationContext context, SyntaxNode node)
    {
        var ifStatement = node as IfStatementSyntax ?? throw new NullReferenceException();

        var choose = new XElement("choose");
        context.AddPolicy(choose);

        IfStatementSyntax? nextIf = ifStatement;
        IfStatementSyntax currentIf;
        do
        {
            currentIf = nextIf;

            if (currentIf.Statement is not BlockSyntax block)
            {
                context.Report(Diagnostic.Create(
                    CompilationErrors.NotSupportedStatement,
                    currentIf.Statement.GetLocation(),
                    currentIf.Statement.GetType().Name
                ));
                nextIf = currentIf.Else?.Statement as IfStatementSyntax;
                continue;
            }

            if (currentIf.Condition is not InvocationExpressionSyntax condition)
            {
                context.Report(Diagnostic.Create(
                    CompilationErrors.ExpressionNotSupported,
                    currentIf.Condition.GetLocation(),
                    currentIf.Condition.GetType().Name,
                    nameof(InvocationExpressionSyntax)
                ));
                nextIf = currentIf.Else?.Statement as IfStatementSyntax;
                continue;
            }

            var section = new XElement("when");
            var innerContext = new SubCompilationContext(context, section);
            _blockCompiler.Compile(innerContext, block);
            section.Add(new XAttribute("condition", CompilerUtils.FindCode(condition, context)));
            choose.Add(section);

            nextIf = currentIf.Else?.Statement as IfStatementSyntax;
        } while (nextIf != null);

        if (currentIf.Else != null)
        {
            var section = new XElement("otherwise");
            var innerContext = new SubCompilationContext(context, section);
            if (currentIf.Else.Statement is BlockSyntax block)
            {
                _blockCompiler.Compile(innerContext, block);
                choose.Add(section);
            }
            else
            {
                context.Report(Diagnostic.Create(
                    CompilationErrors.NotSupportedStatement,
                    currentIf.Else.Statement.GetLocation(),
                    currentIf.Else.Statement.GetType().Name
                ));
            }
        }
    }
}