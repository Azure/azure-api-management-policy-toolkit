// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling;

[TestClass]
public class MockResponseTests
{
    [TestMethod]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context)
            {
                context.MockResponse();
            }
            public void Outbound(IOutboundContext context) 
            { 
                context.MockResponse();
            }
            public void OnError(IOnErrorContext context) 
            { 
                context.MockResponse();
            }
        }
        """,
        """
        <policies>
            <inbound>
                <mock-response />
            </inbound>
            <outbound>
                <mock-response />
            </outbound>
            <on-error>
                <mock-response />
            </on-error>
        </policies>
        """,
        DisplayName = "Should compile mock-response policy in sections"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context)
            {
                context.MockResponse(new MockResponseConfig
                {
                    StatusCode = 200,
                });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <mock-response status-code="200" />
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile mock-response policy with status code"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context)
            {
                context.MockResponse(new MockResponseConfig
                {
                    ContentType = "application/json",
                });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <mock-response content-type="application/json" />
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile mock-response policy with content type"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context)
            {
                context.MockResponse(new MockResponseConfig
                {
                    Index = 1,
                });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <mock-response index="1" />
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile mock-response policy with index"
    )]
    public void ShouldCompileMockResponsePolicy(string code, string expectedXml)
    {
        code.CompileDocument().Should().BeSuccessful().And.DocumentEquivalentTo(expectedXml);
    }
}