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
public class FindAndReplaceTests
{
    private const string Replacement = "caf\u00E9 \u96EA \U0001F600";

    private class ReplaceDocument(string from, string to) : IDocument
    {
        public void Inbound(IInboundContext context) => context.FindAndReplace(from, to);
        public void Backend(IBackendContext context) => context.FindAndReplace(from, to);
        public void Outbound(IOutboundContext context) => context.FindAndReplace(from, to);
        public void OnError(IOnErrorContext context) => context.FindAndReplace(from, to);
    }

    private class ExpressionReplaceDocument : IDocument
    {
        public void Inbound(IInboundContext context) =>
            context.FindAndReplace(From(context.ExpressionContext), To(context.ExpressionContext));

        public void Backend(IBackendContext context) =>
            context.FindAndReplace(From(context.ExpressionContext), To(context.ExpressionContext));

        public void Outbound(IOutboundContext context) =>
            context.FindAndReplace(From(context.ExpressionContext), To(context.ExpressionContext));

        public void OnError(IOnErrorContext context) =>
            context.FindAndReplace(From(context.ExpressionContext), To(context.ExpressionContext));

        [Expression]
        private static string From(IExpressionContext context) => (string)context.Variables["from"];

        [Expression]
        private static string To(IExpressionContext context) => (string)context.Variables["to"];
    }

    class InboundFindAndReplace : IDocument
    {
        public void Inbound(IInboundContext context)
        {
            context.FindAndReplace("foo", "bar");
        }

        public void Backend(IBackendContext context) { }
        public void Outbound(IOutboundContext context) { }
        public void OnError(IOnErrorContext context) { }
    }

    class OutboundFindAndReplace : IDocument
    {
        public void Inbound(IInboundContext context) { }
        public void Backend(IBackendContext context) { }

        public void Outbound(IOutboundContext context)
        {
            context.FindAndReplace("old-url", "new-url");
        }

        public void OnError(IOnErrorContext context) { }
    }

    [TestMethod]
    public void FindAndReplace_Inbound_ShouldReplaceInRequestBody()
    {
        // Arrange
        var test = new TestDocument(new InboundFindAndReplace());
        test.Context.Request.Body.Content = "hello foo world foo";

        // Act
        test.RunInbound();

        // Assert
        test.Context.Request.Body.Content.Should().Be("hello bar world bar");
    }

    [TestMethod]
    public void FindAndReplace_Inbound_NullBody_ShouldNotThrow()
    {
        // Arrange
        var test = new TestDocument(new InboundFindAndReplace());
        test.Context.Request.Body.Content = null;

        // Act & Assert - should not throw
        test.RunInbound();
    }

    [TestMethod]
    public void FindAndReplace_Outbound_ShouldReplaceInResponseBody()
    {
        // Arrange
        var test = new TestDocument(new OutboundFindAndReplace());
        test.Context.Response.Body.Content = "visit old-url for details";

        // Act
        test.RunOutbound();

        // Assert
        test.Context.Response.Body.Content.Should().Be("visit new-url for details");
    }

    [TestMethod]
    public void FindAndReplace_Inbound_Callback()
    {
        // Arrange
        var test = new TestDocument(new InboundFindAndReplace());
        test.Context.Request.Body.Content = "hello foo world";
        var executedCallback = false;

        test.SetupInbound().FindAndReplace().WithCallback((context, from, to) =>
        {
            executedCallback = true;
            // Custom replacement logic
            context.Request.Body.Content = context.Request.Body.Content?.Replace(from, "custom");
        });

        // Act
        test.RunInbound();

        // Assert
        executedCallback.Should().BeTrue();
        test.Context.Request.Body.Content.Should().Be("hello custom world");
    }

    [TestMethod]
    public void FindAndReplace_Outbound_Callback()
    {
        // Arrange
        var test = new TestDocument(new OutboundFindAndReplace());
        test.Context.Response.Body.Content = "visit old-url";
        var executedCallback = false;

        test.SetupOutbound().FindAndReplace().WithCallback((context, from, to) =>
        {
            executedCallback = true;
        });

        // Act
        test.RunOutbound();

        // Assert
        executedCallback.Should().BeTrue();
    }

    [TestMethod]
    public void FindAndReplace_Inbound_NoMatch_ShouldKeepBodyUnchanged()
    {
        // Arrange
        var test = new TestDocument(new InboundFindAndReplace());
        test.Context.Request.Body.Content = "hello world";

        // Act
        test.RunInbound();

        // Assert - "foo" not in body, so no change
        test.Context.Request.Body.Content.Should().Be("hello world");
    }

