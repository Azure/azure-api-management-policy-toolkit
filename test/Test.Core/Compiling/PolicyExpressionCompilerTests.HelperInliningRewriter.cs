// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using static Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling.PolicyExpressionTestHelpers;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling;

public partial class PolicyExpressionCompilerTests
{
    [TestMethod]
    public void ShouldResolveUnqualifiedDocumentClassConstants()
    {
        const string document =
            """
            [Document]
            public class PolicyDocument : IDocument
            {
                private const string VariableName = "feature";

                public void Inbound(IInboundContext context)
                {
                    context.SetVariable(VariableName, "enabled");
                }
            }
            """;

        var result = document.CompileDocument();

        result.Should().BeSuccessful().And.DocumentEquivalentTo(
            """
            <policies>
                <inbound>
                    <set-variable name="feature" value="enabled" />
                </inbound>
            </policies>
            """);
    }

    [TestMethod]
    public void ShouldPreserveNumericConstantTypes()
    {
        const string document =
            """
            [Document]
            public class PolicyDocument : IDocument
            {
                private const double Divisor = 2.0;
                private const long Factor = 2;
                private const float Ratio = 2.0F;

                public void Inbound(IInboundContext context)
                {
                    context.SetVariable("division", Divide(context.ExpressionContext));
                    context.SetVariable("product", Multiply(context.ExpressionContext));
                    context.SetVariable("ratio", GetRatio(context.ExpressionContext));
                }

                private static double Divide(IExpressionContext context) => 5 / Divisor;
                private static long Multiply(IExpressionContext context) => int.MaxValue * Factor;
                private static float GetRatio(IExpressionContext context) => Ratio;
            }
            """;

        var result = document.CompileDocument();

        result.Should().BeSuccessful();
        var values = result.Document.Descendants("set-variable")
            .ToDictionary(
                element => element.Attribute("name")!.Value,
                element => element.Attribute("value")!.Value);
        values["division"].Should().Be("@(5 / 2D)");
        values["product"].Should().Be("@(int.MaxValue * 2L)");
        values["ratio"].Should().Be("@(2F)");
    }

    [TestMethod]
    public void ShouldNotParenthesizeOrdinaryMemberAccessReceivers()
    {
        const string document =
            """
            [Document]
            public class PolicyDocument : IDocument
            {
                public void Inbound(IInboundContext context)
                {
                    context.SetVariable("value", Evaluate(context.ExpressionContext));
                }

                private static bool Evaluate(IExpressionContext context)
                    => context.Response?.Body.As<string>() != null &&
                       System.Collections.Generic.EqualityComparer<string>.Default.Equals("a", "a") &&
                       global::System.StringComparer.Ordinal.Equals("a", "a");
            }
            """;

        var result = document.CompileDocument();

        result.Should().BeSuccessful();
        var value = result.Document.Descendants("set-variable").Single().Attribute("value")!.Value;
        value.Should().Contain("context.Response?.Body.As<string>()");
        value.Should().Contain("System.Collections.Generic.EqualityComparer<string>.Default");
        value.Should().Contain("global::System.StringComparer.Ordinal");
        value.Should().NotContain("?(");
        value.Should().NotContain("(global::");
    }

    [TestMethod]
    [DataRow("private const int Value = -1;", "@((-1).ToString())", DisplayName = "Negative constant")]
    [DataRow("private const short Value = 3;", "@(((short)3).ToString())", DisplayName = "Small integer constant")]
    public void ShouldParenthesizeConstantReceivers(string declaration, string expected)
    {
        var result = CompileEvaluate(
            $$"""
            {{declaration}}
            private static string Evaluate(IExpressionContext context) => Value.ToString();
            """);

        result.Should().BeSuccessful();
        Value(result).Should().Be(expected);
    }

    [TestMethod]
    public void ShouldParenthesizeNegativeConstantAfterUnaryOperator()
    {
        const string document =
            """
            [Document]
            public class PolicyDocument : IDocument
            {
                private const int Missing = -1;

                public void Inbound(IInboundContext context)
                {
                    context.SetVariable("value", Evaluate(context.ExpressionContext));
                }

                private static int Evaluate(IExpressionContext context) => -Missing + context.Variables.Count;
            }
            """;

        var result = document.CompileDocument();

        result.Should().BeSuccessful();
        result.Document.Descendants("set-variable").Single().Attribute("value")!.Value
            .Should().Be("@(-(-1) + context.Variables.Count)");
    }

