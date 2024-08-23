﻿namespace Mielek.Azure.ApiManagement.PolicyToolkit.Compilation;

[TestClass]
public class SetBodyTests
{
    [TestMethod]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IBackendContext context) {
                context.SetBody("inbound");
            }
            public void Outbound(IBackendContext context) {
                context.SetBody("outbound");
            }
        }
        """,
        """
        <policies>
            <inbound>
                <set-body>inbound</set-body>
            </inbound>
            <outbound>
                <set-body>outbound</set-body>
            </outbound>
        </policies>
        """,
        DisplayName = "Should compile set body policy in sections"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IBackendContext context) {
                context.SetBody(Exp(context.ExpressionContext));
            }
            public void Outbound(IBackendContext context) {
                context.SetBody(Exp(context.ExpressionContext));
            }
            
            string Exp(IExpressionContext context)
            {
                return context.RequestId.ToString();
            }
        }
        """,
        """
        <policies>
            <inbound>
                <set-body>@{return context.RequestId.ToString();}</set-body>
            </inbound>
            <outbound>
                <set-body>@{return context.RequestId.ToString();}</set-body>
            </outbound>
        </policies>
        """,
        DisplayName = "Should compile set body policy with expressions"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IBackendContext context) {
                context.SetBody("inbound", new SetBodyConfig {
                    Template = "liquid",
                });
            }
            public void Outbound(IBackendContext context) {
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
            public void Inbound(IBackendContext context) {
                context.SetBody("inbound", new SetBodyConfig {
                    XsiNil = "blank",
                });
            }
            public void Outbound(IBackendContext context) {
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
            public void Inbound(IBackendContext context) {
                context.SetBody("inbound", new SetBodyConfig {
                    ParseDate = true,
                });
            }
            public void Outbound(IBackendContext context) {
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
    public void ShouldCompileForwardRequestPolicy(string code, string expectedXml)
    {
        code.CompileDocument().Should().BeSuccessful().And.DocumentEquivalentTo(expectedXml);
    }
}