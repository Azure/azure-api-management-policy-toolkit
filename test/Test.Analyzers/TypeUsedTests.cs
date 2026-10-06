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
            [.. Net100.References.All, MetadataReference.CreateFromFile(typeof(ExpressionAttribute).Assembly.Location)],
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
            [.. Net100.References.All, MetadataReference.CreateFromFile(typeof(ExpressionAttribute).Assembly.Location)],
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

    [TestMethod]
    public async Task ShouldAnalyseUnattributedHelpersOfDocument()
    {
        await VerifyAsync(
            """
            public class Document : IDocument
            {
                public void Inbound(IInboundContext context)
                {
                    context.SetHeader("X-Tenant", Tenant(context.ExpressionContext));
                    context.SetHeader("X-Host", Host());
                    context.SetHeader("X-Name", Limits().ToString());
                    System.Environment.GetEnvironmentVariable("NotAnExpression");
                }

                // called from a section without the expression context: expanded all the same
                static string Host() => {|#3:System.Environment.MachineName|};

                // nothing of the document calls these, so they never become a policy expression
                public override string ToString() => System.Environment.MachineName;

                public static Document Create() => new Document();

                static string Unused(string value) => value + System.Environment.NewLine;

                static string Tenant(IExpressionContext context) =>
                    Tidy(context.Request.Headers.GetValueOrDefault("X-Tenant", "")) + {|#0:System.Environment.MachineName|};

                static string Tidy(string value) => Text.Trim(value) + Nested.Line() + {|#1:System.Environment.NewLine|};

                static RateLimitConfig Limits() => new RateLimitConfig { Calls = 1, RenewalPeriod = 1 };

                static RateLimitConfig Limits(IInboundContext context) =>
                    new RateLimitConfig { Calls = 1, RenewalPeriod = System.Environment.ProcessorCount };

                static class Nested
                {
                    public static string Line() => {|#2:System.Environment.NewLine|};
                }
            }

            public static class Text
            {
                public static string Trim(string value) => value.Trim();
            }
            """,
            DiagnosticResult.CompilerError(Rules.TypeUsed.DisallowedType.Id).WithLocation(0)
                .WithArguments("System.Environment"),
            DiagnosticResult.CompilerError(Rules.TypeUsed.DisallowedType.Id).WithLocation(1)
                .WithArguments("System.Environment"),
            DiagnosticResult.CompilerError(Rules.TypeUsed.DisallowedType.Id).WithLocation(2)
                .WithArguments("System.Environment"),
            DiagnosticResult.CompilerError(Rules.TypeUsed.DisallowedType.Id).WithLocation(3)
                .WithArguments("System.Environment")
        );
    }

    [TestMethod]
    public async Task ShouldFollowCallsToDocumentHelpersHoweverTheyAreWritten()
    {
        await VerifyAsync(
            """
            using N = Mielek.Test.Document.Nested;

            public abstract class BaseDocument<T> : IDocument
            {
                // expanded where a derived document calls it
                protected static string Host() => {|#0:System.Environment.MachineName|};

                protected string Name() => {|#4:System.Environment.MachineName|};
            }

            public class Document : BaseDocument<int>
            {
                public void Inbound(IInboundContext context)
                {
                    context.SetHeader("X-Host", Host());
                    context.SetHeader("X-Name", base.Name());
                    context.SetHeader("X-Paren", (this).Paren());
                    context.SetHeader("X-Cast", ((Document)this).Cast());
                    context.SetHeader("X-Line", N.Line());
                    context.SetHeader("X-This", this.Instance());
                    context.SetHeader("X-Full", global::Mielek.Test.Document.Nested.Full());
                    context.SetHeader("X-Key", Has(context.ExpressionContext));
                }

                string Instance() => {|#2:System.Environment.MachineName|};

                string Paren() => {|#5:System.Environment.MachineName|};

                string Cast() => {|#6:System.Environment.MachineName|};

                static string Has(IExpressionContext context) => context.Request.Headers.ContainsKey("a") ? "a" : "b";

                public static class Nested
                {
                    public static string Line() => {|#1:System.Environment.NewLine|};

                    public static string Full() => {|#3:System.Environment.NewLine|};
                }

                // context.Request.Headers.ContainsKey(...) is a call on a value, not on this class
                public static class Headers
                {
                    public static bool ContainsKey(string key) => System.Environment.Is64BitProcess;
                }
            }
            """,
            DiagnosticResult.CompilerError(Rules.TypeUsed.DisallowedType.Id).WithLocation(0)
                .WithArguments("System.Environment"),
            DiagnosticResult.CompilerError(Rules.TypeUsed.DisallowedType.Id).WithLocation(1)
                .WithArguments("System.Environment"),
            DiagnosticResult.CompilerError(Rules.TypeUsed.DisallowedType.Id).WithLocation(2)
                .WithArguments("System.Environment"),
            DiagnosticResult.CompilerError(Rules.TypeUsed.DisallowedType.Id).WithLocation(3)
                .WithArguments("System.Environment"),
            DiagnosticResult.CompilerError(Rules.TypeUsed.DisallowedType.Id).WithLocation(4)
                .WithArguments("System.Environment"),
            DiagnosticResult.CompilerError(Rules.TypeUsed.DisallowedType.Id).WithLocation(5)
                .WithArguments("System.Environment"),
            DiagnosticResult.CompilerError(Rules.TypeUsed.DisallowedType.Id).WithLocation(6)
                .WithArguments("System.Environment")
        );
    }

    [TestMethod]
    public async Task ShouldFollowACallQualifiedWithTheClassADocumentIsNestedIn()
    {
        await VerifyAsync(
            """
            public static class Outer
            {
                public class Document : IDocument
                {
                    public void Inbound(IInboundContext context)
                    {
                        context.SetHeader("X-Line", Outer.Document.Nested.Line());
                    }

                    public static class Nested
                    {
                        public static string Line() => {|#0:System.Environment.NewLine|};
                    }
                }
            }
            """,
            DiagnosticResult.CompilerError(Rules.TypeUsed.DisallowedType.Id).WithLocation(0)
                .WithArguments("System.Environment")
        );
    }

    [TestMethod]
    public async Task ShouldAllowDictionaryExtensions()
    {
        await VerifyAsync(
            """
            public static class ExpressionLibrary
            {
                [Expression]
                public static string Header(IExpressionContext context)
                    => context.Request.Headers.GetValueOrDefault("Authorization", "");

                [Expression]
                public static string Query(IExpressionContext context)
                    => context.Request.Body.AsFormUrlEncodedContent().ToQueryString();
            }
            """
        );
    }

    [TestMethod]
    public async Task ShouldAllowJwtAndBasicAuthExtensions()
    {
        await VerifyAsync(
            """
            public static class ExpressionLibrary
            {
                [Expression]
                public static bool IsAdmin(IExpressionContext context)
                {
                    var jwt = context.Request.Headers.GetValueOrDefault("Authorization", "").AsJwt();
                    return jwt != null && jwt.Claims.GetValueOrDefault("role", "") == "admin";
                }

                [Expression]
                public static string User(IExpressionContext context)
                    => context.Request.Headers.GetValueOrDefault("Authorization", "").TryParseBasic(out var credentials)
                        ? credentials.Password
                        : "";
            }
            """
        );
    }

    [TestMethod]
    public async Task ShouldAllowRegexGroupCollectionMembers()
    {
        await VerifyAsync(
            """
            public static class ExpressionLibrary
            {
                [Expression]
                public static string Method(IExpressionContext context)
                {
                    var match = System.Text.RegularExpressions.Regex.Match(context.Request.Url.Path, "(?<id>[0-9]+)");
                    return match.Groups.Count > 1 ? match.Groups[1].Value + match.Groups["id"].Value : "";
                }
            }
            """
        );
    }

    [TestMethod]
    public async Task ShouldReportDisallowedGroupCollectionMember()
    {
        await VerifyAsync(
            """
            public static class ExpressionLibrary
            {
                [Expression]
                public static object Method(IExpressionContext context)
                    => System.Text.RegularExpressions.Regex.Match(context.Request.Url.Path, "[0-9]+").Groups.Keys;
            }
            """,
            DiagnosticResult
                .CompilerError(Rules.TypeUsed.DisallowedMember.Id)
                .WithSpan(10, 12, 10, 102)
                .WithArguments("Keys")
        );
    }

    [TestMethod]
    public async Task ShouldAllowNamespaceQualifiedMemberAccess()
    {
        await VerifyAsync(
            """
            public static class ExpressionLibrary
            {
                [Expression]
                public static string Method(IExpressionContext context) => System.String.Empty;
            }
            """
        );
    }
}