    [TestMethod]
    public void ShouldEmitNonFiniteFloatingPointConstantsByName()
    {
        const string document =
            """
            [Document]
            public class PolicyDocument : IDocument
            {
                private const double NotANumber = double.NaN;
                private const double Unbounded = double.PositiveInfinity;
                private const float Floor = float.NegativeInfinity;

                public void Inbound(IInboundContext context)
                {
                    context.SetVariable("value", Evaluate(context.ExpressionContext));
                }

                private static bool Evaluate(IExpressionContext context)
                    => double.IsNaN(NotANumber) && double.IsInfinity(Unbounded) && float.IsInfinity(Floor);
            }
            """;

        var result = document.CompileDocument();

        result.Should().BeSuccessful();
        result.Document.Descendants("set-variable").Single().Attribute("value")!.Value
            .Should().Be(
                "@(double.IsNaN(double.NaN) && double.IsInfinity(double.PositiveInfinity) && float.IsInfinity(float.NegativeInfinity))");
    }

    [TestMethod]
    public void ShouldKeepLocalFunctionCallsInBlockBodiedHelpers()
    {
        const string document =
            """
            [Document]
            public class PolicyDocument : IDocument
            {
                public void Inbound(IInboundContext context)
                {
                    context.SetVariable("value", Evaluate(context.ExpressionContext));
                }

                private static string Evaluate(IExpressionContext context)
                {
                    string Upper(string value) => value.ToUpper();
                    return Upper(context.Request.Method);
                }
            }
            """;

        var result = document.CompileDocument();

        result.Should().BeSuccessful();
        result.Document.Descendants("set-variable").Single().Attribute("value")!.Value
            .Should().Contain("return Upper(context.Request.Method);");
    }

    [TestMethod]
    public void ShouldEmitEnumConstantsAsEnumMembers()
    {
        const string document =
            """
            using System;

            [Document]
            public class PolicyDocument : IDocument
            {
                private const StringComparison Comparison = StringComparison.OrdinalIgnoreCase;

                public void Inbound(IInboundContext context)
                {
                    context.SetVariable("byDefault", ByDefault(context.ExpressionContext));
                    context.SetVariable("byConstant", ByConstant(context.ExpressionContext));
                }

                private static bool ByDefault(IExpressionContext context) => Matches(context, "GET");

                private static bool Matches(
                    IExpressionContext context,
                    string value,
                    StringComparison comparison = StringComparison.OrdinalIgnoreCase)
                    => string.Equals(context.Request.Method, value, comparison);

                private static bool ByConstant(IExpressionContext context)
                    => string.Equals(context.Request.Method, "GET", Comparison);
            }
            """;

        var result = document.CompileDocument();

        result.Should().BeSuccessful();
        var values = result.Document.Descendants("set-variable")
            .ToDictionary(
                element => element.Attribute("name")!.Value,
                element => element.Attribute("value")!.Value);
        values["byDefault"].Should()
            .Be("@(string.Equals(context.Request.Method, \"GET\", System.StringComparison.OrdinalIgnoreCase))");
        values["byConstant"].Should()
            .Be("@(string.Equals(context.Request.Method, \"GET\", System.StringComparison.OrdinalIgnoreCase))");
    }

    [TestMethod]
    [DataRow("typeof(PolicyDocument).Name", DisplayName = "Source type")]
    [DataRow("System.Array.Exists(new[] { context.Request.Method }, IsGet)", DisplayName = "Source method group")]
    public void ShouldReportSourceMemberReferences(string expression)
    {
        var result = CompileEvaluate(
            $$"""
            private static object Evaluate(IExpressionContext context) => {{expression}};
            private static bool IsGet(string method) => method == "GET";
            """);

        result.Errors.Should().ContainSingle(error => error.Id == "APIM2018");
    }

    [TestMethod]
    public void ShouldFoldNameofToString()
    {
        var result = CompileEvaluate(
            """
            private const string Key = "x";
            private static string Evaluate(IExpressionContext context) => Names(context, "v");
            private static string Names(IExpressionContext context, string method) => nameof(method) + nameof(Key);
            """);

        result.Should().BeSuccessful();
        Value(result).Should().Be("@((\"method\" + \"Key\"))");
    }

