// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using static Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling.PolicyExpressionTestHelpers;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling;

[TestClass]
public partial class PolicyExpressionCompilerTests
{
    [TestMethod]
    public void ShouldRecursivelyLowerCrossClassHelpersAndConstants()
    {
        const string document =
            """
            [Document]
            public class PolicyDocument : IDocument
            {
                private const string VariableName = "feature";

                public void Inbound(IInboundContext context)
                {
                    context.SetVariable("enabled", IsDisabled(context.ExpressionContext));
                }

                private static bool IsDisabled(IExpressionContext context)
                    => !Helpers.IsEnabled(context, VariableName);
            }
            """;
        const string helpers =
            """
            public static class Helpers
            {
                public static bool IsEnabled(IExpressionContext context, string variableName)
                    => context.Variables.ContainsKey(variableName);
            }
            """;

        var result = document.CompileDocument(helpers);

        result.Should().BeSuccessful().And.DocumentEquivalentTo(
            """
            <policies>
                <inbound>
                    <set-variable name="enabled" value="@(!context.Variables.ContainsKey("feature"))" />
                </inbound>
            </policies>
            """);
    }

    [TestMethod]
    public void ShouldPreserveBlockBodiedConditionHelpers()
    {
        const string document =
            """
            [Document]
            public class PolicyDocument : IDocument
            {
                public void Inbound(IInboundContext context)
                {
                    if (IsEnabled(context.ExpressionContext))
                    {
                        context.SetVariable("matched", "true");
                    }
                }

                private static bool IsEnabled(IExpressionContext context)
                {
                    var name = "feature";
                    return context.Variables.ContainsKey(name);
                }
            }
            """;

        var result = document.CompileDocument();

        result.Should().BeSuccessful();
        var condition = result.Document.Descendants("when").Single().Attribute("condition")!.Value;
        condition.Should().StartWith("@{");
        condition.Should().Contain("var name = \"feature\";");
        condition.Should().Contain("return context.Variables.ContainsKey(name);");
    }

    [TestMethod]
    public void ShouldPreserveSingleStatementBlockConditionHelpers()
    {
        const string document =
            """
            [Document]
            public class PolicyDocument : IDocument
            {
                public void Inbound(IInboundContext context)
                {
                    if (CanReadResponse(context.ExpressionContext))
                    {
                        context.SetVariable("matched", "true");
                    }
                }

                private static bool CanReadResponse(IExpressionContext context)
                {
                    try
                    {
                        return context.Response.StatusCode > 0;
                    }
                    catch
                    {
                        return false;
                    }
                }
            }
            """;

        var result = document.CompileDocument();

        result.Should().BeSuccessful();
        var condition = result.Document.Descendants("when").Single().Attribute("condition")!.Value;
        condition.Should().StartWith("@{");
        condition.Should().Contain("try");
        condition.Should().Contain("catch");
    }

    [TestMethod]
    public void ShouldRenderConstantsAsPlainXmlAttributeValues()
    {
        const string document =
            """
            [Document]
            public class PolicyDocument : IDocument
            {
                private const short StatusCode = 3;
                private const double Offset = -0.5;
                private const long Count = -5;

                public void Inbound(IInboundContext context)
                {
                    context.SetStatus(new StatusConfig
                    {
                        Code = StatusCode,
                        Reason = "ready"
                    });
                    context.SetVariable("offset", Offset);
                    context.SetVariable("count", Count);
                }
            }
            """;

        var result = document.CompileDocument();

        result.Should().BeSuccessful();
        result.Document.Descendants("set-status").Single().Attribute("code")!.Value
            .Should().Be("3");
        var values = result.Document.Descendants("set-variable")
            .ToDictionary(
                element => element.Attribute("name")!.Value,
                element => element.Attribute("value")!.Value);
        values["offset"].Should().Be("-0.5");
        values["count"].Should().Be("-5");
    }

    [TestMethod]
    public void ShouldComposeBooleanConditions()
    {
        const string document =
            """
            [Document]
            public class PolicyDocument : IDocument
            {
                public void Inbound(IInboundContext context)
                {
                    if (!IsSet(context.ExpressionContext, "a") &&
                        (IsSet(context.ExpressionContext, "b") || IsSet(context.ExpressionContext, "c")))
                    {
                        context.SetVariable("matched", "true");
                    }
                }

                private static bool IsSet(IExpressionContext context, string name)
                    => context.Variables.ContainsKey(name);
            }
            """;

        var result = document.CompileDocument();

        result.Should().BeSuccessful().And.DocumentEquivalentTo(
            """
            <policies>
                <inbound>
                    <choose>
                        <when condition="@(!context.Variables.ContainsKey("a") && (context.Variables.ContainsKey("b") || context.Variables.ContainsKey("c")))">
                            <set-variable name="matched" value="true" />
                        </when>
                    </choose>
                </inbound>
            </policies>
            """);
    }

