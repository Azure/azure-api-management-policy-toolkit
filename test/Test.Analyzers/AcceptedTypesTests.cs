// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Analyzers;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Analyzers.Test;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Test.Analyzers;

[TestClass]
public class AcceptedTypesTests
{
    private static Task VerifyAsync(string source, params DiagnosticResult[] diags) =>
        new BaseAnalyzerTest<TypeUsedAnalyzer>(source, diags).RunAsync();

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
    public async Task ShouldAllowTraceButNotInvokingADelegateLocal()
    {
        await VerifyAsync(
            """
            public static class ExpressionLibrary
            {
                [Expression]
                public static string Method(IExpressionContext context)
                {
                    context.Trace("message");
                    System.Func<string, string> upper = value => value.ToUpper();
                    return {|#0:upper("a")|};
                }
            }
            """,
            DiagnosticResult.CompilerError(Rules.TypeUsed.DisallowedType.Id).WithLocation(0)
                .WithArguments("System.Func<T, TResult>")
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

    [TestMethod]
    public async Task ShouldStillReportAGenericTypeThatIsNotListed()
    {
        // only the listed names lose their type parameters; a disallowed type argument changes nothing, and nor does
        // the name of a generic type that is listed with its type parameters
        await VerifyAsync(
            """
            public static class ExpressionLibrary
            {
                [Expression]
                public static int Method(IExpressionContext context)
                {
                    var pair = System.Tuple.Create(1, "a");
                    var seen = new System.Collections.Generic.List<string>();
                    var linked = {|#0:new System.Collections.Generic.LinkedList<string>()|};
                    return pair.Item1 + seen.Count;
                }
            }
            """,
            DiagnosticResult.CompilerError(Rules.TypeUsed.DisallowedType.Id).WithLocation(0)
                .WithArguments("System.Collections.Generic.LinkedList<T>")
        );
    }
}
