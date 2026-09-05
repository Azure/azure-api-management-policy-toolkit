// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Xml.Linq;

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling.Diagnostics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling.Policy;

public class IncludeFragmentCompiler : IMethodPolicyHandler
{
    public string MethodName => nameof(IInboundContext.IncludeFragment);

    public void Handle(IDocumentCompilationContext context, InvocationExpressionSyntax node)
    {
        if (node.ArgumentList.Arguments.Count != 1)
        {
            context.Report(Diagnostic.Create(
                CompilationErrors.ArgumentCountMissMatchForPolicy,
                node.ArgumentList.GetLocation(),
                "include-fragment"
            ));
            return;
        }

        var arg = node.ArgumentList.Arguments[0].Expression;

        if (arg is ObjectCreationExpressionSyntax objectCreation)
        {
            HandleFragmentObject(context, objectCreation);
        }
        else
        {
            var fragmentId = arg.ProcessParameter(context);
            context.AddPolicy(new XElement("include-fragment", new XAttribute("fragment-id", fragmentId)));
        }
    }

    private static void HandleFragmentObject(
        IDocumentCompilationContext context,
        ObjectCreationExpressionSyntax objectCreation)
    {
        var className = (objectCreation.Type as IdentifierNameSyntax)?.Identifier.ValueText;
        if (className is null)
        {
            context.Report(Diagnostic.Create(
                CompilationErrors.NotSupportedParameter,
                objectCreation.GetLocation()
            ));
            return;
        }

        ClassDeclarationSyntax? fragmentClass = null;
        foreach (var tree in context.Compilation.SyntaxTrees)
        {
            fragmentClass = tree.GetRoot()
                .DescendantNodes()
                .OfType<ClassDeclarationSyntax>()
                .FirstOrDefault(c => c.Identifier.ValueText == className);
            if (fragmentClass is not null) break;
        }

        var fragmentId = CompilerUtils.ExtractFragmentId(fragmentClass ?? context.SyntaxRoot as ClassDeclarationSyntax);
        if (fragmentClass is null)
            fragmentId = CompilerUtils.ToKebabCase(className);

        foreach (var expression in objectCreation.Initializer?.Expressions ?? [])
        {
            if (expression is not AssignmentExpressionSyntax assignment)
                continue;

            var propName = assignment.Left.ToString();
            var varName = CompilerUtils.GetFragmentVariableName(fragmentClass, propName) ?? propName;
            var value = assignment.Right.ProcessParameter(context);

            context.AddPolicy(new XElement("set-variable",
                new XAttribute("name", $"{fragmentId}-{varName}"),
                new XAttribute("value", value)));
        }

        context.AddPolicy(new XElement("include-fragment", new XAttribute("fragment-id", fragmentId)));
    }
}
