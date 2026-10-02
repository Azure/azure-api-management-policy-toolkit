// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Globalization;
using System.Text;

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring.Expressions;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Document;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Expressions;

namespace Test.Emulator.Emulator.Policies;

[TestClass]
public class SetBodyTests
{
    private const string LiteralBody = "literal <&> {{ body.name }} @(not an expression)\r\ncaf\u00E9 \u96EA \U0001F600";

    private class ConfiguredSetBody(string body, SetBodyConfig? config = null) : IDocument
    {
        public void Inbound(IInboundContext context) => context.SetBody(body, config);
        public void Backend(IBackendContext context) => context.SetBody(body, config);
        public void Outbound(IOutboundContext context) => context.SetBody(body, config);
        public void OnError(IOnErrorContext context) => context.SetBody(body, config);
    }

    private class ExpressionSetBody : IDocument
    {
        public void Inbound(IInboundContext context) => context.SetBody(Value(context.ExpressionContext));
        public void Backend(IBackendContext context) => context.SetBody(Value(context.ExpressionContext));
        public void Outbound(IOutboundContext context) => context.SetBody(Value(context.ExpressionContext));
        public void OnError(IOnErrorContext context) => context.SetBody(Value(context.ExpressionContext));

        [Expression]
        private static string Value(IExpressionContext context) => (string)context.Variables["body"];
    }

    class SimpleSetBody : IDocument
    {
        public void Inbound(IInboundContext context)
        {
            context.SetBody("inbound-body");
        }

        public void Backend(IBackendContext context)
        {
            context.SetBody("backend-body");
        }

        public void Outbound(IOutboundContext context)
        {
            context.SetBody("outbound-body");
        }

        public void OnError(IOnErrorContext context)
        {
            context.SetBody("error-body");
        }
    }

    [TestMethod]
    public void SetBody_Inbound()
    {
        var test = new SimpleSetBody().AsTestDocument();

        test.RunInbound();

        test.Context.Request.Body.Content.Should().Be("inbound-body");
    }

    [TestMethod]
    public void SetBody_Backend()
    {
        var test = new SimpleSetBody().AsTestDocument();

        test.RunBackend();

        test.Context.Request.Body.Content.Should().Be("backend-body");
    }

    [TestMethod]
    public void SetBody_Outbound()
    {
        var test = new SimpleSetBody().AsTestDocument();

        test.RunOutbound();

        test.Context.Response.Body.Content.Should().Be("outbound-body");
    }

    [TestMethod]
    public void SetBody_OnError()
    {
        var test = new SimpleSetBody().AsTestDocument();

        test.RunOnError();

        test.Context.Response.Body.Content.Should().Be("error-body");
    }

    [TestMethod]
    public void SetBody_Callback()
    {
        var test = new SimpleSetBody().AsTestDocument();
        var callbackExecuted = false;
        test.SetupInbound().SetBody().WithCallback((context, body, _) =>
        {
            callbackExecuted = true;
            context.Request.Body.Content = body.ToUpper();
        });

        test.RunInbound();

        callbackExecuted.Should().BeTrue();
        test.Context.Request.Body.Content.Should().Be("INBOUND-BODY");
    }

    [TestMethod]
    public void SetBody_OverwritesExistingBody()
    {
        var test = new SimpleSetBody().AsTestDocument();
        test.Context.Request.Body.Content = "old-body";

        test.RunInbound();

        test.Context.Request.Body.Content.Should().Be("inbound-body");
    }

    [TestMethod]
    [DataRow(nameof(IInboundContext))]
    [DataRow(nameof(IBackendContext))]
    [DataRow(nameof(IOutboundContext))]
    [DataRow(nameof(IOnErrorContext))]
    public void ShouldPreserveLiteralContentAndMutateOnlyTheAuthoredMessage(string section)
    {
        var test = CreateTest(LiteralBody);

        AssertReplacement(test, section, LiteralBody);
    }

