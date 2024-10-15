﻿namespace Mielek.Azure.ApiManagement.PolicyToolkit.Compilation;

[TestClass]
public class SetBackendServiceTests
{
    [TestMethod]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.SetBackendService(new SetBackendServiceConfig { BackendId = "id" });
            }
            public void Backend(IOutboundContext context) {
                context.SetBackendService(new SetBackendServiceConfig { BackendId = "id" });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <set-backend-service backend-id="id" />
            </inbound>
            <backend>
                <set-backend-service backend-id="id" />
            </backend>
        </policies>
        """,
        DisplayName = "Should compile set backend service policy in sections"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.SetBackendService(new SetBackendServiceConfig { BaseUrl = "http://contoso.example/api" });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <set-backend-service base-url="http://contoso.example/api" />
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile set backend service policy with base url"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.SetBackendService(new SetBackendServiceConfig { BaseUrl = "http://contoso.example/api" });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <set-backend-service base-url="http://contoso.example/api" />
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile set backend service policy with base url"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.SetBackendService(new SetBackendServiceConfig { BaseUrl = Exp(context.ExpressionContext) });
            }
            public string Exp(ExpressionContext context)
                => context.User.Email.EndsWith("@contoso.example") ? "http://contoso.example/api-a" : "http://contoso.example/api-b";
        }
        """,
        """
        <policies>
            <inbound>
                <set-backend-service base-url="@(context.User.Email.EndsWith("@contoso.example") ? "http://contoso.example/api-a" : "http://contoso.example/api-b")" />
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile set backend service policy with expression in base url"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.SetBackendService(new SetBackendServiceConfig { BackendId = Exp(context.ExpressionContext) });
            }
            public string Exp(ExpressionContext context)
                => context.User.Email.EndsWith("@contoso.example") ? "backend-a" : "backend-b";
        }
        """,
        """
        <policies>
            <inbound>
                <set-backend-service backend-id="@(context.User.Email.EndsWith("@contoso.example") ? "backend-a" : "backend-b")" />
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile set backend service policy with expression in backend id"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.SetBackendService(new SetBackendServiceConfig 
                { 
                    BackendId = "id",
                    SfResolveCondition = true
                });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <set-backend-service backend-id="id" sf-resolve-condition="true" />
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile set backend service policy with sf resolve condition"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.SetBackendService(new SetBackendServiceConfig 
                { 
                    BackendId = "id",
                    SfResolveCondition = Exp(context.ExpressionContext)
                });
            }
            public bool Exp(ExpressionContext context)
                => context.User.Email.EndsWith("@contoso.example") ? true : false;
        }
        """,
        """
        <policies>
            <inbound>
                <set-backend-service backend-id="id" sf-resolve-condition="@(context.User.Email.EndsWith("@contoso.example") ? true : false)" />
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile set backend service policy with expression in sf resolve condition"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.SetBackendService(new SetBackendServiceConfig 
                { 
                    BackendId = "id",
                    SfServiceInstanceName = "name"
                });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <set-backend-service backend-id="id" sf-service-instance-name="name" />
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile set backend service policy with sf service instance name"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.SetBackendService(new SetBackendServiceConfig 
                { 
                    BackendId = "id",
                    SfServiceInstanceName = Exp(context.ExpressionContext)
                });
            }
            public bool Exp(ExpressionContext context)
                => context.User.Email.EndsWith("@contoso.example") ? "a" : "b";
        }
        """,
        """
        <policies>
            <inbound>
                <set-backend-service backend-id="id" sf-service-instance-name="@(context.User.Email.EndsWith("@contoso.example") ? "a" : "b")" />
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile set backend service policy with expression in sf service instance name"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.SetBackendService(new SetBackendServiceConfig 
                { 
                    BackendId = "id",
                    SfPartitionKey = "key"
                });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <set-backend-service backend-id="id" sf-partition-key="key" />
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile set backend service policy with sf partition key"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.SetBackendService(new SetBackendServiceConfig 
                { 
                    BackendId = "id",
                    SfPartitionKey = Exp(context.ExpressionContext)
                });
            }
            public bool Exp(ExpressionContext context)
                => context.User.Email.EndsWith("@contoso.example") ? "a" : "b";
        }
        """,
        """
        <policies>
            <inbound>
                <set-backend-service backend-id="id" sf-partition-key="@(context.User.Email.EndsWith("@contoso.example") ? "a" : "b")" />
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile set backend service policy with expression in sf partition key"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.SetBackendService(new SetBackendServiceConfig 
                { 
                    BackendId = "id",
                    SfListenerName = "name"
                });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <set-backend-service backend-id="id" sf-listener-name="name" />
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile set backend service policy with sf listener name"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.SetBackendService(new SetBackendServiceConfig 
                { 
                    BackendId = "id",
                    SfListenerName = Exp(context.ExpressionContext)
                });
            }
            public bool Exp(ExpressionContext context)
                => context.User.Email.EndsWith("@contoso.example") ? "a" : "b";
        }
        """,
        """
        <policies>
            <inbound>
                <set-backend-service backend-id="id" sf-listener-name="@(context.User.Email.EndsWith("@contoso.example") ? "a" : "b")" />
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile set backend service policy with expression in sf listener name"
    )]
    public void ShouldCompileSetBackendServicePolicy(string code, string expectedXml)
    {
        code.CompileDocument().Should().BeSuccessful().And.DocumentEquivalentTo(expectedXml);
    }
}