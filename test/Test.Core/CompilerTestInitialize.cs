// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Xml.Linq;

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling;
using Microsoft.Azure.ApiManagement.PolicyToolkit.IoC;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Tests.Extensions;

[TestClass]
public static class CompilerTestInitialize
{
    private static readonly IEnumerable<MetadataReference> References =
    [
        MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
        MetadataReference.CreateFromFile(typeof(XElement).Assembly.Location),
        MetadataReference.CreateFromFile(typeof(IDocument).Assembly.Location)
    ];

    private static ServiceProvider s_serviceProvider = null!;
    private static DocumentCompiler s_compiler = null!;

    [AssemblyInitialize]
    public static void CompilerInitialize(TestContext testContext)
    {
        ServiceCollection serviceCollection = new();
        s_serviceProvider = serviceCollection
            .SetupCompiler()
            .BuildServiceProvider();
        s_compiler = s_serviceProvider.GetRequiredService<DocumentCompiler>();
    }

    [AssemblyCleanup]
    public static void CompilerCleanup()
    {
        s_serviceProvider.Dispose();
    }

    public static IDocumentCompilationResult CompileDocument(this string document)
    {
        var doc = $"""
                   using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
                   using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring.Expressions;

                   namespace Test;

                   {document}
                   """;

        SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(doc);
        var compilation = CSharpCompilation.Create(
            Guid.NewGuid().ToString(),
            syntaxTrees: [syntaxTree],
            references: References);
        var semantics = compilation.GetSemanticModel(syntaxTree);
        ClassDeclarationSyntax policy = syntaxTree
            .GetRoot()
            .DescendantNodes()
            .OfType<ClassDeclarationSyntax>()
            .First(c => c.AttributeLists.ContainsAttributeOfType<DocumentAttribute>(semantics));

        return s_compiler.Compile(compilation, policy);
    }
}