    [TestMethod]
    public void ShouldLowerNestedHelperInsideBlockBodiedRoot()
    {
        const string document =
            """
            [Document]
            public class PolicyDocument : IDocument
            {
                public void Inbound(IInboundContext context)
                {
                    context.SetVariable("enabled", Evaluate(context.ExpressionContext));
                }

                private static bool Evaluate(IExpressionContext context)
                {
                    return IsSet(context);
                }

                private static bool IsSet(IExpressionContext context)
                    => context.Variables.ContainsKey("feature");
            }
            """;

        var result = document.CompileDocument();

        result.Should().BeSuccessful();
        var value = result.Document.Descendants("set-variable").Single().Attribute("value")!.Value;
        value.Should().Contain("return context.Variables.ContainsKey(\"feature\");");
        value.Should().NotContain("IsSet(");
    }

    [TestMethod]
    public void ShouldNotCastToAuthoringTypes()
    {
        var result = CompileEvaluate(
            """
            private static string Evaluate(IExpressionContext context) => ApiName(context.Api);
            private static string ApiName(IApi api) => api.Name;
            """);

        result.Should().BeSuccessful();
        Value(result).Should().Be("@(context.Api.Name)");
    }

    [TestMethod]
    public void ShouldKeepInlinedHelperReturnConversion()
    {
        var result = CompileEvaluate(
            """
            private static double Evaluate(IExpressionContext context) => Two() / 4;
            private static double Two() => 2;
            """);

        result.Should().BeSuccessful();
        Value(result).Should().Be("@(((double)2) / 4)");
    }

    [TestMethod]
    public void ShouldReportRecursiveExpressionHelpers()
    {
        const string document =
            """
            [Document]
            public class PolicyDocument : IDocument
            {
                public void Inbound(IInboundContext context)
                {
                    context.SetVariable("enabled", First(context.ExpressionContext));
                }

                private static bool First(IExpressionContext context) => Second(context);
                private static bool Second(IExpressionContext context) => First(context);
            }
            """;

        var result = document.CompileDocument();

        result.Errors.Should().ContainSingle(error => error.Id == "APIM2012");
    }

    [TestMethod]
    public void ShouldReportRecursionInsideHelperArguments()
    {
        const string document =
            """
            [Document]
            public class PolicyDocument : IDocument
            {
                public void Inbound(IInboundContext context)
                {
                    context.SetVariable("enabled", First(context.ExpressionContext));
                }

                private static bool First(IExpressionContext context)
                    => Identity(context, First(context));

                private static bool Identity(IExpressionContext context, bool value) => value;
            }
            """;

        var result = document.CompileDocument();

        result.Errors.Should().ContainSingle(error => error.Id == "APIM2012");
    }

    [TestMethod]
    public void ShouldReportExpandedOutputThatExceedsNodeBudget()
    {
        var methods = string.Join(
            Environment.NewLine,
            Enumerable.Range(0, 14).Select(index =>
                $"private static string Expand{index}(IExpressionContext context) => " +
                $"Expand{index + 1}(context) + Expand{index + 1}(context);"));
        var document =
            """
            [Document]
            public class PolicyDocument : IDocument
            {
                public void Inbound(IInboundContext context)
                {
                    context.SetVariable("value", Expand0(context.ExpressionContext));
                }

                __METHODS__
                private static string Expand14(IExpressionContext context) => "value";
            }
            """.Replace("__METHODS__", methods, StringComparison.Ordinal);

        var result = document.CompileDocument();

        result.Errors.Should().Contain(error => error.Id == "APIM2017");
    }

    [TestMethod]
    public void ShouldReportDepthLimitAsExpansionLimitRatherThanRecursion()
    {
        var methods = string.Join(
            Environment.NewLine,
            Enumerable.Range(0, 33).Select(index =>
                index == 32
                    ? "private static bool Step32(IExpressionContext context) => true;"
                    : $"private static bool Step{index}(IExpressionContext context) => Step{index + 1}(context);"));
        var document =
            """
            [Document]
            public class PolicyDocument : IDocument
            {
                public void Inbound(IInboundContext context)
                {
                    context.SetVariable("value", Step0(context.ExpressionContext));
                }

                __METHODS__
            }
            """.Replace("__METHODS__", methods, StringComparison.Ordinal);

        var result = document.CompileDocument();

        result.Errors.Should().Contain(error => error.Id == "APIM2017");
        result.Errors.Should().NotContain(error => error.Id == "APIM2012");
    }

