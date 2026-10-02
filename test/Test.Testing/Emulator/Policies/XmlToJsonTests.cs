// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text;
using System.Xml;

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring.Expressions;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Document;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Expressions;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Test.Emulator.Emulator.Policies;

[TestClass]
public class XmlToJsonTests
{
    private static XmlToJsonConfig DefaultConfig => new()
    {
        Kind = "direct",
        Apply = "always",
        ConsiderAcceptHeader = false,
    };

    private class ConversionDocument(XmlToJsonConfig config) : IDocument
    {
        public void Inbound(IInboundContext context) => context.XmlToJson(config);
        public void Backend(IBackendContext context) => context.XmlToJson(config);
        public void Outbound(IOutboundContext context) => context.XmlToJson(config);
        public void OnError(IOnErrorContext context) => context.XmlToJson(config);
    }

    private class ExpressionDocument : IDocument
    {
        public void Inbound(IInboundContext context) => context.XmlToJson(Config(context.ExpressionContext));
        public void Backend(IBackendContext context) => context.XmlToJson(Config(context.ExpressionContext));
        public void Outbound(IOutboundContext context) => context.XmlToJson(Config(context.ExpressionContext));
        public void OnError(IOnErrorContext context) => context.XmlToJson(Config(context.ExpressionContext));

        private static XmlToJsonConfig Config(IExpressionContext context) => new()
        {
            Kind = Kind(context),
            Apply = Apply(context),
            ConsiderAcceptHeader = ConsiderAccept(context),
            AlwaysArrayChildElements = AlwaysArray(context),
        };

        [Expression]
        private static string Kind(IExpressionContext context) => (string)context.Variables["kind"];

        [Expression]
        private static string Apply(IExpressionContext context) => (string)context.Variables["apply"];

        [Expression]
        private static bool ConsiderAccept(IExpressionContext context) => (bool)context.Variables["accept"];

        [Expression]
        private static bool AlwaysArray(IExpressionContext context) => (bool)context.Variables["array"];
    }

    [TestMethod]
    [DataRow("inbound", "direct")]
    [DataRow("backend", "direct")]
    [DataRow("outbound", "direct")]
    [DataRow("on-error", "direct")]
    [DataRow("inbound", "javascript-friendly")]
    [DataRow("backend", "javascript-friendly")]
    [DataRow("outbound", "javascript-friendly")]
    [DataRow("on-error", "javascript-friendly")]
    public void ShouldConvertOnlyTheSelectedBody(string section, string kind)
    {
        var test = new ConversionDocument(DefaultConfig with { Kind = kind }).AsTestDocument();
        var message = Message(test, section);
        var other = OtherMessage(test, section);
        message.Body.Content = "<root><item>one</item><item>two</item><single><value>42</value></single><empty /></root>";
        message.Headers["Content-Type"] = ["text/xml; charset=utf-8"];
        message.Headers["X-Keep"] = ["unchanged"];
        other.Body.Content = "untouched";
        other.Headers["Content-Type"] = ["text/plain"];

        Run(test, section);

        AssertJson(message.Body.Content, """{"root":{"item":["one","two"],"single":{"value":"42"},"empty":null}}""");
        message.Headers["Content-Type"].Should().Equal("application/json");
        message.Headers["X-Keep"].Should().Equal("unchanged");
        other.Body.Content.Should().Be("untouched");
        other.Headers["Content-Type"].Should().Equal("text/plain");
    }

