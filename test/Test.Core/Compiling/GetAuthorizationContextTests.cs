using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using Azure.ApiManagement.PolicyToolkit.Authoring;
using Azure.ApiManagement.PolicyToolkit.Compiling;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Azure.Api.Management.PolicyToolkit.Tests.Compiling
{
    [TestClass]
    public class GetAuthorizationContextTests
    {
        [DataTestMethod]
        [DataRow(
            """
                using Azure.Api.Management.PolicyToolkit.Authoring;

                public class TestDocument : IDocument
                {
                    public void Inbound(IInboundContext context)
                    {
                        context.GetAuthorizationContext(new GetAuthorizationContextConfig
                        {
                            ProviderId = "provider-id",
                            AuthorizationId = "authorization-id",
                            ContextVariableName = "context-variable-name"
                        });
                    }
                }
            """,
            """
                <policies>
                    <inbound>
                        <get-authorization-context provider-id="provider-id" authorization-id="authorization-id" context-variable-name="context-variable-name" />
                    </inbound>
                </policies>
            """,
            DisplayName = "Should compile get-authorization-context policy with required properties"
        )]
        [DataRow(
            """
                using Azure.Api.Management.PolicyToolkit.Authoring;

                public class TestDocument : IDocument
                {
                    public void Inbound(IInboundContext context)
                    {
                        context.GetAuthorizationContext(new GetAuthorizationContextConfig
                        {
                            ProviderId = "provider-id",
                            AuthorizationId = "authorization-id",
                            ContextVariableName = "context-variable-name",
                            IdentityType = "jwt",
                            Identity = "jwt-token",
                            IgnoreError = true
                        });
                    }
                }
            """,
            """
                <policies>
                    <inbound>
                        <get-authorization-context provider-id="provider-id" authorization-id="authorization-id" context-variable-name="context-variable-name" identity-type="jwt" identity="jwt-token" ignore-error="true" />
                    </inbound>
                </policies>
            """,
            DisplayName = "Should compile get-authorization-context policy with all properties"
        )]
        [DataRow(
            """
                using Azure.Api.Management.PolicyToolkit.Authoring;

                public class TestDocument : IDocument
                {
                    public void Inbound(IInboundContext context)
                    {
                        context.GetAuthorizationContext(new GetAuthorizationContextConfig
                        {
                            ProviderId = GetProviderId(context.ExpressionContext),
                            AuthorizationId = GetAuthorizationId(context.ExpressionContext),
                            ContextVariableName = GetContextVariableName(context.ExpressionContext)
                        });
                    }

                    private string GetProviderId(IExpressionContext context) => $"@(context.Variables[""provider-id""])";
                    private string GetAuthorizationId(IExpressionContext context) => $"@(context.Variables[""authorization-id""])";
                    private string GetContextVariableName(IExpressionContext context) => $"@(context.Variables[""context-variable-name""])";
                }
            """,
            """
                <policies>
                    <inbound>
                        <get-authorization-context provider-id="@(context.Variables[""provider-id""])" authorization-id="@(context.Variables[""authorization-id""])" context-variable-name="@(context.Variables[""context-variable-name""])" />
                    </inbound>
                </policies>
            """,
            DisplayName = "Should compile get-authorization-context policy with policy expressions"
        )]
        public void ShouldCompileGetAuthorizationContextPolicy(string code, string expectedXml)
        {
            code.CompileDocument().Should().BeSuccessful().And.DocumentEquivalentTo(expectedXml);
        }
    }
}
