// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using static Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling.PolicyExpressionTestHelpers;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling;

public partial class PolicyExpressionCompilerTests
{
    [TestMethod]
    public void ShouldLowerNamedArgumentsByParameterSymbol()
    {
        const string document =
            """
            [Document]
            public class PolicyDocument : IDocument
            {
                public void Inbound(IInboundContext context)
                {
                    context.SetVariable("enabled", IsEnabled(value: "feature", context: context.ExpressionContext));
                }

                private static bool IsEnabled(IExpressionContext context, string value)
                    => context.Variables.ContainsKey(value);
            }
            """;

        var result = document.CompileDocument();

        result.Should().BeSuccessful().And.DocumentEquivalentTo(
            """
            <policies>
                <inbound>
                    <set-variable name="enabled" value="@(context.Variables.ContainsKey("feature"))" />
                </inbound>
            </policies>
            """);
    }

    [TestMethod]
    public void ShouldNotTreatOrdinaryContextNamedParameterAsExpressionContext()
    {
        const string document =
            """
            [Document]
            public class PolicyDocument : IDocument
            {
                public void Inbound(IInboundContext context)
                {
                    context.SetVariable("value", Outer(context.ExpressionContext, "feature"));
                }

                private static string Outer(IExpressionContext expressionContext, string context)
                    => Inner(expressionContext, context);

                private static string Inner(IExpressionContext expressionContext, string value) => value;
            }
            """;

        var result = document.CompileDocument();

        result.Should().BeSuccessful().And.DocumentEquivalentTo(
            """
            <policies>
                <inbound>
                    <set-variable name="value" value="@("feature")" />
                </inbound>
            </policies>
            """);
    }

    [TestMethod]
    public void ShouldNotCanonicalizeExpressionContextFromArbitraryReceiver()
    {
        const string document =
            """
            [Document]
            public class PolicyDocument : IDocument
            {
                public void Inbound(IInboundContext context)
                {
                    context.SetVariable("value", Read(Alternate.ExpressionContext));
                }

                private static string Read(IExpressionContext context) => context.Request.Method;
            }

            public static class Alternate
            {
                public static IExpressionContext ExpressionContext =>
                    throw new NotImplementedException();
            }
            """;

        var result = document.CompileDocument();

        result.Errors.Should().Contain(error => error.Id == "APIM2013");
    }

    [TestMethod]
    public void ShouldBindGenericAndExtensionHelperParameters()
    {
        const string document =
            """
            [Document]
            public class PolicyDocument : IDocument
            {
                public void Inbound(IInboundContext context)
                {
                    context.SetVariable(
                        "value",
                        Helpers.Identity(" feature ").Normalize(context.ExpressionContext));
                }
            }

            public static class Helpers
            {
                public static T Identity<T>(T value) => value;

                public static string Normalize(
                    this string value,
                    IExpressionContext context)
                    => value.Trim() + context.Request.Method;
            }
            """;

        var result = document.CompileDocument();

        result.Should().BeSuccessful();
        var value = result.Document.Descendants("set-variable").Single().Attribute("value")!.Value;
        value.Should().Be("@(\" feature \".Trim() + context.Request.Method)");
    }

    [TestMethod]
    public void ShouldRejectAssignmentToSubstitutedParameter()
    {
        const string document =
            """
            [Document]
            public class PolicyDocument : IDocument
            {
                public void Inbound(IInboundContext context)
                {
                    context.SetVariable("value", Normalize(context.ExpressionContext, "abc"));
                }

                private static string Normalize(IExpressionContext context, string value)
                {
                    value = value.Trim();
                    return value;
                }
            }
            """;

        var result = document.CompileDocument();

        result.Errors.Should().Contain(error => error.Id == "APIM2013");
        result.Document.ToString().Should().NotContain("\"abc\" =");
    }

    [TestMethod]
    public void ShouldAllowReadingParameterInAssignmentTarget()
    {
        const string document =
            """
            [Document]
            public class PolicyDocument : IDocument
            {
                public void Inbound(IInboundContext context)
                {
                    context.SetVariable(
                        "value",
                        Update(
                            context.ExpressionContext,
                            (System.Collections.Generic.IDictionary<string, object>)context.ExpressionContext.Variables));
                }

                private static object Update(
                    IExpressionContext context,
                    System.Collections.Generic.IDictionary<string, object> values)
                {
                    values["key"] = "v";
                    return values["key"];
                }
            }
            """;

        var result = document.CompileDocument();

        result.Should().BeSuccessful();
        var value = result.Document.Descendants("set-variable").Single().Attribute("value")!.Value;
        value.Should().Contain("[\"key\"] = \"v\";");
    }

    [TestMethod]
    public void ShouldParenthesizeNegativeDefaultParameterValue()
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

                private static int Evaluate(IExpressionContext context) => Negate(context);

