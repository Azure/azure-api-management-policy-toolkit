// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.ApiManagement.PolicyToolkit.Compiling;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Extensions.DependencyInjection;

namespace Azure.ApiManagement.PolicyToolkit.Tests.Extensions;

[TestClass]
public static class CompilerTestInitialize
{
    private static ServiceProvider s_serviceProvider;
    private static CSharpPolicyCompiler s_compiler;

    [AssemblyInitialize]
    public static void CompilerInitialize(TestContext testContext)
    {
        ServiceCollection serviceCollection = new();
        s_serviceProvider = serviceCollection
            .SetupCompiler()
            .BuildServiceProvider();
        s_compiler = s_serviceProvider.GetRequiredService<CSharpPolicyCompiler>();
    }

    [AssemblyCleanup]
    public static void CompilerCleanup()
    {
        s_serviceProvider.Dispose();
    }

    public static ICompilationResult CompileDocument(this string document)
    {
        SyntaxTree code = CSharpSyntaxTree.ParseText(document);
        ClassDeclarationSyntax policy = code
            .GetRoot()
            .DescendantNodes()
            .OfType<ClassDeclarationSyntax>()
            .First(c => c.AttributeLists.ContainsAttributeOfType("Document"));

        return s_compiler.Compile(policy);
    }
}