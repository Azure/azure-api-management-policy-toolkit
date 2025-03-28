// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.ApiManagement.PolicyToolkit.Compiling;

[TestClass]
public class PublishToDarpTests
{
    [TestMethod]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) 
            {
                context.PublishToDarp(new PublishToDarpConfig
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
                <publish-to-darp topic="my-topic">my-content</publish-to-darp>
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile publish-to-darp policy with required properties"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) 
            {
                context.PublishToDarp(new PublishToDarpConfig
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
                <publish-to-darp topic="@($"topic-{context.Api.Id}")">my-content</publish-to-darp>
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile publish-to-darp policy with expression in topic"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) 
            {
                context.PublishToDarp(new PublishToDarpConfig
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
                <publish-to-darp topic="my-topic">@(context.Request.Body.As<string>())</publish-to-darp>
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile publish-to-darp policy with expression in content"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) 
            {
                context.PublishToDarp(new PublishToDarpConfig
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
                <publish-to-darp topic="my-topic" pub-sub-name="my-pubsub">my-content</publish-to-darp>
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile publish-to-darp policy with pub-sub-name"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) 
            {
                context.PublishToDarp(new PublishToDarpConfig
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
                <publish-to-darp topic="my-topic" pub-sub-name="@($"pubsub-{context.Api.Name}")">my-content</publish-to-darp>
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile publish-to-darp policy with expression in pub-sub-name"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) 
            {
                context.PublishToDarp(new PublishToDarpConfig
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
                <publish-to-darp topic="my-topic" ignore-error="true">my-content</publish-to-darp>
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile publish-to-darp policy with ignore-error"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) 
            {
                context.PublishToDarp(new PublishToDarpConfig
                {
                    Topic = "my-topic",
                    Content = "my-content",
                    ResponseVariableName = "darpResponse"
                });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <publish-to-darp topic="my-topic" response-variable-name="darpResponse">my-content</publish-to-darp>
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile publish-to-darp policy with response-variable-name"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) 
            {
                context.PublishToDarp(new PublishToDarpConfig
                {
                    Topic = "my-topic",
                    Content = "my-content",
                    Timeout = 5000
                });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <publish-to-darp topic="my-topic" timeout="5000">my-content</publish-to-darp>
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile publish-to-darp policy with timeout"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) 
            {
                context.PublishToDarp(new PublishToDarpConfig
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
                <publish-to-darp topic="my-topic" template="liquid">my-content</publish-to-darp>
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile publish-to-darp policy with template"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) 
            {
                context.PublishToDarp(new PublishToDarpConfig
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
                <publish-to-darp topic="my-topic" content-type="application/json">my-content</publish-to-darp>
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile publish-to-darp policy with content-type"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) 
            {
                context.PublishToDarp(new PublishToDarpConfig
                {
                    Topic = "my-topic",
                    Content = "my-content",
                    PubSubName = "my-pubsub",
                    IgnoreError = true,
                    ResponseVariableName = "darpResponse",
                    Timeout = 5000,
                    Template = "liquid",
                    ContentType = "application/json"
                });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <publish-to-darp topic="my-topic" pub-sub-name="my-pubsub" ignore-error="true" response-variable-name="darpResponse" timeout="5000" template="liquid" content-type="application/json">my-content</publish-to-darp>
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile publish-to-darp policy with all properties"
    )]
    public void ShouldCompilePublishToDarpPolicy(string code, string expectedXml)
    {
        code.CompileDocument().Should().BeSuccessful().And.DocumentEquivalentTo(expectedXml);
    }
}