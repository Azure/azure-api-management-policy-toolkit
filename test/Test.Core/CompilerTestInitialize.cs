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
        // the authoring library refers to the types of its members through System.Runtime
        MetadataReference.CreateFromFile(
            Path.Combine(Path.GetDirectoryName(typeof(object).Assembly.Location)!, "System.Runtime.dll")),
        MetadataReference.CreateFromFile(typeof(XElement).Assembly.Location),
        MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location),
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

    /// <summary>A policy document whose inbound section holds the statements.</summary>
    public static string InboundDocument(string statements, string members = "") =>
        $$"""
          [Document]
          public class PolicyDocument : IDocument
          {
              public void Inbound(IInboundContext context)
              {
                  {{statements}}
              }

              {{members}}
          }
          """;

    /// <summary>The policy XML of a document whose inbound section holds the policy.</summary>
    public static string InboundXml(string policy) =>
        $"""
         <policies>
             <inbound>
                 {policy.ReplaceLineEndings("\n        ")}
             </inbound>
         </policies>
         """;

    public static IDocumentCompilationResult CompileDocument(this string document) => document.CompileDocument([]);

    public static IDocumentCompilationResult CompileDocument(this string document, params string[] separateDocuments)
    {
        string[] docs = [document, ..separateDocuments];
        var syntaxTrees = docs.Select(d =>
                $"""
                 using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
                 using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring.Expressions;

                 namespace Test;

                 {d}
                 """)
            .Select(d => CSharpSyntaxTree.ParseText(d))
            .ToArray();

        var compilation = CSharpCompilation.Create(
            Guid.NewGuid().ToString(),
            syntaxTrees: syntaxTrees,
            references: References);
        var semantics = compilation.GetSemanticModel(syntaxTrees[0]);
        ClassDeclarationSyntax policy = syntaxTrees[0]
            .GetRoot()
            .DescendantNodes()
            .OfType<ClassDeclarationSyntax>()
            .First(c => c.AttributeLists.ContainsAttributeOfType<DocumentAttribute>(semantics));

        return s_compiler.Compile(compilation, policy);
    }
}