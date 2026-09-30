// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Globalization;
using System.Text;
using System.Xml.Linq;

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Document;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Expressions;

namespace Test.Emulator.Emulator.Policies;

[TestClass]
public class RedirectContentUrlsTests
{
    private const string PublicBase = "https://gateway.example:9443/public";
    private const string BackendBase = "http://backend.example:8080/service";
    private const string LinkSuffix = "/caf%C3%A9?q=one&amp;q=two#details";

    private class RedirectDocument : IDocument
    {
        public void Inbound(IInboundContext context) => context.RedirectContentUrls();
        public void Outbound(IOutboundContext context) => context.RedirectContentUrls();
    }

    class SimpleRedirectContentUrls : IDocument
    {
        public void Inbound(IInboundContext context) { }
        public void Backend(IBackendContext context) { }

        public void Outbound(IOutboundContext context)
        {
            context.RedirectContentUrls();
        }

        public void OnError(IOnErrorContext context) { }
    }

    [TestMethod]
    public void RedirectContentUrls_Outbound_ShouldExecuteWithoutError()
    {
        // Arrange
        var test = new TestDocument(new SimpleRedirectContentUrls());

        // Act & Assert - no-op should not throw
        test.RunOutbound();
    }

    [TestMethod]
    public void RedirectContentUrls_Outbound_Callback()
    {
        // Arrange
        var test = new TestDocument(new SimpleRedirectContentUrls());
        var executedCallback = false;

        test.SetupOutbound().RedirectContentUrls().WithCallback(_ =>
        {
            executedCallback = true;
        });

        // Act
        test.RunOutbound();

        // Assert
        executedCallback.Should().BeTrue();
    }

    [TestMethod]
    public void RedirectContentUrls_Outbound_CallbackWithPredicate()
    {
        // Arrange
        var test = new TestDocument(new SimpleRedirectContentUrls())
        {
            Context = { Variables = { { "redirect", true } } }
        };

        test.SetupOutbound()
            .RedirectContentUrls(context => context.Variables.ContainsKey("redirect"))
            .WithCallback(context =>
            {
                context.Variables["redirected"] = true;
            });

        // Act
        test.RunOutbound();

        // Assert
        test.Context.Variables.Should().ContainKey("redirected")
            .WhoseValue.Should().Be(true);
    }

    [TestMethod]
    public void RedirectContentUrls_Outbound_PredicateNotMatching()
    {
        // Arrange
        var test = new TestDocument(new SimpleRedirectContentUrls());
        var executedCallback = false;

        test.SetupOutbound()
            .RedirectContentUrls(context => context.Variables.ContainsKey("nonexistent"))
            .WithCallback(_ =>
            {
                executedCallback = true;
            });

        // Act
        test.RunOutbound();

        // Assert
        executedCallback.Should().BeFalse();
    }

    [TestMethod]
    [DataRow(nameof(IInboundContext))]
    [DataRow(nameof(IOutboundContext))]
    public void ShouldMapHtmlLinksAndMutateOnlyTheAuthoredMessage(string section)
    {
        var test = CreateTest();
        var inbound = section == nameof(IInboundContext);
        var expected = HtmlLink(inbound ? BackendBase : PublicBase);
        var requestBody = test.Context.Request.Body.Content;
        var responseBody = test.Context.Response.Body.Content;
        var requestHeaders = ExpectedHeaders(test.Context.Request, inbound ? expected : null);
        var responseHeaders = ExpectedHeaders(test.Context.Response, inbound ? null : expected);
        var requestUrl = test.Context.Request.Url.ToString();
        var originalUrl = test.Context.Request.OriginalUrl.ToString();

        RunSection(test, section);

        test.Context.Request.Body.Content.Should().Be(inbound ? expected : requestBody);
        test.Context.Response.Body.Content.Should().Be(inbound ? responseBody : expected);
        test.Context.Request.Headers.Should().BeEquivalentTo(requestHeaders, options => options.WithStrictOrdering());
        test.Context.Response.Headers.Should().BeEquivalentTo(responseHeaders, options => options.WithStrictOrdering());
        test.Context.Request.Url.ToString().Should().Be(requestUrl);
        test.Context.Request.OriginalUrl.ToString().Should().Be(originalUrl);
        test.Context.Api.ServiceUrl.ToString().Should().Be(BackendBase);
        test.Context.Response.StatusCode.Should().Be(202);
        test.Context.Variables["untouched"].Should().Be("value");
        GetMessage(test, section).Body.As<byte[]>(preserveContent: true).Should().Equal(Encoding.UTF8.GetBytes(expected));
        test.Context.Request.Body.Consumed.Should().BeFalse();
        test.Context.Response.Body.Consumed.Should().BeFalse();
    }

