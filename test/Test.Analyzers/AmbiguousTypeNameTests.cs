// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Analyzers;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Analyzers.Test;
using Microsoft.CodeAnalysis;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Test.Analyzers;

[TestClass]
public class AmbiguousTypeNameTests
{
    private static BaseAnalyzerTest<TypeUsedAnalyzer> CreateTest(string source, params DiagnosticResult[] diags)
    {
        var test = new BaseAnalyzerTest<TypeUsedAnalyzer>(source, diags);
        test.TestState.AdditionalReferences.Add(
            MetadataReference.CreateFromFile(Path.Combine(AppContext.BaseDirectory, "Newtonsoft.Json.dll")));
        return test;
    }

    private static DiagnosticResult Ambiguous(int location, string type) =>
        DiagnosticResult.CompilerError(Rules.TypeUsed.AmbiguousTypeName.Id).WithLocation(location)
            .WithArguments("Formatting", type);

    [TestMethod]
    public async Task ShouldReportBareNameInExpressionMethod()
    {
        await CreateTest(
            """
            using Newtonsoft.Json;

            public static class ExpressionLibrary
            {
                [Expression]
                public static string Method(IExpressionContext context)
                {
                    {|#0:Formatting|} formatting = Newtonsoft.Json.Formatting.None;
                    Newtonsoft.Json.Formatting other = global::Newtonsoft.Json.Formatting.None;
                    return context.Request.Body.As<Newtonsoft.Json.Linq.JObject>().ToString({|#1:Formatting|}.Indented)
                        + formatting + other;
                }

                public static Formatting NotAnExpression() => Formatting.Indented;
            }
            """,
            Ambiguous(0, "Newtonsoft.Json.Formatting"),
            Ambiguous(1, "Newtonsoft.Json.Formatting")
        ).RunAsync();
    }

    [TestMethod]
    public async Task ShouldReportBareNameInExpressionLambdaAndNotOutsideIt()
    {
        await CreateTest(
            """
            using Newtonsoft.Json;

            public static class ExpressionLibrary
            {
                static void Use(Expression<string> expression) { }

                static void Run()
                {
                    var outside = Formatting.Indented;
                    Use(context => context.Request.Body.As<Newtonsoft.Json.Linq.JObject>().ToString({|#0:Formatting|}.None));
                }
            }
            """,
            Ambiguous(0, "Newtonsoft.Json.Formatting")
        ).RunAsync();
    }

    [TestMethod]
    public async Task ShouldReportTheBareNameOfSystemXml()
    {
        await CreateTest(
            """
            using System.Xml;

            public static class ExpressionLibrary
            {
                [Expression]
                public static string Method(IExpressionContext context)
                {
                    {|#0:Formatting|} formatting = default;
                    return "" + formatting;
                }
            }
            """,
            Ambiguous(0, "System.Xml.Formatting")
        ).RunAsync();
    }

    [TestMethod]
    public async Task ShouldNotReportTheQualifiedNameOfSystemXml()
    {
        await CreateTest(
            """
            public static class ExpressionLibrary
            {
                [Expression]
                public static string Method(IExpressionContext context) =>
                    System.Xml.Formatting.Indented.ToString();
            }
            """
        ).RunAsync();
    }

    [TestMethod]
    public async Task ShouldNotReportANameQualifiedWithANamespaceAlias()
    {
        // the compiler writes the alias out as the namespace it stands for
        await CreateTest(
            """
            using N = Newtonsoft.Json;

            public static class ExpressionLibrary
            {
                [Expression]
                public static string Method(IExpressionContext context) =>
                    context.Request.Body.As<Newtonsoft.Json.Linq.JObject>().ToString(N::Formatting.Indented);
            }
            """
        ).RunAsync();
    }

    [TestMethod]
    public async Task ShouldNotReportAnAliasOrAnotherTypeOfThatName()
    {
        // the compiler writes a using alias out as the name it stands for
        var test = CreateTest(
            """
            using Formatting = Newtonsoft.Json.Formatting;

            public static class ExpressionLibrary
            {
                [Expression]
                public static string Method(IExpressionContext context) =>
                    context.Request.Body.As<Newtonsoft.Json.Linq.JObject>().ToString(Formatting.Indented);
            }
            """,
            Ambiguous(0, "Newtonsoft.Json.Formatting"));
        test.TestState.Sources.Add(
            """
            using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
            using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring.Expressions;
            using Newtonsoft.Json;

            namespace Mielek.Other
            {
                public static class Bare
                {
                    [Expression]
                    public static string Method(IExpressionContext context)
                    {
                        {|#0:Formatting|} formatting = default;
                        return "" + formatting;
                    }
                }
            }

            namespace Mielek.Own
            {
                public enum Formatting { Plain }

                public static class Library
                {
                    [Expression]
                    public static string Method(IExpressionContext context)
                    {
                        Formatting formatting = default;
                        return "" + formatting;
                    }
                }
            }
            """);
        await test.RunAsync();
    }
}