    [TestMethod]
    public void FindAndReplace_Inbound_CallbackWithPredicate()
    {
        // Arrange
        var test = new TestDocument(new InboundFindAndReplace());
        test.Context.Request.Body.Content = "hello foo world";
        var executedCallback = false;

        test.SetupInbound()
            .FindAndReplace((_, from, _) => from == "foo")
            .WithCallback((_, _, _) =>
            {
                executedCallback = true;
            });

        // Act
        test.RunInbound();

        // Assert
        executedCallback.Should().BeTrue();
    }

    [TestMethod]
    [DataRow(nameof(IInboundContext))]
    [DataRow(nameof(IBackendContext))]
    [DataRow(nameof(IOutboundContext))]
    [DataRow(nameof(IOnErrorContext))]
    public void ShouldReplaceAllLiteralMatchesInOnlyTheAuthoredMessage(string section)
    {
        var test = CreateTest();

        AssertReplacement(test, section, $"{Replacement} FOO {Replacement}");
    }

    [TestMethod]
    [DataRow(nameof(IInboundContext))]
    [DataRow(nameof(IBackendContext))]
    [DataRow(nameof(IOutboundContext))]
    [DataRow(nameof(IOnErrorContext))]
    public void ShouldPreserveEvaluatedFromAndToExpressionValues(string section)
    {
        var test = CreateTest(new ExpressionReplaceDocument());
        test.Context.Variables["from"] = "foo";
        test.Context.Variables["to"] = Replacement;

        AssertReplacement(test, section, $"{Replacement} FOO {Replacement}");
    }

    [TestMethod]
    public void ShouldReplaceMetacharactersLiterallyWithoutRegexOrHtmlEscaping()
    {
        var test = CreateTest(new ReplaceDocument(".+$[", @"\$&<tag>"));
        test.Context.Request.Body.Content = "<p>.+$[ &amp; .+$[</p>";

        AssertReplacement(test, nameof(IInboundContext), @"<p>\$&<tag> &amp; \$&<tag></p>");
    }

    [TestMethod]
    [DataRow(nameof(IInboundContext))]
    [DataRow(nameof(IBackendContext))]
    [DataRow(nameof(IOutboundContext))]
    [DataRow(nameof(IOnErrorContext))]
    public void ShouldSupportAZeroLengthReplacementToRemoveMatches(string section)
    {
        var test = CreateTest(new ReplaceDocument("foo", ""));

        AssertReplacement(test, section, " FOO ");
    }

    [TestMethod]
    [DataRow(null, nameof(IInboundContext))]
    [DataRow("", nameof(IBackendContext))]
    [DataRow("no match", nameof(IOutboundContext))]
    [DataRow("FOO only", nameof(IOnErrorContext))]
    [DataRow("   ", nameof(IBackendContext))]
    public void ShouldLeaveAbsentOrUnmatchedBodiesAndHeadersUnchanged(string? body, string section)
    {
        var test = CreateTest();
        var message = GetMessage(test, section);
        message.Body.Content = body;
        var headers = ExpectedHeaders(message);

        RunSection(test, section);

        message.Body.Content.Should().Be(body);
        message.Headers.Should().BeEquivalentTo(headers, options => options.WithStrictOrdering());
        message.Body.Consumed.Should().BeFalse();
    }

    [TestMethod]
    public void ShouldNotUpdateHeadersWhenTheReplacementEqualsTheSearchString()
    {
        var test = CreateTest(new ReplaceDocument("foo", "foo"));
        var headers = ExpectedHeaders(test.Context.Request);

        test.RunInbound();

        test.Context.Request.Body.Content.Should().Be("foo FOO foo");
        test.Context.Request.Headers.Should().BeEquivalentTo(headers, options => options.WithStrictOrdering());
    }

    [TestMethod]
    [DataRow(nameof(IInboundContext))]
    [DataRow(nameof(IBackendContext))]
    [DataRow(nameof(IOutboundContext))]
    [DataRow(nameof(IOnErrorContext))]
    public void ShouldNotAddContentHeadersWhenTheyWereAbsent(string section)
    {
        var test = CreateTest();
        GetMessage(test, section).Headers.Clear();

        AssertReplacement(test, section, $"{Replacement} FOO {Replacement}");

        GetMessage(test, section).Headers.Should().BeEmpty();
    }

    [TestMethod]
    [DataRow(null, "new", nameof(IInboundContext))]
    [DataRow("", "new", nameof(IBackendContext))]
    [DataRow(null, "new", nameof(IOutboundContext))]
    [DataRow("", "new", nameof(IOnErrorContext))]
    [DataRow("foo", null, nameof(IInboundContext))]
    [DataRow("foo", null, nameof(IBackendContext))]
    [DataRow("foo", null, nameof(IOutboundContext))]
    [DataRow("foo", null, nameof(IOnErrorContext))]
    public void ShouldRejectMissingSearchOrReplacementBeforeMutatingEitherMessage(
        string? from, string? to, string section)
    {
        var test = CreateTest(new ReplaceDocument(from!, to!));

        AssertFailure(test, section).InnerException.Should().BeAssignableTo<ArgumentException>();
    }

