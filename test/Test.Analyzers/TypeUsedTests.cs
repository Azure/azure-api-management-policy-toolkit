// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Basic.Reference.Assemblies;

using Microsoft.Azure.ApiManagement.PolicyToolkit.Analyzers;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Analyzers.Test;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Test.Analyzers;

[TestClass]
public class TypeUsedTests
{
    public static Task VerifyAsync(string source, params DiagnosticResult[] diags)
    {
        return new BaseAnalyzerTest<TypeUsedAnalyzer>(source, diags).RunAsync();
    }

    [TestMethod]
    public async Task Should()
    {
        await VerifyAsync(
            """
            public static class ExpressionLibrary
            {
                [Expression]
                public static string Method(IExpressionContext context)
                { 
                    if(context.Request.Headers.TryGetValue("Authorization", out var value))
                    {
                        return value[0];
                    } else 
                    {
                        return "";
                    }
                }

                public static string Good(IExpressionContext context)
                { 
                    return "test".GetType().FullName;
                }
            }
            """
        );
    }

    [TestMethod]
    public async Task ShouldReportDisallowedTypeInExpressionLambda()
    {
        await VerifyAsync(
            """
            public static class ExpressionLibrary
            {
                static void Use(Expression<string> expression) { }

                static void Run()
                {
                    Use(context => System.IO.File.ReadAllText("path"));
                }
            }
            """,
            DiagnosticResult
                .CompilerError(Rules.TypeUsed.DisallowedType.Id)
                .WithSpan(12, 24, 12, 58)
                .WithArguments("System.IO.File")
        );
    }

    [TestMethod]
    public async Task ShouldReportDisallowedMethodCallOnce()
    {
        await VerifyAsync(
            """
            public static class ExpressionLibrary
            {
                [Expression]
                public static string Method(IExpressionContext context) => System.IO.File.ReadAllText("path");
            }
            """,
            DiagnosticResult
                .CompilerError(Rules.TypeUsed.DisallowedType.Id)
                .WithSpan(9, 64, 9, 98)
                .WithArguments("System.IO.File")
        );
    }

    [TestMethod]
    public async Task ShouldAllowSourceConstants()
    {
        await VerifyAsync(
            """
            public static class Names
            {
                public const string Header = "x-id";
            }

            public static class ExpressionLibrary
            {
                private const string Default = "none";

                [Expression]
                public static string Method(IExpressionContext context)
                    => context.Request.Headers.GetValueOrDefault(Names.Header, Default);
            }
            """
        );
    }

    [TestMethod]
    public async Task ShouldReportSourceEnumMember()
    {
        await VerifyAsync(
            """
            public enum Mode { A, B }

            public static class ExpressionLibrary
            {
                [Expression]
                public static bool Method(IExpressionContext context)
                    => context.Variables.Count == (int)Mode.A;
            }
            """,
            DiagnosticResult
                .CompilerError(Rules.TypeUsed.DisallowedType.Id)
                .WithSpan(12, 44, 12, 50)
                .WithArguments("Mielek.Test.Mode")
        );
    }

    [TestMethod]
    public async Task ShouldReportExpressionHelperMethodGroup()
    {
        await VerifyAsync(
            """
            public static class ExpressionLibrary
            {
                [Expression]
                public static bool IsX(IExpressionContext context) => true;

                [Expression]
                public static System.Predicate<IExpressionContext> Method(IExpressionContext context) => ExpressionLibrary.IsX;
            }
            """,
            DiagnosticResult
                .CompilerError(Rules.TypeUsed.DisallowedType.Id)
                .WithSpan(12, 94, 12, 115)
                .WithArguments("Mielek.Test.ExpressionLibrary")
        );
    }

    [TestMethod]
    public async Task ShouldReportConstantOfSourceEnumType()
    {
        await VerifyAsync(
            """
            public enum Mode { A, B }

            public static class ExpressionLibrary
            {
                private const Mode Current = Mode.B;

                [Expression]
                public static string Method(IExpressionContext context) => ExpressionLibrary.Current.ToString();
            }
            """,
            DiagnosticResult
                .CompilerError(Rules.TypeUsed.DisallowedType.Id)
                .WithSpan(13, 64, 13, 89)
                .WithArguments("Mielek.Test.ExpressionLibrary")
        );
    }