    [TestMethod]
    public void ShouldNameSubstitutedAnonymousObjectProjection()
    {
        var result = CompileEvaluate(
            """
            private static string Evaluate(IExpressionContext context) => Describe(context, "v");
            private static string Describe(IExpressionContext context, string value) => new { value }.ToString();
            """);

        result.Should().BeSuccessful();
        Value(result).Should().Contain("value = \"v\"");
    }

    [TestMethod]
    public void ShouldAllowAnonymousTypeAndTupleMembers()
    {
        var result = CompileEvaluate(
            """
            private static int Evaluate(IExpressionContext context)
            {
                var pair = (count: context.Variables.Count, one: 1);
                return new { Total = pair.count + pair.one }.Total;
            }
            """);

        result.Should().BeSuccessful();
    }

    [TestMethod]
    public void ShouldReportUnsupportedConstantOncePerReference()
    {
        var result = CompileEvaluate(
            """
            public enum Mode { A, B }
            private const Mode Current = Mode.B;
            private static string Evaluate(IExpressionContext context) => Current.ToString() + PolicyDocument.Current;
            """);

        result.Errors.Should().HaveCount(2).And.OnlyContain(error => error.Id == "APIM2014");
    }

    [TestMethod]
    public void ShouldEmitAuthoringTypeArgumentsBySimpleName()
    {
        var result = CompileEvaluate(
            """
            private static IResponse Evaluate(IExpressionContext context) => Variable<IResponse>(context, "response");
            private static T Variable<T>(IExpressionContext context, string name)
                => context.Variables.GetValueOrDefault<T>(name);
            """);

        result.Should().BeSuccessful();
        Value(result).Should().Be("@(context.Variables.GetValueOrDefault<IResponse>(\"response\"))");
    }

    [TestMethod]
    public void ShouldPassTypeArgumentsThroughNestedGenericHelpers()
    {
        var result = CompileEvaluate(
            """
            private static string Evaluate(IExpressionContext context) => Outer<string>(context, "k");
            private static T Outer<T>(IExpressionContext context, string key) => Inner<T>(context, key);
            private static T Inner<T>(IExpressionContext context, string key) => context.Variables.GetValueOrDefault<T>(key);
            """);

        result.Should().BeSuccessful();
        Value(result).Should().Be("@(context.Variables.GetValueOrDefault<string>(\"k\"))");
    }

    [TestMethod]
    public void ShouldKeepInferredTupleElementNames()
    {
        var result = CompileEvaluate(
            """
            private static string Evaluate(IExpressionContext context) => Pair(context, "v");
            private static string Pair(IExpressionContext context, string method) => (method, 1).method;
            """);

        result.Should().BeSuccessful();
        Value(result).Should().Be("@((method: \"v\", 1).method)");
    }

    [TestMethod]
    [DataRow("using static System.Math;\nusing Builder = System.Text.StringBuilder;",
        "new Builder().Append(Max(1, 2)).ToString()",
        "@(new System.Text.StringBuilder().Append(System.Math.Max(1, 2)).ToString())",
        DisplayName = "using static and type alias")]
    [DataRow("using Text = System.Text;", "new Text.StringBuilder().ToString()",
        "@(new System.Text.StringBuilder().ToString())", DisplayName = "Namespace alias")]
    [DataRow("using Text = System.Text;", "new Text::StringBuilder().Append(global::System.Math.Max(1, 2)).ToString()",
        "@(new System.Text.StringBuilder().Append(global::System.Math.Max(1, 2)).ToString())",
        DisplayName = "Alias-qualified name, global:: kept")]
    public void ShouldQualifyImportedNames(string usings, string expression, string expected)
    {
        var result =
            $$"""
            {{usings}}

            [Document]
            public class PolicyDocument : IDocument
            {
                public void Inbound(IInboundContext context)
                {
                    context.SetVariable("value", Evaluate(context.ExpressionContext));
                }

                private static string Evaluate(IExpressionContext context) => {{expression}};
            }
            """.CompileDocument();

        result.Should().BeSuccessful();
        Value(result).Should().Be(expected);
    }
}
