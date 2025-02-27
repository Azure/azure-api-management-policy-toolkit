// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Reflection;
using System.Xml.Linq;

using Azure.ApiManagement.PolicyToolkit.Authoring;
using Azure.ApiManagement.PolicyToolkit.Compiling.Diagnostics;
using Azure.ApiManagement.PolicyToolkit.Compiling.Policy;
using Azure.ApiManagement.PolicyToolkit.Compiling.Syntax;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Azure.ApiManagement.PolicyToolkit.Compiling;

public class CSharpPolicyCompiler
{
    private ClassDeclarationSyntax _document;

    private BlockCompiler _blockCompiler;

    public CSharpPolicyCompiler(ClassDeclarationSyntax document)
    {
        _document = document;
        var handlers = Assembly.GetExecutingAssembly()
            .GetTypes()
            .Where(type =>
                type is
                {
                    IsClass: true,
                    IsAbstract: false,
                    IsPublic: true,
                    Namespace: "Azure.ApiManagement.PolicyToolkit.Compiling.Policy"
                }
                && typeof(IMethodPolicyHandler).IsAssignableFrom(type))
            .Select(t => Activator.CreateInstance(t) as IMethodPolicyHandler)
            .Where(h => h is not null)!
            .ToArray<IMethodPolicyHandler>();
        var invStatement = new ExpressionStatementCompiler(handlers);
        var loc = new LocalDeclarationStatementCompiler([
            new AuthenticationManageIdentityReturnValueCompiler()
        ]);
        _blockCompiler = new([
            invStatement,
            loc
        ]);
        _blockCompiler.AddCompiler(new IfStatementCompiler(_blockCompiler));
    }

    public ICompilationResult Compile()
    {
        var methods = _document.DescendantNodes()
            .OfType<MethodDeclarationSyntax>();
        var policyDocument = new XElement("policies");
        var context = new CompilationContext(_document, policyDocument);

        foreach (var method in methods)
        {
            var sectionName = method.Identifier.ValueText switch
            {
                nameof(IDocument.Inbound) => "inbound",
                nameof(IDocument.Outbound) => "outbound",
                nameof(IDocument.Backend) => "backend",
                nameof(IDocument.OnError) => "on-error",
                _ => string.Empty
            };

            if (string.IsNullOrEmpty(sectionName))
            {
                continue;
            }

            CompileSection(context, sectionName, method);
        }

        return context;
    }


    private void CompileSection(ICompilationContext context, string section, MethodDeclarationSyntax method)
    {
        if (method.Body is null)
        {
            context.Report(Diagnostic.Create(
                CompilationErrors.PolicySectionCannotBeExpression,
                method.GetLocation(),
                method.Identifier.ValueText
            ));
            return;
        }

        var sectionElement = new XElement(section);
        var sectionContext = new SubCompilationContext(context, sectionElement);
        _blockCompiler.Compile(sectionContext, method.Body);
        context.AddPolicy(sectionElement);
    }
}