    [TestMethod]
    [DataRow(nameof(IInboundContext))]
    [DataRow(nameof(IBackendContext))]
    [DataRow(nameof(IOutboundContext))]
    [DataRow(nameof(IOnErrorContext))]
    public void ShouldKeepExpressionResultsWithoutParsingOrRenderingThem(string section)
    {
        var test = CreateTest(new ExpressionSetBody());
        test.Context.Variables["body"] = LiteralBody;

        AssertReplacement(test, section, LiteralBody);
    }

    [TestMethod]
    [DataRow(nameof(IInboundContext))]
    [DataRow(nameof(IBackendContext))]
    [DataRow(nameof(IOutboundContext))]
    [DataRow(nameof(IOnErrorContext))]
    public void ShouldAllowAnEmptyLiteralAndUpdateExistingLengthToZero(string section)
    {
        AssertReplacement(CreateTest(""), section, "");
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void ShouldTreatUseValueElementAsCompilationStructureNotBodyTransformation(bool useValueElement)
    {
        var test = CreateTest(LiteralBody, new SetBodyConfig { UseValueElement = useValueElement });

        AssertReplacement(test, nameof(IInboundContext), LiteralBody);
    }

    [TestMethod]
    public void ShouldAcceptAnEmptyConfigurationAsLiteralMode()
    {
        AssertReplacement(CreateTest(LiteralBody, new SetBodyConfig()), nameof(IOutboundContext), LiteralBody);
    }

    [TestMethod]
    [DataRow(nameof(IInboundContext))]
    [DataRow(nameof(IBackendContext))]
    [DataRow(nameof(IOutboundContext))]
    [DataRow(nameof(IOnErrorContext))]
    public void ShouldNotAddContentHeadersWhenTheyWereAbsent(string section)
    {
        var test = CreateTest(LiteralBody);
        GetMessage(test, section).Headers.Clear();

        AssertReplacement(test, section, LiteralBody);

        GetMessage(test, section).Headers.Should().BeEmpty();
    }

    [TestMethod]
    [DataRow(nameof(IInboundContext))]
    [DataRow(nameof(IBackendContext))]
    [DataRow(nameof(IOutboundContext))]
    [DataRow(nameof(IOnErrorContext))]
    public void ShouldExplicitlyRejectLiquidRenderingWithoutChangingEitherMessage(string section)
    {
        var config = new SetBodyConfig { Template = "liquid", XsiNil = "null", ParseDate = false };
        var test = CreateTest("{{ body.name | Upcase }}", config);

        AssertFailure(test, section).InnerException.Should().BeOfType<NotSupportedException>()
            .Which.Message.Should().Contain("liquid").And.Contain("WithCallback");
    }

    [TestMethod]
    [DataRow("")]
    [DataRow(" ")]
    [DataRow("Liquid")]
    [DataRow("razor")]
    [DataRow("json")]
    public void ShouldRejectUndocumentedTemplateModes(string template)
    {
        var test = CreateTest(LiteralBody, new SetBodyConfig { Template = template });

        AssertFailure(test, nameof(IInboundContext)).InnerException.Should().BeOfType<ArgumentException>()
            .Which.Message.Should().Contain(nameof(SetBodyConfig.Template));
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("NULL")]
    [DataRow("false")]
    public void ShouldRejectInvalidXsiNilSettings(string xsiNil)
    {
        var test = CreateTest(LiteralBody, new SetBodyConfig { XsiNil = xsiNil });

        AssertFailure(test, nameof(IOutboundContext)).InnerException.Should().BeOfType<ArgumentException>()
            .Which.Message.Should().Contain(nameof(SetBodyConfig.XsiNil));
    }

    [TestMethod]
    [DataRow("blank", null)]
    [DataRow("null", null)]
    [DataRow(null, true)]
    [DataRow(null, false)]
    public void ShouldExplicitlyRejectRenderingOptionsEvenWithoutATemplate(string? xsiNil, bool? parseDate)
    {
        var test = CreateTest(LiteralBody, new SetBodyConfig { XsiNil = xsiNil, ParseDate = parseDate });

        AssertFailure(test, nameof(IBackendContext)).InnerException.Should().BeOfType<NotSupportedException>()
            .Which.Message.Should().Contain("WithCallback");
    }

    [TestMethod]
    [DataRow(nameof(IInboundContext))]
    [DataRow(nameof(IBackendContext))]
    [DataRow(nameof(IOutboundContext))]
    [DataRow(nameof(IOnErrorContext))]
    public void ShouldRejectANullBodyBeforeMutatingEitherMessage(string section)
    {
        AssertFailure(CreateTest(body: null!), section).InnerException.Should().BeAssignableTo<ArgumentException>();
    }

    [TestMethod]
    [DataRow(nameof(IInboundContext))]
    [DataRow(nameof(IBackendContext))]
    [DataRow(nameof(IOutboundContext))]
    [DataRow(nameof(IOnErrorContext))]
    public void ShouldAllowCallbacksToRenderUnsupportedTemplatesInEverySection(string section)
    {
        var config = new SetBodyConfig { Template = "liquid", XsiNil = "null", ParseDate = false };
        var test = CreateTest("{{ body.name }}", config);
        var message = GetMessage(test, section);
        var headers = ExpectedHeaders(message);
        var calls = 0;
        Setup(test, section, (_, body, actualConfig) => body == "{{ body.name }}" && actualConfig == config)
            .WithCallback((_, body, actualConfig) =>
            {
                body.Should().Be("{{ body.name }}");
                actualConfig.Should().BeSameAs(config);
                calls++;
                message.Body.Content = "rendered by callback";
            });
        Setup(test, section, (_, _, _) => true).WithCallback((_, _, _) => calls += 100);

        RunSection(test, section);

        calls.Should().Be(1);
        message.Body.Content.Should().Be("rendered by callback");
        message.Headers.Should().BeEquivalentTo(headers, options => options.WithStrictOrdering());
    }

    [TestMethod]
    [DataRow(nameof(IInboundContext))]
    [DataRow(nameof(IBackendContext))]
    [DataRow(nameof(IOutboundContext))]
    [DataRow(nameof(IOnErrorContext))]
    public void ShouldUseDefaultLiteralBehaviorWhenTheCallbackPredicateDoesNotMatch(string section)
    {
        var test = CreateTest(LiteralBody);
        var called = false;
        Setup(test, section, (_, _, _) => false).WithCallback((_, _, _) => called = true);

        AssertReplacement(test, section, LiteralBody);

        called.Should().BeFalse();
    }

    [TestMethod]
    [DataRow(nameof(IInboundContext))]
    [DataRow(nameof(IBackendContext))]
    [DataRow(nameof(IOutboundContext))]
    [DataRow(nameof(IOnErrorContext))]
    public void ShouldReportCallbackFailuresForTheCorrectSection(string section)
    {
        var test = CreateTest(LiteralBody);
        Setup(test, section, (_, _, _) => true)
            .WithCallback((_, _, _) => throw new InvalidOperationException("callback failure"));

        AssertFailure(test, section).InnerException.Should().BeOfType<InvalidOperationException>()
            .Which.Message.Should().Be("callback failure");
    }

    private static TestDocument CreateTest(string body, SetBodyConfig? config = null) =>
        CreateTest(new ConfiguredSetBody(body, config));

    private static TestDocument CreateTest(IDocument document)
    {
        var test = document.AsTestDocument();
        test.Context.Request.Body.Content = "original request";
        test.Context.Response.Body.Content = "original response";
        foreach (var message in new MockMessage[] { test.Context.Request, test.Context.Response })
        {
            message.Headers = new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                ["content-type"] = ["application/custom+json; charset=utf-8"],
                ["Content-Length"] = ["999"],
                ["content-length"] = ["1000", "1001"],
                ["X-Unchanged"] = ["one", "two"],
            };
        }

        test.Context.Response.StatusCode = 202;
        return test;
    }

