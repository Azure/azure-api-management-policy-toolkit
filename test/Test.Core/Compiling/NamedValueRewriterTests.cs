// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using static Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling.PolicyExpressionTestHelpers;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling;

[TestClass]
public class NamedValueRewriterTests
{
    [TestMethod]
    public void ShouldLowerNestedNamedValueHelper()
    {
        const string document =
            """
            [Document]
            public class PolicyDocument : IDocument
            {
                public void Inbound(IInboundContext context)
                {
                    context.SetVariable("url", Resolve(context.ExpressionContext));
                }

                private static string Resolve(IExpressionContext context) => BackendUrl(context);

                [NamedValue("Backend-Url")]
                private static string BackendUrl(IExpressionContext context)
                    => throw new NotImplementedException();
            }
            """;

        var result = document.CompileDocument();

        result.Should().BeSuccessful().And.DocumentEquivalentTo(
            """
            <policies>
                <inbound>
                    <set-variable name="url" value="{{Backend-Url}}" />
                </inbound>
            </policies>
            """);
    }

    [TestMethod]
    public void ShouldRestoreNamedValuePlaceholdersInConditions()
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

                private static bool IsEnabled(IExpressionContext context) => FeatureEnabled(context);

                [NamedValue("Feature-Enabled")]
                private static bool FeatureEnabled(IExpressionContext context)
                    => throw new NotImplementedException();
            }
            """;

        var result = document.CompileDocument();

        result.Should().BeSuccessful();
        result.Document.Descendants("when").Single().Attribute("condition")!.Value
            .Should().Be("@({{Feature-Enabled}})");
    }

    [TestMethod]
    public void ShouldEmitNamedValueForBlockBodiedConditionHelper()
    {
        const string document =
            """
            [Document]
            public class PolicyDocument : IDocument
            {
                public void Inbound(IInboundContext context)
                {
                    if (FeatureEnabled(context.ExpressionContext))
                    {
                        context.SetVariable("matched", "true");
                    }
                }

                [NamedValue("Feature-Enabled")]
                private static bool FeatureEnabled(IExpressionContext context)
                {
                    throw new NotImplementedException();
                }
            }
            """;

        var result = document.CompileDocument();

        result.Should().BeSuccessful();
        result.Document.Descendants("when").Single().Attribute("condition")!.Value
            .Should().Be("{{Feature-Enabled}}");
    }

    [TestMethod]
    public void ShouldQuoteStringNamedValueInsideExpression()
    {
        const string document =
            """
            [Document]
            public class PolicyDocument : IDocument
            {
                public void Inbound(IInboundContext context)
                {
                    context.SetVariable("authorization", Bearer(context.ExpressionContext));
                }

                private static string Bearer(IExpressionContext context) => "Bearer " + Token(context);

                [NamedValue("api-token")]
                private static string Token(IExpressionContext context) => throw new NotImplementedException();
            }
            """;

        var result = document.CompileDocument();

        result.Should().BeSuccessful();
        result.Document.Descendants("set-variable").Single().Attribute("value")!.Value
            .Should().Be("@(\"Bearer {{api-token}}\")");
    }

    [TestMethod]
    public void ShouldNotReplacePlaceholderTextInsideStringLiteral()
    {
        var result = CompileEvaluate(
            """
            private static string Evaluate(IExpressionContext context) => "__APIM_NAMED_VALUE_0__" + Token(context);

            [NamedValue("api-token")]
            private static string Token(IExpressionContext context) => throw new NotImplementedException();
            """);

        result.Should().BeSuccessful();
        Value(result).Should().Be("@(\"__APIM_NAMED_VALUE_0__{{api-token}}\")");
    }

    [TestMethod]
    public void ShouldParenthesizeNonStringNamedValueInInterpolation()
    {
        var result = CompileEvaluate(
            """
            private static string Evaluate(IExpressionContext context) => $"{Port(context)}-{Token(context)}";

            [NamedValue("port")]
            private static int Port(IExpressionContext context) => throw new NotImplementedException();

            [NamedValue("api-token")]
            private static string Token(IExpressionContext context) => throw new NotImplementedException();
            """);

        result.Should().BeSuccessful();
        Value(result).Should().Be("@($\"{({{port}})}-{@\"{{api-token}}\"}\")");
    }

    [TestMethod]
    [DataRow("var path = context.Request.Url.Path; return \"https://\" + context.NamedValue(\"host\") + path;",
        "return \"https://{{host}}\" + path;", DisplayName = "Variable after the merged string")]
    [DataRow("var first = \"a\"; var second = \"b\"; return first + second + context.NamedValue(\"suffix\");",
        "return first + second + {{suffix}};", DisplayName = "Named value after variables stays raw")]
    public void ShouldNotMergeNamedValueWithVariable(string statements, string expected)
    {
        var result = CompileEvaluate(
            $$"""
            private static string Evaluate(IExpressionContext context)
            {
                {{statements}}
            }
            """);

        result.Should().BeSuccessful();
        Value(result).Should().Contain(expected);
    }

    [TestMethod]
    public void ShouldKeepParenthesizedNamedValueReturnedByTypedHelperRaw()
    {
        var result = CompileEvaluate(
            """
            private static int Evaluate(IExpressionContext context) => N(context) * 2;
            private static int N(IExpressionContext context) => (context.NamedValue("n"));
            """);

        result.Should().BeSuccessful();
        Value(result).Should().Be("@(((int){{n}}) * 2)");
    }

    [TestMethod]
    public void ShouldNotMergeCastNamedValueHelperIntoString()
    {
        var result = CompileEvaluate(
            """
            private static string Evaluate(IExpressionContext context) => "flag=" + Flag(context);
            private static bool Flag(IExpressionContext context) => context.NamedValue("flag");
            """);

        result.Should().BeSuccessful();
        Value(result).Should().Be("@(\"flag=\" + ((bool)({{flag}})))");
    }

    [TestMethod]
    public void ShouldNotMergeNamedValueAfterNumericOperandsIntoFollowingString()
    {
        var result = CompileEvaluate(
            """
            private static string Evaluate(IExpressionContext context) => 1 + context.NamedValue("port") + Host(context);
            private static string Host(IExpressionContext context) => context.NamedValue("host");
            """);

        result.Should().BeSuccessful();
        Value(result).Should().Be("@(1 + {{port}} + @\"{{host}}\")");
    }

    [TestMethod]
    public void ShouldMergeNamedValueAfterNonLiteralStringOperand()
    {
        var result = CompileEvaluate(
            """
            private static string Evaluate(IExpressionContext context) =>
                context.Request.Method + context.NamedValue("sep") + Host(context);
            private static string Host(IExpressionContext context) => context.NamedValue("host");
            """);

        result.Should().BeSuccessful();
        Value(result).Should().Be("@(context.Request.Method + @\"{{sep}}{{host}}\")");
    }

    [TestMethod]
    [DataRow("Bearer(context.NamedValue(\"k\"))", "@(\"Bearer {{k}}\")", DisplayName = "Used once")]
    [DataRow("Join(context.NamedValue(\"a\"), context.NamedValue(\"b\"))", "@(\"{{a}}-{{b}}{{a}}\")",
        DisplayName = "Used more than once")]
    public void ShouldPassNamedValueToStringParameterAsString(string call, string expected)
    {
        var result = CompileEvaluate(
            $$"""
            private static string Evaluate(IExpressionContext context) => {{call}};
            private static string Bearer(string token) => "Bearer " + token;
            private static string Join(string first, string second) => first + "-" + second + first;
            """);

        result.Should().BeSuccessful();
        Value(result).Should().Be(expected);
    }

    [TestMethod]
    public void ShouldCastNamedValueHelperPassedToRootHelper()
    {
        var result =
            """
            [Document]
            public class PolicyDocument : IDocument
            {
                public void Inbound(IInboundContext context)
                {
                    context.SetVariable("value", Evaluate(Flag(context.ExpressionContext)));
                }

                private static bool Flag(IExpressionContext context) => context.NamedValue("f");
                private static string Evaluate(bool value) => !value ? "n" : "y";
            }
            """.CompileDocument();

        result.Should().BeSuccessful();
        Value(result).Should().Contain("!((bool)({{f}}))");
    }

    [TestMethod]
    [DataRow("context.NamedValue(\"n\")", "((long)({{n}})) * 2", DisplayName = "Named value call")]
    [DataRow("Port()", "((long)({{port}})) * 2", DisplayName = "[NamedValue] helper")]
    public void ShouldCastWholeNamedValuePassedToTypedParameter(string argument, string expected)
    {
        var result = CompileEvaluate(
            $$"""
            private static long Evaluate(IExpressionContext context) => Twice({{argument}});
            [NamedValue("port")]
            private static int Port() => 0;
            private static long Twice(long value) => value * 2;
            """);

        result.Should().BeSuccessful();
        Value(result).Should().Contain(expected);
    }

    [TestMethod]
    public void ShouldKeepParenthesizedNamedValueReturnedByStringHelperRaw()
    {
        var result = CompileEvaluate(
            """
            private static string Evaluate(IExpressionContext context) => "Bearer " + N(context);
            private static string N(IExpressionContext context) => (context.NamedValue("n"));
            """);

        result.Should().BeSuccessful();
        Value(result).Should().Be("@(\"Bearer \" + ((string){{n}}))");
    }

    [TestMethod]
    [DataRow("context.NamedValue(\"n\")", "{{n}}", DisplayName = "Literal name")]
    [DataRow("context.NamedValue(Prefix + \"key\")", "{{pre-key}}", DisplayName = "Constant name")]
    [DataRow("context.NamedValue(name: \"n\")", "{{n}}", DisplayName = "Named argument")]
    public void ShouldCastWholeNamedValueCallInExplicitCast(string call, string token)
    {
        var result = CompileEvaluate(
            $$"""
            private const string Prefix = "pre-";
            private static int Evaluate(IExpressionContext context) => (int){{call}} * 2;
            """);

        result.Should().BeSuccessful();
        Value(result).Should().Be("@((int)(" + token + ") * 2)");
    }

    [TestMethod]
    [DataRow("((context.NamedValue(\"n\"))) * 2", "@(({{n}}) * 2)",
        DisplayName = "A second pair of parentheses is kept")]
    [DataRow("((string)context.NamedValue(\"k\")).ToUpper()", "@(@\"{{k}}\".ToUpper())",
        DisplayName = "(string) cast gives a verbatim string")]
    [DataRow("((System.String)context.NamedValue(\"x\")).Length", "@(@\"{{x}}\".Length)",
        DisplayName = "(System.String) cast gives a verbatim string")]
    [DataRow("((string)(context.NamedValue(\"k\"))).ToUpper()", "@(((string){{k}}).ToUpper())",
        DisplayName = "Cast of the parenthesized call keeps the raw token")]
    [DataRow("@\"\" + context.NamedValue(\"pattern\")", "@(@\"{{pattern}}\")",
        DisplayName = "Merged into an empty verbatim literal")]
    [DataRow("(string)context?.NamedValue(\"x\") + \"a\"", "@(\"{{x}}a\")",
        DisplayName = "Conditional access cast to string")]
    [DataRow("context?.NamedValue(\"x\").Length", "@({{x}}.Length)",
        DisplayName = "Conditional access continued by a member")]
    [DataRow("context?.NamedValue(\"x\")?.ToString()", "@({{x}}?.ToString())",
        DisplayName = "Conditional access continued by another conditional access")]
    [DataRow("$\"{ (context.NamedValue(\"a\")) + 1 }\"", "@($\"{({{a}}) + 1}\")",
        DisplayName = "Parenthesized call at the start of an interpolation hole")]
    [DataRow("$\"{context.NamedValue(\"a\").Length}\"", "@($\"{({{a}}).Length}\")",
        DisplayName = "Call at the start of an interpolation hole")]
    public void ShouldCompileDirectNamedValueCallForms(string expression, string expected)
    {
        var result = CompileEvaluate(
            $$"""
            private static object Evaluate(IExpressionContext context) => {{expression}};
            """);

        result.Should().BeSuccessful();
        Value(result).Should().Be(expected);
    }

    [TestMethod]
    [DataRow("Join(context.NamedValue(\"a\"), \"b\")", "@(string.Join(\",\", new string[] { @\"{{a}}\", \"b\" }))")]
    [DataRow("Join(context.NamedValue(\"a\"))", "@(string.Join(\",\", new string[] { @\"{{a}}\" }))")]
    public void ShouldBindNamedValueArgumentsToParamsArray(string call, string expected)
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
    [DataRow("N(context, \"x\")")]
    [DataRow("L(context, \"x\")")]
    public void ShouldCompileConditionalNamedValueWithNameFromArgument(string call)
    {
        var result = CompileEvaluate(
            $$"""
            private static object Evaluate(IExpressionContext context) => {{call}};
            private static object N(IExpressionContext c, string name) => c?.NamedValue(name);
            private static object L(IExpressionContext c, string name) => c?.NamedValue(name).Length;
            """);

        result.Should().BeSuccessful();
        Value(result).Should().Contain("{{x}}").And.NotContain("NamedValue");
    }

    [TestMethod]
    public void ShouldReportNamedValueAfterConditionalExtensionCall()
    {
        var result = CompileEvaluate(
            """
            private static object Evaluate(IExpressionContext context) => context?.Self().NamedValue("x").Length;
            """,
            """
            public static class ContextExtensions
            {
                public static IExpressionContext Self(this IExpressionContext context) => context;
            }
            """);

        result.Errors.Should().Contain(error => error.Id == "APIM2013");
    }

    [TestMethod]
    [DataRow("(string)Get(context) + 1", "@(@\"{{n}}\" + 1)", DisplayName = "(string) cast of a helper result")]
    [DataRow("(int)Get(context) + 1", "@((int)({{n}}) + 1)", DisplayName = "(int) cast of a helper result")]
    [DataRow("ToText(context.NamedValue(\"n\")).Length", "@(@\"{{n}}\".Length)",
        DisplayName = "(string) cast of an argument")]
    [DataRow("(int)N() * 2", "@((int)({{n}}) * 2)", DisplayName = "(int) cast of a [NamedValue] helper")]
    [DataRow("((string)N()).Length", "@(@\"{{n}}\".Length)", DisplayName = "(string) cast of a [NamedValue] helper")]
    public void ShouldConvertExplicitCastOfNamedValueFromHelperOrArgument(string expression, string expected)
    {
        var result = CompileEvaluate(
            $$"""
            private static object Evaluate(IExpressionContext context) => {{expression}};
            private static dynamic Get(IExpressionContext context) => context.NamedValue("n");
            private static string ToText(dynamic value) => (string)value;
            [NamedValue("n")]
            private static dynamic N() => null;
            """);

        result.Should().BeSuccessful();
        Value(result).Should().Be(expected);
    }

    [TestMethod]
    [DataRow("IsOn(context.ExpressionContext)", "context.NamedValue(\"flag\")", "@({{flag}})",
        DisplayName = "Called directly in the condition, without a cast")]
    [DataRow("!IsOn(context.ExpressionContext)", "context.NamedValue(\"flag\")", "@(!({{flag}}))",
        DisplayName = "Named value call under !")]
    [DataRow("!IsOn(context.ExpressionContext)", "N()", "@(!({{flag}}))",
        DisplayName = "[NamedValue] helper under !")]
    public void ShouldCompileNamedValueConditionHelper(string condition, string body, string expected)
    {
        var result =
            $$"""
            [Document]
            public class PolicyDocument : IDocument
            {
                public void Inbound(IInboundContext context)
                {
                    if ({{condition}})
                    {
                        context.SetVariable("matched", "true");
                    }
                }

                private static bool IsOn(IExpressionContext context) => {{body}};
                [NamedValue("flag")]
                private static dynamic N() => null;
            }
            """.CompileDocument();

        result.Should().BeSuccessful();
        result.Document.Descendants("when").Single().Attribute("condition")!.Value.Should().Be(expected);
    }

    [TestMethod]
    public void ShouldNotMergeNonStringNamedValueIntoString()
    {
        var result = CompileEvaluate(
            """
            private static string Evaluate(IExpressionContext context) => "flag=" + Enabled(context);

            [NamedValue("enabled")]
            private static bool Enabled(IExpressionContext context) => throw new NotImplementedException();
            """);

        result.Should().BeSuccessful();
        Value(result).Should().Be("@(\"flag=\" + {{enabled}})");
    }

    [TestMethod]
    [DataRow("\"host\"", "@(\"https://{{host}}/api\")", DisplayName = "Literal name")]
    [DataRow("HostName", "@(\"https://{{backend-host}}/api\")", DisplayName = "Constant name")]
    public void ShouldQuoteNamedValueReturnedFromStringHelper(string name, string expected)
    {
        var result = CompileEvaluate(
            $$"""
            private const string HostName = "backend-host";
            private static string Evaluate(IExpressionContext context) => "https://" + Host(context) + "/api";
            private static string Host(IExpressionContext context) => context.NamedValue({{name}});
            """);

        result.Should().BeSuccessful();
        Value(result).Should().Be(expected);
    }

    [TestMethod]
    [DataRow("context.NamedValue(\"host\")", "string host = @\"{{host}}\";", DisplayName = "Named value call")]
    [DataRow("N()", "string host = @\"{{n}}\";", DisplayName = "[NamedValue] helper")]
    [DataRow("(context.NamedValue(\"host\"))", "string host = {{host}};",
        DisplayName = "Parenthesized call keeps the raw token")]
    public void ShouldConvertNamedValueAssignedToString(string initializer, string expected)
    {
        var result = CompileEvaluate(
            $$"""
            private static string Evaluate(IExpressionContext context)
            {
                string host = {{initializer}};
                return host.ToUpper();
            }
            [NamedValue("n")]
            private static dynamic N() => null;
            """);

        result.Should().BeSuccessful();
        Value(result).Should().Contain(expected);
    }
}