    [TestMethod]
    public void ShouldAllowHelperCallInsideItsOwnArgument()
    {
        var result = CompileEvaluate(
            """
            private static string Evaluate(IExpressionContext context) => Or(Or(context.Request.Method, "a"), "b");
            private static string Or(string first, string second) => first ?? second;
            """);

        result.Should().BeSuccessful();
        Value(result).Should().Be("@(((context.Request.Method ?? \"a\") ?? \"b\"))");
    }

    [TestMethod]
    public void ShouldReportExpansionLimitForArgumentsDuplicatedAcrossLevels()
    {
        var methods = string.Join(
            Environment.NewLine,
            Enumerable.Range(1, 21).Select(index =>
                $"private static string D{index}(string value) => D{index - 1}(value + value);"));

        var result = CompileEvaluate(
            $"""
             private static string Evaluate(IExpressionContext context) => D21("a");
             private static string D0(string value) => value + value;
             {methods}
             """);

        result.Errors.Should().ContainSingle(error => error.Id == "APIM2017");
    }

    [TestMethod]
    public void ShouldCountNestedHelperExpansionsOnce()
    {
        var methods = string.Join(
            Environment.NewLine,
            Enumerable.Range(0, 30).Select(index =>
                $"private static string L{index}(IExpressionContext context) => L{index + 1}(context) + context.Request.Method;"));

        var result = CompileEvaluate(
            $"""
             private static string Evaluate(IExpressionContext context) =>
                 string.Concat(L0(context), L0(context), L0(context), L0(context), L0(context), L0(context));
             {methods}
             private static string L30(IExpressionContext context) => context.Request.Method;
             """);

        result.Should().BeSuccessful();
        Value(result).Split("context.Request.Method").Should().HaveCount(6 * 31 + 1);
    }

    [TestMethod]
    public void ShouldLimitWorkOnHelperExpansionsThatAreDropped()
    {
        var methods = string.Join(
            Environment.NewLine,
            Enumerable.Range(0, 30).Select(index =>
                $"private static int P{index}(IExpressionContext context) => Pick(P{index + 1}(context), P{index + 1}(context));"));

        var result = CompileEvaluate(
            $"""
             private static int Evaluate(IExpressionContext context) => P0(context);
             private static int Pick(int first, int second) => first;
             {methods}
             private static int P30(IExpressionContext context) => 1;
             """);

        result.Errors.Should().ContainSingle(error => error.Id == "APIM2017");
    }

    [TestMethod]
    public void ShouldNotCountFirstConditionPassTowardsExpansionLimit()
    {
        var methods = string.Join(
            Environment.NewLine,
            Enumerable.Range(0, 10).Select(index =>
                $"private static string T{index}(IExpressionContext context) => T{index + 1}(context) + T{index + 1}(context);"));

        var result =
            $$"""
            [Document]
            public class PolicyDocument : IDocument
            {
                public void Inbound(IInboundContext context)
                {
                    if (HasA(context.ExpressionContext) && HasB(context.ExpressionContext))
                    {
                        context.SetVariable("matched", "true");
                    }
                }

                private static bool HasA(IExpressionContext context) =>
                    T0(context).Length > 0 && context.Variables.TryGetValue("a", out var value);
                private static bool HasB(IExpressionContext context) => context.Variables.TryGetValue("b", out var value);
                {{methods}}
                private static string T10(IExpressionContext context) => context.Request.Method;
            }
            """.CompileDocument();

        result.Should().BeSuccessful();
    }

    [TestMethod]
    public void ShouldStopExpandingOnceExpansionLimitIsExceeded()
    {
        var methods = string.Join(
            Environment.NewLine,
            Enumerable.Range(0, 24).Select(index =>
                $"private static string H{index}(IExpressionContext context) => H{index + 1}(context) + H{index + 1}(context);"));

        var result = CompileEvaluate(
            $"""
             private static string Evaluate(IExpressionContext context) => H0(context);
             {methods}
             private static string H24(IExpressionContext context) => context.Request.Method;
             """);

        result.Errors.Should().ContainSingle(error => error.Id == "APIM2017");
    }

