// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling;

// Policies compile in the sections API Management accepts them in when a policy is saved (verified on Consumption
// and Basic v2 gateways, eastus, 2026-09-22).
[TestClass]
public class SectionAlignmentTests
{
    [TestMethod]
    [DataRow("Outbound", "IOutboundContext", "context.LlmContentSafety(new LlmContentSafetyConfig { BackendId = \"safety\" });",
        "outbound", "llm-content-safety", DisplayName = "llm-content-safety in outbound")]
    [DataRow("Inbound", "IInboundContext", "context.RedirectContentUrls();",
        "inbound", "redirect-content-urls", DisplayName = "redirect-content-urls in inbound")]
    public void ShouldCompilePolicyInSection(string method, string contextType, string statement, string section,
        string policy)
    {
        var result =
            $$"""
              [Document]
              public class PolicyDocument : IDocument
              {
                  public void {{method}}({{contextType}} context)
                  {
                      {{statement}}
                  }
              }
              """.CompileDocument();

        result.Should().BeSuccessful();
        result.Document.Element(section)!.Element(policy).Should().NotBeNull();
    }
}
