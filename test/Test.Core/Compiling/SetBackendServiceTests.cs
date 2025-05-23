// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling;

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
            public void Backend(IBackendContext context) {
                context.SetBackendService(new SetBackendServiceConfig { BackendId = "id" });
            }
            public void Outbound(IOutboundContext context) {
                context.SetBackendService(new SetBackendServiceConfig { BackendId = "id" });
            }
            public void OnError(IOnErrorContext context) {
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
            <outbound>
                <set-backend-service backend-id="id" />
            </outbound>
            <on-error>
                <set-backend-service backend-id="id" />
            </on-error>
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
                context.SetBackendService(new SetBackendServiceConfig { BackendId = "id" });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <set-backend-service backend-id="id" />
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile set backend service policy with backend-id"
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
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.SetBackendService(new SetBackendServiceConfig 
                { 
                    BackendId = "dapr",
                    DaprAppId = "app1"
                });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <set-backend-service backend-id="dapr" dapr-app-id="app1" />
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile set backend service policy with dapr app id"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.SetBackendService(new SetBackendServiceConfig 
                { 
                    BackendId = "dapr",
                    DaprAppId = Exp(context.ExpressionContext)
                });
            }
            public string Exp(ExpressionContext context)
                => context.User.Email.EndsWith("@contoso.example") ? "app1" : "app2";
        }
        """,
        """
        <policies>
            <inbound>
                <set-backend-service backend-id="dapr" dapr-app-id="@(context.User.Email.EndsWith("@contoso.example") ? "app1" : "app2")" />
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile set backend service policy with expression in dapr app id"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.SetBackendService(new SetBackendServiceConfig 
                { 
                    BackendId = "dapr",
                    DaprMethod = "method1"
                });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <set-backend-service backend-id="dapr" dapr-method="method1" />
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile set backend service policy with dapr method"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.SetBackendService(new SetBackendServiceConfig 
                { 
                    BackendId = "dapr",
                    DaprMethod = Exp(context.ExpressionContext)
                });
            }
            public string Exp(ExpressionContext context)
                => context.User.Email.EndsWith("@contoso.example") ? "method1" : "method2";
        }
        """,
        """
        <policies>
            <inbound>
                <set-backend-service backend-id="dapr" dapr-method="@(context.User.Email.EndsWith("@contoso.example") ? "method1" : "method2")" />
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile set backend service policy with expression in dapr method"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.SetBackendService(new SetBackendServiceConfig 
                { 
                    BackendId = "dapr",
                    DaprNamespace = "namespace1"
                });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <set-backend-service backend-id="dapr" dapr-namespace="namespace1" />
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile set backend service policy with dapr namespace"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.SetBackendService(new SetBackendServiceConfig 
                { 
                    BackendId = "dapr",
                    DaprNamespace = Exp(context.ExpressionContext)
                });
            }
            public string Exp(ExpressionContext context)
                => context.User.Email.EndsWith("@contoso.example") ? "namespace1" : "namespace2";
        }
        """,
        """
        <policies>
            <inbound>
                <set-backend-service backend-id="dapr" dapr-namespace="@(context.User.Email.EndsWith("@contoso.example") ? "namespace1" : "namespace2")" />
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile set backend service policy with expression in dapr namespace"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.SetBackendService(new SetBackendServiceConfig 
                { 
                    BackendId = "dapr",
                    DaprAppId = "app1",
                    DaprMethod = "method1",
                    DaprNamespace = "namespace1"
                });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <set-backend-service backend-id="dapr" dapr-app-id="app1" dapr-method="method1" dapr-namespace="namespace1" />
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile set backend service policy with all dapr properties"
    )]
    public void ShouldCompileSetBackendServicePolicy(string code, string expectedXml)
    {
        code.CompileDocument().Should().BeSuccessful().And.DocumentEquivalentTo(expectedXml);
    }
}