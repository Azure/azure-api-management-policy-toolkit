// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Analyzers;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Analyzers.Test;

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
}