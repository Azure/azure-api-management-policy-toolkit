// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling;

[TestClass]
public class NullConfigPropertyTests
{
    [TestMethod]
    [DataRow("null")]
    [DataRow("default")]
    [DataRow("default(string)")]
    [DataRow("(string?)null")]
    [DataRow("(null)")]
    [DataRow("null!")]
    [DataRow("default!")]
    [DataRow("NoCachingType")]
    public void ShouldTreatNullStringPropertyAsNotSet(string value)
    {
        var code =
            $$"""
            [Document]
            public class PolicyDocument : IDocument
            {
                private const string? NoCachingType = null;

                public void Inbound(IInboundContext context)
                {
                    context.CacheRemoveValue(new CacheRemoveValueConfig { Key = "k", CachingType = {{value}} });
                }
            }
            """;

        code.CompileDocument().Should().BeSuccessful().And.DocumentEquivalentTo(
            """
            <policies>
                <inbound>
                    <cache-remove-value key="k" />
                </inbound>
            </policies>
            """);
    }

    [TestMethod]
    [DataRow("null")]
    [DataRow("default")]
    [DataRow("default(bool?)")]
    [DataRow("(bool?)null")]
    public void ShouldTreatNullNullableValuePropertyAsNotSet(string value)
    {
        var code =
            $$"""
            [Document]
            public class PolicyDocument : IDocument
            {
                public void Inbound(IInboundContext context)
                {
                    context.LlmContentSafety(new LlmContentSafetyConfig { BackendId = "b", ShieldPrompt = {{value}} });
                }
            }
            """;

        code.CompileDocument().Should().BeSuccessful().And.DocumentEquivalentTo(
            """
            <policies>
                <inbound>
                    <llm-content-safety backend-id="b" />
                </inbound>
            </policies>
            """);
    }

    [TestMethod]
    public void ShouldLeaveOutReasonThatIsNull()
    {
        var code =
            """
            [Document]
            public class PolicyDocument : IDocument
            {
                public void Inbound(IInboundContext context)
                {
                    context.SetStatus(new StatusConfig { Code = 404, Reason = null });
                }
            }
            """;

        code.CompileDocument().Should().BeSuccessful().And.DocumentEquivalentTo(
            """
            <policies>
                <inbound>
                    <set-status code="404" />
                </inbound>
            </policies>
            """);
    }

    [TestMethod]
    public void ShouldReportRequiredPropertyThatIsNull()
    {
        var result =
            """
            [Document]
            public class PolicyDocument : IDocument
            {
                public void Inbound(IInboundContext context)
                {
                    context.CacheRemoveValue(new CacheRemoveValueConfig { Key = null });
                }
            }
            """.CompileDocument();

        result.Errors.Should().ContainSingle(error => error.Id == "APIM2006");
    }

    [TestMethod]
    public void ShouldCompileDefaultOfAValueTypeAsItsValue()
    {
        // default is false here, not "not set"
        var code =
            """
            [Document]
            public class PolicyDocument : IDocument
            {
                public void Inbound(IInboundContext context)
                {
                    context.CheckHeader(new CheckHeaderConfig
                    {
                        Name = "X-Required",
                        FailCheckHttpCode = 400,
                        FailCheckErrorMessage = "Missing header",
                        IgnoreCase = default,
                        Values = ["a"]
                    });
                }
            }
            """;

        code.CompileDocument().Should().BeSuccessful().And.DocumentEquivalentTo(
            """
            <policies>
                <inbound>
                    <check-header name="X-Required" failed-check-httpcode="400" failed-check-error-message="Missing header" ignore-case="False">
                        <value>a</value>
                    </check-header>
                </inbound>
            </policies>
            """);
    }
}