    [TestMethod]
    [DataRow("inbound", "Content-Length", false)]
    [DataRow("backend", "content-length", false)]
    [DataRow("outbound", "cOnTeNt-LeNgTh", true)]
    [DataRow("on-error", "duplicates", false)]
    [DataRow("inbound", "empty", false)]
    public void ShouldCanonicalizeExistingContentLengthUsingUtf8Bytes(
        string section, string lengthKey, bool ignoreCase)
    {
        var test = new ConversionDocument(DefaultConfig).AsTestDocument();
        var message = Message(test, section);
        var other = OtherMessage(test, section);
        message.Body.Content = "<root>caf&#xE9; &#x1F600;</root>";
        var headers = new Dictionary<string, string[]>(ignoreCase ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal)
        {
            ["cOnTeNt-TyPe"] = ["text/xml"],
            ["X-Keep"] = ["first", "second"],
            ["content-language"] = ["fr"],
        };
        if (lengthKey == "duplicates")
        {
            headers["Content-Length"] = ["999"];
            headers["content-length"] = ["123", "456"];
            headers["CONTENT-LENGTH"] = [];
        }
        else
        {
            headers[lengthKey == "empty" ? "content-length" : lengthKey] = lengthKey == "empty" ? [] : ["999"];
        }

        message.Headers = headers;
        other.Body.Content = "untouched";
        other.Headers["content-length"] = ["777"];
        other.Headers["Content-Type"] = ["text/plain"];

        Run(test, section);

        var expected = "{\"root\":\"caf\u00e9 \U0001F600\"}";
        message.Body.Content.Should().Be(expected);
        var bytes = message.Body.As<byte[]>(true);
        bytes.Should().Equal(Encoding.UTF8.GetBytes(expected));
        bytes.Length.Should().Be(21).And.BeGreaterThan(expected.Length);
        message.Headers.Should().BeSameAs(headers);
        message.Headers.Keys.Where(key => key.Equals("Content-Length", StringComparison.OrdinalIgnoreCase))
            .Should().Equal("Content-Length");
        message.Headers["Content-Length"].Should().Equal("21");
        message.Headers["Content-Type"].Should().Equal("application/json");
        message.Headers["X-Keep"].Should().Equal("first", "second");
        message.Headers["content-language"].Should().Equal("fr");
        message.Headers.Should().HaveCount(4);
        other.Body.Content.Should().Be("untouched");
        other.Headers["content-length"].Should().Equal("777");
        other.Headers["Content-Type"].Should().Equal("text/plain");
        other.Headers.Should().HaveCount(2);
    }