    private static Dictionary<string, string[]> ExpectedHeaders(MockMessage message, string? replacement = null)
    {
        var headers = message.Headers.ToDictionary(header => header.Key, header => header.Value.ToArray());
        if (replacement is not null)
        {
            foreach (var key in headers.Keys.Where(key => key.Equals("Content-Length", StringComparison.OrdinalIgnoreCase)).ToArray())
            {
                headers[key] = [Encoding.UTF8.GetByteCount(replacement).ToString(CultureInfo.InvariantCulture)];
            }
        }

        return headers;
    }

    private static void AssertReplacement(TestDocument test, string section, string expected)
    {
        var request = section is nameof(IInboundContext) or nameof(IBackendContext);
        var requestBody = test.Context.Request.Body.Content;
        var responseBody = test.Context.Response.Body.Content;
        var requestHeaders = ExpectedHeaders(test.Context.Request, request ? expected : null);
        var responseHeaders = ExpectedHeaders(test.Context.Response, request ? null : expected);

        RunSection(test, section);

        test.Context.Request.Body.Content.Should().Be(request ? expected : requestBody);
        test.Context.Response.Body.Content.Should().Be(request ? responseBody : expected);
        test.Context.Request.Headers.Should().BeEquivalentTo(requestHeaders, options => options.WithStrictOrdering());
        test.Context.Response.Headers.Should().BeEquivalentTo(responseHeaders, options => options.WithStrictOrdering());
        test.Context.Response.StatusCode.Should().Be(202);
        GetMessage(test, section).Body.As<byte[]>(preserveContent: true).Should().Equal(Encoding.UTF8.GetBytes(expected));
        test.Context.Request.Body.Consumed.Should().BeFalse();
        test.Context.Response.Body.Consumed.Should().BeFalse();
    }

