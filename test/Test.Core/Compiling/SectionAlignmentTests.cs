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

    [TestMethod]
    [DataRow("public void Inbound(IInboundContext context) { context.ForwardRequest(); }", "ForwardRequest")]
    [DataRow("public void Inbound(IInboundContext context) { context.WithId(\"x\").CacheStore(10, null); context.Base(); }", "CacheStore")]
    [DataRow("public void Backend(IBackendContext context) { context.EmitMetric(new EmitMetricConfig { Name = \"n\", Dimensions = [] }); }", "EmitMetric")]
    public void ShouldReportPolicyUsedInSectionThatDoesNotAllowIt(string section, string method)
    {
        var result =
            $$"""
              [Document]
              public class PolicyDocument : IDocument
              {
                  {{section}}
              }
              """.CompileDocument();

        result.Errors.Should().ContainSingle(error =>
            error.Id == "APIM2031" && error.GetMessage(null).Contains(method));
        // the id of the rejected policy doesn't move on to the next one
        result.Document.Descendants().Where(element => element.Attribute("id") is not null).Should().BeEmpty();
    }

    [TestMethod]
    public void ShouldReportBaseInFragment()
    {
        var result =
            """
            [Document]
            public class Fragment : IFragment
            {
                public void Fragment(IFragmentContext context) { context.Base(); }
            }
            """.CompileDocument();

        result.Errors.Should().ContainSingle(error => error.Id == "APIM2031");
    }

    [TestMethod]
    public void ShouldOnlyTreatTheDocumentsOwnSectionContextMethodsAsSections()
    {
        var code =
            """
            using Aliased = Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring.IBackendContext;

            public interface IOnErrorContext { }

            [Document]
            public class PolicyDocument : IDocument
            {
                public void Inbound(IInboundContext context)
                {
                    context.Base();
                }

                public void Backend(Aliased context)
                {
                    context.Base();
                }

                public void OnError(Test.IOnErrorContext notTheAuthoringContext)
                {
                }

                public void Inbound(IInboundContext context, int extra)
                {
                    context.SetHeader("X-Overload", "1");
                }

                public void Outbound(int notASectionContext)
                {
                }

                public class Nested
                {
                    public void Outbound(IOutboundContext context)
                    {
                        context.SetHeader("X-Nested", "1");
                    }
                }
            }
            """;

        code.CompileDocument().Should().BeSuccessful().And.DocumentEquivalentTo(
            """
            <policies>
                <inbound>
                    <base />
                </inbound>
                <backend>
                    <base />
                </backend>
            </policies>
            """);
    }

    [TestMethod]
    public void ShouldReportPolicyAllowedOncePerSectionUsedTwice()
    {
        var result = CompilerTestInitialize.InboundDocument(
            """
            if (IsGet(context.ExpressionContext))
            {
                context.RateLimit(new RateLimitConfig { Calls = 1, RenewalPeriod = 60 });
            }
            else
            {
                context.RateLimit(new RateLimitConfig { Calls = 5, RenewalPeriod = 60 });
            }
            context.RateLimitByKey(new RateLimitByKeyConfig { Calls = 1, RenewalPeriod = 60, CounterKey = "a" });
            context.RateLimitByKey(new RateLimitByKeyConfig { Calls = 1, RenewalPeriod = 60, CounterKey = "b" });
            """.Replace("IsGet(context.ExpressionContext)", "true")).CompileDocument();

        result.Errors.Should().ContainSingle(error =>
            error.Id == "APIM2032" && error.GetMessage(null).Contains("'rate-limit'"));
    }
}