    [TestMethod]
    public void ShouldPreferConfiguredBackendAndRespectBasePathBoundaries()
    {
        var test = CreateTest();
        test.Context.BackendUrl = "https://override.example:8443/alternate/";
        test.Context.Response.Body.Content = """
            <a href="https://override.example:8443/alternate/item">one</a>
            <img src='https://override.example:8443/alternate/image.png?x=1&amp;y=2#top'>
            <a href=https://override.example:8443/alternate>root</a>
            <a href="https://override.example:8443/alternate-other/item">other</a>
            <a href="http://backend.example:8080/service/item">original</a>
            """;
        var expected = """
            <a href="https://gateway.example:9443/public/item">one</a>
            <img src='https://gateway.example:9443/public/image.png?x=1&amp;y=2#top'>
            <a href=https://gateway.example:9443/public>root</a>
            <a href="https://override.example:8443/alternate-other/item">other</a>
            <a href="http://backend.example:8080/service/item">original</a>
            """;
        var headers = ExpectedHeaders(test.Context.Response, expected);

        test.RunOutbound();

        test.Context.Response.Body.Content.Should().Be(expected);
        test.Context.Response.Headers.Should().BeEquivalentTo(headers, options => options.WithStrictOrdering());
    }

    [TestMethod]
    [DataRow("http://BACKEND.EXAMPLE:8080/service/item", BackendBase)]
    [DataRow("https://BACKEND.EXAMPLE:443/service/item", "https://backend.example/service")]
    [DataRow("http://[::1]:8080/service/item", "http://[::1]:8080/service/")]
    public void ShouldCompareHostsAndDefaultPortsAsUris(string link, string backend)
    {
        var test = CreateTest();
        test.Context.BackendUrl = backend;
        test.Context.Response.Body.Content = $"<a href=\"{link}\">item</a>";

        test.RunOutbound();

        test.Context.Response.Body.Content.Should().Be($"<a href=\"{PublicBase}/item\">item</a>");
    }

    [TestMethod]
    public void ShouldSupportRootBaseUrlsAndBodyLinksWithoutHtml()
    {
        var test = CreateTest();
        test.Context.Api.Path = "";
        test.Context.BackendUrl = "http://backend.example:8080/";
        test.Context.Response.Headers["content-type"] = ["application/json"];
        test.Context.Response.Body.Content = """{"url":"http://backend.example:8080/item?a=1&a=2#top"}""";

        test.RunOutbound();

        test.Context.Response.Body.Content.Should().Be(
            """{"url":"https://gateway.example:9443/item?a=1&a=2#top"}""");
        test.Context.Response.Headers["content-type"].Should().Equal("application/json");
    }

    [TestMethod]
    [DataRow(null, nameof(IInboundContext))]
    [DataRow("", nameof(IInboundContext))]
    [DataRow("   ", nameof(IInboundContext))]
    [DataRow(null, nameof(IOutboundContext))]
    [DataRow("", nameof(IOutboundContext))]
    [DataRow("   ", nameof(IOutboundContext))]
    public void ShouldLeaveAbsentOrLinklessBodiesAndHeadersUntouched(string? body, string section)
    {
        var test = CreateTest();
        var message = GetMessage(test, section);
        message.Body.Content = body;
        var headers = ExpectedHeaders(message);

        RunSection(test, section);

        message.Body.Content.Should().Be(body);
        message.Headers.Should().BeEquivalentTo(headers, options => options.WithStrictOrdering());
    }

