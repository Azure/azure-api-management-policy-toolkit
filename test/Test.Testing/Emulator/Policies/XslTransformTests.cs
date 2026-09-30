// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Globalization;
using System.Text;
using System.Xml.Linq;

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring.Expressions;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Document;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Expressions;

namespace Test.Emulator.Emulator.Policies;

[TestClass]
public class XslTransformTests
{
    private const string ValueStyleSheet = """
        <xsl:stylesheet version="1.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform">
          <xsl:output method="xml" omit-xml-declaration="yes" indent="no" />
          <xsl:template match="/">
            <transformed><xsl:value-of select="/root/value" /></transformed>
          </xsl:template>
        </xsl:stylesheet>
        """;

    private const string ParameterStyleSheet = """
        <xsl:stylesheet version="1.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform">
          <xsl:output method="xml" omit-xml-declaration="yes" indent="no" />
          <xsl:param name="greeting" select="'default'" />
          <xsl:param name="name" select="'fallback'" />
          <xsl:template match="/">
            <result><xsl:value-of select="$greeting" />:<xsl:value-of select="$name" /></result>
          </xsl:template>
        </xsl:stylesheet>
        """;

    private const string TextParameterStyleSheet = """
        <xsl:stylesheet version="1.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform">
          <xsl:output method="text" />
          <xsl:param name="value" select="'default'" />
          <xsl:template match="/"><xsl:value-of select="$value" /></xsl:template>
        </xsl:stylesheet>
        """;

    private const string IdentityStyleSheet = """
        <xsl:stylesheet version="1.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform">
          <xsl:output method="xml" omit-xml-declaration="yes" indent="no" />
          <xsl:template match="@*|node()">
            <xsl:copy><xsl:apply-templates select="@*|node()" /></xsl:copy>
          </xsl:template>
        </xsl:stylesheet>
        """;

    private class XslTransformDocument(XslTransformConfig config) : IDocument
    {
        public void Inbound(IInboundContext context) => context.XslTransform(config);
        public void Outbound(IOutboundContext context) => context.XslTransform(config);
        public void OnError(IOnErrorContext context) => context.XslTransform(config);
        public void Backend(IBackendContext context) { }
    }

    private class RepeatedXslTransformDocument(
        XslTransformConfig first,
        XslTransformConfig second) : IDocument
    {
        public void Inbound(IInboundContext context)
        {
            context.XslTransform(first);
            context.XslTransform(second);
        }

        public void Outbound(IOutboundContext context) { }
        public void OnError(IOnErrorContext context) { }
        public void Backend(IBackendContext context) { }
    }

    private class ExpressionXslTransformDocument : IDocument
    {
        public void Inbound(IInboundContext context)
        {
            context.XslTransform(new XslTransformConfig
            {
                StyleSheet = GetStyleSheet(context.ExpressionContext),
                Parameters =
                [
                    new() { Name = "greeting", Value = GetGreeting(context.ExpressionContext) },
                    new() { Name = "name", Value = GetName(context.ExpressionContext) },
                ],
            });
        }

        public void Outbound(IOutboundContext context) { }
        public void OnError(IOnErrorContext context) { }
        public void Backend(IBackendContext context) { }

        [Expression]
        private static string GetStyleSheet(IExpressionContext context) =>
            (string)context.Variables["stylesheet"];

        [Expression]
        private static string GetGreeting(IExpressionContext context) =>
            (string)context.Variables["greeting"];

        [Expression]
        private static string GetName(IExpressionContext context) =>
            (string)context.Variables["name"];
    }