    [TestMethod]
    public void ShouldReportConditionalExtensionHelperNeedingReceiverConversion()
    {
        var result = CompileEvaluate(
            """
            private static int? Evaluate(IExpressionContext context) =>
                context.Variables.GetValueOrDefault<string>("a", "")?.AsInt();
            """,
            """
            public static class ConvertibleExtensions
            {
                public static int AsInt(this System.IConvertible value) => value.ToInt32(null);
            }
            """);

        result.Errors.Should().Contain(error => error.Id == "APIM2013");
    }

    [TestMethod]
    [DataRow("Headers(context)?.Tidy()", "@(context.Request.Headers.GetValueOrDefault(\"a\", \"\")?.Trim().ToUpper())",
        DisplayName = "Should continue conditional access with extension helper chain")]
    [DataRow("Headers(context)?.Tidy().Length.ToString()",
        "@(context.Request.Headers.GetValueOrDefault(\"a\", \"\")?.Trim().ToUpper().Length.ToString())",
        DisplayName = "Should keep members after conditional extension helper")]
    public void ShouldInlineConditionalExtensionHelper(string call, string expected)
    {
        var result = CompileEvaluate(
            $$"""
              private static string Evaluate(IExpressionContext context) => {{call}};
              private static string Headers(IExpressionContext context) => context.Request.Headers.GetValueOrDefault("a", "");
              """,
            """
            public static class Extensions
            {
                public static string Tidy(this string value) => value.Trim().ToUpper();
            }
            """);

        result.Should().BeSuccessful();
        Value(result).Should().Be(expected);
    }

    [TestMethod]
    public void ShouldContinueConditionalAccessThroughMemberBeforeExtensionHelper()
    {
        var result = CompileEvaluate(
            """
            private static string Evaluate(IExpressionContext context) => Holder(context)?.Length.ToString().Tidy();
            private static string Holder(IExpressionContext context) => context.Request.Headers.GetValueOrDefault("a", "");
            """,
            """
            public static class Extensions
            {
                public static string Tidy(this string value) => value.Trim().ToUpper();
            }
            """);

        result.Should().BeSuccessful();
        Value(result).Should().Be("@(context.Request.Headers.GetValueOrDefault(\"a\", \"\")?.Length.ToString().Trim().ToUpper())");
    }

    [TestMethod]
    public void ShouldContinueConditionalAccessThroughPropertyBeforeExtensionHelper()
    {
        var result = CompileEvaluate(
            """
            private static string Evaluate(IExpressionContext context) => Error(context)?.Message.Tidy();
            private static System.Exception Error(IExpressionContext context) => (System.Exception)context.Variables["error"];
            """,
            """
            public static class Extensions
            {
                public static string Tidy(this string value) => value.Trim().ToUpper();
            }
            """);

        result.Should().BeSuccessful();
        Value(result).Should().Be("@(((System.Exception)context.Variables[\"error\"])?.Message.Trim().ToUpper())");
    }

    [TestMethod]
    [DataRow("Holder(context)?.Length.ToString().Blank()", DisplayName = "Should report non-chain helper after conditional member")]
    [DataRow("(Holder(context)?.Half() ?? 0).ToString()", DisplayName = "Should report conditional helper needing return conversion")]
    public void ShouldReportConditionalExtensionHelperThatCannotBeInlined(string call)
    {
        var result = CompileEvaluate(
            $$"""
              private static string Evaluate(IExpressionContext context) => {{call}};
              private static string Holder(IExpressionContext context) => context.Request.Headers.GetValueOrDefault("a", "");
              """,
            """
            public static class Extensions
            {
                public static bool Blank(this string value) => string.IsNullOrWhiteSpace(value);
                public static double Half(this string value) => value.Length;
            }
            """);

        result.Errors.Should().Contain(error => error.Id == "APIM2013");
    }

    [TestMethod]
    public void ShouldReportConditionalExtensionHelperThatIsNotAChain()
    {
        var result = CompileEvaluate(
            """
            private static string Evaluate(IExpressionContext context) => Headers(context)?.Suffix();
            private static string Headers(IExpressionContext context) => context.Request.Headers.GetValueOrDefault("a", "");
            """,
            """
            public static class Extensions
            {
                public static string Suffix(this string value) => value + "x";
            }
            """);

        result.Errors.Should().Contain(error => error.Id == "APIM2013");
    }
}