                private static int Negate(IExpressionContext context, int value = -1)
                    => -value + context.Variables.Count;
            }
            """;

        var result = document.CompileDocument();

        result.Should().BeSuccessful();
        result.Document.Descendants("set-variable").Single().Attribute("value")!.Value
            .Should().Be("@((-(-1) + context.Variables.Count))");
    }

    [TestMethod]
    public void ShouldReportSideEffectingHelperArgument()
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

                private static int Evaluate(IExpressionContext context)
                {
                    var count = context.Variables.Count;
                    return Twice(++count);
                }

                private static int Twice(int value) => value + value;
            }
            """;

        var result = document.CompileDocument();

        result.Errors.Should().ContainSingle(error => error.Id == "APIM2013");
    }

    [TestMethod]
    public void ShouldKeepArgumentConversionToParameterType()
    {
        var result = CompileEvaluate(
            """
            private static double Evaluate(IExpressionContext context) => Half(5);
            private static double Half(double value) => value / 2;
            """);

        result.Should().BeSuccessful();
        Value(result).Should().Be("@((((double)5) / 2))");
    }

    [TestMethod]
    public void ShouldBindExtensionHelperArgumentsAfterNamedArgument()
    {
        const string document =
            """
            [Document]
            public class PolicyDocument : IDocument
            {
                public void Inbound(IInboundContext context)
                {
                    context.SetVariable("value", context.ExpressionContext.Join(first: "1", "2"));
                }
            }
            """;
        const string extensions =
            """
            public static class Extensions
            {
                public static string Join(this IExpressionContext context, string first, string second = "default")
                    => first + second;
            }
            """;

        var result = document.CompileDocument(extensions);

        result.Should().BeSuccessful();
        Value(result).Should().Be("@(\"1\" + \"2\")");
    }

    [TestMethod]
    public void ShouldReportDeconstructionWriteToParameter()
    {
        const string document =
            """
            [Document]
            public class PolicyDocument : IDocument
            {
                public void Inbound(IInboundContext context)
                {
                    context.SetVariable("value", Swap(context.ExpressionContext, "v"));
                }

                private static string Swap(IExpressionContext context, string value)
                {
                    string other;
                    (value, other) = ("a", "b");
                    return value + other;
                }
            }
            """;

        var result = document.CompileDocument();

        result.Errors.Should().ContainSingle(error => error.Id == "APIM2013");
    }

    [TestMethod]
    [DataRow("context")]
    [DataRow("ctx")]
    public void ShouldMapSectionContextExpressionContextToContext(string section)
    {
        var document =
            $$"""
            [Document]
            public class PolicyDocument : IDocument
            {
                public void Inbound(IInboundContext {{section}})
                {
                    {{section}}.SetVariable("value", Count({{section}}));
                }

                private static string Count(IInboundContext inbound)
                    => inbound.ExpressionContext.Variables.Count.ToString();
            }
            """;

        var result = document.CompileDocument();

        result.Should().BeSuccessful();
        Value(result).Should().Be("@(context.Variables.Count.ToString())");
    }

    [TestMethod]
    public void ShouldReportSectionContextMemberUseInHelper()
    {
        const string document =
            """
            [Document]
            public class PolicyDocument : IDocument
            {
                public void Inbound(IInboundContext context)
                {
                    context.SetVariable("value", Evaluate(context));
                }

                private static string Evaluate(IInboundContext inbound)
                {
                    inbound.SetHeader("x", "y");
                    return "z";
                }
            }
            """;

        var result = document.CompileDocument();

        result.Errors.Should().ContainSingle(error => error.Id == "APIM2013");
    }

    [TestMethod]
    public void ShouldSubstituteComplexArgumentEvaluatedOnce()
    {
        var result = CompileEvaluate(
            """
            private static string Evaluate(IExpressionContext context)
                => Upper(context.Request.Headers.GetValueOrDefault("a", ""));

            private static string Upper(string value) => value.ToUpper();
            """);

        result.Should().BeSuccessful();
        Value(result).Should().Be("@(context.Request.Headers.GetValueOrDefault(\"a\", \"\").ToUpper())");
    }

    [TestMethod]
    [DataRow("private static string Twice(string value) => value + value;", "Twice(Header(context))",
        DisplayName = "Should reject complex argument used twice")]
    [DataRow("private static string Twice(string value) => value.Length > 0 ? value : \"\";", "Twice(Header(context))",
        DisplayName = "Should reject complex argument used conditionally")]
    [DataRow("private static string Twice(string first, string second) => first + second;",
        "Twice(Header(context), Header(context))",
        DisplayName = "Should reject two complex arguments")]
    [DataRow("private static string Combine(IExpressionContext context, string first) => context.Request.Headers.GetValueOrDefault(\"b\", \"\") + first;",
        "Combine(context, Header(context))",
        DisplayName = "Should reject complex argument evaluated after a call in the helper")]
    [DataRow("private static string Tag(IExpressionContext context, string id) => string.Join(\",\", from header in context.Request.Headers select header.Key + id);",
        "Tag(context, System.Guid.NewGuid().ToString())",
        DisplayName = "Should reject complex argument used in a query body")]
    [DataRow("private static string Pick(string first, int second) => first;",
        "Pick(\"a\", 1 / context.Variables.Count)",
        DisplayName = "Should reject dropped division, which can throw")]
    [DataRow("private static string Pick(string first, int second) => first;",
        "Pick(\"a\", 1 % context.Variables.Count)",
        DisplayName = "Should reject dropped modulo, which can throw")]
    public void ShouldReportComplexArgumentNotEvaluatedOnce(string helper, string call)
    {
        var result = CompileEvaluate(
            $$"""
              private static string Evaluate(IExpressionContext context) => {{call}};
              private static string Header(IExpressionContext context) => context.Request.Headers.GetValueOrDefault("a", "").Trim();
              {{helper}}
              """);

        result.Errors.Should().Contain(error => error.Id == "APIM2013");
    }

    [TestMethod]
    [DataRow("Pick(\"a\", 1 + context.Variables.Count)", "@(\"a\")", DisplayName = "Dropped addition")]
    [DataRow("Twice(context.Variables[\"x\"] as string)",
        "@(((context.Variables[\"x\"] as string) + (context.Variables[\"x\"] as string)))",
        DisplayName = "as expression used twice")]
    [DataRow("Both(context.Variables[\"x\"] is string)",
        "@(((context.Variables[\"x\"] is string) && (context.Variables[\"x\"] is string)).ToString())",
        DisplayName = "is expression used twice")]
    public void ShouldSubstituteSimpleOperatorArguments(string call, string expected)
    {
        var result = CompileEvaluate(
            $$"""
            private static string Evaluate(IExpressionContext context) => {{call}};
            private static string Pick(string first, int second) => first;
            private static string Twice(string value) => value + value;
            private static string Both(bool value) => (value && value).ToString();
            """);

        result.Should().BeSuccessful();
        Value(result).Should().Be(expected);
    }

    [TestMethod]
    public void ShouldSubstituteInParameter()
    {
        var result = CompileEvaluate(
            """
            private static string Evaluate(IExpressionContext context) => Format(context.Variables.Count);
            private static string Format(in int value) => value.ToString();
            """);

        result.Should().BeSuccessful();
        Value(result).Should().Be("@(context.Variables.Count.ToString())");
    }

    [TestMethod]
    public void ShouldEmitDefaultForStructParameterDefault()
    {
        var result = CompileEvaluate(
            """
            private static string Evaluate(IExpressionContext context) => Stamp(context);
            private static string Stamp(IExpressionContext context, System.DateTime at = default) => at.ToString();
            """);

        result.Should().BeSuccessful();
        Value(result).Should().Be("@(default(System.DateTime).ToString())");
    }

    [TestMethod]
    public void ShouldCastNullDefaultToParameterType()
    {
        var result = CompileEvaluate(
            """
            private static string Evaluate(IExpressionContext context) => Describe(context);
            private static string Describe(IExpressionContext context, int? count = null) => count.ToString();
            """);

        result.Should().BeSuccessful();
        Value(result).Should().Be("@(((int? )null).ToString())");
    }

    [TestMethod]
    public void ShouldMapExpressionContextHolderParameterToContext()
    {
        const string document =
            """
            [Document]
            public class PolicyDocument : IDocument
            {
                public void Inbound(IInboundContext context)
                {
                    context.SetVariable("value", Key(context));
                }

                private static string Key(IHaveExpressionContext holder) => holder.ExpressionContext.Request.IpAddress;
            }
            """;

        var result = document.CompileDocument();

        result.Should().BeSuccessful();
        Value(result).Should().Be("@(context.Request.IpAddress)");
    }

    [TestMethod]
    [DataRow("Join(\"a\", \"b\")", "@(string.Join(\",\", new string[] { \"a\", \"b\" }))",
        DisplayName = "Should build array for params arguments")]
    [DataRow("Join()", "@(string.Join(\",\", new string[] { }))",
        DisplayName = "Should build empty array for no params arguments")]
    [DataRow("Join(new[] { \"a\" })", "@(string.Join(\",\", (new[] { \"a\" })))",
        DisplayName = "Should pass params array argument through")]
    public void ShouldInlineParamsHelper(string call, string expected)
    {
        var result = CompileEvaluate(
            $$"""
              private static string Evaluate(IExpressionContext context) => {{call}};
              private static string Join(params string[] parts) => string.Join(",", parts);
              """);

        result.Should().BeSuccessful();
        Value(result).Should().Be(expected);
    }

    [TestMethod]
    public void ShouldReportParamsCollectionParameter()
    {
        var result = CompileEvaluate(
            """
            private static string Evaluate(IExpressionContext context) => Join("a", "b");
            private static string Join(params System.Collections.Generic.IEnumerable<string> parts) => string.Join(",", parts);
            """);

        result.Errors.Should().Contain(error => error.Id == "APIM2013");
    }

    [TestMethod]
    public void ShouldReportParamsParameterUsedTwice()
    {
        var result = CompileEvaluate(
            """
            private static string Evaluate(IExpressionContext context) => Join("a", "b");
            private static string Join(params string[] parts) => string.Join(",", parts) + parts.Length;
            """);

        result.Errors.Should().Contain(error => error.Id == "APIM2013");
    }
}