    private static PolicyException AssertFailure(TestDocument test, string section)
    {
        var requestBody = test.Context.Request.Body.Content;
        var responseBody = test.Context.Response.Body.Content;
        var requestHeaders = ExpectedHeaders(test.Context.Request);
        var responseHeaders = ExpectedHeaders(test.Context.Response);
        var run = () => RunSection(test, section);

        var exception = run.Should().Throw<PolicyException>().Which;

        exception.Policy.Should().Be("SetBody");
        exception.Section.Should().Be(section);
        test.Context.Request.Body.Content.Should().Be(requestBody);
        test.Context.Response.Body.Content.Should().Be(responseBody);
        test.Context.Request.Headers.Should().BeEquivalentTo(requestHeaders, options => options.WithStrictOrdering());
        test.Context.Response.Headers.Should().BeEquivalentTo(responseHeaders, options => options.WithStrictOrdering());
        return exception;
    }

    private static MockMessage GetMessage(TestDocument test, string section) => section switch
    {
        nameof(IInboundContext) or nameof(IBackendContext) => test.Context.Request,
        nameof(IOutboundContext) or nameof(IOnErrorContext) => test.Context.Response,
        _ => throw new ArgumentException("Unsupported SetBody section.", nameof(section)),
    };

    private static MockSetBodyProvider.Setup Setup(
        TestDocument test, string section, Func<GatewayContext, string, SetBodyConfig?, bool> predicate) => section switch
    {
        nameof(IInboundContext) => test.SetupInbound().SetBody(predicate),
        nameof(IBackendContext) => test.SetupBackend().SetBody(predicate),
        nameof(IOutboundContext) => test.SetupOutbound().SetBody(predicate),
        nameof(IOnErrorContext) => test.SetupOnError().SetBody(predicate),
        _ => throw new ArgumentException("Unsupported SetBody section.", nameof(section)),
    };

    private static void RunSection(TestDocument test, string section)
    {
        switch (section)
        {
            case nameof(IInboundContext): test.RunInbound(); break;
            case nameof(IBackendContext): test.RunBackend(); break;
            case nameof(IOutboundContext): test.RunOutbound(); break;
            case nameof(IOnErrorContext): test.RunOnError(); break;
            default: throw new ArgumentException("Unsupported SetBody section.", nameof(section));
        }
    }
}
