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
        var semanticModel = compilation.GetSemanticModel(document.SyntaxTree);
        var documentType = document.ExtractDocumentType(semanticModel);
        // Only the document's own section methods: not those of nested classes, nor overloads taking anything
        // other than the section context, which would otherwise each become another section.
        var methods = document.Members.OfType<MethodDeclarationSyntax>()
            .Where(method => TakesSectionContext(method, semanticModel));
        var rootElement = new XElement(documentType == DocumentType.Fragment ? "fragment" : "policies");
        var context = new DocumentCompilationContext(compilation, document, rootElement);
        document.ValidateDocumentName(semanticModel, context);

        if (documentType == DocumentType.Fragment)
            CompileFragment(context, methods);
        else
            CompilePolicy(context, methods);

        return context;
    }

    // A section takes one section context of the authoring library: an overload such as Inbound(int) isn't one.
    // The name as written decides only when the type can't be resolved.
    private static bool TakesSectionContext(MethodDeclarationSyntax method, SemanticModel model)
    {
        if (method.ParameterList.Parameters is not [{ Type: { } type }])
        {
            return false;
        }

        if (model.GetTypeInfo(type).Type is { TypeKind: not TypeKind.Error } symbol)
        {
            return PolicyExpressionCompiler.IsAuthoringSectionContext(symbol);
        }

        var name = type switch
        {
            QualifiedNameSyntax qualified => qualified.Right,
            AliasQualifiedNameSyntax aliased => aliased.Name,
            _ => type as SimpleNameSyntax
        };
        return name is not null && PolicyExpressionCompiler.IsSectionContextName(name.Identifier.ValueText);
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

        // The gateway rejects a backend section with more than one top-level policy, <base /> included.
        var policyCount = sectionElement.Elements().Count();
        if (section == "backend" && policyCount > 1)
        {
            context.Report(Diagnostic.Create(
                CompilationErrors.BackendAllowsOnePolicy,
                method.Identifier.GetLocation(),
                policyCount));
        }

        // The gateway accepts these once per section, also when they are in different branches of a choose.
        foreach (var policy in OncePerSection)
        {
            var count = sectionElement.Descendants(policy).Count();
            if (count > 1)
            {
                context.Report(Diagnostic.Create(
                    CompilationErrors.PolicyAllowedOncePerSection,
                    method.Identifier.GetLocation(),
                    policy,
                    count));
            }
        }

        context.AddPolicy(sectionElement);
    }

    private static readonly string[] OncePerSection = ["cors", "quota", "rate-limit"];

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