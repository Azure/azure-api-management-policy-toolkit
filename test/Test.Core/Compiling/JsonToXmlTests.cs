// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling;

[TestClass]
public class JsonToXmlTests
{
    [TestMethod]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.JsonToXml(new JsonToXmlConfig { Apply = "always" });
            }
            public void Backend(IBackendContext context) {
                context.JsonToXml(new JsonToXmlConfig { Apply = "always" });
            }
            public void Outbound(IOutboundContext context) {
                context.JsonToXml(new JsonToXmlConfig { Apply = "always" });
            }
            public void OnError(IOnErrorContext context) {
                context.JsonToXml(new JsonToXmlConfig { Apply = "always" });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <json-to-xml apply="always" />
            </inbound>
            <backend>
                <json-to-xml apply="always" />
            </backend>
            <outbound>
                <json-to-xml apply="always" />
            </outbound>
            <on-error>
                <json-to-xml apply="always" />
            </on-error>
        </policies>
        """,
        DisplayName = "Should compile json-to-xml policy in sections"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.JsonToXml(new JsonToXmlConfig { Apply = ApplyExp(context.ExpressionContext) });
            }
            public string ApplyExp(IExpressionContext context) 
                => context.Api.Name.EndsWith("-xml") ? "always" : "content-type-json";
        }
        """,
        """
        <policies>
            <inbound>
                <json-to-xml apply="@(context.Api.Name.EndsWith("-xml") ? "always" : "content-type-json")" />
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile json-to-xml policy with expression in apply"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.JsonToXml(new JsonToXmlConfig
                { 
                    Apply = "always",
                    ConsiderAcceptHeader = false,
                });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <json-to-xml apply="always" consider-accept-header="false" />
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile json-to-xml policy with consider-accept-header"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.JsonToXml(new JsonToXmlConfig
                { 
                    Apply = "always",
                    ConsiderAcceptHeader = ConsiderAcceptHeaderExp(context.ExpressionContext),
                });
            }
            public string ConsiderAcceptHeaderExp(IExpressionContext context) 
                => context.Api.Name.EndsWith("-xml");
        }
        """,
        """
        <policies>
            <inbound>
                <json-to-xml apply="always" consider-accept-header="@(context.Api.Name.EndsWith("-xml"))" />
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile json-to-xml policy with expression in consider-accept-header"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.JsonToXml(new JsonToXmlConfig
                { 
                    Apply = "always",
                    ParseDate = false,
                });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <json-to-xml apply="always" parse-date="false" />
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile json-to-xml policy with parse-date"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.JsonToXml(new JsonToXmlConfig
                { 
                    Apply = "always",
                    NamespaceSeparator = ':',
                });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <json-to-xml apply="always" namespace-separator=":" />
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile json-to-xml policy with namespace-separator"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.JsonToXml(new JsonToXmlConfig
                { 
                    Apply = "always",
                    NamespaceSeparator = Exp(context.ExpressionContext),
                });
            }
            public char Exp(IExpressionContext context) 
                => context.Api.Name.EndsWith("-xml") ? ':' : '-';
        }
        """,
        """
        <policies>
            <inbound>
                <json-to-xml apply="always" namespace-separator="@(context.Api.Name.EndsWith("-xml") ? ':' : '-')" />
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile json-to-xml policy with expression in namespace-separator"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.JsonToXml(new JsonToXmlConfig
                { 
                    Apply = "always",
                    NamespacePrefix = "prefix",
                });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <json-to-xml apply="always" namespace-prefix="prefix" />
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile json-to-xml policy with namespace-prefix"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.JsonToXml(new JsonToXmlConfig
                { 
                    Apply = "always",
                    NamespacePrefix = Exp(context.ExpressionContext),
                });
            }
            public char Exp(IExpressionContext context) 
                => context.Api.Name.EndsWith("-xml") ? "a" : "b";
        }
        """,
        """
        <policies>
            <inbound>
                <json-to-xml apply="always" namespace-prefix="@(context.Api.Name.EndsWith("-xml") ? "a" : "b")" />
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile json-to-xml policy with expression in namespace-prefix"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.JsonToXml(new JsonToXmlConfig
                { 
                    Apply = "always",
                    AttributeBlockName = "#attrs",
                });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <json-to-xml apply="always" attribute-block-name="#attrs" />
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile json-to-xml policy with attribute-block-name"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.JsonToXml(new JsonToXmlConfig
                { 
                    Apply = "always",
                    AttributeBlockName = Exp(context.ExpressionContext),
                });
            }
            public char Exp(IExpressionContext context) 
                => context.Api.Name.EndsWith("-xml") ? "#attrs1" : "#attrs2";
        }
        """,
        """
        <policies>
            <inbound>
                <json-to-xml apply="always" attribute-block-name="@(context.Api.Name.EndsWith("-xml") ? "#attrs1" : "#attrs2")" />
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile json-to-xml policy with expression in attribute-block-name"
    )]
    public void ShouldCompileJsonToXmlPolicy(string code, string expectedXml)
    {
        code.CompileDocument().Should().BeSuccessful().And.DocumentEquivalentTo(expectedXml);
    }
}