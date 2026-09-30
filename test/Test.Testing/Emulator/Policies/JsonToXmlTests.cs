// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text;
using System.Text.Json;
using System.Xml.Linq;

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring.Expressions;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Document;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Expressions;

namespace Test.Emulator.Emulator.Policies;

[TestClass]
public class JsonToXmlTests
{
    private static JsonToXmlConfig DefaultConfig => new()
    {
        Apply = "always",
        ConsiderAcceptHeader = false,
    };

    private class ConversionDocument(JsonToXmlConfig config) : IDocument
    {
        public void Inbound(IInboundContext context) => context.JsonToXml(config);
        public void Backend(IBackendContext context) => context.JsonToXml(config);
        public void Outbound(IOutboundContext context) => context.JsonToXml(config);
        public void OnError(IOnErrorContext context) => context.JsonToXml(config);
    }

    private class ExpressionDocument : IDocument
    {
        public void Inbound(IInboundContext context) => context.JsonToXml(Config(context.ExpressionContext));
        public void Backend(IBackendContext context) => context.JsonToXml(Config(context.ExpressionContext));
        public void Outbound(IOutboundContext context) => context.JsonToXml(Config(context.ExpressionContext));
        public void OnError(IOnErrorContext context) => context.JsonToXml(Config(context.ExpressionContext));

        private static JsonToXmlConfig Config(IExpressionContext context) => new()
        {
            Apply = Apply(context),
            ConsiderAcceptHeader = ConsiderAccept(context),
            ParseDate = ParseDate(context),
            NamespaceSeparator = Separator(context),
            NamespacePrefix = Prefix(context),
            AttributeBlockName = AttributeBlock(context),
        };

        [Expression]
        private static string Apply(IExpressionContext context) => (string)context.Variables["apply"];

        [Expression]
        private static bool ConsiderAccept(IExpressionContext context) => (bool)context.Variables["accept"];

        [Expression]
        private static bool ParseDate(IExpressionContext context) => (bool)context.Variables["date"];

        [Expression]
        private static char Separator(IExpressionContext context) => (char)context.Variables["separator"];

        [Expression]
        private static string Prefix(IExpressionContext context) => (string)context.Variables["prefix"];

        [Expression]
        private static string AttributeBlock(IExpressionContext context) => (string)context.Variables["attributes"];
    }

    [TestMethod]
    [DataRow("inbound")]
    [DataRow("backend")]
    [DataRow("outbound")]
    [DataRow("on-error")]
    public void ShouldConvertOnlyTheSelectedBody(string section)
    {
        var test = new ConversionDocument(DefaultConfig).AsTestDocument();
        var message = Message(test, section);
        var other = OtherMessage(test, section);
        message.Body.Content = """{"root":{"item":[1,2],"enabled":true,"missing":null,"empty":""}}""";
        message.Headers["Content-Type"] = ["application/json; charset=utf-8"];
        message.Headers["X-Keep"] = ["unchanged"];
        other.Body.Content = "untouched";
        other.Headers["Content-Type"] = ["text/plain"];

        Run(test, section);

        message.Body.Content.Should().Be("<root><item>1</item><item>2</item><enabled>true</enabled><missing /><empty></empty></root>");
        message.Headers["Content-Type"].Should().Equal("application/xml");
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
        message.Body.Content = """{"root":"caf\u00e9 \ud83d\ude00"}""";
        var headers = new Dictionary<string, string[]>(ignoreCase ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal)
        {
            ["cOnTeNt-TyPe"] = ["application/json"],
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

        var expected = "<root>caf\u00e9 \U0001F600</root>";
        message.Body.Content.Should().Be(expected);
        var bytes = message.Body.As<byte[]>(true);
        bytes.Should().Equal(Encoding.UTF8.GetBytes(expected));
        bytes.Length.Should().Be(23).And.BeGreaterThan(expected.Length);
        message.Headers.Should().BeSameAs(headers);
        message.Headers.Keys.Where(key => key.Equals("Content-Length", StringComparison.OrdinalIgnoreCase))
            .Should().Equal("Content-Length");
        message.Headers["Content-Length"].Should().Equal("23");
        message.Headers["Content-Type"].Should().Equal("application/xml");
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
        test.Context.Request.Body.Content = """{"root":"value"}""";

        test.RunInbound();

        test.Context.Request.Body.Content.Should().Be("<root>value</root>");
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
            Apply = outcome == "apply" ? "content-type-json" : "always",
            ConsiderAcceptHeader = outcome == "accept",
        };
        var test = new ConversionDocument(config).AsTestDocument();
        var body = outcome == "error" ? "{\"root\":" : DeclaredBody("utf-16");
        var headers = new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["Content-Type"] = [outcome == "apply" ? "text/plain" : "application/json"],
            ["content-type"] = [outcome == "apply" ? "text/plain; charset=utf-8" : "application/json; charset=utf-8"],
            ["Content-Length"] = ["000999"],
            ["content-length"] = ["123", "456"],
            ["X-Keep"] = ["first", "second"],
        };
        var snapshot = headers.ToDictionary(header => header.Key, header => header.Value.ToArray(), StringComparer.Ordinal);
        test.Context.Response.Body.Content = body;
        test.Context.Response.Headers = headers;
        test.Context.Request.Headers["Accept"] = ["application/json"];

