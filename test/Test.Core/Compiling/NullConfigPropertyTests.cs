// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.IoC;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling;

[TestClass]
public class NullConfigPropertyTests
{
    // Compiled with the references of a project, as only there bool? resolves and default(bool) converts to it.
    [TestMethod]
    [DataRow("default(bool)", """<llm-content-safety backend-id="b" shield-prompt="false" />""")]
    [DataRow("default", """<llm-content-safety backend-id="b" />""")]
    [DataRow("default(bool?)", """<llm-content-safety backend-id="b" />""")]
    [DataRow("(bool?)null", """<llm-content-safety backend-id="b" />""")]
    [DataRow("(bool?)default", """<llm-content-safety backend-id="b" />""")]
    [DataRow("((bool?)null)", """<llm-content-safety backend-id="b" />""")]
    [DataRow("(default)", """<llm-content-safety backend-id="b" />""")]
    public void ShouldReadADefaultByItsOwnTypeInAProject(string value, string expected)
    {
        var tree = CSharpSyntaxTree.ParseText(
            $$"""
            using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;

            [Document]
            public class PolicyDocument : IDocument
            {
                public void Inbound(IInboundContext context)
                {
                    context.LlmContentSafety(new LlmContentSafetyConfig { BackendId = "b", ShieldPrompt = {{value}} });
                }
            }
            """);
        var runtime = Path.GetDirectoryName(typeof(object).Assembly.Location)!;
        var compilation = CSharpCompilation.Create(
            Guid.NewGuid().ToString(),
            syntaxTrees: [tree],
            references:
            [
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                MetadataReference.CreateFromFile(Path.Combine(runtime, "System.Runtime.dll")),
                MetadataReference.CreateFromFile(typeof(IDocument).Assembly.Location)
            ]);
        using var services = new ServiceCollection().SetupCompiler().BuildServiceProvider();

        var result = services.GetRequiredService<DocumentCompiler>().Compile(
            compilation,
            tree.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>().Single());

        result.Should().BeSuccessful().And.DocumentEquivalentTo(
            $"""
            <policies>
                <inbound>
                    {expected}
                </inbound>
            </policies>
            """);
    }

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
