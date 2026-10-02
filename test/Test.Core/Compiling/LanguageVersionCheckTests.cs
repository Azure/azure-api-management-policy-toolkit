// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using static Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling.PolicyExpressionTestHelpers;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling;

[TestClass]
public class LanguageVersionCheckTests
{
    [TestMethod]
    [DataRow("return context.Variables.Count switch { 0 => \"none\", _ => \"some\" };",
        DisplayName = "Should report switch expression")]
    [DataRow("System.Text.StringBuilder builder = new(); return builder.ToString();",
        DisplayName = "Should report target-typed new")]
    [DataRow("string? value = context.Request.Method; return value;",
        DisplayName = "Should report nullable reference type annotation")]
    [DataRow("return @$\"C:\\{context.Request.Method}\";",
        DisplayName = "Should report @$ interpolated verbatim string")]
    public void ShouldReportLanguageFeaturesNewerThanCSharp73(string statements)
    {
        var result = CompileEvaluate(
            $$"""
              private static string Evaluate(IExpressionContext context)
              {
                  {{statements}}
              }
              """);

        result.Errors.Should().Contain(error => error.Id == "APIM2019");
    }

    [TestMethod]
    public void ShouldAcceptCSharp73LanguageFeatures()
    {
        var result = CompileEvaluate(
            """
            private static string Evaluate(IExpressionContext context)
            {
                var pair = (context.Variables.Count, 1);
                int total = default;
                return (pair == (0, 1)) + (context.Request.Method is string method ? method : "") + total;
            }
            """);

        result.Should().BeSuccessful();
    }
}