// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling;

// Attributes the compiler emitted with a different name than API Management's reference, or didn't emit at all, and
// the diagnostics for invalid values.
[TestClass]
public class PolicyAttributeTests
{
    [TestMethod]
    [DataRow("LlmTokenLimit", "llm-token-limit")]
    [DataRow("AzureOpenAiTokenLimit", "azure-openai-token-limit")]
    public void ShouldEmitEstimatePromptTokens(string method, string element)
    {
        var result = Compile(
            $"context.{method}(new TokenLimitConfig {{ CounterKey = \"k\", EstimatePromptTokens = true, TokensPerMinute = 10 }});");

        result.Should().BeSuccessful();
        result.Document.Descendants(element).Single().Attribute("estimate-prompt-tokens")!.Value.Should().Be("true");
    }

    [TestMethod]
    public void ShouldReportMissingEstimatePromptTokens()
    {
        var result = Compile("context.LlmTokenLimit(new TokenLimitConfig { CounterKey = \"k\", TokensPerMinute = 10 });");

        result.Errors.Should().ContainSingle(error => error.Id == "APIM2006");
    }

    [TestMethod]
    public void ShouldEmitErrorsVariableNameForValidateStatusCode()
    {
        var result = Compile(
            "context.ValidateStatusCode(new ValidateStatusCodeConfig { UnspecifiedStatusCodeAction = \"ignore\", ErrorsVariableName = \"errors\" });",
            "Outbound", "IOutboundContext");

        result.Should().BeSuccessful();
        var element = result.Document.Descendants("validate-status-code").Single();
        element.Attribute("errors-variable-name")!.Value.Should().Be("errors");
        element.Attribute("error-variable-name").Should().BeNull();
    }

    [TestMethod]
    public void ShouldEmitOpenIdConfigValidateConnectivity()
    {
        var result = Compile(
            "context.ValidateJwt(new ValidateJwtConfig { HeaderName = \"Authorization\", OpenIdConfigs = [new OpenIdConfig { Url = \"https://login/.well-known/openid-configuration\", ValidateConnectivity = false }] });");

        result.Should().BeSuccessful();
        result.Document.Descendants("openid-config").Single().Attribute("validate-connectivity")!.Value
            .Should().Be("false");
    }

    [TestMethod]
    [DataRow("Template = \"razor\"", "set-body.template", DisplayName = "Template")]
    [DataRow("XsiNil = \"empty\"", "set-body.xsi-nil", DisplayName = "XsiNil")]
    public void ShouldReportInvalidSetBodyConfigValues(string property, string name)
    {
        var result = Compile($"context.SetBody(\"body\", new SetBodyConfig {{ {property} }});");

        result.Errors.Should().ContainSingle(error => error.Id == "APIM9994")
            .Which.GetMessage().Should().Contain(name);
    }

    private static IDocumentCompilationResult Compile(string statement, string section = "Inbound",
        string context = "IInboundContext") =>
        $$"""
          [Document]
          public class PolicyDocument : IDocument
          {
              public void {{section}}({{context}} context)
              {
                  {{statement}}
              }
          }
          """.CompileDocument();
}
