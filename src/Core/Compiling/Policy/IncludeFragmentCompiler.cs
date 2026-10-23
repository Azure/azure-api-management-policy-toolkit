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
        var model = CompilerUtils.CachedModel(context.Compilation, objectCreation.SyntaxTree);
        var fragmentType = model.GetTypeInfo(objectCreation).Type as INamedTypeSymbol;
        var fragmentClass = fragmentType?.DeclaringSyntaxReferences
            .Select(reference => reference.GetSyntax())
            .OfType<ClassDeclarationSyntax>()
            .FirstOrDefault();
        if (fragmentType is null || fragmentClass is null)
        {
            context.Report(Diagnostic.Create(
                CompilationErrors.NotSupportedParameter,
                objectCreation.GetLocation()
            ));
            return;
        }

        var fragmentCompilation = context.Compilation.ContainsSyntaxTree(fragmentClass.SyntaxTree)
            ? context.Compilation
            : CompilerUtils.FindCompilation(context.Compilation, fragmentClass.SyntaxTree);
        if (fragmentCompilation is null)
        {
            context.Report(Diagnostic.Create(
                CompilationErrors.NotSupportedParameter,
                objectCreation.GetLocation()
            ));
            return;
        }

        var fragmentContext = fragmentCompilation == context.Compilation
            ? context
            : new ReferencedCompilationContext(context, fragmentCompilation);
        var fragmentId = fragmentClass.ExtractDocumentFileName(
            CompilerUtils.CachedModel(fragmentCompilation, fragmentClass.SyntaxTree));
        var assignments = objectCreation.Initializer?.Expressions
            .OfType<AssignmentExpressionSyntax>()
            .ToDictionary(assignment => assignment.Left.ToString(), assignment => assignment.Right)
            ?? [];
        var policies = new List<XElement>();
        foreach (var property in fragmentType.GetMembers().OfType<IPropertySymbol>())
        {
            if (CompilerUtils.GetFragmentVariableAttribute(property) is not { } attribute)
                continue;

            var variableName = CompilerUtils.GetFragmentVariableName(property, attribute);
            string value;
            if (assignments.TryGetValue(property.Name, out var expression))
            {
                var constant = model.GetConstantValue(expression);
                value = constant is { HasValue: true, Value: null }
                    ? CompilerUtils.GetFragmentVariableFallback(attribute)
                    : expression.ProcessParameter(context);
            }
            else
            {
                value = CompilerUtils.GetFragmentVariableDefaultValue(property, attribute, fragmentContext);
            }

            policies.Add(new XElement("set-variable",
                new XAttribute("name", $"{fragmentId}-{variableName}"),
                new XAttribute("value", value)));
        }

        policies.Add(new XElement("include-fragment", new XAttribute("fragment-id", fragmentId)));
        if (context.CurrentElement.Name.LocalName == "backend" && policies.Count > 1)
        {
            context.AddPolicy(new XElement("choose",
                new XElement("when", new XAttribute("condition", "@(true)"), policies)));
        }
        else
        {
            foreach (var policy in policies)
                context.AddPolicy(policy);
        }
    }
}
