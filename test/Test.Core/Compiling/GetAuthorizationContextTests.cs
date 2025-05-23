// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling;

[TestClass]
public class GetAuthorizationContextTests
{
    [DataTestMethod]
    [DataRow(
        """
        [Document]
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
            public void Backend(IBackendContext context)
            {
                context.GetAuthorizationContext(new GetAuthorizationContextConfig
                {
                    ProviderId = "provider-id",
                    AuthorizationId = "authorization-id",
                    ContextVariableName = "context-variable-name"
                });
            }
            public void Outbound(IOutboundContext context)
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
            <backend>
                <get-authorization-context provider-id="provider-id" authorization-id="authorization-id" context-variable-name="context-variable-name" />
            </backend>
            <outbound>
                <get-authorization-context provider-id="provider-id" authorization-id="authorization-id" context-variable-name="context-variable-name" />
            </outbound>
        </policies>
        """,
        DisplayName = "Should compile get-authorization-context policy with required properties in sections"
    )]
    [DataRow(
        """
        [Document]
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
        [Document]
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

            private string GetProviderId(IExpressionContext context) => context.Variables["provider-id"];
            private string GetAuthorizationId(IExpressionContext context) => context.Variables["authorization-id"];
            private string GetContextVariableName(IExpressionContext context) => context.Variables["context-variable-name"];
        }
        """,
        """
        <policies>
            <inbound>
                <get-authorization-context provider-id="@(context.Variables["provider-id"])" authorization-id="@(context.Variables["authorization-id"])" context-variable-name="@(context.Variables["context-variable-name"])" />
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile get-authorization-context policy with policy expressions"
    )]
    [DataRow(
        """
        [Document]
        public class TestDocument : IDocument
        {
            public void Inbound(IInboundContext context)
            {
                context.GetAuthorizationContext(new GetAuthorizationContextConfig
                {
                    ProviderId = GetProviderId(context.ExpressionContext),
                    AuthorizationId = GetAuthorizationId(context.ExpressionContext),
                    ContextVariableName = GetContextVariableName(context.ExpressionContext),
                    IdentityType = GetIdentityType(context.ExpressionContext),
                    Identity = GetIdentity(context.ExpressionContext),
                    IgnoreError = GetIgnoreError(context.ExpressionContext)
                });
            }

            private string GetProviderId(IExpressionContext context) => context.Variables["provider-id"];
            private string GetAuthorizationId(IExpressionContext context) => context.Variables["authorization-id"];
            private string GetContextVariableName(IExpressionContext context) => context.Variables["context-variable-name"];
            private string GetIdentityType(IExpressionContext context) => context.Variables["identity-type"];
            private string GetIdentity(IExpressionContext context) => context.Variables["identity"];
            private bool GetIgnoreError(IExpressionContext context) => (bool)context.Variables["ignore-error"];
        }
        """,
        """
        <policies>
            <inbound>
                <get-authorization-context provider-id="@(context.Variables["provider-id"])" authorization-id="@(context.Variables["authorization-id"])" context-variable-name="@(context.Variables["context-variable-name"])" identity-type="@(context.Variables["identity-type"])" identity="@(context.Variables["identity"])" ignore-error="@((bool)context.Variables["ignore-error"])" />
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile get-authorization-context policy with policy expressions in all properties"
    )]
    public void ShouldCompileGetAuthorizationContextPolicy(string code, string expectedXml)
    {
        code.CompileDocument().Should().BeSuccessful().And.DocumentEquivalentTo(expectedXml);
    }
}