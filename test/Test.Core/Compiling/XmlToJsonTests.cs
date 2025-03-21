// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.ApiManagement.PolicyToolkit.Compiling;

[TestClass]
public class XmlToJsonTests
{
    [TestMethod]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context)
            {
                context.XmlToJson(new XmlToJsonConfig
                {
                    Kind = "javascript-friendly",
                    Apply = "always",
                });
            }
            public void Outbound(IOutboundContext context)
            {
                context.XmlToJson(new XmlToJsonConfig
                {
                    Kind = "direct",
                    Apply = "always",
                });
            }
            public void OnError(IOnErrorContext context)
            {
                context.XmlToJson(new XmlToJsonConfig
                {
                    Kind = "javascript-friendly",
                    Apply = "content-type-xml",
                });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <xml-to-json kind="javascript-friendly" apply="always" />
            </inbound>
            <outbound>
                <xml-to-json kind="direct" apply="always" />
            </outbound>
            <on-error>
                <xml-to-json kind="javascript-friendly" apply="content-type-xml" />
            </on-error>
        </policies>
        """,
        DisplayName = "Should compile xml-to-json policy in sections"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context)
            {
                context.XmlToJson(new XmlToJsonConfig
                {
                    Kind = GetKind(context.ExpressionContext),
                    Apply = "always",
                });
            }
            string GetKind(IExpressionContext context) => context.Variables["kind"].ToString();
        }
        """,
        """
        <policies>
            <inbound>
                <xml-to-json kind="@(context.Variables["kind"].ToString())" apply="always" />
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile xml-to-json policy with expression in kind attribute"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context)
            {
                context.XmlToJson(new XmlToJsonConfig
                {
                    Kind = "javascript-friendly",
                    Apply = GetApply(context.ExpressionContext),
                });
            }
            string GetApply(IExpressionContext context) => context.Variables["applyMode"].ToString();
        }
        """,
        """
        <policies>
            <inbound>
                <xml-to-json kind="javascript-friendly" apply="@(context.Variables["applyMode"].ToString())" />
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile xml-to-json policy with expression in apply attribute"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context)
            {
                context.XmlToJson(new XmlToJsonConfig
                {
                    Kind = "javascript-friendly",
                    Apply = "always",
                    ConsiderAcceptHeader = true
                });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <xml-to-json kind="javascript-friendly" apply="always" consider-accept-header="true" />
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile xml-to-json policy with consider-accept-header attribute"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context)
            {
                context.XmlToJson(new XmlToJsonConfig
                {
                    Kind = "javascript-friendly",
                    Apply = "always",
                    ConsiderAcceptHeader = GetConsiderAcceptHeader(context.ExpressionContext)
                });
            }
            bool GetConsiderAcceptHeader(IExpressionContext context) => (bool)context.Variables["shouldConsiderAcceptHeader"];
        }
        """,
        """
        <policies>
            <inbound>
                <xml-to-json kind="javascript-friendly" apply="always" consider-accept-header="@((bool)context.Variables["shouldConsiderAcceptHeader"])" />
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile xml-to-json policy with expression in consider-accept-header attribute"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context)
            {
                context.XmlToJson(new XmlToJsonConfig
                {
                    Kind = "javascript-friendly",
                    Apply = "always",
                    AlwaysArrayChildElements = true
                });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <xml-to-json kind="javascript-friendly" apply="always" always-array-child-elements="true" />
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile xml-to-json policy with always-array-child-elements attribute"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context)
            {
                context.XmlToJson(new XmlToJsonConfig
                {
                    Kind = "javascript-friendly",
                    Apply = "always",
                    AlwaysArrayChildElements = GetAlwaysArrayChildElements(context.ExpressionContext)
                });
            }
            bool GetAlwaysArrayChildElements(IExpressionContext context) => (bool)context.Variables["useArrayChildElements"];
        }
        """,
        """
        <policies>
            <inbound>
                <xml-to-json kind="javascript-friendly" apply="always" always-array-child-elements="@((bool)context.Variables["useArrayChildElements"])" />
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile xml-to-json policy with expression in always-array-child-elements attribute"
    )]
    public void ShouldCompileXmlToJsonPolicy(string code, string expectedXml)
    {
        code.CompileDocument().Should().BeSuccessful().And.DocumentEquivalentTo(expectedXml);
    }
}