    [TestMethod]
    [DataRow("""<a href="/service/item">relative</a>""")]
    [DataRow("""<a href="//backend.example:8080/service/item">relative</a>""")]
    [DataRow("""<a href="http://backend.example.evil:8080/service/item">other host</a>""")]
    [DataRow("""<a href="http://backend.example:8081/service/item">other port</a>""")]
    [DataRow("""<a href="https://backend.example:8080/service/item">other scheme</a>""")]
    [DataRow("""<a href="http://backend.example:8080/service-other/item">other path</a>""")]
    [DataRow("""<a href="http://backend.example:8080/service%2Fitem">encoded boundary</a>""")]
    [DataRow("""<a href="http://backend.example:8080/service/../elsewhere">outside base</a>""")]
    [DataRow("""<a href="http://backend.example:8080/service/%GG">bad escape</a>""")]
    [DataRow("""<a href="http://backend.example:8080/service/%">bad escape</a>""")]
    [DataRow("""<a href="http://backend.example:99999/service/item">bad port</a>""")]
    [DataRow("""<a href="http://backend.example:8080/service/has space">bad path</a>""")]
    [DataRow("""<a href="http://backend.example:8080/service\item">bad separator</a>""")]
    [DataRow("""<a href="http://user@backend.example:8080/service/item">credentials</a>""")]
    [DataRow("""<a href="https://elsewhere.example/?next=http://backend.example:8080/service/item">nested</a>""")]
    [DataRow("nothttp://backend.example:8080/service/item")]
    public void ShouldNotPartiallyRewriteUnrelatedRelativeOrMalformedUrls(string body)
    {
        var test = CreateTest();
        test.Context.Response.Body.Content = body;
        var headers = ExpectedHeaders(test.Context.Response);

        test.RunOutbound();

        test.Context.Response.Body.Content.Should().Be(body);
        test.Context.Response.Headers.Should().BeEquivalentTo(headers, options => options.WithStrictOrdering());
    }

    [TestMethod]
    [DataRow(nameof(IInboundContext))]
    [DataRow(nameof(IOutboundContext))]
    public void ShouldRespectPublicPathBoundariesInEitherDirection(string section)
    {
        var test = CreateTest();
        var message = GetMessage(test, section);
        var source = section == nameof(IInboundContext) ? PublicBase : BackendBase;
        var body = $"<a href=\"{source}-other/item\">not this API</a>";
        message.Body.Content = body;
        var headers = ExpectedHeaders(message);

        RunSection(test, section);

        message.Body.Content.Should().Be(body);
        message.Headers.Should().BeEquivalentTo(headers, options => options.WithStrictOrdering());
    }

