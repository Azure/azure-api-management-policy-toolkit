// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Analyzers;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Analyzers.Test;
using Microsoft.CodeAnalysis;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Test.Analyzers;

[TestClass]
public class TypeUsedAllowListTests
{
    public static Task VerifyAsync(string source, params DiagnosticResult[] diags)
    {
        var test = new BaseAnalyzerTest<TypeUsedAnalyzer>(source, diags);
        test.TestState.AdditionalReferences.Add(
            MetadataReference.CreateFromFile(Path.Combine(AppContext.BaseDirectory, "Newtonsoft.Json.dll")));
        return test.RunAsync();
    }

    private static DiagnosticResult DisallowedType(int location, string type) =>
        DiagnosticResult.CompilerError(Rules.TypeUsed.DisallowedType.Id).WithLocation(location).WithArguments(type);

    [TestMethod]
    public async Task ShouldAllowNullableMembers()
    {
        await VerifyAsync(
            """
            public static class ExpressionLibrary
            {
                [Expression]
                public static int Method(IExpressionContext context)
                {
                    int? length = context.Request.Body?.As<string>().Length;
                    System.DateTime? time = null;
                    return length.HasValue && !time.HasValue ? length.Value : length.GetValueOrDefault(1);
                }
            }
            """
        );
    }

    [TestMethod]
    public async Task ShouldAllowAnonymousTypeMembers()
    {
        await VerifyAsync(
            """
            public static class ExpressionLibrary
            {
                [Expression]
                public static int Method(IExpressionContext context)
                {
                    var value = new { a = 1, b = context.Request.Method };
                    return value.a + value.b.Length;
                }
            }
            """
        );
    }

    [TestMethod]
    public async Task ShouldReportMembersOfObject()
    {
        await VerifyAsync(
            """
            public static class ExpressionLibrary
            {
                [Expression]
                public static string Method(IExpressionContext context)
                {
                    object value = context.Variables["x"];
                    var text = {|#0:value.ToString()|} + {|#1:context.User.ToString()|};
                    return text + context.Request.Url.ToString() + context.RequestId.ToString() + (value == null);
                }
            }
            """,
            DiagnosticResult.CompilerError(Rules.TypeUsed.DisallowedMember.Id).WithLocation(0).WithArguments("ToString"),
            DiagnosticResult.CompilerError(Rules.TypeUsed.DisallowedMember.Id).WithLocation(1).WithArguments("ToString")
        );
    }

    [TestMethod]
    public async Task ShouldReportTypeNameAmbiguousInApiManagement()
    {
        await VerifyAsync(
            """
            using Newtonsoft.Json;

            public static class ExpressionLibrary
            {
                [Expression]
                public static string Method(IExpressionContext context)
                {
                    {|#0:Formatting|} formatting = Newtonsoft.Json.Formatting.None;
                    return context.Request.Body.As<Newtonsoft.Json.Linq.JObject>().ToString({|#1:Formatting|}.Indented)
                        + formatting;
                }

                public static Formatting NotAnExpression() => Formatting.Indented;
            }
            """,
            DiagnosticResult.CompilerError(Rules.TypeUsed.AmbiguousTypeName.Id).WithLocation(0)
                .WithArguments("Formatting", "Newtonsoft.Json.Formatting"),
            DiagnosticResult.CompilerError(Rules.TypeUsed.AmbiguousTypeName.Id).WithLocation(1)
                .WithArguments("Formatting", "Newtonsoft.Json.Formatting")
        );
    }

    [TestMethod]
    public async Task ShouldNotReportAnAliasForAnAmbiguousTypeName()
    {
        // the compiler writes a using alias out as the name it stands for
        await VerifyAsync(
            """
            using Formatting = Newtonsoft.Json.Formatting;

            public static class ExpressionLibrary
            {
                [Expression]
                public static string Method(IExpressionContext context) =>
                    context.Request.Body.As<Newtonsoft.Json.Linq.JObject>().ToString(Formatting.Indented);
            }
            """
        );
    }

    [TestMethod]
    public async Task ShouldNotReportTypeThatIsOnlyNamed()
    {
        // the gateway checks the members an expression uses: a cast, as, is or typeof naming another type is accepted
        await VerifyAsync(
            """
            public static class ExpressionLibrary
            {
                [Expression]
                public static bool Method(IExpressionContext context)
                {
                    return (System.IO.Stream)null == null &&
                           (context.Variables["x"] as System.Threading.Thread) == null &&
                           typeof(System.IO.File) != null;
                }
            }
            """
        );
    }

    [TestMethod]
    public async Task ShouldAllowTupleMembers()
    {
        await VerifyAsync(
            """
            public static class ExpressionLibrary
            {
                [Expression]
                public static string Method(IExpressionContext context)
                {
                    var pair = System.Tuple.Create(1, "a");
                    var triple = new System.Tuple<int, string, bool>(pair.Item1, pair.Item2, true);
                    return triple.Item2 + triple.Item3.ToString();
                }
            }
            """
        );
    }

    [TestMethod]
    public async Task ShouldAllowTrace()
    {
        await VerifyAsync(
            """
            public static class ExpressionLibrary
            {
                [Expression]
                public static string Method(IExpressionContext context)
                {
                    context.Trace("message");
                    return "";
                }
            }
            """
        );
    }

    [TestMethod]
    public async Task ShouldReportInvocationOfDelegateLocal()
    {
        await VerifyAsync(
            """
            public static class ExpressionLibrary
            {
                [Expression]
                public static string Method(IExpressionContext context)
                {
                    System.Func<string, string> upper = value => value.ToUpper();
                    return {|#0:upper("a")|};
                }
            }
            """,
            DisallowedType(0, "System.Func<T, TResult>")
        );
    }

    [TestMethod]
    public async Task ShouldAllowProductStateAndAuthorization()
    {
        await VerifyAsync(
            """
            public static class ExpressionLibrary
            {
                [Expression]
                public static string Method(IExpressionContext context)
                {
                    if (context.Product.State == ProductState.Published)
                    {
                        return context.Product.State.ToString();
                    }

                    var authorization = (Authorization)context.Variables["auth"];
                    return authorization.AccessToken + authorization.Claims.Count;
                }
            }
            """
        );
    }
}
