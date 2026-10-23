// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling;

[TestClass]
public class PublishToDaprTests
{
    [TestMethod]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) 
            {
                context.PublishToDapr(new PublishToDaprConfig
                {
                    Topic = "my-topic",
                    Content = "my-content"
                });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <publish-to-dapr topic="my-topic">my-content</publish-to-dapr>
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile publish-to-dapr policy with required properties in inbound"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) 
            {
                context.PublishToDapr(new PublishToDaprConfig
                {
                    Topic = GetTopic(context.ExpressionContext),
                    Content = "my-content"
                });
            }
            
            string GetTopic(IExpressionContext context) => $"topic-{context.Api.Id}";
        }
        """,
        """
        <policies>
            <inbound>
                <publish-to-dapr topic="@($"topic-{context.Api.Id}")">my-content</publish-to-dapr>
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile publish-to-dapr policy with expression in topic"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) 
            {
                context.PublishToDapr(new PublishToDaprConfig
                {
                    Topic = "my-topic",
                    Content = GetContent(context.ExpressionContext)
                });
            }
            
            string GetContent(IExpressionContext context) => context.Request.Body.As<string>();
        }
        """,
        """
        <policies>
            <inbound>
                <publish-to-dapr topic="my-topic">@(context.Request.Body.As<string>())</publish-to-dapr>
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile publish-to-dapr policy with expression in content"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) 
            {
                context.PublishToDapr(new PublishToDaprConfig
                {
                    Topic = "my-topic",
                    Content = "my-content",
                    PubSubName = "my-pubsub"
                });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <publish-to-dapr topic="my-topic" pubsub-name="my-pubsub">my-content</publish-to-dapr>
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile publish-to-dapr policy with pubsub-name"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) 
            {
                context.PublishToDapr(new PublishToDaprConfig
                {
                    Topic = "my-topic",
                    Content = "my-content",
                    PubSubName = GetPubSubName(context.ExpressionContext)
                });
            }
            
            string GetPubSubName(IExpressionContext context) => $"pubsub-{context.Api.Name}";
        }
        """,
        """
        <policies>
            <inbound>
                <publish-to-dapr topic="my-topic" pubsub-name="@($"pubsub-{context.Api.Name}")">my-content</publish-to-dapr>
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile publish-to-dapr policy with expression in pubsub-name"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) 
            {
                context.PublishToDapr(new PublishToDaprConfig
                {
                    Topic = "my-topic",
                    Content = "my-content",
                    IgnoreError = true
                });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <publish-to-dapr topic="my-topic" ignore-error="true">my-content</publish-to-dapr>
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile publish-to-dapr policy with ignore-error"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) 
            {
                context.PublishToDapr(new PublishToDaprConfig
                {
                    Topic = "my-topic",
                    Content = "my-content",
                    ResponseVariableName = "daprResponse"
                });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <publish-to-dapr topic="my-topic" response-variable-name="daprResponse">my-content</publish-to-dapr>
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile publish-to-dapr policy with response-variable-name"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) 
            {
                context.PublishToDapr(new PublishToDaprConfig
                {
                    Topic = "my-topic",
                    Content = "my-content",
                    Timeout = 30
                });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <publish-to-dapr topic="my-topic" timeout="30">my-content</publish-to-dapr>
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile publish-to-dapr policy with timeout"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) 
            {
                context.PublishToDapr(new PublishToDaprConfig
                {
                    Topic = "my-topic",
                    Content = "my-content",
                    Template = "liquid"
                });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <publish-to-dapr topic="my-topic" template="liquid">my-content</publish-to-dapr>
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile publish-to-dapr policy with template"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) 
            {
                context.PublishToDapr(new PublishToDaprConfig
                {
                    Topic = "my-topic",
                    Content = "my-content",
                    ContentType = "application/json"
                });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <publish-to-dapr topic="my-topic" content-type="application/json">my-content</publish-to-dapr>
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile publish-to-dapr policy with content-type"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) 
            {
                context.PublishToDapr(new PublishToDaprConfig
                {
                    Topic = "my-topic",
                    Content = "my-content",
                    PubSubName = "my-pubsub",
                    IgnoreError = true,
                    ResponseVariableName = "daprResponse",
                    Timeout = 30,
                    Template = "liquid",
                    ContentType = "application/json"
                });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <publish-to-dapr topic="my-topic" pubsub-name="my-pubsub" ignore-error="true" response-variable-name="daprResponse" timeout="30" template="liquid" content-type="application/json">my-content</publish-to-dapr>
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile publish-to-dapr policy with all properties"
    )]
    public void ShouldCompilePublishToDaprPolicy(string code, string expectedXml)
    {
        code.CompileDocument().Should().BeSuccessful().And.DocumentEquivalentTo(expectedXml);
    }

    [TestMethod]
    [DataRow(1)]
    [DataRow(240)]
    public void ShouldAcceptTimeoutAtRangeBoundaries(int timeout)
    {
        var result = $$"""
                       [Document]
                       public class PolicyDocument : IDocument
                       {
                           public void Inbound(IInboundContext context)
                           {
                               context.PublishToDapr(new PublishToDaprConfig
                               {
                                   Topic = "my-topic",
                                   Content = "my-content",
                                   Timeout = {{timeout}}
                               });
                           }
                       }
                       """.CompileDocument();

        result.Should().BeSuccessful();
    }

    [TestMethod]
    [DataRow("0")]
    [DataRow("241")]
    [DataRow("5000")]
    [DataRow("-1")]
    [DataRow("Seconds")]
    public void ShouldRejectTimeoutOutsideOfRange(string timeout)
    {
        var result = $$"""
                       [Document]
                       public class PolicyDocument : IDocument
                       {
                           private const int Seconds = 300;

                           public void Inbound(IInboundContext context)
                           {
                               context.PublishToDapr(new PublishToDaprConfig
                               {
                                   Topic = "my-topic",
                                   Content = "my-content",
                                   Timeout = {{timeout}}
                               });
                           }
                       }
                       """.CompileDocument();

        result.Errors.Should().ContainSingle(error => error.Id == "APIM2020");
    }

    [TestMethod]
    [DataRow(typeof(IOutboundContext))]
    [DataRow(typeof(IOnErrorContext))]
    [DataRow(typeof(IBackendContext))]
    public void ShouldOnlyBeAvailableInInbound(Type section)
    {
        // API Management only accepts publish-to-dapr in the inbound section.
        section.GetMethod(nameof(IInboundContext.PublishToDapr)).Should().BeNull();
    }
}
