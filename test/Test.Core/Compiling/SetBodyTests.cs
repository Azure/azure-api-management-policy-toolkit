// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling;

[TestClass]
public class SetBodyTests
{
    [TestMethod]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.SetBody("inbound");
            }
            public void Backend(IBackendContext context) {
                context.SetBody("backend");
            }
            public void Outbound(IOutboundContext context) {
                context.SetBody("outbound");
            }
            public void OnError(IOnErrorContext context) {
                context.SetBody("on-error");
            }
        }
        """,
        """
        <policies>
            <inbound>
                <set-body>inbound</set-body>
            </inbound>
            <backend>
                <set-body>backend</set-body>
            </backend>
            <outbound>
                <set-body>outbound</set-body>
            </outbound>
            <on-error>
                <set-body>on-error</set-body>
            </on-error>
        </policies>
        """,
        DisplayName = "Should compile set body policy in sections"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.SetBody(Exp(context.ExpressionContext));
            }
            public void Outbound(IOutboundContext context) {
                context.SetBody(Exp(context.ExpressionContext));
            }
            
            string Exp(IExpressionContext context)
            {
                return context.RequestId.ToString();
            }
        }
        """,
        "<policies>\r\n    <inbound>\r\n        <set-body>@{\r\nreturn context.RequestId.ToString();\r\n}</set-body>\r\n    </inbound>\r\n    <outbound>\r\n        <set-body>@{\r\nreturn context.RequestId.ToString();\r\n}</set-body>\r\n    </outbound>\r\n</policies>",
        DisplayName = "Should compile set body policy with expressions"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.SetBody("inbound", new SetBodyConfig {
                    Template = "liquid",
                });
            }
            public void Outbound(IOutboundContext context) {
                context.SetBody("outbound", new SetBodyConfig {
                    Template = "liquid",
                });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <set-body template="liquid">inbound</set-body>
            </inbound>
            <outbound>
                <set-body template="liquid">outbound</set-body>
            </outbound>
        </policies>
        """,
        DisplayName = "Should compile set body policy with template in config"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.SetBody("inbound", new SetBodyConfig {
                    XsiNil = "blank",
                });
            }
            public void Outbound(IOutboundContext context) {
                context.SetBody("outbound", new SetBodyConfig {
                    XsiNil = "null",
                });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <set-body xsi-nil="blank">inbound</set-body>
            </inbound>
            <outbound>
                <set-body xsi-nil="null">outbound</set-body>
            </outbound>
        </policies>
        """,
        DisplayName = "Should compile set body policy with XsiNil in config"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.SetBody("inbound", new SetBodyConfig {
                    ParseDate = true,
                });
            }
            public void Outbound(IOutboundContext context) {
                context.SetBody("outbound", new SetBodyConfig {
                    ParseDate = false,
                });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <set-body parse-date="true">inbound</set-body>
            </inbound>
            <outbound>
                <set-body parse-date="false">outbound</set-body>
            </outbound>
        </policies>
        """,
        DisplayName = "Should compile set body policy with ParseDate in config"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.SetBody("content", new SetBodyConfig {
                    UseValueElement = true,
                });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <set-body>
                    <value>content</value>
                </set-body>
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile set body policy with UseValueElement wrapping content"
    )]
    public void ShouldCompileForwardRequestPolicy(string code, string expectedXml)
    {
        code.CompileDocument().Should().BeSuccessful().And.DocumentEquivalentTo(expectedXml);
    }

    [TestMethod]
    public void ShouldWriteLiquidXmlBodyAsMarkupAndOtherBodiesAsText()
    {
        var code = CompilerTestInitialize.InboundDocument(
            """
            context.SetBody("<Envelope><a>1</a>  <b note='x &amp; y'>{{body.x}}</b></Envelope>", new SetBodyConfig { Template = "liquid" });
            context.SetBody("<plain><a>1</a></plain>");
            context.SetBody("{ \"a\": \"{{body.x}}\" }", new SetBodyConfig { Template = "liquid" });
            context.SetBody("{% if body.n < 2 && body.m %}<p>{{body.x}} & co<![CDATA[a & b < c]]><!-- d & e < f --></p>{% endif %}", new SetBodyConfig { Template = "liquid" });
            context.SetBody("@(context.RequestId)<a />", new SetBodyConfig { Template = "liquid" });
            context.SetBody("@(1 < 2 ? \"<a>\" : \"b\")", new SetBodyConfig { Template = "liquid", UseValueElement = true });
            """);

        var result = code.CompileDocument();
        result.Should().BeSuccessful();

        var written = new System.Text.StringBuilder();
        using (var writer = Serialization.CustomXmlWriter.Create(
                   written, new System.Xml.XmlWriterSettings { Indent = true, OmitXmlDeclaration = true }))
        {
            writer.Write(result.Document);
        }

        var xml = written.ToString();
        // the gateway returns a liquid template written as escaped text still escaped: it has to be markup,
        // written without indentation added inside it, in the normal form of the markup (double-quoted attributes)
        xml.Should().Contain(
            "<set-body template=\"liquid\"><Envelope><a>1</a>  <b note=\"x &amp; y\">{{body.x}}</b></Envelope></set-body>");
        // and returns a body without a template that is written as markup empty: it has to stay text
        xml.Should().Contain("<set-body>&lt;plain&gt;&lt;a&gt;1&lt;/a&gt;&lt;/plain&gt;</set-body>");
        xml.Should().Contain("<set-body template=\"liquid\">{ \"a\": \"{{body.x}}\" }</set-body>");
        // a body given through a value element is a policy value: its expression is written as code
        xml.Should().Contain("<value>@(1 < 2 ? \"<a>\" : \"b\")</value>");
        // only a body that is one whole expression is code; one that merely starts with @( is a template
        xml.Should().Contain("<set-body template=\"liquid\">@(context.RequestId)<a /></set-body>");
        // a comparison in a liquid tag doesn't stop the body from being markup: the gateway reads &lt; in a tag
        xml.Should().Contain(
            "<set-body template=\"liquid\">{% if body.n &lt; 2 &amp;&amp; body.m %}<p>{{body.x}} &amp; co<![CDATA[a & b < c]]><!-- d & e < f --></p>{% endif %}</set-body>");
    }
}
