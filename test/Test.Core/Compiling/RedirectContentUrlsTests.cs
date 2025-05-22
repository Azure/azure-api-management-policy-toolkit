// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling;

[TestClass]
public class RedirectContentUrlsTests
{
    [TestMethod]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) { context.RedirectContentUrls(); }
            public void Outbound(IOutboundContext context) { context.RedirectContentUrls(); }
        }
        """,
        """
        <policies>
            <inbound>
                <redirect-content-urls />
            </inbound>
            <outbound>
                <redirect-content-urls />
            </outbound>
        </policies>
        """,
        DisplayName = "Should compile redirect content urls policy in sections"
    )]
    public void ShouldCompileRedirectContentUrlsPolicy(string code, string expectedXml)
    {
        code.CompileDocument().Should().BeSuccessful().And.DocumentEquivalentTo(expectedXml);
    }
}