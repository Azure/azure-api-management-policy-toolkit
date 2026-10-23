// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling;

// API Management rejects a backend section with more than one top-level policy when it's saved, counting <base />
// but not policies nested in choose or retry (verified on a Consumption gateway, eastus, 2026-09-22).
[TestClass]
public class BackendSectionTests
{
    private static IDocumentCompilationResult Compile(string body) =>
        $$"""
          [Document]
          public class PolicyDocument : IDocument
          {
              public void Backend(IBackendContext context)
              {
                  {{body}}
              }

              bool HasA(IExpressionContext context) => context.Variables.ContainsKey("a");
          }
          """.CompileDocument();

    [TestMethod]
    [DataRow("context.Base();", DisplayName = "base")]
    [DataRow("context.ForwardRequest();", DisplayName = "forward-request")]
    [DataRow("""
             if (HasA(context.ExpressionContext)) { context.ForwardRequest(); }
             else { context.SetVariable("b", "1"); context.ForwardRequest(); }
             """, DisplayName = "choose with several policies in a branch")]
    [DataRow("""context.Retry(new RetryConfig { Condition = "@(false)", Count = 1, Interval = 1 }, () => { context.SetVariable("a", "1"); context.ForwardRequest(); });""",
        DisplayName = "retry wrapping several policies")]
    public void OnePolicy_Compiles(string body) => Compile(body).Should().BeSuccessful();

    [TestMethod]
    [DataRow("context.Base(); context.ForwardRequest();", DisplayName = "base and forward-request")]
    [DataRow("""context.SetVariable("a", "1"); context.SetVariable("b", "2");""", DisplayName = "two policies")]
    public void SeveralPolicies_AreAnError(string body) =>
        Compile(body).Errors.Should().ContainSingle(error => error.Id == "APIM9990");
}
