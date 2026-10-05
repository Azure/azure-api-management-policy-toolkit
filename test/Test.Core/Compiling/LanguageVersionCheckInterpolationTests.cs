// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using static Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling.PolicyExpressionTestHelpers;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling;

[TestClass]
public class LanguageVersionCheckInterpolationTests
{
    [TestMethod]
    public void ShouldAcceptNewLineInInterpolationOfExpressionBody()
    {
        var result = CompileEvaluate(
            """
            private static string Evaluate(IExpressionContext context) =>
                $"{context.Request.Method
                    .ToUpper()}-{
                    context.Api.Name}";
            """);

        result.Should().BeSuccessful();
        Value(result).Should().Be("@($\"{context.Request.Method.ToUpper()}-{context.Api.Name}\")");
    }

    [TestMethod]
    public void ShouldAcceptNewLineInInterpolationOfBlockBody()
    {
        var result = CompileEvaluate(
            """
            private static string Evaluate(IExpressionContext context)
            {
                var name = context.Api.Name;
                return $"{context.Request.Method
                    .ToUpper()}-{
                    name}";
            }
            """);

        result.Should().BeSuccessful();
        Value(result).Should().Contain("return $\"{context.Request.Method.ToUpper()}-{name}\";");
    }

    [TestMethod]
    public void ShouldStillReportOtherNewerFeatureInInterpolationWithNewLine()
    {
        var result = CompileEvaluate(
            """
            private static string Evaluate(IExpressionContext context) =>
                $"{context.Variables.Count switch { 0 => "none",
                    _ => "some" }}";
            """);

        result.Errors.Should().NotBeEmpty();
        result.Errors.Should().OnlyContain(error => error.Id == "APIM2019");
        result.Errors.Should().NotContain(error => error.GetMessage(null).Contains("Newlines"));
    }
}
