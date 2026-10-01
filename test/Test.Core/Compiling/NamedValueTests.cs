// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling;

[TestClass]
public class NamedValueTests
{
    [TestMethod]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.SetVariable("backend-url", BackendUrl(context.ExpressionContext));
            }
            
            [NamedValue("Api-Backend-Url")]
            string BackendUrl(IExpressionContext context) => throw new NotImplementedException();
        }
        """,
        """
        <policies>
            <inbound>
                <set-variable name="backend-url" value="{{Api-Backend-Url}}" />
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile simple NamedValue to {{token}}"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.SetVariable("url", BackendUrl(context.ExpressionContext));
            }
            
            [NamedValue("{{Api-Backend-Url}}/v2.0/prediction")]
            string BackendUrl(IExpressionContext context) => throw new NotImplementedException();
        }
        """,
        """
        <policies>
            <inbound>
                <set-variable name="url" value="{{Api-Backend-Url}}/v2.0/prediction" />
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile NamedValue template with embedded tokens as-is"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.SetHeader("Authorization", AuthHeader(context.ExpressionContext));
            }
            
            [NamedValue("{{Auth-Scheme}} {{Auth-Token}}")]
            string AuthHeader(IExpressionContext context) => throw new NotImplementedException();
        }
        """,
        """
        <policies>
            <inbound>
                <set-header name="Authorization">
                    <value>{{Auth-Scheme}} {{Auth-Token}}</value>
                </set-header>
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile NamedValue template with multiple tokens"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.SetVariable("key", ApiKey(context.ExpressionContext));
                context.SetVariable("endpoint", Endpoint(context.ExpressionContext));
            }
            
            [NamedValue("My-Api-Key")]
            string ApiKey(IExpressionContext context) => throw new NotImplementedException();
            
            [NamedValue("{{Service-Url}}/api/v1")]
            string Endpoint(IExpressionContext context) => throw new NotImplementedException();
        }
        """,
        """
        <policies>
            <inbound>
                <set-variable name="key" value="{{My-Api-Key}}" />
                <set-variable name="endpoint" value="{{Service-Url}}/api/v1" />
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile mixed simple and template NamedValues"
    )]
    public void ShouldCompileNamedValue(string code, string expectedXml)
    {
        code.CompileDocument().Should().BeSuccessful().And.DocumentEquivalentTo(expectedXml);
    }

    [TestMethod]
    [DataRow("\"Bearer \" + context.NamedValue(\"api-token\")", "@(\"Bearer {{api-token}}\")",
        DisplayName = "Should merge named value call into preceding string literal")]
    [DataRow("context.NamedValue(\"host\") + \"/api\"", "@(\"{{host}}/api\")",
        DisplayName = "Should merge named value call into following string literal")]
    [DataRow("\"a\" + context.NamedValue(\"x\") + \"b\"", "@(\"a{{x}}b\")",
        DisplayName = "Should merge named value call between string literals")]
    [DataRow("\"a\" + context.NamedValue(\"x\") + context.NamedValue(\"y\")", "@(\"a{{x}}{{y}}\")",
        DisplayName = "Should merge consecutive named value calls into preceding string literal")]
    [DataRow("\"a\" + context.NamedValue(\"n\") * 2", "@(\"a\" + {{n}} * 2)",
        DisplayName = "Should not merge named value call bound to a tighter operator")]
    [DataRow("1 + context.NamedValue(\"n\") + \"b\"", "@(1 + {{n}} + \"b\")",
        DisplayName = "Should not merge named value call added to a number first")]
    [DataRow("context.NamedValue(\"count\") % 3", "@({{count}} % 3)",
        DisplayName = "Should keep named value call used as code")]
    [DataRow("\"enabled=\" + (context.NamedValue(\"flag\"))", "@(\"enabled=\" + {{flag}})",
        DisplayName = "Should keep parenthesized named value call raw next to string")]
    [DataRow("\"a\" + context.NamedValue(\"x\") + @\"\\p\"", "@(\"a{{x}}\" + @\"\\p\")",
        DisplayName = "Should not merge regular and verbatim strings")]
    [DataRow("@\"C:\\\" + context.NamedValue(\"dir\") + @\"\\file\"", "@(@\"C:\\{{dir}}\\file\")",
        DisplayName = "Should keep verbatim string merged with named value call verbatim")]
    [DataRow("$\"a\" + context.NamedValue(\"x\") + $\"b{context.RequestId}\"", "@($\"a{{x}}b{context.RequestId}\")",
        DisplayName = "Should merge named value call into interpolated strings")]
    [DataRow("\"a\" + context.NamedValue(\"x\") + $\"b{context.RequestId}\"", "@(\"a{{x}}\" + $\"b{context.RequestId}\")",
        DisplayName = "Should not merge regular and interpolated strings")]
    [DataRow("\"{{x}}\" + context.NamedValue(\"y\")", "@(\"{{x}}{{y}}\")",
        DisplayName = "Should keep named value text inside existing literal")]
    public void ShouldCompileNamedValueCallInExpression(string expression, string expectedValue)
    {
        var code =
            $$"""
            [Document]
            public class PolicyDocument : IDocument
            {
                public void Inbound(IInboundContext context) {
                    context.SetVariable("value", Value(context.ExpressionContext));
                }

                object Value(IExpressionContext context) => {{expression}};
            }
            """;

        var result = code.CompileDocument();

        result.Should().BeSuccessful();
        result.Document.Descendants("set-variable").Single().Attribute("value")!.Value.Should().Be(expectedValue);
    }
}
