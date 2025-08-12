// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Xml.Linq;

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling.Diagnostics;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling.Syntax;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling;

public class DocumentCompiler
{
    private readonly Lazy<BlockCompiler> _blockCompiler;

    public DocumentCompiler(Lazy<BlockCompiler> blockCompiler)
    {
        _blockCompiler = blockCompiler;
    }

    public IDocumentCompilationResult Compile(Compilation compilation, ClassDeclarationSyntax document)
    {
        var documentType = document.ExtractDocumentType();
        var methods = document.DescendantNodes().OfType<MethodDeclarationSyntax>();
        var rootElement = new XElement(documentType == DocumentType.Fragment ? "fragment" : "policies");
        var context = new DocumentCompilationContext(compilation, document, rootElement);

        if (documentType == DocumentType.Fragment)
            CompileFragment(context, methods);
        else
            CompilePolicy(context, methods);

        return context;
    }

    private void CompilePolicy(DocumentCompilationContext context, IEnumerable<MethodDeclarationSyntax> methods)
    {
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
    }

    private void CompileFragment(DocumentCompilationContext context, IEnumerable<MethodDeclarationSyntax> methods)
    {
        var fragmentMethod = methods.FirstOrDefault(m => m.Identifier.ValueText == "Fragment");

        if (fragmentMethod != null && ValidateMethodBody(fragmentMethod, context))
        {
            _blockCompiler.Value.Compile(context, fragmentMethod.Body!);
        }
    }

    private void CompileSection(DocumentCompilationContext context, string section, MethodDeclarationSyntax method)
    {
        if (!ValidateMethodBody(method, context))
            return;

        var sectionElement = new XElement(section);
        var sectionContext = new DocumentCompilationContext(context, sectionElement);
        _blockCompiler.Value.Compile(sectionContext, method.Body!);
        context.AddPolicy(sectionElement);
    }

    private bool ValidateMethodBody(MethodDeclarationSyntax method, DocumentCompilationContext context)
    {
        if (method.Body is null)
        {
            context.Report(Diagnostic.Create(
                CompilationErrors.PolicySectionCannotBeExpression,
                method.GetLocation(),
                method.Identifier.ValueText
            ));
            return false;
        }
        return true;
    }
}