    [TestMethod]
    [DataRow(nameof(IInboundContext), "/../{source}/item", "/item")]
    [DataRow(nameof(IOutboundContext), "/../{source}/item", "/item")]
    [DataRow(nameof(IInboundContext), "/nested/../item", "/item")]
    [DataRow(nameof(IOutboundContext), "/nested/../item", "/item")]
    [DataRow(nameof(IInboundContext), "/./item", "/item")]
    [DataRow(nameof(IOutboundContext), "/./item", "/item")]
    [DataRow(nameof(IInboundContext), "/../../{source}/item", "/item")]
    [DataRow(nameof(IOutboundContext), "/../../{source}/item", "/item")]
    [DataRow(nameof(IInboundContext), "/%2E%2E/{source}/item", "/item")]
    [DataRow(nameof(IOutboundContext), "/%2E%2E/{source}/item", "/item")]
    [DataRow(nameof(IInboundContext), "/%2e./{source}/item", "/item")]
    [DataRow(nameof(IOutboundContext), "/%2e./{source}/item", "/item")]
    [DataRow(nameof(IInboundContext), "/.%2E/{source}/item", "/item")]
    [DataRow(nameof(IOutboundContext), "/.%2E/{source}/item", "/item")]
    [DataRow(nameof(IInboundContext), "/nested/%2e%2E/item", "/item")]
    [DataRow(nameof(IOutboundContext), "/nested/%2e%2E/item", "/item")]
    [DataRow(nameof(IInboundContext), "/%2E%2E/%2e%2e/{source}/item", "/item")]
    [DataRow(nameof(IOutboundContext), "/%2E%2E/%2e%2e/{source}/item", "/item")]
    [DataRow(nameof(IInboundContext), "/%2E/item", "/item")]
    [DataRow(nameof(IOutboundContext), "/%2E/item", "/item")]
    [DataRow(nameof(IInboundContext), "/../{source}/caf%C3%A9", "/caf%C3%A9")]
    [DataRow(nameof(IOutboundContext), "/../{source}/caf%C3%A9", "/caf%C3%A9")]
    [DataRow(nameof(IInboundContext), "/nested/..", "/")]
    [DataRow(nameof(IOutboundContext), "/nested/..", "/")]
    public void ShouldCanonicalizeDotSegmentsWithoutEscapingTheDestinationBase(
        string section, string suffix, string canonicalSuffix)
    {
        var test = CreateTest();
        var inbound = section == nameof(IInboundContext);
        var source = inbound ? PublicBase : BackendBase;
        var destination = inbound ? BackendBase : PublicBase;
        var sourceSegment = new Uri(source).AbsolutePath.Trim('/');
        var rawSuffix = suffix.Replace("{source}", sourceSegment, StringComparison.Ordinal);
        const string queryAndFragment = "?next=%2F..%2Foutside&amp;tag=one&amp;tag=two#section%2F..%2Fpart";

        var rewritten = AssertRedirect(test, section,
            source + rawSuffix + queryAndFragment,
            destination + canonicalSuffix + queryAndFragment);

        rewritten.GetLeftPart(UriPartial.Authority).Should().Be(new Uri(destination).GetLeftPart(UriPartial.Authority));
        rewritten.AbsolutePath.Should().Be(new Uri(destination).AbsolutePath.TrimEnd('/') + canonicalSuffix);
        rewritten.Query.Should().Be("?next=%2F..%2Foutside&tag=one&tag=two");
        rewritten.Fragment.Should().Be("#section%2F..%2Fpart");
    }

    [TestMethod]
    [DataRow(nameof(IInboundContext), "/../outside/item")]
    [DataRow(nameof(IOutboundContext), "/../outside/item")]
    [DataRow(nameof(IInboundContext), "/%2E%2E/outside/item")]
    [DataRow(nameof(IOutboundContext), "/%2E%2E/outside/item")]
    [DataRow(nameof(IInboundContext), "/%2e./outside/item")]
    [DataRow(nameof(IOutboundContext), "/%2e./outside/item")]
    [DataRow(nameof(IInboundContext), "/.%2E/outside/item")]
    [DataRow(nameof(IOutboundContext), "/.%2E/outside/item")]
    public void ShouldLeaveLinksUnchangedWhenDotSegmentsResolveOutsideTheSourceBase(string section, string suffix)
    {
        var test = CreateTest();
        var source = section == nameof(IInboundContext) ? PublicBase : BackendBase;
        var link = source + suffix + "?keep=%2E%2E&amp;keep=two#original";

        var unchanged = AssertRedirect(test, section, link, link);

        unchanged.AbsolutePath.Should().Be("/outside/item");
        unchanged.GetLeftPart(UriPartial.Authority).Should().Be(new Uri(source).GetLeftPart(UriPartial.Authority));
    }

