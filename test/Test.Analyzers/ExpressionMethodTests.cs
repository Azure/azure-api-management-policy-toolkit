// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Analyzers;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Analyzers.Test;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Test.Analyzers;

[TestClass]
public class ExpressionDefinitionTests
{
    public static Task VerifyAsync(string source, params DiagnosticResult[] diags)
    {
        return new BaseAnalyzerTest<ExpressionDefinitionAnalyzer>(source, diags).RunAsync();
    }

    [TestMethod]
    public async Task ShouldReportWrongReturnType()
    {
        await VerifyAsync(
            """
            class Test
            {
                [Expression]
                void Method(IExpressionContext context)
                {
                    return;
                }
            }
            """,
            DiagnosticResult
                .CompilerError(Rules.Expression.ReturnTypeNotAllowed.Id)
                .WithSpan(9, 5, 9, 9)
                .WithArguments(typeof(void).FullName)
        );
    }

    [TestMethod]
    [DataRow("", 0)]
    [DataRow("IExpressionContext c1, IExpressionContext c2", 2)]
    [DataRow("IExpressionContext c1, IExpressionContext c2, IExpressionContext c3", 3)]
    public async Task ShouldReportWrongParameterCount(string parameters, int count)
    {
        await VerifyAsync(
            $$"""
              class Test
              {
                  [Expression]
                  int Method({{parameters}})
                  {
                      return 10;
                  }
              }
              """,
            DiagnosticResult
                .CompilerError(Rules.Expression.WrongParameterCount.Id)
                .WithSpan(9, 15, 9, 15 + parameters.Length + 2)
                .WithArguments(count)
        );
    }
}