    [TestMethod]
    public async Task ShouldAllowExpressionHelperFromReferencedAssembly()
    {
        var library = CSharpCompilation.Create(
            "Library",
            [
                CSharpSyntaxTree.ParseText(
                    """
                    using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
                    using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring.Expressions;

                    namespace Lib;

                    public static class Helpers
                    {
                        [Expression]
                        public static bool IsAdmin(IExpressionContext context) => context.User.Id == "admin";
                    }
                    """)
            ],
            [.. Net80.References.All, MetadataReference.CreateFromFile(typeof(ExpressionAttribute).Assembly.Location)],
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        using var image = new MemoryStream();
        Assert.IsTrue(library.Emit(image).Success);

        var test = new BaseAnalyzerTest<TypeUsedAnalyzer>(
            """
            public static class ExpressionLibrary
            {
                [Expression]
                public static bool Method(IExpressionContext context)
                    => !Lib.Helpers.IsAdmin(context);
            }
            """);
        test.TestState.AdditionalReferences.Add(MetadataReference.CreateFromImage(image.ToArray()));
        await test.RunAsync();
    }

    [TestMethod]
    public async Task ShouldAllowHelperInExpressionLibrary()
    {
        await VerifyAsync(
            """
            public static class ExpressionLibrary
            {
                [Expression]
                public static string Method(IExpressionContext context) => Headers.Pick(context, "abc").ToUpper();
            }

            [Expression]
            public static class Headers
            {
                public static string Pick(IExpressionContext context, string name)
                    => context.Request.Headers.GetValueOrDefault(name, "");
            }
            """
        );
    }

    [TestMethod]
    public async Task ShouldAnalyseUnattributedPartOfExpressionLibrary()
    {
        await VerifyAsync(
            """
            [Expression]
            public static partial class Helpers
            {
            }

            public static partial class Helpers
            {
                public static string Secret() => System.IO.File.ReadAllText("secret");
            }
            """,
            DiagnosticResult
                .CompilerError(Rules.TypeUsed.DisallowedType.Id)
                .WithSpan(13, 38, 13, 74)
                .WithArguments("System.IO.File")
        );
    }

    [TestMethod]
    public async Task ShouldAllowExpressionLibraryMembersFromReferencedAssembly()
    {
        var library = CSharpCompilation.Create(
            "Library",
            [
                CSharpSyntaxTree.ParseText(
                    """
                    using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;

                    namespace Lib;

                    [Expression]
                    public static class Text
                    {
                        public const string Header = "X-Tenant";

                        public static string Tidy(this string value) => value.Trim().ToLowerInvariant();
                    }
                    """)
            ],
            [.. Net80.References.All, MetadataReference.CreateFromFile(typeof(ExpressionAttribute).Assembly.Location)],
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        using var image = new MemoryStream();
        Assert.IsTrue(library.Emit(image).Success);

        var test = new BaseAnalyzerTest<TypeUsedAnalyzer>(
            """
            using Lib;

            public static class ExpressionLibrary
            {
                [Expression]
                public static string Method(IExpressionContext context)
                    => context.Request.Headers.GetValueOrDefault(Lib.Text.Header, "").Tidy();
            }
            """);
        test.TestState.AdditionalReferences.Add(MetadataReference.CreateFromImage(image.ToArray()));
        await test.RunAsync();
    }

    [TestMethod]
    public async Task ShouldReportSourceStaticReadonlyField()
    {
        await VerifyAsync(
            """
            public static class Names
            {
                public static readonly string Header = "x-id";
            }

            public static class ExpressionLibrary
            {
                [Expression]
                public static bool Method(IExpressionContext context)
                    => context.Request.Headers.ContainsKey(Names.Header);
            }
            """,
            DiagnosticResult
                .CompilerError(Rules.TypeUsed.DisallowedType.Id)
                .WithSpan(15, 48, 15, 60)
                .WithArguments("Mielek.Test.Names")
        );
    }

    [TestMethod]
    public async Task ShouldAllowExpressionHelperCalls()
    {
        await VerifyAsync(
            """
            public static class ExpressionLibrary
            {
                [Expression]
                public static bool IsAdmin(IExpressionContext context) => context.User.Id == "admin";

                [Expression]
                public static bool Method(IExpressionContext context) => !IsAdmin(context);
            }
            """
        );
    }

    [TestMethod]
    public async Task ShouldReportNonExpressionHelperCalls()
    {
        await VerifyAsync(
            """
            public static class ExpressionLibrary
            {
                public static bool IsAdmin(IExpressionContext context) => context.User.Id == "admin";

                [Expression]
                public static bool Method(IExpressionContext context) => !IsAdmin(context);
            }
            """,
            DiagnosticResult
                .CompilerError(Rules.TypeUsed.DisallowedType.Id)
                .WithSpan(11, 63, 11, 79)
                .WithArguments("Mielek.Test.ExpressionLibrary")
        );
    }

}
