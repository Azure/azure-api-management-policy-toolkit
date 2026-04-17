// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling;

[TestClass]
public class PublishEventTests
{
    [TestMethod]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Outbound(IOutboundContext context) 
            {
                context.PublishEvent(new PublishEventConfig
                {
                    Subscriptions = [
                        new GraphqlSubscriptionConfig { Id = "onUserCreated" }
                    ]
                });
            }
        }
        """,
        """
        <policies>
            <outbound>
                <publish-event>
                    <targets>
                        <graphql-subscriptions id="onUserCreated" />
                    </targets>
                </publish-event>
            </outbound>
        </policies>
        """,
        DisplayName = "Should compile publish-event policy with single subscription"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Outbound(IOutboundContext context) 
            {
                context.PublishEvent(new PublishEventConfig
                {
                    Subscriptions = [
                        new GraphqlSubscriptionConfig { Id = "onUserCreated" },
                        new GraphqlSubscriptionConfig { Id = "onUserUpdated" }
                    ]
                });
            }
        }
        """,
        """
        <policies>
            <outbound>
                <publish-event>
                    <targets>
                        <graphql-subscriptions id="onUserCreated" />
                        <graphql-subscriptions id="onUserUpdated" />
                    </targets>
                </publish-event>
            </outbound>
        </policies>
        """,
        DisplayName = "Should compile publish-event policy with multiple subscriptions"
    )]
    public void ShouldCompilePublishEventPolicy(string code, string expectedXml)
    {
        code.CompileDocument().Should().BeSuccessful().And.DocumentEquivalentTo(expectedXml);
    }
}