        if (outcome == "error")
        {
            var act = () => test.RunOutbound();
            act.Should().Throw<PolicyException>().Which.Policy.Should().Be("JsonToXml");
        }
        else
        {
            test.RunOutbound();
        }

        test.Context.Response.Body.Content.Should().Be(body);
        test.Context.Response.Headers.Should().BeSameAs(headers);
        test.Context.Response.Headers.Keys.Should().Equal(snapshot.Keys);
        test.Context.Response.Headers.Should().BeEquivalentTo(snapshot, options => options.WithStrictOrdering());
        test.Context.Request.Headers["Accept"].Should().Equal("application/json");
    }

    [TestMethod]
    public void ShouldConvertArraysToRepeatedElementsAndOmitEmptyArrays()
    {
        var test = new ConversionDocument(DefaultConfig).AsTestDocument();
        test.Context.Request.Body.Content =
            """{"root":{"person":[{"name":"A"},{"name":"B"}],"single":["one"],"empty":[],"object":{}}}""";

        test.RunInbound();

        test.Context.Request.Body.Content.Should().Be(
            "<root><person><name>A</name></person><person><name>B</name></person><single>one</single><object /></root>");
    }

    [TestMethod]
    [DataRow("""{"root":null}""", "<root />")]
    [DataRow("""{"root":{}}""", "<root />")]
    [DataRow("""{"root":""}""", "<root></root>")]
    [DataRow("""{"root":false}""", "<root>false</root>")]
    [DataRow("""{"root":123.45}""", "<root>123.45</root>")]
    [DataRow("""{"root":[{"child":"one"}]}""", "<root><child>one</child></root>")]
    [DataRow("""{"root":"a & b < c"}""", "<root>a &amp; b &lt; c</root>")]
    public void ShouldSupportScalarAndSingletonRootValues(string json, string expected)
    {
        var test = new ConversionDocument(DefaultConfig).AsTestDocument();
        test.Context.Request.Body.Content = json;

        test.RunInbound();

        test.Context.Request.Body.Content.Should().Be(expected);
    }

    [TestMethod]
    [DataRow(false, "2024-01-02T03:04:05.1200Z", "2024-01-02T03:04:05.1200Z")]
    [DataRow(true, "2024-01-02T03:04:05.1200Z", "2024-01-02T03:04:05.12Z")]
    [DataRow(null, "2024-01-02T03:04:05.1200Z", "2024-01-02T03:04:05.12Z")]
    [DataRow(true, "2024-01-02T03:04:05.1200+05:30", "2024-01-02T03:04:05.12+05:30")]
    [DataRow(true, "not a date", "not a date")]
    [DataRow(true, "/Date(0)/", "1970-01-01T00:00:00Z")]
    public void ShouldRespectDateParsingIncludingItsDefault(bool? parseDate, string input, string expected)
    {
        var test = new ConversionDocument(DefaultConfig with { ParseDate = parseDate }).AsTestDocument();
        test.Context.Request.Body.Content = JsonSerializer.Serialize(new { root = new { date = input } });

        test.RunInbound();

        test.Context.Request.Body.Content.Should().Be($"<root><date>{expected}</date></root>");
    }

    [TestMethod]
    public void ShouldSupportConfiguredNamespacesAndAttributeBlocks()
    {
        var test = new ConversionDocument(DefaultConfig with
        {
            NamespaceSeparator = ':',
            NamespacePrefix = "xmlns",
            AttributeBlockName = "#attrs",
        }).AsTestDocument();
        test.Context.Request.Body.Content =
            """{"soap:Envelope":{"soap:Body":{"v1:Query":{"#attrs":{"name":"test","v1:enabled":true},"v1:Item":{"name":"dummy"}}},"xmlns:soap":"urn:soap","xmlns:v1":"urn:query"}}""";

        test.RunInbound();

        test.Context.Request.Body.Content.Should().Be(
            """<soap:Envelope xmlns:soap="urn:soap" xmlns:v1="urn:query"><soap:Body><v1:Query name="test" v1:enabled="true"><v1:Item><name>dummy</name></v1:Item></v1:Query></soap:Body></soap:Envelope>""");
    }

    [TestMethod]
    public void ShouldUseTheDefaultNamespaceSeparatorWithoutChangingOrdinaryUnderscores()
    {
        var test = new ConversionDocument(DefaultConfig with { NamespacePrefix = "xmlns" }).AsTestDocument();
        test.Context.Request.Body.Content =
            """{"n_root":{"xmlns_n":"urn:sample","n_item":"value","first_name":"Ada"}}""";

        test.RunInbound();

        test.Context.Request.Body.Content.Should().Be(
            """<n:root xmlns:n="urn:sample"><n:item>value</n:item><first_name>Ada</first_name></n:root>""");
    }

    [TestMethod]
    public void ShouldRespectDefaultNamespacesAttributeNamespacesAndNestedScope()
    {
        var test = new ConversionDocument(DefaultConfig with
        {
            NamespacePrefix = "xmlns",
            AttributeBlockName = "attributes",
        }).AsTestDocument();
        test.Context.Request.Body.Content =
            """{"root":{"xmlns":"urn:outer","xmlns_p":"urn:attribute","attributes":{"id":1,"p_flag":true,"xml:lang":"en"},"child":{"xmlns":"urn:inner","value":"inside"},"plain":{"xmlns":"","value":"outside"}}}""";

        test.RunInbound();

        test.Context.Request.Body.Content.Should().Be(
            """<root xmlns="urn:outer" xmlns:p="urn:attribute" id="1" p:flag="true" xml:lang="en"><child xmlns="urn:inner"><value>inside</value></child><plain xmlns=""><value>outside</value></plain></root>""");
    }

    [TestMethod]
    public void ShouldSupportDirectXmlAttributesTextAndCdata()
    {
        var test = new ConversionDocument(DefaultConfig).AsTestDocument();
        test.Context.Request.Body.Content =
            """{"p:root":{"@xmlns:p":"urn:direct","@id":"7","p:item":{"@code":"A","#text":"hello"},"data":{"#cdata-section":"a < b"}}}""";

        test.RunInbound();

        test.Context.Request.Body.Content.Should().Be(
            """<p:root xmlns:p="urn:direct" id="7"><p:item code="A">hello</p:item><data><![CDATA[a < b]]></data></p:root>""");
    }

    [TestMethod]
    public void ShouldSupportAnOptionalDirectXmlDeclaration()
    {
        var test = new ConversionDocument(DefaultConfig).AsTestDocument();
        test.Context.Request.Body.Content =
            """{"?xml":{"@version":"1.0","@encoding":"utf-8","@standalone":"yes"},"root":{"child":"value"}}""";

        test.RunInbound();

        test.Context.Request.Body.Content.Should().Be(
            """<?xml version="1.0" encoding="utf-8" standalone="yes"?><root><child>value</child></root>""");
    }

    [TestMethod]
    [DataRow("application/json", true)]
    [DataRow("text/json; charset=utf-8", true)]
    [DataRow("APPLICATION/PROBLEM+JSON; CHARSET=UTF-8", true)]
    [DataRow("application/xml", false)]
    [DataRow("application/jsonish", false)]
    [DataRow("text/plain; profile=json", false)]
    [DataRow("application/example+jsonish", false)]
    [DataRow("", false)]
    [DataRow(null, false)]
    public void ShouldGateOnJsonMediaTypes(string? contentType, bool shouldConvert)
    {
        var test = new ConversionDocument(DefaultConfig with { Apply = "content-type-json" }).AsTestDocument();
        var message = test.Context.Response;
        message.Body.Content = shouldConvert ? """{"root":"text"}""" : "not JSON";
        if (contentType is not null)
        {
            message.Headers["Content-Type"] = [contentType];
        }

        test.RunOutbound();

        if (shouldConvert)
        {
            message.Body.Content.Should().Be("<root>text</root>");
            message.Headers["Content-Type"].Should().Equal("application/xml");
        }
        else
        {
            message.Body.Content.Should().Be("not JSON");
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
        var test = new ConversionDocument(DefaultConfig with { Apply = "content-type-json" }).AsTestDocument();
        var message = Message(test, section);
        var other = OtherMessage(test, section);
        message.Body.Content = """{"root":"value"}""";
        message.Headers["content-type"] = ["application/json"];
        other.Headers["Content-Type"] = ["application/xml"];

        Run(test, section);

        message.Body.Content.Should().Be("<root>value</root>");
        message.Headers.Keys.Count(key => key.Equals("Content-Type", StringComparison.OrdinalIgnoreCase))
            .Should().Be(1);
        message.Headers["Content-Type"].Should().Equal("application/xml");
        other.Headers["Content-Type"].Should().Equal("application/xml");
    }

    [TestMethod]
    public void AlwaysShouldIgnoreSourceContentTypeAndAnUnconsideredAcceptHeader()
    {
        var test = new ConversionDocument(DefaultConfig).AsTestDocument();
        test.Context.Request.Body.Content = """{"root":"value"}""";
        test.Context.Request.Headers["Content-Type"] = ["text/plain"];
        test.Context.Request.Headers["Accept"] = ["not a media type"];

        test.RunInbound();

        test.Context.Request.Body.Content.Should().Be("<root>value</root>");
    }

    [TestMethod]
    [DataRow(null, false)]
    [DataRow("", false)]
    [DataRow("application/json", false)]
    [DataRow("*/*", false)]
    [DataRow("application/*", false)]
    [DataRow("application/xml", true)]
    [DataRow("text/xml; q=0.5", true)]
    [DataRow("APPLICATION/SOAP+XML; Q=0.2", true)]
    [DataRow("text/plain, application/xml; q=0.1", true)]
    [DataRow("application/xml; q=0", false)]
    [DataRow("application/xml; q=0, */*; q=1", false)]
    public void ShouldConsiderRequestAcceptByDefault(string? accept, bool shouldConvert)
    {
        var test = new ConversionDocument(DefaultConfig with { ConsiderAcceptHeader = null }).AsTestDocument();
        test.Context.Response.Body.Content = shouldConvert ? """{"root":"text"}""" : "not JSON";
        test.Context.Response.Headers["Content-Type"] = ["application/json"];
        test.Context.Response.Headers["Accept"] = ["application/xml"];
        if (accept is not null)
        {
            test.Context.Request.Headers["Accept"] = [accept];
        }

        test.RunOutbound();

        test.Context.Response.Body.Content.Should().Be(shouldConvert ? "<root>text</root>" : "not JSON");
        test.Context.Response.Headers["Content-Type"].Should().Equal(shouldConvert ? "application/xml" : "application/json");
    }

    [TestMethod]
    public void AcceptGateShouldReadAllHeaderValuesCaseInsensitively()
    {
        var test = new ConversionDocument(DefaultConfig with { ConsiderAcceptHeader = true }).AsTestDocument();
        test.Context.Request.Body.Content = """{"root":"text"}""";
        test.Context.Request.Headers["aCcEpT"] = ["text/plain; q=1", "application/xml; q=0.2"];

        test.RunInbound();

        test.Context.Request.Body.Content.Should().Be("<root>text</root>");
        test.Context.Request.Headers["aCcEpT"].Should().Equal("text/plain; q=1", "application/xml; q=0.2");
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow(" \r\n\t ")]
    public void ShouldFailExplicitlyForAnEmptySelectedBody(string? body)
    {
        var test = new ConversionDocument(DefaultConfig).AsTestDocument();
        test.Context.Request.Body.Content = body;
        test.Context.Request.Headers["Content-Type"] = ["application/json"];

        var act = () => test.RunInbound();

        var exception = act.Should().Throw<PolicyException>().Which;
        exception.Policy.Should().Be("JsonToXml");
        exception.Section.Should().Be(nameof(IInboundContext));
        exception.InnerException.Should().BeOfType<InvalidOperationException>();
        test.Context.Request.Body.Content.Should().Be(body);
        test.Context.Request.Headers["Content-Type"].Should().Equal("application/json");
    }

    [TestMethod]
    [DataRow("not JSON")]
    [DataRow("{")]
    [DataRow("""{"root":1,}""")]
    [DataRow("""{"root":NaN}""")]
    [DataRow("""{/*comment*/"root":1}""")]
    [DataRow("""{"root":1}{"other":2}""")]
    public void ShouldFailExplicitlyForMalformedJson(string body)
    {
        var test = new ConversionDocument(DefaultConfig).AsTestDocument();
        test.Context.Request.Body.Content = body;
        test.Context.Request.Headers["Content-Type"] = ["application/json"];

        var act = () => test.RunInbound();

        var exception = act.Should().Throw<PolicyException>().Which;
        exception.Policy.Should().Be("JsonToXml");
        exception.InnerException.Should().BeAssignableTo<JsonException>();
        test.Context.Request.Body.Content.Should().Be(body);
        test.Context.Request.Headers["Content-Type"].Should().Equal("application/json");
    }

    [TestMethod]
    [DataRow("null")]
    [DataRow("1")]
    [DataRow("\"text\"")]
    [DataRow("[]")]
    [DataRow("{}")]
    [DataRow("""{"first":1,"second":2}""")]
    [DataRow("""{"root":[]}""")]
    [DataRow("""{"root":[1,2]}""")]
    [DataRow("""{"root":{"item":[[1,2]]}}""")]
    [DataRow("""{"root":{"id":1,"id":2}}""")]
    [DataRow("""{"root":{"#attrs":[]}}""")]
    [DataRow("""{"root":{"#attrs":{"id":{}}}}""")]
    [DataRow("""{"root":{"@id":"one","#attrs":{"id":"two"}}}""")]
    [DataRow("""{"p:root":{"child":1}}""")]
    [DataRow("""{"1invalid":"value"}""")]
    [DataRow("""{"root":"\u0000"}""")]
    [DataRow("""{"root":{"xmlns:p":""}}""")]
    public void ShouldRejectInputsThatCannotBeRepresentedWithoutLosingData(string body)
    {
        var test = new ConversionDocument(DefaultConfig with
        {
            NamespaceSeparator = ':',
            NamespacePrefix = "xmlns",
            AttributeBlockName = "#attrs",
        }).AsTestDocument();
        test.Context.Request.Body.Content = body;
        test.Context.Request.Headers["Content-Type"] = ["application/json"];

        var act = () => test.RunInbound();

        var exception = act.Should().Throw<PolicyException>().Which;
        exception.Policy.Should().Be("JsonToXml");
        exception.InnerException.Should().NotBeNull();
        test.Context.Request.Body.Content.Should().Be(body);
        test.Context.Request.Headers["Content-Type"].Should().Equal("application/json");
    }

    [TestMethod]
    [DataRow("apply", "")]
    [DataRow("apply", "content-type-xml")]
    [DataRow("apply", null)]
    [DataRow("prefix", "")]
    [DataRow("attributes", "")]
    [DataRow("separator", "\0")]
    [DataRow("separator", " ")]
    public void ShouldRejectInvalidOptionsEvenWhenAcceptWouldSkipConversion(string property, string? value)
    {
        var config = DefaultConfig with { ConsiderAcceptHeader = true };
        config = property switch
        {
            "apply" => config with { Apply = value! },
            "prefix" => config with { NamespacePrefix = value },
            "attributes" => config with { AttributeBlockName = value },
            "separator" => config with { NamespaceSeparator = value![0] },
            _ => throw new ArgumentOutOfRangeException(nameof(property)),
        };
        var test = new ConversionDocument(config).AsTestDocument();
        test.Context.Request.Body.Content = """{"root":1}""";

        var act = () => test.RunInbound();

        act.Should().Throw<PolicyException>().Which.InnerException.Should().BeOfType<ArgumentException>();
        test.Context.Request.Body.Content.Should().Be("""{"root":1}""");
    }

    [TestMethod]
    [DataRow("Content-Type")]
    [DataRow("Accept")]
    public void ShouldRejectMalformedGatingHeaders(string header)
    {
        var test = new ConversionDocument(DefaultConfig with
        {
            Apply = "content-type-json",
            ConsiderAcceptHeader = true,
        }).AsTestDocument();
        test.Context.Request.Body.Content = """{"root":1}""";
        test.Context.Request.Headers["Content-Type"] = ["application/json"];
        test.Context.Request.Headers["Accept"] = ["application/xml"];
        test.Context.Request.Headers[header] = ["not a media type"];

        var act = () => test.RunInbound();

        act.Should().Throw<PolicyException>().Which.InnerException.Should().BeOfType<FormatException>();
        test.Context.Request.Body.Content.Should().Be("""{"root":1}""");
    }

    [TestMethod]
    [DataRow("inbound")]
    [DataRow("backend")]
    [DataRow("outbound")]
    [DataRow("on-error")]
    public void ShouldEvaluateAllAuthoredExpressionOptions(string section)
    {
        var test = new ExpressionDocument().AsTestDocument();
        test.Context.Variables["apply"] = "content-type-json";
        test.Context.Variables["accept"] = false;
        test.Context.Variables["date"] = false;
        test.Context.Variables["separator"] = ':';
        test.Context.Variables["prefix"] = "xmlns";
        test.Context.Variables["attributes"] = "#attrs";
        var message = Message(test, section);
        message.Body.Content =
            """{"p:root":{"xmlns:p":"urn:expression","#attrs":{"date":"2024-01-02T03:04:05.1200Z"},"p:child":"value"}}""";
        message.Headers["Content-Type"] = ["application/json"];
        test.Context.Request.Headers["Accept"] = ["text/plain"];

        Run(test, section);

        message.Body.Content.Should().Be(
            """<p:root xmlns:p="urn:expression" date="2024-01-02T03:04:05.1200Z"><p:child>value</p:child></p:root>""");
    }

    [TestMethod]
    [DataRow("inbound")]
    [DataRow("backend")]
    [DataRow("outbound")]
    [DataRow("on-error")]
    public void CallbackShouldOverrideConversionAndValidation(string section)
    {
        var config = DefaultConfig with { Apply = "invalid" };
        var test = new ConversionDocument(config).AsTestDocument();
        var message = Message(test, section);
        message.Body.Content = "not JSON";
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
        test.Context.Request.Body.Content = """{"root":"value"}""";
        var called = false;
        Callback(test, "inbound", (_, _) => called = true, (_, config) => config.Apply == "content-type-json");

        test.RunInbound();

        called.Should().BeFalse();
        test.Context.Request.Body.Content.Should().Be("<root>value</root>");
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
        }, (_, config) => config.Apply == "always");

        test.RunInbound();

        test.Context.Response.StatusCode.Should().Be(422);
        test.Context.Response.Body.Content.Should().Be("conversion failed");
    }

    [TestMethod]
    [DataRow("utf-16")]
    [DataRow("iso-8859-1")]
    public void ShouldRejectNonUtf8DeclarationsWithoutChangingBodyOrHeaders(string encoding)
    {
        var test = new ConversionDocument(DefaultConfig).AsTestDocument();
        var body = DeclaredBody(encoding);
        var headers = new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["Content-Type"] = ["application/json"],
            ["content-type"] = ["application/json; charset=utf-8"],
            ["Content-Length"] = ["999"],
            ["content-length"] = ["123", "456"],
            ["X-Keep"] = ["first", "second"],
        };
        var snapshot = headers.ToDictionary(header => header.Key, header => header.Value.ToArray(), StringComparer.Ordinal);
        test.Context.Request.Body.Content = body;
        test.Context.Request.Headers = headers;

        var act = () => test.RunInbound();

        var exception = act.Should().Throw<PolicyException>().Which;
        exception.Policy.Should().Be("JsonToXml");
        exception.InnerException.Should().BeOfType<ArgumentException>()
            .Which.Message.Should().Contain("UTF-8");
        test.Context.Request.Body.Content.Should().Be(body);
        test.Context.Request.Headers.Should().BeSameAs(headers);
        test.Context.Request.Headers.Keys.Should().Equal(snapshot.Keys);
        test.Context.Request.Headers.Should().BeEquivalentTo(snapshot, options => options.WithStrictOrdering());
    }

    [TestMethod]
    [DataRow("utf-8")]
    [DataRow("UTF-8")]
    public void Utf8DeclarationShouldMatchActualBytesAndContentLength(string encoding)
    {
        var test = new ConversionDocument(DefaultConfig).AsTestDocument();
        test.Context.Request.Body.Content = DeclaredBody(encoding);
        test.Context.Request.Headers["content-length"] = ["999"];

        test.RunInbound();

        var expected = $"<?xml version=\"1.0\" encoding=\"{encoding}\"?><root>caf\u00e9 \U0001F600</root>";
        test.Context.Request.Body.Content.Should().Be(expected);
        var bytes = test.Context.Request.Body.As<byte[]>(true);
        bytes.Should().Equal(Encoding.UTF8.GetBytes(expected));
        bytes.Length.Should().Be(61).And.BeGreaterThan(expected.Length);
        test.Context.Request.Headers.Keys.Where(key => key.Equals("Content-Length", StringComparison.OrdinalIgnoreCase))
            .Should().Equal("Content-Length");
        test.Context.Request.Headers["Content-Length"].Should().Equal("61");
        using var stream = new MemoryStream(bytes);
        XDocument.Load(stream).Root!.Value.Should().Be("caf\u00e9 \U0001F600");
    }

    private static string DeclaredBody(string encoding) => JsonSerializer.Serialize(new Dictionary<string, object>
    {
        ["?xml"] = new Dictionary<string, string>
        {
            ["@version"] = "1.0",
            ["@encoding"] = encoding,
        },
        ["root"] = "caf\u00e9 \U0001F600",
    });

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
        Action<GatewayContext, JsonToXmlConfig> callback,
        Func<GatewayContext, JsonToXmlConfig, bool>? predicate = null)
    {
        predicate ??= (_, _) => true;
        switch (section)
        {
            case "inbound": test.SetupInbound().JsonToXml(predicate).WithCallback(callback); break;
            case "backend": test.SetupBackend().JsonToXml(predicate).WithCallback(callback); break;
            case "outbound": test.SetupOutbound().JsonToXml(predicate).WithCallback(callback); break;
            case "on-error": test.SetupOnError().JsonToXml(predicate).WithCallback(callback); break;
            default: throw new ArgumentOutOfRangeException(nameof(section));
        }
    }
}
