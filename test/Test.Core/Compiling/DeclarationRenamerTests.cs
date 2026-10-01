// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using static Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling.PolicyExpressionTestHelpers;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling;

[TestClass]
public class DeclarationRenamerTests
{
    [TestMethod]
    public void ShouldRenameHelperLambdaParameterCapturingArgument()
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
                {
                    var h = "x";
                    return Contains(context, h);
                }

                private static bool Contains(IExpressionContext context, string value)
                    => System.Array.Exists(new[] { context.Request.Method }, h => h == value);
            }
            """;

        var result = document.CompileDocument();

        result.Should().BeSuccessful();
        Value(result).Should().Contain("h__1 => h__1 == h");
    }

    [TestMethod]
    public void ShouldRenameVariablesDeclaredByInlinedHelper()
    {
        var result = CompileEvaluate(
            """
            private static string Evaluate(IExpressionContext context) => (Parse("1") + Parse("2")).ToString();
            private static int Parse(string value) => int.TryParse(value, out var number) ? number : 0;
            """);

        result.Should().BeSuccessful();
        Value(result).Should().Be(
            "@(((int.TryParse(\"1\", out var number__1) ? number__1 : 0) + " +
            "(int.TryParse(\"2\", out var number__2) ? number__2 : 0)).ToString())");
    }

    [TestMethod]
    public void ShouldRenameInlinedLambdaParameterClashingWithCallSite()
    {
        var result = CompileEvaluate(
            """
            private static bool Evaluate(IExpressionContext context)
                => System.Array.Exists(new[] { context.Request.Method }, x => HasA(x));

            private static bool HasA(string value)
                => value.Length > 0 && System.Array.Exists(value.ToCharArray(), x => x == 'a');
            """);

        result.Should().BeSuccessful();
        Value(result).Should().Contain("x => (x.Length > 0 && System.Array.Exists(x.ToCharArray(), x__1 => x__1 == 'a'))");
    }

    [TestMethod]
    public void ShouldRenameHelperLocalNamedContext()
    {
        var result = CompileEvaluate(
            """
            private static string Evaluate(IExpressionContext ctx)
            {
                var context = "x";
                return context;
            }
            """);

        result.Should().BeSuccessful();
        Value(result).Should().Contain("var context__1 = \"x\";").And.Contain("return context__1;");
    }

    [TestMethod]
    [DataRow("context.Variables.TryGetValue(\"k\", out var value) && value != null",
        DisplayName = "Should allow out variable in condition helper")]
    [DataRow("context.Variables[\"k\"] is string text && text.Length > 0",
        DisplayName = "Should allow pattern variable in condition helper")]
    public void ShouldAllowDeclarationsInConditionHelper(string body)
    {
        var result =
            $$"""
              [Document]
              public class PolicyDocument : IDocument
              {
                  public void Inbound(IInboundContext context)
                  {
                      if (HasKey(context.ExpressionContext))
                      {
                          context.SetVariable("matched", "true");
                      }
                  }

                  private static bool HasKey(IExpressionContext context) => {{body}};
              }
              """.CompileDocument();

        result.Should().BeSuccessful();
        result.Document.Descendants("when").Single().Attribute("condition")!.Value.Should().Be($"@({body})");
    }

    [TestMethod]
    public void ShouldRenameSameOutVariableAcrossConditionHelpers()
    {
        var result =
            """
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

                private static bool HasA(IExpressionContext context) => context.Variables.TryGetValue("a", out var value);
                private static bool HasB(IExpressionContext context) => context.Variables.TryGetValue("b", out var value);
            }
            """.CompileDocument();

        result.Should().BeSuccessful();
        result.Document.Descendants("when").Single().Attribute("condition")!.Value
            .Should().Contain("out var value__1").And.Contain("out var value__2");
    }

    [TestMethod]
    [DataRow(
        "System.Array.Exists(new[] { context.Request.Method }, a => int.TryParse(a, out var n) && n > 1) || " +
        "System.Array.Exists(new[] { context.Request.Method }, b => int.TryParse(b, out var n) && n > 2)",
        DisplayName = "Should allow same out variable in sibling lambdas")]
    [DataRow(
        "System.Array.Exists(new[] { context.Request.Method }, a => a is string s && s.Length > 1) || " +
        "System.Array.Exists(new[] { context.Request.Method }, b => b is string s && s.Length > 2)",
        DisplayName = "Should allow same pattern variable in sibling lambdas")]
    public void ShouldAllowSameDeclarationInSiblingLambdasOfCondition(string body)
    {
        var result = CompileCondition($"private static bool Matches(IExpressionContext context) => {body};");

        result.Should().BeSuccessful();
    }

    [TestMethod]
    public void ShouldRenameLambdaParameterPassedToNestedHelper()
    {
        var result = CompileEvaluate(
            """
            private static bool Evaluate(IExpressionContext context) => Outer(context);
            private static bool Outer(IExpressionContext context) =>
                System.Array.Exists(new[] { context.Request.Method }, x => HasA(x));
            private static bool HasA(string value) => value.Length > 0;
            """);

        result.Should().BeSuccessful();
        Value(result).Should().MatchRegex(@"(x__\d+) => \(\1\.Length > 0\)");
    }

    [TestMethod]
    public void ShouldRenameVariablePassedToNestedHelper()
    {
        var result = CompileEvaluate(
            """
            private static bool Evaluate(IExpressionContext context) =>
                context.Variables.TryGetValue("x", out var v) && Outer(context);
            private static bool Outer(IExpressionContext context) =>
                context.Variables.TryGetValue("a", out var v) && HasA((string)v);
            private static bool HasA(string value) => value.Length > 0;
            """);

        result.Should().BeSuccessful();
        Value(result).Should().MatchRegex(@"out var (v__\d+)\) && \(\(\(string\)\1\)\.Length > 0\)");
    }

    [TestMethod]
    public void ShouldRenameContextLambdaParameterPassedToHelper()
    {
        var result = CompileEvaluate(
            """
            private static bool Evaluate(IExpressionContext c) =>
                System.Array.Exists(new object[] { null }, context => IsSet(context));
            private static bool IsSet(object value) => value != null;
            """);

        result.Should().BeSuccessful();
        Value(result).Should().MatchRegex(@"(context__\d+) => \(\1 != null\)");
    }

    [TestMethod]
    public void ShouldRenameVariablePassedToNestedHelperInCondition()
    {
        var result =
            """
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
                    context.Variables.TryGetValue("a", out var value) && Check((string)value);
                private static bool HasB(IExpressionContext context) => context.Variables.TryGetValue("b", out var value);
                private static bool Check(string text) => text.Length > 0;
            }
            """.CompileDocument();

        result.Should().BeSuccessful();
        result.Document.Descendants("when").Single().Attribute("condition")!.Value
            .Should().MatchRegex(@"out var (value__\d+)\) && \(\(\(string\)\1\)\.Length > 0\)");
    }

    [TestMethod]
    public void ShouldRenameRootHelperDeclarationsClashingWithArguments()
    {
        var result =
            """
            [Document]
            public class PolicyDocument : IDocument
            {
                public void Inbound(IInboundContext context)
                {
                    context.SetVariable("value", Evaluate(context.ExpressionContext, context.ExpressionContext.Variables["a"] is string s ? s : ""));
                }

                private static string Evaluate(IExpressionContext context, string value) =>
                    value + (context.Variables["b"] is string s ? s : "");
            }
            """.CompileDocument();

        result.Should().BeSuccessful();
        Value(result).Should().MatchRegex(@"is string s \? s : """"\) \+ \(.*is string (s__\d+) \? \1 : """"\)");
    }

    [TestMethod]
    public void ShouldKeepRootHelperNamesInSiblingScopes()
    {
        var result = CompileEvaluate(
            """
            private static string Evaluate(IExpressionContext context)
            {
                var result = "";
                foreach (var item in context.Request.Method.Split(',')) { result += item; }
                foreach (var item in context.Request.Method.Split(';')) { result += item; }
                return result;
            }
            """);

        result.Should().BeSuccessful();
        Value(result).Should().NotContain("__");
    }

    [TestMethod]
    public void ShouldRenameForEachVariablesOfInlinedHelpers()
    {
        var result = CompileEvaluate(
            """
            private static bool Evaluate(IExpressionContext context) =>
                context.Variables.TryGetValue("a", out var item) && AnyMatch(context, (string)item);
            private static bool AnyMatch(IExpressionContext context, string value) =>
                context.Variables.Keys.Any(key => { foreach (var item in key.Split(',')) { if (item == value) { return true; } } return false; });
            """);

        result.Should().BeSuccessful();
        Value(result).Should().Contain("foreach (var item__").And.Contain("== ((string)item)");
    }

    [TestMethod]
    public void ShouldRenameCatchVariablesOfInlinedHelpers()
    {
        var result = CompileEvaluate(
            """
            private static bool Evaluate(IExpressionContext context) =>
                context.Variables.TryGetValue("a", out var e) && Matches(context, (string)e);
            private static bool Matches(IExpressionContext context, string value) =>
                context.Variables.Keys.Any(key => { try { return key == value; } catch (System.Exception e) { return e == null; } });
            """);

        result.Should().BeSuccessful();
        Value(result).Should().Contain("catch (System.Exception e__").And.Contain("key__");
    }

    [TestMethod]
    [DataRow("context.Variables.Keys.Any(key => { var value = key; return value == \"b\"; })", "var value__")]
    [DataRow("(from value in context.Variables.Keys select value).Any()", "from value__")]
    [DataRow("context.Variables.Keys.Any(key => { foreach (var value in key.Split(',')) { return true; } return false; })", "foreach (var value__")]
    public void ShouldRenameConditionHelperDeclarationsOfAnyKind(string body, string renamed)
    {
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

                private static bool HasA(IExpressionContext context) => context.Variables.TryGetValue("a", out var value);
                private static bool HasB(IExpressionContext context) => {{body}};
            }
            """.CompileDocument();

        result.Should().BeSuccessful();
        result.Document.Descendants("when").Single().Attribute("condition")!.Value.Should().Contain(renamed);
    }

    [TestMethod]
    public void ShouldRenameClashingDeclarationsAcrossConditionHelpers()
    {
        var result =
            """
            [Document]
            public class PolicyDocument : IDocument
            {
                public void Inbound(IInboundContext context)
                {
                    if (IsNamed(context.ExpressionContext) && HasB(context.ExpressionContext))
                    {
                        context.SetVariable("matched", "true");
                    }
                }

                private static bool IsNamed(IExpressionContext context) => context.Variables["n"] is string s && s.Length > 0;
                private static bool HasB(IExpressionContext context) => System.Array.Exists(new[] { "a" }, s => s == "b");
            }
            """.CompileDocument();

        result.Should().BeSuccessful();
        result.Document.Descendants("when").Single().Attribute("condition")!.Value
            .Should().Contain("is string s__").And.Contain("s__2 => s__2 == \"b\"");
    }

    [TestMethod]
    [DataRow(
        "{ var h = context.Request.Method; var found = HasX(context); return h + found; }",
        DisplayName = "Should rename nested helper lambda clashing with root local")]
    [DataRow(
        "=> System.Array.Exists(new[] { context.Request.Method }, h => h == \"a\" && HasX(context)).ToString();",
        DisplayName = "Should rename nested helper lambda clashing with enclosing lambda")]
    public void ShouldRenameNestedHelperDeclarationsClashingWithCaller(string body)
    {
        var result = CompileEvaluate(
            $$"""
              private static string Evaluate(IExpressionContext context) {{body}}
              private static bool HasX(IExpressionContext context) => Has(context, "x");
              private static bool Has(IExpressionContext context, string name)
                  => System.Array.Exists(new[] { context.Request.Method }, h => h == name);
              """);

        result.Should().BeSuccessful();
        Value(result).Should().Contain("h__1 => h__1 == \"x\"");
    }
}