    [TestMethod]
    public void ShouldValidateTheSearchEvenWhenTheBodyIsAbsent()
    {
        var test = CreateTest(new ReplaceDocument("", "new"));
        test.Context.Request.Body.Content = null;

        AssertFailure(test, nameof(IInboundContext)).InnerException.Should().BeOfType<ArgumentException>();
    }

    [TestMethod]
    [DataRow(nameof(IInboundContext))]
    [DataRow(nameof(IBackendContext))]
    [DataRow(nameof(IOutboundContext))]
    [DataRow(nameof(IOnErrorContext))]
    public void ShouldAllowCallbackOverridesInEveryAuthoredSection(string section)
    {
        var test = CreateTest();
        var message = GetMessage(test, section);
        var headers = ExpectedHeaders(message);
        var calls = 0;
        Setup(test, section, (_, from, to) => from == "foo" && to == Replacement)
            .WithCallback((_, from, to) =>
            {
                from.Should().Be("foo");
                to.Should().Be(Replacement);
                calls++;
                message.Body.Content = "callback replacement";
            });
        Setup(test, section, (_, _, _) => true).WithCallback((_, _, _) => calls += 100);

        RunSection(test, section);

        calls.Should().Be(1);
        message.Body.Content.Should().Be("callback replacement");
        message.Headers.Should().BeEquivalentTo(headers, options => options.WithStrictOrdering());
    }

    [TestMethod]
    [DataRow(nameof(IInboundContext))]
    [DataRow(nameof(IBackendContext))]
    [DataRow(nameof(IOutboundContext))]
    [DataRow(nameof(IOnErrorContext))]
    public void ShouldUseDefaultReplacementWhenTheCallbackPredicateDoesNotMatch(string section)
    {
        var test = CreateTest();
        var called = false;
        Setup(test, section, (_, _, _) => false).WithCallback((_, _, _) => called = true);

        AssertReplacement(test, section, $"{Replacement} FOO {Replacement}");

        called.Should().BeFalse();
    }

    [TestMethod]
    [DataRow(nameof(IInboundContext))]
    [DataRow(nameof(IBackendContext))]
    [DataRow(nameof(IOutboundContext))]
    [DataRow(nameof(IOnErrorContext))]
    public void ShouldReportCallbackFailuresForTheCorrectSection(string section)
    {
        var test = CreateTest();
        Setup(test, section, (_, _, _) => true)
            .WithCallback((_, _, _) => throw new InvalidOperationException("callback failure"));

        AssertFailure(test, section).InnerException.Should().BeOfType<InvalidOperationException>()
            .Which.Message.Should().Be("callback failure");
    }

    private static TestDocument CreateTest(IDocument? document = null)
    {
        var test = (document ?? new ReplaceDocument("foo", Replacement)).AsTestDocument();
        test.Context.Request.Body.Content = "foo FOO foo";
        test.Context.Response.Body.Content = "foo FOO foo";
        foreach (var message in new MockMessage[] { test.Context.Request, test.Context.Response })
        {
            message.Headers = new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                ["content-type"] = ["text/plain; charset=utf-8"],
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

        exception.Policy.Should().Be("FindAndReplace");
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
        _ => throw new ArgumentException("Unsupported FindAndReplace section.", nameof(section)),
    };

    private static MockFindAndReplaceProvider.Setup Setup(
        TestDocument test, string section, Func<GatewayContext, string, string, bool> predicate) => section switch
    {
        nameof(IInboundContext) => test.SetupInbound().FindAndReplace(predicate),
        nameof(IBackendContext) => test.SetupBackend().FindAndReplace(predicate),
        nameof(IOutboundContext) => test.SetupOutbound().FindAndReplace(predicate),
        nameof(IOnErrorContext) => test.SetupOnError().FindAndReplace(predicate),
        _ => throw new ArgumentException("Unsupported FindAndReplace section.", nameof(section)),
    };

    private static void RunSection(TestDocument test, string section)
    {
        switch (section)
        {
            case nameof(IInboundContext): test.RunInbound(); break;
            case nameof(IBackendContext): test.RunBackend(); break;
            case nameof(IOutboundContext): test.RunOutbound(); break;
            case nameof(IOnErrorContext): test.RunOnError(); break;
            default: throw new ArgumentException("Unsupported FindAndReplace section.", nameof(section));
        }
    }
}