    [TestMethod]
    public void ShouldNotAddContentLengthWhenItWasAbsent()
    {
        var test = new ConversionDocument(DefaultConfig).AsTestDocument();
        test.Context.Request.Body.Content = "<root>value</root>";

        test.RunInbound();

        AssertJson(test.Context.Request.Body.Content, """{"root":"value"}""");
        test.Context.Request.Headers.Keys.Should().NotContain(
            key => key.Equals("Content-Length", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    [DataRow("apply")]
    [DataRow("accept")]
    [DataRow("error")]
    public void ShouldPreserveAllOriginalHeadersAndBodyWhenConversionDoesNotSucceed(string outcome)
    {
        var config = DefaultConfig with
        {
            Apply = outcome == "apply" ? "content-type-xml" : "always",
            ConsiderAcceptHeader = outcome == "accept",
        };
        var test = new ConversionDocument(config).AsTestDocument();
        var body = outcome == "error" ? "<root>" : "<root>unchanged</root>";
        var headers = new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["Content-Type"] = [outcome == "apply" ? "text/plain" : "text/xml"],
            ["content-type"] = [outcome == "apply" ? "text/plain; charset=utf-8" : "application/xml"],
            ["Content-Length"] = ["000999"],
            ["content-length"] = ["123", "456"],
            ["X-Keep"] = ["first", "second"],
        };
        var snapshot = headers.ToDictionary(header => header.Key, header => header.Value.ToArray(), StringComparer.Ordinal);
        test.Context.Response.Body.Content = body;
        test.Context.Response.Headers = headers;
        test.Context.Request.Headers["Accept"] = ["application/xml"];

        if (outcome == "error")
        {
            var act = () => test.RunOutbound();
            act.Should().Throw<PolicyException>().Which.Policy.Should().Be("XmlToJson");
        }
        else
        {
            test.RunOutbound();
        }

        test.Context.Response.Body.Content.Should().Be(body);
        test.Context.Response.Headers.Should().BeSameAs(headers);
        test.Context.Response.Headers.Keys.Should().Equal(snapshot.Keys);
        test.Context.Response.Headers.Should().BeEquivalentTo(snapshot, options => options.WithStrictOrdering());
        test.Context.Request.Headers["Accept"].Should().Equal("application/xml");
    }

    [TestMethod]
    public void DirectShouldPreserveDeclarationNamespacesAttributesAndCharacterData()
    {
        var test = new ConversionDocument(DefaultConfig).AsTestDocument();
        test.Context.Request.Body.Content =
            """<?xml version="1.0" encoding="utf-8"?><n:root xmlns:n="urn:sample" id="7"><n:item code="A">hello</n:item><empty /><paired></paired><data><![CDATA[a < b]]></data></n:root>""";

        test.RunInbound();

        AssertJson(test.Context.Request.Body.Content,
            """{"?xml":{"@version":"1.0","@encoding":"utf-8"},"n:root":{"@xmlns:n":"urn:sample","@id":"7","n:item":{"@code":"A","#text":"hello"},"empty":null,"paired":"","data":{"#cdata-section":"a < b"}}}""");
    }

    [TestMethod]
    public void JavascriptFriendlyShouldNormalizeMetadataWithoutLosingValues()
    {
        var test = new ConversionDocument(DefaultConfig with { Kind = "javascript-friendly" }).AsTestDocument();
        test.Context.Request.Body.Content =
            """<?xml version="1.0"?><n:root xmlns:n="urn:sample" id="7"><n:item code="A">hello</n:item><empty /><data><![CDATA[a < b]]></data></n:root>""";

        test.RunInbound();

        AssertJson(test.Context.Request.Body.Content,
            """{"n_root":{"id":"7","n_item":{"code":"A","$text":"hello"},"empty":null,"data":{"$cdata":"a < b"}}}""");
    }

    [TestMethod]
    [DataRow("direct", false)]
    [DataRow("direct", true)]
    [DataRow("javascript-friendly", false)]
    [DataRow("javascript-friendly", true)]
    public void ShouldApplyArrayOptionRecursivelyButNotToTheRoot(string kind, bool alwaysArray)
    {
        var test = new ConversionDocument(DefaultConfig with
        {
            Kind = kind,
            AlwaysArrayChildElements = alwaysArray,
        }).AsTestDocument();
        test.Context.Request.Body.Content = "<root><item>one</item><group><child>two</child><empty /></group></root>";

        test.RunInbound();

        AssertJson(test.Context.Request.Body.Content, alwaysArray
            ? """{"root":{"item":["one"],"group":[{"child":["two"],"empty":[null]}]}}"""
            : """{"root":{"item":"one","group":{"child":"two","empty":null}}}""");
    }

    [TestMethod]
    public void ShouldPreserveLexicalValuesEntitiesAndMixedText()
    {
        var test = new ConversionDocument(DefaultConfig).AsTestDocument();
        test.Context.Request.Body.Content =
            "<root>before<child>a &amp; b &#x3A9;</child>after<number>001</number><boolean>true</boolean><date>2024-01-02T03:04:05.1200Z</date><spaces> hello </spaces></root>";

        test.RunInbound();

        AssertJson(test.Context.Request.Body.Content,
            """{"root":{"#text":["before","after"],"child":"a & b \u03a9","number":"001","boolean":"true","date":"2024-01-02T03:04:05.1200Z","spaces":" hello "}}""");
    }

    [TestMethod]
    [DataRow("application/xml", true)]
    [DataRow("text/xml; charset=utf-8", true)]
    [DataRow("application/soap+xml", true)]
    [DataRow("IMAGE/SVG+XML; CHARSET=UTF-8", true)]
    [DataRow("application/json", false)]
    [DataRow("application/xmlish", false)]
    [DataRow("text/plain; profile=xml", false)]
    [DataRow("application/example+xmlish", false)]
    [DataRow("", false)]
    [DataRow(null, false)]
    public void ShouldGateOnXmlMediaTypes(string? contentType, bool shouldConvert)
    {
        var test = new ConversionDocument(DefaultConfig with { Apply = "content-type-xml" }).AsTestDocument();
        var message = test.Context.Response;
        message.Body.Content = shouldConvert ? "<root>text</root>" : "not XML";
        if (contentType is not null)
        {
            message.Headers["Content-Type"] = [contentType];
        }

        test.RunOutbound();

        if (shouldConvert)
        {
            AssertJson(message.Body.Content, """{"root":"text"}""");
            message.Headers["Content-Type"].Should().Equal("application/json");
        }
        else
        {
            message.Body.Content.Should().Be("not XML");
            message.Headers.Should().HaveCount(contentType is null ? 0 : 1);
            if (contentType is not null)
            {
                message.Headers["Content-Type"].Should().Equal(contentType);
            }
        }
    }

    [TestMethod]
    [DataRow("inbound")]
    [DataRow("backend")]
    [DataRow("outbound")]
    [DataRow("on-error")]
    public void ContentTypeGateShouldUseTheSelectedMessageAndCaseInsensitiveHeaderNames(string section)
    {
        var test = new ConversionDocument(DefaultConfig with { Apply = "content-type-xml" }).AsTestDocument();
        var message = Message(test, section);
        var other = OtherMessage(test, section);
        message.Body.Content = "<root>value</root>";
        message.Headers["content-type"] = ["application/xml"];
        other.Headers["Content-Type"] = ["application/json"];

        Run(test, section);

        AssertJson(message.Body.Content, """{"root":"value"}""");
        message.Headers.Keys.Count(key => key.Equals("Content-Type", StringComparison.OrdinalIgnoreCase))
            .Should().Be(1);
        message.Headers["Content-Type"].Should().Equal("application/json");
        other.Headers["Content-Type"].Should().Equal("application/json");
    }

    [TestMethod]
    public void AlwaysShouldIgnoreSourceContentTypeAndAnUnconsideredAcceptHeader()
    {
        var test = new ConversionDocument(DefaultConfig).AsTestDocument();
        test.Context.Request.Body.Content = "<root>value</root>";
        test.Context.Request.Headers["Content-Type"] = ["text/plain"];
        test.Context.Request.Headers["Accept"] = ["not a media type"];

        test.RunInbound();

        AssertJson(test.Context.Request.Body.Content, """{"root":"value"}""");
    }

    [TestMethod]
    [DataRow(null, false)]
    [DataRow("", false)]
    [DataRow("application/xml", false)]
    [DataRow("*/*", false)]
    [DataRow("application/*", false)]
    [DataRow("application/json", true)]
    [DataRow("text/json; q=0.5", true)]
    [DataRow("APPLICATION/PROBLEM+JSON; Q=0.2", true)]
    [DataRow("text/plain, application/json; q=0.1", true)]
    [DataRow("application/json; q=0", false)]
    [DataRow("application/json; q=0, */*; q=1", false)]
    public void ShouldConsiderRequestAcceptByDefault(string? accept, bool shouldConvert)
    {
        var test = new ConversionDocument(DefaultConfig with { ConsiderAcceptHeader = null }).AsTestDocument();
        test.Context.Response.Body.Content = shouldConvert ? "<root>text</root>" : "not XML";
        test.Context.Response.Headers["Content-Type"] = ["text/xml"];
        test.Context.Response.Headers["Accept"] = ["application/json"];
        if (accept is not null)
        {
            test.Context.Request.Headers["Accept"] = [accept];
        }

        test.RunOutbound();

        if (shouldConvert)
        {
            AssertJson(test.Context.Response.Body.Content, """{"root":"text"}""");
            test.Context.Response.Headers["Content-Type"].Should().Equal("application/json");
        }
        else
        {
            test.Context.Response.Body.Content.Should().Be("not XML");
            test.Context.Response.Headers["Content-Type"].Should().Equal("text/xml");
        }
    }

    [TestMethod]
    public void AcceptGateShouldReadAllHeaderValuesCaseInsensitively()
    {
        var test = new ConversionDocument(DefaultConfig with { ConsiderAcceptHeader = true }).AsTestDocument();
        test.Context.Request.Body.Content = "<root>text</root>";
        test.Context.Request.Headers["aCcEpT"] = ["text/plain; q=1", "application/json; q=0.2"];

        test.RunInbound();

        AssertJson(test.Context.Request.Body.Content, """{"root":"text"}""");
        test.Context.Request.Headers["aCcEpT"].Should().Equal("text/plain; q=1", "application/json; q=0.2");
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow(" \r\n\t ")]
    public void ShouldFailExplicitlyForAnEmptySelectedBody(string? body)
    {
        var test = new ConversionDocument(DefaultConfig).AsTestDocument();
        test.Context.Request.Body.Content = body;
        test.Context.Request.Headers["Content-Type"] = ["text/xml"];

        var act = () => test.RunInbound();

        var exception = act.Should().Throw<PolicyException>().Which;
        exception.Policy.Should().Be("XmlToJson");
        exception.Section.Should().Be(nameof(IInboundContext));
        exception.InnerException.Should().BeOfType<InvalidOperationException>();
        test.Context.Request.Body.Content.Should().Be(body);
        test.Context.Request.Headers["Content-Type"].Should().Equal("text/xml");
    }

    [TestMethod]
    [DataRow("not XML")]
    [DataRow("<root>")]
    [DataRow("<one /><two />")]
    [DataRow("<?xml version=\"1.0\"?>")]
    [DataRow("<!DOCTYPE root [<!ENTITY value 'text'>]><root>&value;</root>")]
    [DataRow("<!DOCTYPE root SYSTEM 'https://invalid.example.test/entities.dtd'><root />")]
    public void ShouldFailExplicitlyForMalformedXmlOrDtds(string body)
    {
        var test = new ConversionDocument(DefaultConfig).AsTestDocument();
        test.Context.Request.Body.Content = body;
        test.Context.Request.Headers["Content-Type"] = ["text/xml"];

        var act = () => test.RunInbound();

        var exception = act.Should().Throw<PolicyException>().Which;
        exception.Policy.Should().Be("XmlToJson");
        exception.InnerException.Should().BeOfType<XmlException>();
        test.Context.Request.Body.Content.Should().Be(body);
        test.Context.Request.Headers["Content-Type"].Should().Equal("text/xml");
    }

    [TestMethod]
    [DataRow("kind", "")]
    [DataRow("kind", "unknown")]
    [DataRow("kind", "DIRECT")]
    [DataRow("kind", null)]
    [DataRow("apply", "")]
    [DataRow("apply", "content-type-json")]
    [DataRow("apply", null)]
    public void ShouldRejectInvalidOptionsEvenWhenAcceptWouldSkipConversion(string property, string? value)
    {
        var config = property == "kind"
            ? DefaultConfig with { Kind = value!, ConsiderAcceptHeader = true }
            : DefaultConfig with { Apply = value!, ConsiderAcceptHeader = true };
        var test = new ConversionDocument(config).AsTestDocument();
        test.Context.Request.Body.Content = "<root />";

        var act = () => test.RunInbound();

        act.Should().Throw<PolicyException>().Which.InnerException.Should().BeOfType<ArgumentException>();
        test.Context.Request.Body.Content.Should().Be("<root />");
    }

    [TestMethod]
    [DataRow("Content-Type")]
    [DataRow("Accept")]
    public void ShouldRejectMalformedGatingHeaders(string header)
    {
        var test = new ConversionDocument(DefaultConfig with
        {
            Apply = "content-type-xml",
            ConsiderAcceptHeader = true,
        }).AsTestDocument();
        test.Context.Request.Body.Content = "<root />";
        test.Context.Request.Headers["Content-Type"] = ["application/xml"];
        test.Context.Request.Headers["Accept"] = ["application/json"];
        test.Context.Request.Headers[header] = ["not a media type"];

        var act = () => test.RunInbound();

        act.Should().Throw<PolicyException>().Which.InnerException.Should().BeOfType<FormatException>();
        test.Context.Request.Body.Content.Should().Be("<root />");
    }

    [TestMethod]
    public void JavascriptFriendlyShouldRejectNameCollisionsRatherThanDropData()
    {
        var test = new ConversionDocument(DefaultConfig with { Kind = "javascript-friendly" }).AsTestDocument();
        test.Context.Request.Body.Content = """<root id="attribute"><id>element</id></root>""";

        var act = () => test.RunInbound();

        act.Should().Throw<PolicyException>().Which.InnerException.Should().BeOfType<ArgumentException>();
        test.Context.Request.Body.Content.Should().Be("""<root id="attribute"><id>element</id></root>""");
    }

    [TestMethod]
    [DataRow("inbound")]
    [DataRow("backend")]
    [DataRow("outbound")]
    [DataRow("on-error")]
    public void ShouldEvaluateAllAuthoredExpressionOptions(string section)
    {
        var test = new ExpressionDocument().AsTestDocument();
        test.Context.Variables["kind"] = "javascript-friendly";
        test.Context.Variables["apply"] = "content-type-xml";
        test.Context.Variables["accept"] = false;
        test.Context.Variables["array"] = true;
        var message = Message(test, section);
        message.Body.Content = """<n:root xmlns:n="urn:expression" id="7"><n:child>value</n:child></n:root>""";
        message.Headers["Content-Type"] = ["application/xml"];
        test.Context.Request.Headers["Accept"] = ["text/plain"];

        Run(test, section);

        AssertJson(message.Body.Content, """{"n_root":{"id":"7","n_child":["value"]}}""");
    }

    [TestMethod]
    [DataRow("inbound")]
    [DataRow("backend")]
    [DataRow("outbound")]
    [DataRow("on-error")]
    public void CallbackShouldOverrideConversionAndValidation(string section)
    {
        var config = DefaultConfig with { Kind = "invalid", Apply = "invalid" };
        var test = new ConversionDocument(config).AsTestDocument();
        var message = Message(test, section);
        message.Body.Content = "not XML";
        message.Headers["Content-Type"] = ["text/plain"];
        var called = false;
        Callback(test, section, (context, actualConfig) =>
        {
            called = true;
            actualConfig.Should().BeSameAs(config);
            Message(test, section).Body.Content = "callback";
            context.Variables["callback"] = true;
        });

        Run(test, section);

        called.Should().BeTrue();
        message.Body.Content.Should().Be("callback");
        message.Headers["Content-Type"].Should().Equal("text/plain");
        test.Context.Variables["callback"].Should().Be(true);
    }

    [TestMethod]
    public void AnUnmatchedCallbackPredicateShouldRunTheDefaultConversion()
    {
        var test = new ConversionDocument(DefaultConfig).AsTestDocument();
        test.Context.Request.Body.Content = "<root>value</root>";
        var called = false;
        Callback(test, "inbound", (_, _) => called = true, (_, config) => config.Kind == "javascript-friendly");

        test.RunInbound();

        called.Should().BeFalse();
        AssertJson(test.Context.Request.Body.Content, """{"root":"value"}""");
    }

    [TestMethod]
    public void CallbackShouldSupportErrorSimulation()
    {
        var test = new ConversionDocument(DefaultConfig).AsTestDocument();
        Callback(test, "inbound", (context, _) =>
        {
            context.Response.StatusCode = 422;
            context.Response.Body.Content = "conversion failed";
            throw new FinishSectionProcessingException();
        }, (_, config) => config.Kind == "direct");

        test.RunInbound();

        test.Context.Response.StatusCode.Should().Be(422);
        test.Context.Response.Body.Content.Should().Be("conversion failed");
    }

    private static void AssertJson(string? actual, string expected)
    {
        actual.Should().NotBeNull();
        JToken.DeepEquals(ParseJson(actual!), ParseJson(expected)).Should().BeTrue(
            "the entire converted JSON body should match {0}, but was {1}", expected, actual);
    }

    private static JToken ParseJson(string value)
    {
        using var reader = new JsonTextReader(new StringReader(value)) { DateParseHandling = DateParseHandling.None };
        return JToken.ReadFrom(reader);
    }

    private static MockMessage Message(TestDocument test, string section) =>
        section is "inbound" or "backend" ? test.Context.Request : test.Context.Response;

    private static MockMessage OtherMessage(TestDocument test, string section) =>
        section is "inbound" or "backend" ? test.Context.Response : test.Context.Request;

    private static void Run(TestDocument test, string section)
    {
        switch (section)
        {
            case "inbound": test.RunInbound(); break;
            case "backend": test.RunBackend(); break;
            case "outbound": test.RunOutbound(); break;
            case "on-error": test.RunOnError(); break;
            default: throw new ArgumentOutOfRangeException(nameof(section));
        }
    }

    private static void Callback(
        TestDocument test,
        string section,
        Action<GatewayContext, XmlToJsonConfig> callback,
        Func<GatewayContext, XmlToJsonConfig, bool>? predicate = null)
    {
        switch (section)
        {
            case "inbound": Register(test.SetupInbound(), callback, predicate); break;
            case "backend": Register(test.SetupBackend(), callback, predicate); break;
            case "outbound": Register(test.SetupOutbound(), callback, predicate); break;
            case "on-error": Register(test.SetupOnError(), callback, predicate); break;
            default: throw new ArgumentOutOfRangeException(nameof(section));
        }
    }

    private static void Register<TSection>(
        MockPoliciesProvider<TSection> mock,
        Action<GatewayContext, XmlToJsonConfig> callback,
        Func<GatewayContext, XmlToJsonConfig, bool>? predicate) where TSection : class
    {
        // Resolve the new public adapter at runtime so the handler-missing red phase still compiles.
        var provider = typeof(TestDocument).Assembly.GetType(
            "Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Document.MockXmlToJsonProvider");
        provider.Should().NotBeNull("XmlToJson should expose the same callback API as other policies");
        var method = provider!.GetMethods().Single(candidate =>
            candidate.Name == "XmlToJson" && candidate.GetParameters().Length == (predicate is null ? 1 : 2));
        object?[] arguments = predicate is null ? [mock] : [mock, predicate];
        var setup = method.MakeGenericMethod(typeof(TSection)).Invoke(null, arguments);
        setup.Should().NotBeNull();
        var withCallback = setup!.GetType().GetMethod("WithCallback");
        withCallback.Should().NotBeNull();
        withCallback!.Invoke(setup, [callback]);
    }
}