    [TestMethod]
    [DataRow(nameof(IInboundContext))]
    [DataRow(nameof(IOutboundContext))]
    [DataRow(nameof(IOnErrorContext))]
    public void ShouldTransformOnlyTheBodyForTheAuthoredSection(string section)
    {
        var test = CreateTest();
        var request = test.Context.Request.Body.Content;
        var response = test.Context.Response.Body.Content;
        test.Context.Request.Headers["Content-Type"] = ["application/xml"];
        test.Context.Response.Headers["Content-Type"] = ["application/xml"];
        test.Context.Response.StatusCode = 202;
        test.Context.Variables["unchanged"] = "value";

        RunSection(test, section);

        test.Context.Request.Body.Content.Should().Be(section == nameof(IInboundContext)
            ? "<transformed>request</transformed>"
            : request);
        test.Context.Response.Body.Content.Should().Be(section == nameof(IInboundContext)
            ? response
            : "<transformed>response</transformed>");
        test.Context.Request.Headers["Content-Type"].Should().Equal("application/xml");
        test.Context.Response.Headers["Content-Type"].Should().Equal("application/xml");
        test.Context.Response.StatusCode.Should().Be(202);
        test.Context.Variables["unchanged"].Should().Be("value");
        test.Context.Request.Body.Consumed.Should().BeFalse();
        test.Context.Response.Body.Consumed.Should().BeFalse();
    }

    [TestMethod]
    public void ShouldExecuteTemplatesSortingAndXPath()
    {
        var test = CreateTest("""
            <xsl:stylesheet version="1.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform">
              <xsl:output omit-xml-declaration="yes" indent="no" />
              <xsl:template match="/">
                <ordered>
                  <xsl:apply-templates select="/items/item">
                    <xsl:sort select="@rank" data-type="number" />
                  </xsl:apply-templates>
                </ordered>
              </xsl:template>
              <xsl:template match="item">
                <value rank="{@rank}"><xsl:value-of select="." /></value>
              </xsl:template>
            </xsl:stylesheet>
            """);
        test.Context.Request.Body.Content =
            "<items><item rank=\"2\">second</item><item rank=\"1\">first</item></items>";

        test.RunInbound();

        test.Context.Request.Body.Content.Should().Be(
            "<ordered><value rank=\"1\">first</value><value rank=\"2\">second</value></ordered>");
    }

    [TestMethod]
    public void ShouldPreserveNamespacesAttributesAndEscapedContent()
    {
        var test = CreateTest(IdentityStyleSheet);
        test.Context.Request.Body.Content =
            "<r:root xmlns:r=\"urn:input\" id=\"a&amp;b\"><r:value>&lt;text&gt;</r:value></r:root>";

        test.RunInbound();

        var result = XElement.Parse(test.Context.Request.Body.Content!);
        result.Name.Should().Be(XName.Get("root", "urn:input"));
        result.Attribute("id")!.Value.Should().Be("a&b");
        result.Element(XName.Get("value", "urn:input"))!.Value.Should().Be("<text>");
    }

    [TestMethod]
    public void ShouldPreserveSignificantWhitespace()
    {
        var test = CreateTest(IdentityStyleSheet);
        var input = "<root xml:space=\"preserve\"> before \t<value>  text  </value> after </root>";
        test.Context.Request.Body.Content = input;

        test.RunInbound();

        test.Context.Request.Body.Content.Should().Be(input);
    }

