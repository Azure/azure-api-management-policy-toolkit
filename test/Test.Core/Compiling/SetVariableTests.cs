// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling;

[TestClass]
public class SetVariableTests
{
    [TestMethod]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.SetVariable("Inbound", "setting");
            }
            public void Backend(IBackendContext context) {
                context.SetVariable("Backend", "setting");
            }
            public void Outbound(IOutboundContext context) {
                context.SetVariable("Outbound", "setting");
            }
            public void OnError(IOnErrorContext context) {
                context.SetVariable("OnError", "setting");
            }
        }
        """,
        """
        <policies>
            <inbound>
                <set-variable name="Inbound" value="setting" />
            </inbound>
            <backend>
                <set-variable name="Backend" value="setting" />
            </backend>
            <outbound>
                <set-variable name="Outbound" value="setting" />
            </outbound>
            <on-error>
                <set-variable name="OnError" value="setting" />
            </on-error>
        </policies>
        """,
        DisplayName = "Should compile set variable policy in sections"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.SetVariable("Inbound", Exp(context.ExpressionContext));
            }
            
            string Exp(IExpressionContext context)
                => context.RequestId.ToString();
        }
        """,
        """
        <policies>
            <inbound>
                <set-variable name="Inbound" value="@(context.RequestId.ToString())" />
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile set variable policy with one line expression"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.SetVariable("Inbound", Exp(context.ExpressionContext));
            }
            
            string Exp(IExpressionContext context) {
                return context.RequestId.ToString();
            }
        }
        """,
        "<policies>\r\n    <inbound>\r\n        <set-variable name=\"Inbound\" value=\"@{\r\nreturn context.RequestId.ToString();\r\n}\" />\r\n    </inbound>\r\n</policies>",
        DisplayName = "Should compile set variable policy with multi line expression"
    )]
    public void ShouldCompileSetVariablePolicy(string code, string expectedXml)
    {
        code.CompileDocument().Should().BeSuccessful().And.DocumentEquivalentTo(expectedXml);
    }

    [TestMethod]
    public void ShouldWriteNonStringConstantVariableValuesAsExpressions()
    {
        var code =
            """
            [Document]
            public class PolicyDocument : IDocument
            {
                private const int Limit = 10;
                private const string Name = "text";

                public void Inbound(IInboundContext context)
                {
                    context.SetVariable("a", 5);
                    context.SetVariable("b", true);
                    context.SetVariable("c", 5L);
                    context.SetVariable("d", 5u);
                    context.SetVariable("e", 1.5);
                    context.SetVariable("f", 2d);
                    context.SetVariable("g", 1.5m);
                    context.SetVariable("h", 1.5f);
                    context.SetVariable("i", 'x');
                    context.SetVariable("j", Limit + 1);
                    context.SetVariable("k", "5");
                    context.SetVariable("l", Name);
                }
            }
            """;

        code.CompileDocument().Should().BeSuccessful().And.DocumentEquivalentTo(
            """
            <policies>
                <inbound>
                    <set-variable name="a" value="@(5)" />
                    <set-variable name="b" value="@(true)" />
                    <set-variable name="c" value="@(5L)" />
                    <set-variable name="d" value="@(5u)" />
                    <set-variable name="e" value="@(1.5)" />
                    <set-variable name="f" value="@(2d)" />
                    <set-variable name="g" value="@(1.5m)" />
                    <set-variable name="h" value="@(1.5f)" />
                    <set-variable name="i" value="@('x')" />
                    <set-variable name="j" value="@(11)" />
                    <set-variable name="k" value="5" />
                    <set-variable name="l" value="text" />
                </inbound>
            </policies>
            """);
    }

    [TestMethod]
    // Uri and enum values are reported too; the references of this test compilation don't resolve them
    [DataRow("object", "new { a = 1 }")]
    [DataRow("object", "System.Tuple.Create(1, \"a\")")]
    [DataRow("object", "new System.Collections.Generic.Dictionary<string, string>()")]
    [DataRow("int[]", "new[] { 1 }")]
    public void ShouldReportVariableValueOfATypeTheGatewayRejects(string returnType, string expression)
    {
        var result =
            $$"""
              [Document]
              public class PolicyDocument : IDocument
              {
                  public void Inbound(IInboundContext context)
                  {
                      context.SetVariable("v", Value(context.ExpressionContext));
                  }

                  {{returnType}} Value(IExpressionContext context) => {{expression}};
              }
              """.CompileDocument();

        result.Errors.Should().ContainSingle(error => error.Id == "APIM2034");
    }

    [TestMethod]
    [DataRow("string[]", "new[] { \"a\" }")]
    [DataRow("byte[]", "new byte[] { 1 }")]
    [DataRow("object", "context.RequestId")]
    [DataRow("System.TimeSpan", "context.Elapsed")]
    public void ShouldAcceptVariableValueOfATypeTheGatewayAccepts(string returnType, string expression)
    {
        var result =
            $$"""
              [Document]
              public class PolicyDocument : IDocument
              {
                  public void Inbound(IInboundContext context)
                  {
                      context.SetVariable("v", Value(context.ExpressionContext));
                  }

                  {{returnType}} Value(IExpressionContext context) => {{expression}};
              }
              """.CompileDocument();

        result.Errors.Should().BeEmpty();
    }

    [TestMethod]
    [DataRow("double.NaN", "NaN")]
    [DataRow("float.PositiveInfinity", "Infinity")]
    public void ShouldKeepNonFiniteConstantAsText(string constant, string expected)
    {
        // NaN and the infinities have no literal that could be written as an expression
        var result = CompilerTestInitialize.InboundDocument($"context.SetVariable(\"v\", {constant});").CompileDocument();

        result.Should().BeSuccessful();
        result.Document.Descendants("set-variable").Single().Attribute("value")!.Value.Should().Be(expected);
    }
}