    [TestMethod]
    [DataRow(nameof(IInboundContext), "")]
    [DataRow(nameof(IOutboundContext), "")]
    [DataRow(nameof(IInboundContext), "?q=../outside")]
    [DataRow(nameof(IOutboundContext), "?q=../outside")]
    [DataRow(nameof(IInboundContext), "#../outside")]
    [DataRow(nameof(IOutboundContext), "#../outside")]
    [DataRow(nameof(IInboundContext), "?tag=one&amp;tag=two#details")]
    [DataRow(nameof(IOutboundContext), "?tag=one&amp;tag=two#details")]
    [DataRow(nameof(IInboundContext), "?q=%2E%2E%2Foutside&amp;plus=%2B#part%2F..")]
    [DataRow(nameof(IOutboundContext), "?q=%2E%2E%2Foutside&amp;plus=%2B#part%2F..")]
    public void ShouldPreserveOriginalQueryAndFragmentWhileCanonicalizingOnlyThePath(
        string section, string queryAndFragment)
    {
        var test = CreateTest();
        var inbound = section == nameof(IInboundContext);
        var source = inbound ? PublicBase : BackendBase;
        var destination = inbound ? BackendBase : PublicBase;
        var sourceSegment = new Uri(source).AbsolutePath.Trim('/');
        var link = $"{source}/%2E%2E/{sourceSegment}/item{queryAndFragment}";

        var rewritten = AssertRedirect(test, section, link, destination + "/item" + queryAndFragment);

        rewritten.AbsolutePath.Should().Be(new Uri(destination).AbsolutePath + "/item");
    }

    [TestMethod]
    [DataRow(nameof(IInboundContext), "")]
    [DataRow(nameof(IOutboundContext), "")]
    [DataRow(nameof(IInboundContext), "?q=../outside#../details")]
    [DataRow(nameof(IOutboundContext), "?q=../outside#../details")]
    public void ShouldPreserveAuthorityOnlyLinkFormattingForRootBases(string section, string queryAndFragment)
    {
        var test = CreateTest();
        var publicOrigin = new Uri(PublicBase).GetLeftPart(UriPartial.Authority);
        var backendOrigin = new Uri(BackendBase).GetLeftPart(UriPartial.Authority);
        test.Context.Api.Path = "";
        test.Context.BackendUrl = backendOrigin;
        var inbound = section == nameof(IInboundContext);
        var source = inbound ? publicOrigin : backendOrigin;
        var destination = inbound ? backendOrigin : publicOrigin;

        var rewritten = AssertRedirect(test, section, source + queryAndFragment, destination + queryAndFragment);

        rewritten.AbsolutePath.Should().Be("/");
    }

    [TestMethod]
    [DataRow(nameof(IInboundContext))]
    [DataRow(nameof(IOutboundContext))]
    public void ShouldNotAddContentHeadersWhenTheyAreAbsent(string section)
    {
        var test = CreateTest();
        var message = GetMessage(test, section);
        message.Headers.Clear();
        message.Headers["X-Unchanged"] = ["one", "two"];

        RunSection(test, section);

        message.Body.Content.Should().Be(HtmlLink(section == nameof(IInboundContext) ? BackendBase : PublicBase));
        message.Headers.Should().ContainSingle().Which.Key.Should().Be("X-Unchanged");
        message.Headers["X-Unchanged"].Should().Equal("one", "two");
    }

    [TestMethod]
    [DataRow(nameof(IInboundContext))]
    [DataRow(nameof(IOutboundContext))]
    public void ShouldLetCallbacksOverrideDefaultRewritingAndInvalidMapping(string section)
    {
        var test = CreateTest();
        test.Context.BackendUrl = "invalid backend";
        var message = GetMessage(test, section);
        var headers = ExpectedHeaders(message);
        var calls = 0;
        Setup(test, section, context => context.Variables.ContainsKey("untouched")).WithCallback(_ =>
        {
            calls++;
            message.Body.Content = "callback body";
        });
        Setup(test, section, _ => true).WithCallback(_ => calls += 100);

        RunSection(test, section);

        calls.Should().Be(1);
        message.Body.Content.Should().Be("callback body");
        message.Headers.Should().BeEquivalentTo(headers, options => options.WithStrictOrdering());
    }

