// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using static Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling.PolicyExpressionTestHelpers;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling;

[TestClass]
public class NamedValueNameTests
{
    [TestMethod]
    [DataRow("context.NamedValue(context.Request.Headers.GetValueOrDefault(\"name\", \"\"))",
        DisplayName = "Name from the request")]
    [DataRow("context.NamedValue(\"prefix-\" + context.Api.Name)", DisplayName = "Concatenated name")]
    [DataRow("context.NamedValue(context.Api.Name).ToString()", DisplayName = "Member of the named value")]
    public void ShouldReportNamedValueWithNonConstantName(string expression)
    {
        var result = CompileEvaluate(
            $$"""
              private static string Evaluate(IExpressionContext context) => {{expression}};
              """);

        result.Errors.Should().ContainSingle().Which.Id.Should().Be("APIM2040");
    }

    [TestMethod]
    public void ShouldReportNamedValueWithLocalVariableName()
    {
        var result = CompileEvaluate(
            """
            private static string Evaluate(IExpressionContext context)
            {
                var name = context.Api.Name;
                return context.NamedValue(name);
            }
            """);

        result.Errors.Should().ContainSingle().Which.Id.Should().Be("APIM2040");
    }

    [TestMethod]
    public void ShouldReportNamedValueWithNonConstantNameInHelper()
    {
        var result = CompileEvaluate(
            """
            private static string Evaluate(IExpressionContext context) => Get(context, context.Api.Name);
            private static string Get(IExpressionContext context, string name) => context.NamedValue(name);
            """);

        result.Errors.Should().NotBeEmpty();
        result.Errors.Should().OnlyContain(error => error.Id == "APIM2040");
    }

    [TestMethod]
    public void ShouldCompileNamedValueWithConstantName()
    {
        var result = CompileEvaluate(
            """
            private const string Name = "host";
            private static string Evaluate(IExpressionContext context) =>
                "https://" + context.NamedValue(Name) + context.NamedValue("pa" + "th") + context.NamedValue(nameof(Name));
            """);

        result.Should().BeSuccessful();
        Value(result).Should().Contain("{{host}}").And.Contain("{{path}}").And.Contain("{{Name}}");
    }

    [TestMethod]
    public void ShouldReportNamedValueWithNonConstantNameInExpressionMethod()
    {
        var result =
            """
            [Document]
            public class PolicyDocument : IDocument
            {
                public void Inbound(IInboundContext context)
                {
                    context.SetVariable("value", Get(context.ExpressionContext));
                }

                [Expression]
                private static string Get(IExpressionContext context) => context.NamedValue(context.Api.Name);
            }
            """.CompileDocument();

        result.Errors.Should().ContainSingle().Which.Id.Should().Be("APIM2040");
    }
}