    [TestMethod]
    public void ShouldSupplyMultipleParametersAndEscapeXmlOutput()
    {
        var test = CreateTest(ParameterStyleSheet,
        [
            new() { Name = "greeting", Value = "Hello & <everyone>" },
            new() { Name = "name", Value = "Bob & Alice" },
        ]);

        test.RunInbound();

        test.Context.Request.Body.Content.Should().Be(
            "<result>Hello &amp; &lt;everyone&gt;:Bob &amp; Alice</result>");
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("1 + 1")]
    [DataRow("/root/value")]
    [DataRow("\t leading & <'\" trailing  ")]
    public void ShouldPassParameterValuesAsLiteralStrings(string value)
    {
        var test = CreateTest(TextParameterStyleSheet, [new() { Name = "value", Value = value }]);

        test.RunInbound();

        test.Context.Request.Body.Content.Should().Be(value);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void ShouldUseStylesheetDefaultsWhenParametersAreAbsent(bool emptyArray)
    {
        var test = CreateTest(ParameterStyleSheet, emptyArray ? [] : null);

        test.RunInbound();

        test.Context.Request.Body.Content.Should().Be("<result>default:fallback</result>");
    }

    [TestMethod]
    public void ShouldKeepDefaultsForParametersNotSupplied()
    {
        var test = CreateTest(ParameterStyleSheet, [new() { Name = "greeting", Value = "Hello" }]);

        test.RunInbound();

        test.Context.Request.Body.Content.Should().Be("<result>Hello:fallback</result>");
    }

    [TestMethod]
    public void ShouldUseExpressionEvaluatedStylesheetAndParameterValues()
    {
        var test = new ExpressionXslTransformDocument().AsTestDocument();
        test.Context.Request.Body.Content = "<root />";
        test.Context.Variables["stylesheet"] = ParameterStyleSheet;
        test.Context.Variables["greeting"] = "Welcome & hello";
        test.Context.Variables["name"] = "expression-value";

        test.RunInbound();

        test.Context.Request.Body.Content.Should().Be(
            "<result>Welcome &amp; hello:expression-value</result>");
    }

    [TestMethod]
    [DataRow("utf-8")]
    [DataRow("UTF-8")]
    public void ShouldRoundTripUtf8XmlBytesWithAMatchingDeclaration(string encoding)
    {
        var test = CreateTest($$"""
            <xsl:stylesheet version="1.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform">
              <xsl:output method="xml" encoding="{{encoding}}" omit-xml-declaration="no" indent="no" />
              <xsl:template match="/"><result>caf&#xE9; &#x96EA; &#x1F600;</result></xsl:template>
            </xsl:stylesheet>
            """);

        test.RunInbound();

        var expected = "<?xml version=\"1.0\" encoding=\"utf-8\"?><result>caf\u00E9 \u96EA \U0001F600</result>";
        test.Context.Request.Body.Content.Should().Be(expected);
        var bytes = test.Context.Request.Body.As<byte[]>(preserveContent: true);
        bytes.Should().Equal(Encoding.UTF8.GetBytes(expected));
        using var stream = new MemoryStream(bytes);
        var document = XDocument.Load(stream);
        document.Declaration!.Encoding.Should().Be("utf-8");
        document.Root!.Name.Should().Be(XName.Get("result"));
        document.Root.Value.Should().Be("caf\u00E9 \u96EA \U0001F600");
    }

    [TestMethod]
    [DataRow("utf-16", nameof(IInboundContext))]
    [DataRow("iso-8859-1", nameof(IInboundContext))]
    [DataRow("utf-16", nameof(IOutboundContext))]
    [DataRow("iso-8859-1", nameof(IOutboundContext))]
    [DataRow("utf-16", nameof(IOnErrorContext))]
    [DataRow("iso-8859-1", nameof(IOnErrorContext))]
    public void ShouldRejectNonUtf8OutputBeforeChangingBodiesOrHeaders(string encoding, string section)
    {
        var test = CreateTest($$"""
            <xsl:stylesheet version="1.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform">
              <xsl:output method="xml" encoding="{{encoding}}" omit-xml-declaration="no" />
              <xsl:template match="/"><result>&#x96EA;</result></xsl:template>
            </xsl:stylesheet>
            """);

        var exception = AssertFailure(test, section);

        exception.InnerException.Should().BeOfType<NotSupportedException>().Which.Message.Should()
            .Contain("UTF-8").And.Contain(encoding);
    }

    [TestMethod]
    [DataRow("utf-16")]
    [DataRow("iso-8859-1")]
    public void ShouldRejectNonUtf8OutputEvenWithoutAnXmlDeclaration(string encoding)
    {
        var test = CreateTest($$"""
            <xsl:stylesheet version="1.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform">
              <xsl:output method="xml" encoding="{{encoding}}" omit-xml-declaration="yes" />
              <xsl:template match="/"><result /></xsl:template>
            </xsl:stylesheet>
            """);

        AssertFailure(test).InnerException.Should().BeOfType<NotSupportedException>();
    }

    [TestMethod]
    [DataRow("utf-16", "text")]
    [DataRow("iso-8859-1", "text")]
    [DataRow("utf-16", "html")]
    [DataRow("iso-8859-1", "html")]
    public void ShouldRejectNonUtf8TextAndHtmlOutput(string encoding, string method)
    {
        var test = CreateTest($$"""
            <xsl:stylesheet version="1.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform">
              <xsl:output method="{{method}}" encoding="{{encoding}}" />
              <xsl:template match="/"><result>caf&#xE9;</result></xsl:template>
            </xsl:stylesheet>
            """);

        AssertFailure(test).InnerException.Should().BeOfType<NotSupportedException>();
    }

    [TestMethod]
    public void ShouldPreserveUnicodeAndLiteralMarkupInTextOutput()
    {
        var test = CreateTest("""
            <xsl:stylesheet version="1.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform">
              <xsl:output method="text" encoding="utf-8" />
              <xsl:template match="/"><xsl:text>caf&#xE9; &#x1F600; &amp; &lt;text&gt;</xsl:text></xsl:template>
            </xsl:stylesheet>
            """);

        test.RunInbound();

        var expected = "caf\u00E9 \U0001F600 & <text>";
        test.Context.Request.Body.Content.Should().Be(expected);
        test.Context.Request.Body.As<byte[]>(preserveContent: true).Should()
            .Equal(Encoding.UTF8.GetBytes(expected));
        test.Context.Request.Headers["Content-Length"].Should()
            .Equal(Encoding.UTF8.GetByteCount(expected).ToString(CultureInfo.InvariantCulture));
    }

    [TestMethod]
    [DataRow("Content-Length", nameof(IInboundContext))]
    [DataRow("content-length", nameof(IInboundContext))]
    [DataRow("Content-Length", nameof(IOutboundContext))]
    [DataRow("content-length", nameof(IOutboundContext))]
    [DataRow("Content-Length", nameof(IOnErrorContext))]
    [DataRow("content-length", nameof(IOnErrorContext))]
    public void ShouldUpdateExistingContentLengthUsingUtf8Bytes(string headerName, string section)
    {
        var test = CreateTest("""
            <xsl:stylesheet version="1.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform">
              <xsl:output method="text" encoding="utf-8" />
              <xsl:template match="/"><xsl:text>caf&#xE9; &#x96EA; &#x1F600;</xsl:text></xsl:template>
            </xsl:stylesheet>
            """);
        var message = GetMessageForSection(test, section);
        var otherMessage = section == nameof(IInboundContext)
            ? GetMessageForSection(test, nameof(IOutboundContext))
            : GetMessageForSection(test, nameof(IInboundContext));
        message.Headers.Remove("Content-Length");
        message.Headers.Remove("content-length");
        message.Headers[headerName] = ["999", "1000"];
        var headers = message.Headers.ToDictionary(header => header.Key, header => header.Value.ToArray());
        var otherBody = otherMessage.Body.Content;
        var otherHeaders = otherMessage.Headers.ToDictionary(header => header.Key, header => header.Value.ToArray());
        var expected = "caf\u00E9 \u96EA \U0001F600";
        var byteCount = Encoding.UTF8.GetByteCount(expected);
        byteCount.Should().BeGreaterThan(expected.Length);
        headers[headerName] = [byteCount.ToString(CultureInfo.InvariantCulture)];

        RunSection(test, section);

        message.Body.Content.Should().Be(expected);
        message.Headers.Should().BeEquivalentTo(headers);
        otherMessage.Body.Content.Should().Be(otherBody);
        otherMessage.Headers.Should().BeEquivalentTo(otherHeaders);
    }

    [TestMethod]
    public void ShouldUpdateAllExistingContentLengthSpellings()
    {
        var test = CreateTest();
        test.Context.Request.Headers["content-length"] = ["999"];

        test.RunInbound();

        var expected = Encoding.UTF8.GetByteCount("<transformed>request</transformed>")
            .ToString(CultureInfo.InvariantCulture);
        test.Context.Request.Headers["Content-Length"].Should().Equal(expected);
        test.Context.Request.Headers["content-length"].Should().Equal(expected);
    }

    [TestMethod]
    [DataRow(nameof(IInboundContext))]
    [DataRow(nameof(IOutboundContext))]
    [DataRow(nameof(IOnErrorContext))]
    public void ShouldNotAddContentLengthWhenItWasAbsent(string section)
    {
        var test = CreateTest();
        var message = GetMessageForSection(test, section);
        message.Headers.Remove("Content-Length");
        message.Headers.Remove("content-length");
        var headers = message.Headers.ToDictionary(header => header.Key, header => header.Value.ToArray());

        RunSection(test, section);

        message.Body.Content.Should().Be(section == nameof(IInboundContext)
            ? "<transformed>request</transformed>"
            : "<transformed>response</transformed>");
        message.Headers.Should().BeEquivalentTo(headers);
    }

    [TestMethod]
    public void ShouldPreserveAnIntentionalLeadingBomCharacterInTextOutput()
    {
        var test = CreateTest("""
            <xsl:stylesheet version="1.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform">
              <xsl:output method="text" encoding="utf-8" />
              <xsl:template match="/"><xsl:text>&#xFEFF;text</xsl:text></xsl:template>
            </xsl:stylesheet>
            """);

        test.RunInbound();

        test.Context.Request.Body.Content.Should().Be("\uFEFFtext");
    }

    [TestMethod]
    public void ShouldHonorHtmlOutputSerialization()
    {
        var test = CreateTest("""
            <xsl:stylesheet version="1.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform">
              <xsl:output method="html" indent="no" />
              <xsl:template match="/"><html><body><br /><p>Tom &amp; Jerry</p></body></html></xsl:template>
            </xsl:stylesheet>
            """);

        test.RunInbound();

        test.Context.Request.Body.Content.Should().Be(
            "<html><body><br><p>Tom &amp; Jerry</p></body></html>");
    }

    [TestMethod]
    public void ShouldPreserveXmlFragmentOutputWithoutReparsingIt()
    {
        var test = CreateTest("""
            <xsl:stylesheet version="1.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform">
              <xsl:output method="xml" omit-xml-declaration="yes" indent="no" />
              <xsl:template match="/"><first />tail<second /></xsl:template>
            </xsl:stylesheet>
            """);

        test.RunInbound();

        test.Context.Request.Body.Content.Should().Be("<first />tail<second />");
    }

    [TestMethod]
    public void ShouldAllowAnEmptyTransformationResult()
    {
        var test = CreateTest("""
            <xsl:stylesheet version="1.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform">
              <xsl:output omit-xml-declaration="yes" />
              <xsl:template match="/" />
            </xsl:stylesheet>
            """);

        test.RunInbound();

        test.Context.Request.Body.Content.Should().BeEmpty();
        test.Context.Request.Headers["Content-Length"].Should().Equal("0");
    }

    [TestMethod]
    public void ShouldTransformAnEmptyXmlElement()
    {
        var test = CreateTest();
        test.Context.Request.Body.Content = "<root />";

        test.RunInbound();

        var document = XDocument.Parse(test.Context.Request.Body.Content!, LoadOptions.PreserveWhitespace);
        var result = document.Nodes().Should().ContainSingle().Which
            .Should().BeOfType<XElement>().Which;
        result.Name.Should().Be(XName.Get("transformed"));
        result.Attributes().Should().BeEmpty();
        result.Nodes().Should().BeEmpty();
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("   ")]
    [DataRow("not XML")]
    [DataRow("<root>")]
    [DataRow("<root /><other />")]
    [DataRow("<root>\u0001</root>")]
    public void ShouldRejectMissingOrMalformedXmlWithoutChangingTheBody(string? input)
    {
        var test = CreateTest();
        test.Context.Request.Body.Content = input;

        AssertFailure(test);
    }

    [TestMethod]
    [DataRow(nameof(IInboundContext))]
    [DataRow(nameof(IOutboundContext))]
    [DataRow(nameof(IOnErrorContext))]
    public void ShouldReportFailuresForTheCorrectSectionAndPreserveBothBodies(string section)
    {
        var test = CreateTest();
        if (section == nameof(IInboundContext))
        {
            test.Context.Request.Body.Content = "<broken>";
        }
        else
        {
            test.Context.Response.Body.Content = "<broken>";
        }

        AssertFailure(test, section);
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("   ")]
    [DataRow("<broken")]
    [DataRow("<not-a-stylesheet />")]
    [DataRow("""
        <xsl:stylesheet version="1.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform">
          <xsl:template match="/"><xsl:value-of select="[" /></xsl:template>
        </xsl:stylesheet>
        """)]
    public void ShouldRejectMissingOrMalformedStylesheets(string? stylesheet)
    {
        AssertFailure(CreateTest(stylesheet!));
    }

    [TestMethod]
    public void ShouldRejectANullConfiguration()
    {
        var test = new XslTransformDocument(null!).AsTestDocument();
        test.Context.Request.Body.Content = "<root />";

        AssertFailure(test).InnerException.Should().BeAssignableTo<ArgumentException>();
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow(" ")]
    [DataRow("prefix:value")]
    [DataRow("bad name")]
    [DataRow("1name")]
    public void ShouldRejectInvalidParameterNames(string? name)
    {
        AssertFailure(CreateTest(TextParameterStyleSheet, [new() { Name = name!, Value = "value" }]));
    }

    [TestMethod]
    [DataRow("_value")]
    [DataRow("valid-name")]
    [DataRow("value.name")]
    [DataRow("\u00E9")]
    public void ShouldAcceptValidXmlParameterNames(string name)
    {
        var test = CreateTest($$"""
            <xsl:stylesheet version="1.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform">
              <xsl:output method="text" />
              <xsl:param name="{{name}}" />
              <xsl:template match="/"><xsl:value-of select="${{name}}" /></xsl:template>
            </xsl:stylesheet>
            """, [new() { Name = name, Value = "supplied" }]);

        test.RunInbound();

        test.Context.Request.Body.Content.Should().Be("supplied");
    }

    [TestMethod]
    public void ShouldRejectNullParameterValues()
    {
        AssertFailure(CreateTest(TextParameterStyleSheet, [new() { Name = "value", Value = null! }]));
    }

    [TestMethod]
    public void ShouldRejectNullParameterEntries()
    {
        AssertFailure(CreateTest(TextParameterStyleSheet, [null!]));
    }

    [TestMethod]
    public void ShouldRejectDuplicateParameterNames()
    {
        AssertFailure(CreateTest(TextParameterStyleSheet,
        [
            new() { Name = "value", Value = "first" },
            new() { Name = "value", Value = "second" },
        ]));
    }

    [TestMethod]
    [DataRow("<!DOCTYPE root [<!ENTITY value 'expanded'>]><root><value>&value;</value></root>")]
    [DataRow("<!DOCTYPE root SYSTEM 'https://xslt.invalid/input.dtd'><root />")]
    public void ShouldProhibitInputDtds(string input)
    {
        var test = CreateTest();
        test.Context.Request.Body.Content = input;

        AssertFailure(test);
    }

    [TestMethod]
    public void ShouldProhibitStylesheetDtds()
    {
        AssertFailure(CreateTest("""
            <!DOCTYPE xsl:stylesheet [<!ENTITY value "expanded">]>
            <xsl:stylesheet version="1.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform">
              <xsl:template match="/"><result>&value;</result></xsl:template>
            </xsl:stylesheet>
            """));
    }

    [TestMethod]
    [DataRow("include")]
    [DataRow("import")]
    public void ShouldRejectExternalStylesheetResources(string directive)
    {
        AssertFailure(CreateTest($$"""
            <xsl:stylesheet version="1.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform">
              <xsl:{{directive}} href="https://xslt.invalid/external.xsl" />
              <xsl:template match="/"><result /></xsl:template>
            </xsl:stylesheet>
            """));
    }

    [TestMethod]
    public void ShouldDisableTheDocumentFunction()
    {
        AssertFailure(CreateTest("""
            <xsl:stylesheet version="1.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform">
              <xsl:template match="/">
                <result><xsl:value-of select="document('https://xslt.invalid/data.xml')/root/value" /></result>
              </xsl:template>
            </xsl:stylesheet>
            """));
    }

    [TestMethod]
    public void ShouldDisableEmbeddedScripts()
    {
        AssertFailure(CreateTest("""
            <xsl:stylesheet version="1.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform"
                xmlns:msxsl="urn:schemas-microsoft-com:xslt" xmlns:user="urn:xslt-test">
              <msxsl:script language="C#" implements-prefix="user">
                <![CDATA[public string Echo() { return "unsafe"; }]]>
              </msxsl:script>
              <xsl:template match="/"><result><xsl:value-of select="user:Echo()" /></result></xsl:template>
            </xsl:stylesheet>
            """));
    }

    [TestMethod]
    public void ShouldPreserveTheOriginalBodyWhenTransformationFailsAfterProducingOutput()
    {
        AssertFailure(CreateTest("""
            <xsl:stylesheet version="1.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform">
              <xsl:template match="/">
                <partial />
                <xsl:message terminate="yes">Transformation stopped.</xsl:message>
              </xsl:template>
            </xsl:stylesheet>
            """));
    }

    [TestMethod]
    [DataRow(nameof(IInboundContext))]
    [DataRow(nameof(IOutboundContext))]
    [DataRow(nameof(IOnErrorContext))]
    public void ShouldLetCallbacksOverrideDefaultTransformationAndValidation(string section)
    {
        var test = CreateTest("invalid stylesheet");
        test.Context.Request.Body.Content = "invalid request XML";
        test.Context.Response.Body.Content = "invalid response XML";
        var callbackCount = 0;
        SetupForSection(test, section).WithCallback((context, config) =>
        {
            callbackCount++;
            config.StyleSheet.Should().Be("invalid stylesheet");
            context.Variables["callback"] = section;
        });

        RunSection(test, section);

        callbackCount.Should().Be(1);
        test.Context.Variables["callback"].Should().Be(section);
        test.Context.Request.Body.Content.Should().Be("invalid request XML");
        test.Context.Response.Body.Content.Should().Be("invalid response XML");
    }

    [TestMethod]
    [DataRow(nameof(IInboundContext))]
    [DataRow(nameof(IOutboundContext))]
    [DataRow(nameof(IOnErrorContext))]
    public void ShouldUseDefaultTransformationWhenCallbackPredicateDoesNotMatch(string section)
    {
        var test = CreateTest();
        var callbackExecuted = false;
        SetupForSection(test, section, (_, _) => false).WithCallback((_, _) => callbackExecuted = true);

        RunSection(test, section);

        callbackExecuted.Should().BeFalse();
        (section == nameof(IInboundContext)
            ? test.Context.Request.Body.Content
            : test.Context.Response.Body.Content).Should().Be(section == nameof(IInboundContext)
            ? "<transformed>request</transformed>"
            : "<transformed>response</transformed>");
    }

    [TestMethod]
    public void ShouldSelectPredicateCallbacksAndThenTransformTheUpdatedBody()
    {
        var first = new XslTransformConfig { StyleSheet = "callback-only" };
        var second = new XslTransformConfig { StyleSheet = ValueStyleSheet };
        var test = new RepeatedXslTransformDocument(first, second).AsTestDocument();
        test.Context.Request.Body.Content = "<root><value>original</value></root>";
        var callbackCount = 0;
        test.SetupInbound().XslTransform((_, config) => config.StyleSheet == "callback-only")
            .WithCallback((context, config) =>
            {
                callbackCount++;
                config.Should().BeSameAs(first);
                context.Request.Body.Content = "<root><value>callback</value></root>";
            });

        test.RunInbound();

        callbackCount.Should().Be(1);
        test.Context.Request.Body.Content.Should().Be("<transformed>callback</transformed>");
    }

    [TestMethod]
    public void ShouldKeepCallbackRegistrationsLocalToTheirSection()
    {
        var test = CreateTest();
        var callbackExecuted = false;
        test.SetupInbound().XslTransform().WithCallback((_, _) => callbackExecuted = true);

        test.RunOutbound();

        callbackExecuted.Should().BeFalse();
        test.Context.Response.Body.Content.Should().Be("<transformed>response</transformed>");
        test.Context.Request.Body.Content.Should().Be("<root><value>request</value></root>");
    }

    [TestMethod]
    [DataRow(nameof(IInboundContext))]
    [DataRow(nameof(IOutboundContext))]
    [DataRow(nameof(IOnErrorContext))]
    public void ShouldSurfaceCallbackFailuresWithPolicyAndSectionInformation(string section)
    {
        var test = CreateTest();
        var failure = new InvalidOperationException("Injected XSLT failure");
        SetupForSection(test, section).WithCallback((_, _) => throw failure);

        AssertFailure(test, section).InnerException.Should().BeSameAs(failure);
    }

    private static TestDocument CreateTest(
        string stylesheet = ValueStyleSheet,
        XslTransformParameter[]? parameters = null)
    {
        var test = new XslTransformDocument(new XslTransformConfig
        {
            StyleSheet = stylesheet,
            Parameters = parameters,
        }).AsTestDocument();
        var request = "<root><value>request</value></root>";
        var response = "<root><value>response</value></root>";
        test.Context.Request.Body.Content = request;
        test.Context.Request.Headers["Content-Type"] = ["application/xml"];
        test.Context.Request.Headers["Content-Length"] =
            [Encoding.UTF8.GetByteCount(request).ToString(CultureInfo.InvariantCulture)];
        test.Context.Request.Headers["X-Preserved"] = ["request", "unchanged"];
        test.Context.Response.Body.Content = response;
        test.Context.Response.Headers["Content-Type"] = ["application/xml"];
        test.Context.Response.Headers["content-length"] =
            [Encoding.UTF8.GetByteCount(response).ToString(CultureInfo.InvariantCulture)];
        test.Context.Response.Headers["X-Preserved"] = ["response", "unchanged"];
        return test;
    }

    private static PolicyException AssertFailure(
        TestDocument test,
        string section = nameof(IInboundContext))
    {
        var request = test.Context.Request.Body.Content;
        var response = test.Context.Response.Body.Content;
        var requestHeaders = test.Context.Request.Headers
            .ToDictionary(header => header.Key, header => header.Value.ToArray());
        var responseHeaders = test.Context.Response.Headers
            .ToDictionary(header => header.Key, header => header.Value.ToArray());
        Action action = () => RunSection(test, section);

        var exception = action.Should().Throw<PolicyException>().Which;

        exception.Policy.Should().Be(nameof(IInboundContext.XslTransform));
        exception.Section.Should().Be(section);
        exception.PolicyArgs.Should().HaveCount(1);
        exception.InnerException.Should().NotBeNull();
        test.Context.Request.Body.Content.Should().Be(request);
        test.Context.Response.Body.Content.Should().Be(response);
        test.Context.Request.Headers.Should().BeEquivalentTo(requestHeaders);
        test.Context.Response.Headers.Should().BeEquivalentTo(responseHeaders);
        return exception;
    }

    private static MockMessage GetMessageForSection(TestDocument test, string section) => section switch
    {
        nameof(IInboundContext) => test.Context.Request,
        nameof(IOutboundContext) => test.Context.Response,
        nameof(IOnErrorContext) => test.Context.Response,
        _ => throw new ArgumentException("Unsupported XslTransform section.", nameof(section)),
    };

    private static void RunSection(TestDocument test, string section)
    {
        switch (section)
        {
            case nameof(IInboundContext):
                test.RunInbound();
                break;
            case nameof(IOutboundContext):
                test.RunOutbound();
                break;
            case nameof(IOnErrorContext):
                test.RunOnError();
                break;
            default:
                throw new ArgumentException("Unsupported XslTransform section.", nameof(section));
        }
    }

    private static MockXslTransformProvider.Setup SetupForSection(TestDocument test, string section) =>
        section switch
        {
            nameof(IInboundContext) => test.SetupInbound().XslTransform(),
            nameof(IOutboundContext) => test.SetupOutbound().XslTransform(),
            nameof(IOnErrorContext) => test.SetupOnError().XslTransform(),
            _ => throw new ArgumentException("Unsupported XslTransform section.", nameof(section)),
        };

    private static MockXslTransformProvider.Setup SetupForSection(
        TestDocument test,
        string section,
        Func<GatewayContext, XslTransformConfig, bool> predicate) => section switch
        {
            nameof(IInboundContext) => test.SetupInbound().XslTransform(predicate),
            nameof(IOutboundContext) => test.SetupOutbound().XslTransform(predicate),
            nameof(IOnErrorContext) => test.SetupOnError().XslTransform(predicate),
            _ => throw new ArgumentException("Unsupported XslTransform section.", nameof(section)),
        };
}