    [TestMethod]
    [DataRow(nameof(IInboundContext))]
    [DataRow(nameof(IOutboundContext))]
    public void ShouldFallThroughToRewritingWhenTheCallbackPredicateDoesNotMatch(string section)
    {
        var test = CreateTest();
        var called = false;
        Setup(test, section, _ => false).WithCallback(_ => called = true);

        RunSection(test, section);

        called.Should().BeFalse();
        GetMessage(test, section).Body.Content.Should().Be(
            HtmlLink(section == nameof(IInboundContext) ? BackendBase : PublicBase));
    }

    [TestMethod]
    [DataRow(nameof(IInboundContext))]
    [DataRow(nameof(IOutboundContext))]
    public void ShouldReportCallbackErrorsForTheCorrectSection(string section)
    {
        var test = CreateTest();
        Setup(test, section, _ => true).WithCallback(_ => throw new InvalidOperationException("callback failure"));

        AssertFailure(test, section).InnerException.Should().BeOfType<InvalidOperationException>()
            .Which.Message.Should().Be("callback failure");
    }

    [TestMethod]
    [DataRow("", nameof(IInboundContext))]
    [DataRow("/relative", nameof(IInboundContext))]
    [DataRow("ftp://backend.example/service", nameof(IInboundContext))]
    [DataRow("http://backend.example/service?query=value", nameof(IOutboundContext))]
    [DataRow("http://backend.example/service#fragment", nameof(IOutboundContext))]
    [DataRow("http://user@backend.example/service", nameof(IOutboundContext))]
    [DataRow("http://backend.example:invalid/service", nameof(IOutboundContext))]
    [DataRow("http://backend.example/service/%GG", nameof(IOutboundContext))]
    public void ShouldRejectInvalidBackendMappingWithoutChangingEitherMessage(string backend, string section)
    {
        var test = CreateTest();
        test.Context.BackendUrl = backend;

        AssertFailure(test, section).InnerException.Should().BeAssignableTo<ArgumentException>();
    }

    [TestMethod]
    public void ShouldRejectAnInvalidPublicUrlWithoutChangingEitherMessage()
    {
        var test = CreateTest();
        test.Context.Request.OriginalUrl.Host = "";

        AssertFailure(test, nameof(IOutboundContext)).InnerException.Should().BeAssignableTo<ArgumentException>();
    }

    private static string HtmlLink(string baseUrl) =>
        $"<a href=\"{baseUrl}{LinkSuffix}\">caf\u00E9 \u96EA \U0001F600</a>";

    private static Uri AssertRedirect(TestDocument test, string section, string link, string expectedLink)
    {
        var inbound = section == nameof(IInboundContext);
        var message = GetMessage(test, section);
        message.Body.Content = $"<a href=\"{link}\">caf\u00E9 \u96EA \U0001F600</a>";
        var expected = $"<a href=\"{expectedLink}\">caf\u00E9 \u96EA \U0001F600</a>";
        var changed = link != expectedLink;
        var requestBody = test.Context.Request.Body.Content;
        var responseBody = test.Context.Response.Body.Content;
        var requestHeaders = ExpectedHeaders(test.Context.Request, inbound && changed ? expected : null);
        var responseHeaders = ExpectedHeaders(test.Context.Response, !inbound && changed ? expected : null);
        var requestUrl = test.Context.Request.Url.ToString();
        var originalUrl = test.Context.Request.OriginalUrl.ToString();

        RunSection(test, section);

        test.Context.Request.Body.Content.Should().Be(inbound ? expected : requestBody);
        test.Context.Response.Body.Content.Should().Be(inbound ? responseBody : expected);
        test.Context.Request.Headers.Should().BeEquivalentTo(requestHeaders, options => options.WithStrictOrdering());
        test.Context.Response.Headers.Should().BeEquivalentTo(responseHeaders, options => options.WithStrictOrdering());
        test.Context.Request.Url.ToString().Should().Be(requestUrl);
        test.Context.Request.OriginalUrl.ToString().Should().Be(originalUrl);
        test.Context.Response.StatusCode.Should().Be(202);
        test.Context.Variables["untouched"].Should().Be("value");
        message.Body.As<byte[]>(preserveContent: true).Should().Equal(Encoding.UTF8.GetBytes(expected));
        test.Context.Request.Body.Consumed.Should().BeFalse();
        test.Context.Response.Body.Consumed.Should().BeFalse();
        return new Uri(XElement.Parse(message.Body.Content!).Attribute("href")!.Value);
    }

