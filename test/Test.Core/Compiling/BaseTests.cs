// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling;

[TestClass]
public class BaseTests
{
    [TestMethod]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) { context.Base(); }
            public void Outbound(IOutboundContext context) { context.Base(); }
            public void Backend(IBackendContext context) { context.Base(); }
            public void OnError(IOnErrorContext context) { context.Base(); }
        }
        """,
        """
        <policies>
            <inbound>
                <base />
            </inbound>
            <outbound>
                <base />
            </outbound>
            <backend>
                <base />
            </backend>
            <on-error>
                <base />
            </on-error>
        </policies>
        """,
        DisplayName = "Should compile base policy in sections"
    )]
    public void ShouldCompileBasePolicy(string code, string expectedXml)
    {
        code.CompileDocument().Should().BeSuccessful().And.DocumentEquivalentTo(expectedXml);
    }
}