// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.ApiManagement.PolicyToolkit.Compiling;

[TestClass]
public class IncludeFragmentTests
{
    [TestMethod]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) { context.IncludeFragment("fragment-inbound"); }
            public void Outbound(IOutboundContext context) { context.IncludeFragment("fragment-outbound"); }
            public void Backend(IBackendContext context) { context.IncludeFragment("fragment-backend"); }
            public void OnError(IOnErrorContext context) { context.IncludeFragment("fragment-on-error"); }
        }
        """,
        """
        <policies>
            <inbound>
                <include-fragment fragment-id="fragment-inbound" />
            </inbound>
            <outbound>
                <include-fragment fragment-id="fragment-outbound" />
            </outbound>
            <backend>
                <include-fragment fragment-id="fragment-backend" />
            </backend>
            <on-error>
                <include-fragment fragment-id="fragment-on-error" />
            </on-error>
        </policies>
        """,
        DisplayName = "Should compile include-fragment policy in sections"
    )]
    public void ShouldCompileIncludeFragmentPolicy(string code, string expectedXml)
    {
        code.CompileDocument().Should().BeSuccessful().And.DocumentEquivalentTo(expectedXml);
    }
}