    private static TestDocument CreateTest()
    {
        var test = new RedirectDocument().AsTestDocument();
        test.Context.Request.OriginalUrl = new MockUrl(new Uri($"{PublicBase}/orders?original=value"));
        test.Context.Request.Url = new MockUrl(new Uri("https://rewritten.example/private/orders"));
        test.Context.Api.Path = "public";
        test.Context.Api.ServiceUrl = new MockUrl(new Uri(BackendBase));
        test.Context.Request.Body.Content = HtmlLink(PublicBase);
        test.Context.Response.Body.Content = HtmlLink(BackendBase);
        foreach (var message in new MockMessage[] { test.Context.Request, test.Context.Response })
        {
            message.Headers = new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                ["content-type"] = ["text/html; charset=utf-8"],
                ["Content-Length"] = ["999"],
                ["content-length"] = ["1000", "1001"],
                ["Location"] = [$"{BackendBase}/header"],
                ["X-Unchanged"] = ["one", "two"],
            };
        }

        test.Context.Response.StatusCode = 202;
        test.Context.Variables["untouched"] = "value";
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

    private static PolicyException AssertFailure(TestDocument test, string section)
    {
        var requestBody = test.Context.Request.Body.Content;
        var responseBody = test.Context.Response.Body.Content;
        var requestHeaders = ExpectedHeaders(test.Context.Request);
        var responseHeaders = ExpectedHeaders(test.Context.Response);
        var run = () => RunSection(test, section);

        var exception = run.Should().Throw<PolicyException>().Which;

        exception.Policy.Should().Be("RedirectContentUrls");
        exception.Section.Should().Be(section);
        test.Context.Request.Body.Content.Should().Be(requestBody);
        test.Context.Response.Body.Content.Should().Be(responseBody);
        test.Context.Request.Headers.Should().BeEquivalentTo(requestHeaders, options => options.WithStrictOrdering());
        test.Context.Response.Headers.Should().BeEquivalentTo(responseHeaders, options => options.WithStrictOrdering());
        return exception;
    }

    private static MockMessage GetMessage(TestDocument test, string section) => section switch
    {
        nameof(IInboundContext) => test.Context.Request,
        nameof(IOutboundContext) => test.Context.Response,
        _ => throw new ArgumentException("Unsupported RedirectContentUrls section.", nameof(section)),
    };

    private static MockRedirectContentUrlsProvider.Setup Setup(
        TestDocument test, string section, Func<GatewayContext, bool> predicate) => section switch
    {
        nameof(IInboundContext) => test.SetupInbound().RedirectContentUrls(predicate),
        nameof(IOutboundContext) => test.SetupOutbound().RedirectContentUrls(predicate),
        _ => throw new ArgumentException("Unsupported RedirectContentUrls section.", nameof(section)),
    };

    private static void RunSection(TestDocument test, string section)
    {
        switch (section)
        {
            case nameof(IInboundContext): test.RunInbound(); break;
            case nameof(IOutboundContext): test.RunOutbound(); break;
            default: throw new ArgumentException("Unsupported RedirectContentUrls section.", nameof(section));
        }
    }
}
