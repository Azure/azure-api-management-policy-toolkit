// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring.Expressions;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Document;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Expressions;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Services;

using Newtonsoft.Json.Linq;

namespace Test.Emulator.Emulator.Policies;

[TestClass]
public class ValidateContentTests
{
    [TestMethod]
    [DataRow("inbound")]
    [DataRow("outbound")]
    [DataRow("on-error")]
    public void ShouldValidateTheCorrectMessageWithoutConsumingOrChangingIt(string section)
    {
        var test = Create();
        var message = SchemaValidationTest.Message(test.Context, section);
        message.Headers = new Dictionary<string, string[]>
        {
            ["cOnTeNt-TyPe"] = ["Application/JSON; charset=utf-8"],
            ["X-Unrelated"] = ["preserved"],
        };
        message.Body.Content = SchemaValidationTest.ValidBody;
        var headers = message.Headers;
        var body = message.Body;

        SchemaValidationTest.Run(test, section);

        test.Context.Variables["after"].Should().Be(true);
        SchemaValidationTest.Errors(test.Context).Should().BeEmpty();
        message.Headers.Should().BeSameAs(headers);
        message.Headers["cOnTeNt-TyPe"].Should().Equal("Application/JSON; charset=utf-8");
        message.Body.Should().BeSameAs(body);
        body.Content.Should().Be(SchemaValidationTest.ValidBody);
        body.Consumed.Should().BeFalse();
        SchemaValidationTest.AssertPreserved(test.Context);
    }

    [TestMethod]
    [DataRow("{}")]
    [DataRow("""{"id":"1","name":"Ada"}""")]
    [DataRow("""{"id":0,"name":"Ada"}""")]
    [DataRow("""{"id":1,"name":""}""")]
    [DataRow("""{"id":1,"name":"TooLong"}""")]
    [DataRow("""{"id":1,"name":"Ada","extra":true}""")]
    [DataRow("""{"id":1,"id":2,"name":"Ada"}""")]
    [DataRow("""{"id":""")]
    [DataRow("""{"id":1,"name":"Ada"} trailing""")]
    public void ShouldRejectNonconformingOrMalformedJson(string content)
    {
        var test = Create();
        test.Context.Request.Headers["Content-Type"] = ["application/json"];
        test.Context.Request.Body.Content = content;

        test.RunInbound();

        SchemaValidationTest.AssertRejected(test.Context, 400, "validate-content", "inbound");
        SchemaValidationTest.Errors(test.Context).Should().Contain(error =>
            error.Type == "RequestBody" && error.ValidationRule == "IncorrectMessage" && error.Action == "prevent");
        test.Context.Request.Body.Content.Should().Be(content);
        test.Context.Request.Body.Consumed.Should().BeFalse();
    }

    [TestMethod]
    public void ShouldDetectAndReplaceTheErrorCollectionWithoutChangingTheResponseOrLastError()
    {
        var config = SchemaValidationTest.ContentConfig("detect");
        var test = Create(config);
        var traces = new List<string>();
        test.Context.Trace = traces.Add;
        test.Context.Variables["errors"] = new[] { new SchemaValidationError("old", "old", "old", "old", "detect") };
        test.Context.Request.Headers["Content-Type"] = ["application/json"];
        test.Context.Request.Body.Content = "{}";

        test.RunInbound();

        var errors = SchemaValidationTest.Errors(test.Context);
        errors.Should().NotBeEmpty().And.NotContain(error => error.Name == "old");
        errors.Should().OnlyContain(error => error.Action == "detect" && error.Type == "RequestBody");
        JObject.FromObject(errors[0]).Properties().Select(property => property.Name)
            .Should().BeEquivalentTo(new[] { "Name", "Type", "ValidationRule", "Details", "Action" });
        traces.Should().Contain(message => message.Contains("validate-content") && message.Contains("IncorrectMessage"));
        test.Context.Variables["after"].Should().Be(true);
        SchemaValidationTest.AssertPreserved(test.Context);
    }

    [TestMethod]
    public void ShouldSkipIgnoredChecksWithoutRequiringSchemas()
    {
        var config = SchemaValidationTest.ContentConfig("ignore") with
        {
            SizeExceededAction = "ignore",
            UnspecifiedContentTypeAction = "ignore",
            MaxSize = 0,
        };
        var test = SchemaValidationTest.Create(SchemaValidationTest.ContentDocument(_ => config));
        test.Context.Request.Body.Content = "not json";

        test.RunInbound();

        SchemaValidationTest.Errors(test.Context).Should().BeEmpty();
        test.Context.Variables["after"].Should().Be(true);
        SchemaValidationTest.AssertPreserved(test.Context);
    }

    [TestMethod]
    [DataRow("\u00e9\u00e9\u00e9", 6, false)]
    [DataRow("\u00e9\u00e9\u00e9", 5, true)]
    [DataRow("\U0001f600", 4, false)]
    [DataRow("\U0001f600", 3, true)]
    [DataRow("abc", 3, false)]
    [DataRow("abc", 2, true)]
    public void ShouldMeasureActualUtf8BytesAtTheExactLimit(string content, int maxSize, bool rejected)
    {
        var config = SchemaValidationTest.ContentConfig() with
        {
            MaxSize = maxSize,
            UnspecifiedContentTypeAction = "ignore",
            Contents = null,
        };
        var test = SchemaValidationTest.Create(SchemaValidationTest.ContentDocument(_ => config));
        test.Context.Request.Body.Content = content;
        test.Context.Request.Headers["Content-Length"] = ["1"];

        test.RunInbound();

        if (rejected)
        {
            SchemaValidationTest.AssertRejected(test.Context, 400, "validate-content", "inbound");
            SchemaValidationTest.Errors(test.Context).Should().ContainSingle()
                .Which.ValidationRule.Should().Be("SizeLimit");
        }
        else
        {
            SchemaValidationTest.Errors(test.Context).Should().BeEmpty();
            test.Context.Variables["after"].Should().Be(true);
            SchemaValidationTest.AssertPreserved(test.Context);
        }
        test.Context.Request.Body.Consumed.Should().BeFalse();
    }

    [TestMethod]
    [DataRow("detect", 1)]
    [DataRow("ignore", 0)]
    public void ShouldApplyTheSizeAction(string action, int errors)
    {
        var config = SchemaValidationTest.ContentConfig() with
        {
            MaxSize = 1,
            SizeExceededAction = action,
            UnspecifiedContentTypeAction = "ignore",
            Contents = null,
        };
        var test = SchemaValidationTest.Create(SchemaValidationTest.ContentDocument(_ => config));
        test.Context.Request.Body.Content = "\u00e9";

        test.RunInbound();

        SchemaValidationTest.Errors(test.Context).Should().HaveCount(errors);
        test.Context.Variables["after"].Should().Be(true);
        SchemaValidationTest.AssertPreserved(test.Context);
    }

    [TestMethod]
    [DataRow("prevent", true, 1)]
    [DataRow("detect", false, 1)]
    [DataRow("ignore", false, 0)]
    public void ShouldApplyTheUnspecifiedContentTypeAction(string action, bool rejected, int errors)
    {
        var test = Create(SchemaValidationTest.ContentConfig() with { UnspecifiedContentTypeAction = action });
        test.Context.Request.Headers["Content-Type"] = ["text/plain"];
        test.Context.Request.Body.Content = SchemaValidationTest.ValidBody;

        test.RunInbound();

        SchemaValidationTest.Errors(test.Context).Should().HaveCount(errors);
        if (rejected)
        {
            SchemaValidationTest.AssertRejected(test.Context, 400, "validate-content", "inbound");
            SchemaValidationTest.Errors(test.Context)[0].ValidationRule.Should().Be("Unspecified");
        }
        else
        {
            test.Context.Variables["after"].Should().Be(true);
            SchemaValidationTest.AssertPreserved(test.Context);
        }
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("not a media type")]
    public void ShouldRejectMissingOrInvalidContentTypes(string? contentType)
    {
        var test = Create();
        if (contentType is not null)
        {
            test.Context.Request.Headers["Content-Type"] = [contentType];
        }
        test.Context.Request.Body.Content = SchemaValidationTest.ValidBody;

        test.RunInbound();

        SchemaValidationTest.AssertRejected(test.Context, 400, "validate-content", "inbound");
        SchemaValidationTest.Errors(test.Context).Should().NotBeEmpty();
    }

    [TestMethod]
    [DataRow("missing")]
    [DataRow("any")]
    [DataRow("from")]
    [DataRow("when")]
    [DataRow("false-condition")]
    public void ShouldMapContentTypesWithoutRewritingHeaders(string mapping)
    {
        var map = mapping switch
        {
            "missing" => new ContentTypeMapConfig { MissingContentTypeValue = "application/json" },
            "any" => new ContentTypeMapConfig { AnyContentTypeValue = "application/json" },
            "from" => new ContentTypeMapConfig
            {
                AnyContentTypeValue = "text/plain",
                Types = [new ContentTypeMap { From = "application/hal+json", To = "application/json" }],
            },
            "when" => new ContentTypeMapConfig
            {
                Types = [new ContentTypeMap { When = true, To = "application/json" }],
            },
            _ => new ContentTypeMapConfig
            {
                AnyContentTypeValue = "application/json",
                Types = [new ContentTypeMap { When = false, To = "text/plain" }],
            },
        };
        var test = Create(SchemaValidationTest.ContentConfig() with { ContentTypeMap = map });
        if (mapping != "missing")
        {
            test.Context.Request.Headers["Content-Type"] = ["Application/Hal+Json; charset=utf-8"];
        }
        var headers = test.Context.Request.Headers;
        test.Context.Request.Body.Content = SchemaValidationTest.ValidBody;

        test.RunInbound();

        SchemaValidationTest.Errors(test.Context).Should().BeEmpty();
        test.Context.Request.Headers.Should().BeSameAs(headers);
        if (mapping == "missing")
        {
            headers.Should().NotContainKey("Content-Type");
        }
        else
        {
            headers["Content-Type"].Should().Equal("Application/Hal+Json; charset=utf-8");
        }
    }

    [TestMethod]
    [DataRow("inbound")]
    [DataRow("outbound")]
    [DataRow("on-error")]
    public void ShouldUseTheInjectedOperationSchemaWhenNoSchemaIdIsAuthored(string section)
    {
        var config = SchemaValidationTest.ContentConfig() with
        {
            Contents = [new ValidateContent { ValidateAs = "json", Action = "prevent" }],
        };
        var test = SchemaValidationTest.Create(SchemaValidationTest.ContentDocument(_ => config));
        test.Context.Services.Register(SchemaValidationTest.Metadata());
        var message = SchemaValidationTest.Message(test.Context, section);
        message.Headers["Content-Type"] = ["application/json"];
        message.Body.Content = SchemaValidationTest.ValidBody;

        SchemaValidationTest.Run(test, section);

        SchemaValidationTest.Errors(test.Context).Should().BeEmpty();
        test.Context.Variables["after"].Should().Be(true);
        test.Context.ResponseTerminated.Should().BeFalse();
    }

    [TestMethod]
    [DataRow("inbound", "headers", true)]
    [DataRow("outbound", "headers", true)]
    [DataRow("on-error", "headers", true)]
    [DataRow("inbound", "headers", false)]
    [DataRow("outbound", "headers", false)]
    [DataRow("on-error", "headers", false)]
    [DataRow("inbound", "different-media", true)]
    [DataRow("inbound", "different-media", false)]
    [DataRow("inbound", "conflicting-schema", true)]
    [DataRow("inbound", "conflicting-schema", false)]
    [DataRow("inbound", "none", true)]
    [DataRow("inbound", "none", false)]
    public void NamedSchemaShouldRemainAuthoritativeRegardlessOfUnrelatedApiMetadata(
        string section, string metadataKind, bool valid)
    {
        var test = Create();
        test.Context.Services.Register("customer", new ContentValidationSchema("json",
            """{"type":"object","required":["id"],"properties":{"id":{"type":"integer"}},"additionalProperties":false}"""));
        if (metadataKind != "none")
        {
            test.Context.Services.Register(new ApiValidationMetadata
            {
                RequestHeaders = new Dictionary<string, ApiParameterValidationMetadata>
                {
                    ["X-Id"] = SchemaValidationTest.Parameter("""{"type":"string"}"""),
                },
                RequestContent = metadataKind == "headers" ? new Dictionary<string, ContentValidationSchema>() :
                    new Dictionary<string, ContentValidationSchema>
                    {
                        [metadataKind == "different-media" ? "text/plain" : "application/json"] =
                            new("json", """{"type":"object","required":["unrelated"]}"""),
                    },
            });
        }
        var message = SchemaValidationTest.Message(test.Context, section);
        message.Headers["Content-Type"] = ["application/json"];
        message.Body.Content = valid ? """{"id":1}""" : """{"id":"invalid"}""";
        var body = message.Body;
        var originalContent = body.Content;

        SchemaValidationTest.Run(test, section);

        if (valid)
        {
            SchemaValidationTest.Errors(test.Context).Should().BeEmpty();
            test.Context.Variables["after"].Should().Be(true);
            message.Body.Should().BeSameAs(body);
            body.Content.Should().Be(originalContent);
            SchemaValidationTest.AssertPreserved(test.Context);
        }
        else
        {
            SchemaValidationTest.AssertRejected(test.Context, section == "inbound" ? 400 : 502,
                "validate-content", section);
            SchemaValidationTest.Errors(test.Context).Should().ContainSingle()
                .Which.ValidationRule.Should().Be("IncorrectMessage");
        }
        body.Consumed.Should().BeFalse();
    }

    [TestMethod]
    public void ShouldNotInventAnApiSchema()
    {
        var config = SchemaValidationTest.ContentConfig() with { Contents = null };
        var test = SchemaValidationTest.Create(SchemaValidationTest.ContentDocument(_ => config));
        test.Context.Request.Headers["Content-Type"] = ["application/json"];

        SchemaValidationTest.AssertUnsupported(() => test.RunInbound(), "ApiValidationMetadata");
        SchemaValidationTest.AssertPreserved(test.Context);
    }

    [TestMethod]
    public void ShouldNotPretendAnUnregisteredNamedSchemaValidated()
    {
        var test = SchemaValidationTest.Create(
            SchemaValidationTest.ContentDocument(_ => SchemaValidationTest.ContentConfig("detect")));
        test.Context.Request.Headers["Content-Type"] = ["application/json"];
        test.Context.Request.Body.Content = SchemaValidationTest.ValidBody;

        SchemaValidationTest.AssertUnsupported(() => test.RunInbound(), "customer");
        SchemaValidationTest.AssertPreserved(test.Context);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void ShouldResolveSchemaRefAndLocalJsonReferences(bool useSchemaRef)
    {
        var schema = useSchemaRef
            ? "{\"components\":{\"schemas\":{\"customer\":" + SchemaValidationTest.JsonSchema + "}}}"
            : "{\"$ref\":\"#/definitions/customer\",\"definitions\":{\"customer\":" + SchemaValidationTest.JsonSchema + "}}";
        var config = SchemaValidationTest.ContentConfig();
        if (useSchemaRef)
        {
            config = config with { Contents = [config.Contents![0] with { SchemaRef = "#/components/schemas/customer" }] };
        }
        var test = Create(config);
        test.Context.Services.Register("customer", new ContentValidationSchema("json", schema));
        test.Context.Request.Headers["Content-Type"] = ["application/json"];
        test.Context.Request.Body.Content = SchemaValidationTest.ValidBody;

        test.RunInbound();

        SchemaValidationTest.Errors(test.Context).Should().BeEmpty();
        test.Context.Variables["after"].Should().Be(true);
    }

    [TestMethod]
    [DataRow("""{"type":"object","patternProperties":{".*":{"type":"string"}}}""", "patternProperties")]
    [DataRow("""{"type":"object","properties":{"unused":{"type":"string","format":"custom"}}}""", "custom")]
    [DataRow("""{"$ref":"https://example.invalid/schema.json"}""", "reference")]
    public void ShouldSurfaceUnsupportedSchemaFeaturesEvenInAbsentProperties(string schema, string feature)
    {
        var test = Create();
        test.Context.Services.Register("customer", new ContentValidationSchema("json", schema));
        test.Context.Request.Headers["Content-Type"] = ["application/json"];
        test.Context.Request.Body.Content = "{}";

        SchemaValidationTest.AssertUnsupported(() => test.RunInbound(), feature);
        SchemaValidationTest.AssertPreserved(test.Context);
    }

    [TestMethod]
    [DataRow(false, false)]
    [DataRow(true, true)]
    public void ShouldHonorCaseInsensitivePropertyNames(bool ignoreCase, bool accepted)
    {
        var config = SchemaValidationTest.ContentConfig();
        config = config with { Contents = [config.Contents![0] with { CaseInsensitivePropertyNames = ignoreCase }] };
        var test = Create(config);
        test.Context.Request.Headers["Content-Type"] = ["application/json"];
        test.Context.Request.Body.Content = """{"ID":1,"NAME":"Ada"}""";

        test.RunInbound();

        test.Context.Variables.ContainsKey("after").Should().Be(accepted);
        SchemaValidationTest.Errors(test.Context).Any().Should().Be(!accepted);
    }

    [TestMethod]
    [DataRow(true, false, true)]
    [DataRow(false, true, false)]
    [DataRow(false, false, false)]
    public void ShouldOverrideAdditionalProperties(bool allow, bool schemaAllows, bool accepted)
    {
        var config = SchemaValidationTest.ContentConfig();
        config = config with { Contents = [config.Contents![0] with { AllowAdditionalProperties = allow }] };
        var test = Create(config);
        var schema = SchemaValidationTest.JsonSchema.Replace("\"additionalProperties\":false",
            $"\"additionalProperties\":{schemaAllows.ToString().ToLowerInvariant()}");
        test.Context.Services.Register("customer", new ContentValidationSchema("json", schema));
        test.Context.Request.Headers["Content-Type"] = ["application/json"];
        test.Context.Request.Body.Content = """{"id":1,"name":"Ada","extra":true}""";

        test.RunInbound();

        test.Context.Variables.ContainsKey("after").Should().Be(accepted);
        SchemaValidationTest.Errors(test.Context).Any().Should().Be(!accepted);
    }

    [TestMethod]
    [DataRow("""{"values":[1,2],"code":"ABC"}""", true)]
    [DataRow("""{"values":[],"code":"ABC"}""", false)]
    [DataRow("""{"values":[1,1],"code":"ABC"}""", false)]
    [DataRow("""{"values":[1,2,3],"code":"ABC"}""", false)]
    [DataRow("""{"values":[0],"code":"ABC"}""", false)]
    [DataRow("""{"values":[1],"code":"abc"}""", false)]
    public void ShouldValidateNestedArrayAndStringConstraints(string content, bool accepted)
    {
        var test = Create();
        test.Context.Services.Register("customer", new ContentValidationSchema("json",
            """
            {"type":"object","required":["values","code"],"properties":{
              "values":{"type":"array","minItems":1,"maxItems":2,"uniqueItems":true,"items":{"type":"integer","minimum":1}},
              "code":{"type":"string","pattern":"^[A-Z]{3}$"}
            },"additionalProperties":false}
            """));
        test.Context.Request.Headers["Content-Type"] = ["application/json"];
        test.Context.Request.Body.Content = content;

        test.RunInbound();

        test.Context.Variables.ContainsKey("after").Should().Be(accepted);
        SchemaValidationTest.Errors(test.Context).Any().Should().Be(!accepted);
    }

    [TestMethod]
    [DataRow("1", true)]
    [DataRow("2", true)]
    [DataRow("4", true)]
    [DataRow("5", true)]
    [DataRow("0", false)]
    [DataRow("1.5", false)]
    [DataRow("3", false)]
    [DataRow("5.5", false)]
    public void ShouldValidateCompositionsAndNumericConstraints(string content, bool accepted)
    {
        var test = Create();
        test.Context.Services.Register("customer", new ContentValidationSchema("json",
            """
            {"allOf":[{"type":"number","minimum":1,"maximum":5,"multipleOf":0.5},{"not":{"const":3}}],
             "anyOf":[{"const":1},{"minimum":2}],"oneOf":[{"maximum":2},{"minimum":4}]}
            """));
        test.Context.Request.Headers["Content-Type"] = ["application/json"];
        test.Context.Request.Body.Content = content;

        test.RunInbound();

        test.Context.Variables.ContainsKey("after").Should().Be(accepted);
        SchemaValidationTest.Errors(test.Context).Any().Should().Be(!accepted);
    }

    [TestMethod]
    [DataRow("uuid", "\"01234567-89ab-cdef-0123-456789abcdef\"", true)]
    [DataRow("uuid", "\"invalid\"", false)]
    [DataRow("date", "\"2024-02-29\"", true)]
    [DataRow("date", "\"2023-02-29\"", false)]
    [DataRow("email", "\"person@example.test\"", true)]
    [DataRow("email", "\"invalid\"", false)]
    public void ShouldValidateSupportedStringFormats(string format, string content, bool accepted)
    {
        var test = Create();
        test.Context.Services.Register("customer",
            new ContentValidationSchema("json", $"{{\"type\":\"string\",\"format\":\"{format}\"}}"));
        test.Context.Request.Headers["Content-Type"] = ["application/json"];
        test.Context.Request.Body.Content = content;

        test.RunInbound();

        test.Context.Variables.ContainsKey("after").Should().Be(accepted);
        SchemaValidationTest.Errors(test.Context).Any().Should().Be(!accepted);
    }

    [TestMethod]
    [DataRow("{")]
    [DataRow("""{"type":"imaginary"}""")]
    [DataRow("""{"allOf":[]}""")]
    public void ShouldSurfaceMalformedSchemaConfigurationRatherThanReportingPayloadSuccess(string schema)
    {
        var test = Create();
        test.Context.Services.Register("customer", new ContentValidationSchema("json", schema));
        test.Context.Request.Headers["Content-Type"] = ["application/json"];
        test.Context.Request.Body.Content = SchemaValidationTest.ValidBody;

        var error = Assert.ThrowsException<PolicyException>(() => test.RunInbound());

        error.InnerException.Should().BeAssignableTo<ArgumentException>();
        SchemaValidationTest.AssertPreserved(test.Context);
    }

    [TestMethod]
    public void ShouldNotResolveExternalXsdIncludes()
    {
        var config = SchemaValidationTest.ContentConfig() with
        {
            Contents = [new ValidateContent
            {
                Type = "application/xml", ValidateAs = "xml", Action = "prevent", SchemaId = "external",
            }],
        };
        var test = Create(config);
        test.Context.Services.Register("external", new ContentValidationSchema("xml",
            """<xs:schema xmlns:xs="http://www.w3.org/2001/XMLSchema"><xs:include schemaLocation="https://example.invalid/external.xsd"/></xs:schema>"""));
        test.Context.Request.Headers["Content-Type"] = ["application/xml"];
        test.Context.Request.Body.Content = "<root>5</root>";

        SchemaValidationTest.AssertUnsupported(() => test.RunInbound(), "schemaLocation");
        SchemaValidationTest.AssertPreserved(test.Context);
    }

    [TestMethod]
    [DataRow("xml", "<root>5</root>", true)]
    [DataRow("xml", "<root>bad</root>", false)]
    [DataRow("xml", "<wrong>5</wrong>", false)]
    [DataRow("xml", "<root>", false)]
    [DataRow("xml", "<!DOCTYPE root [<!ENTITY x SYSTEM 'file:///not-accessed'>]><root>&x;</root>", false)]
    [DataRow("soap", "<s:Envelope xmlns:s='http://schemas.xmlsoap.org/soap/envelope/'><s:Body><root>5</root></s:Body></s:Envelope>", true)]
    [DataRow("soap", "<s:Envelope xmlns:s='http://www.w3.org/2003/05/soap-envelope'><s:Body><root>bad</root></s:Body></s:Envelope>", false)]
    [DataRow("soap", "<root>5</root>", false)]
    public void ShouldValidateXmlAndSoapPayloadsAgainstAnInjectedXsd(string validateAs, string content, bool accepted)
    {
        var config = SchemaValidationTest.ContentConfig() with
        {
            Contents = [new ValidateContent
            {
                ValidateAs = validateAs, Type = validateAs == "xml" ? "application/xml" : "text/xml",
                SchemaId = "xml-schema", Action = "prevent",
            }],
        };
        var test = SchemaValidationTest.Create(SchemaValidationTest.ContentDocument(_ => config));
        test.Context.Services.Register("xml-schema", new ContentValidationSchema("xml",
            """<xs:schema xmlns:xs="http://www.w3.org/2001/XMLSchema"><xs:element name="root" type="xs:int"/></xs:schema>"""));
        test.Context.Request.Headers["Content-Type"] = [config.Contents[0].Type!];
        test.Context.Request.Body.Content = content;

        test.RunInbound();

        test.Context.Variables.ContainsKey("after").Should().Be(accepted);
        SchemaValidationTest.Errors(test.Context).Any().Should().Be(!accepted);
        test.Context.Request.Body.Consumed.Should().BeFalse();
    }

    [TestMethod]
    [DataRow("inbound", "xml", "utf-16", false)]
    [DataRow("outbound", "xml", "utf-16", false)]
    [DataRow("on-error", "xml", "utf-16", false)]
    [DataRow("inbound", "soap", "utf-16", false)]
    [DataRow("inbound", "xml", "utf-16LE", false)]
    [DataRow("inbound", "xml", "iso-8859-1", false)]
    [DataRow("inbound", "xml", "utf-8", true)]
    [DataRow("inbound", "soap", "UTF-8", true)]
    [DataRow("inbound", "xml", "", true)]
    public void XmlEncodingDeclarationsShouldMatchActualUtf8MessageBytes(
        string section, string validateAs, string encoding, bool accepted)
    {
        var payload = validateAs == "xml" ? "<root>5</root>" :
            "<s:Envelope xmlns:s='http://www.w3.org/2003/05/soap-envelope'><s:Body><root>5</root></s:Body></s:Envelope>";
        var content = (encoding.Length == 0 ? string.Empty : $"<?xml version='1.0' encoding='{encoding}'?>") + payload;
        var test = CreateXml(IntegerXmlSchema, content, section, validateAs);
        var body = SchemaValidationTest.Message(test.Context, section).Body;

        SchemaValidationTest.Run(test, section);

        if (accepted)
        {
            SchemaValidationTest.Errors(test.Context).Should().BeEmpty();
            test.Context.Variables["after"].Should().Be(true);
            body.Content.Should().Be(content);
            SchemaValidationTest.AssertPreserved(test.Context);
        }
        else
        {
            SchemaValidationTest.AssertRejected(test.Context, section == "inbound" ? 400 : 502,
                "validate-content", section);
            SchemaValidationTest.Errors(test.Context).Should().Contain(error =>
                error.ValidationRule == "IncorrectMessage" && error.Action == "prevent");
        }
        body.Consumed.Should().BeFalse();
    }

    [TestMethod]
    [DataRow("prevent", true, 1)]
    [DataRow("detect", false, 1)]
    [DataRow("ignore", false, 0)]
    public void XmlEncodingFailuresShouldHonorActionsAndPreserveRequestState(string action, bool rejected, int errors)
    {
        const string content = "<?xml version='1.0' encoding='utf-16'?><root>5</root>";
        var test = CreateXml(IntegerXmlSchema, content, action: action);
        var headers = test.Context.Request.Headers;
        var body = test.Context.Request.Body;

        test.RunInbound();

        SchemaValidationTest.Errors(test.Context).Should().HaveCount(errors);
        if (rejected)
        {
            SchemaValidationTest.AssertRejected(test.Context, 400, "validate-content", "inbound");
        }
        else
        {
            test.Context.Variables["after"].Should().Be(true);
            SchemaValidationTest.AssertPreserved(test.Context);
        }
        test.Context.Request.Headers.Should().BeSameAs(headers);
        test.Context.Request.Body.Should().BeSameAs(body);
        body.Content.Should().Be(content);
        body.Consumed.Should().BeFalse();
    }

    [TestMethod]
    [DataRow("inbound", "envelope", true)]
    [DataRow("outbound", "envelope", true)]
    [DataRow("on-error", "envelope", true)]
    [DataRow("inbound", "body-shadow", true)]
    [DataRow("inbound", "payload-shadow", true)]
    [DataRow("inbound", "envelope", false)]
    [DataRow("outbound", "envelope", false)]
    [DataRow("on-error", "envelope", false)]
    [DataRow("inbound", "body-shadow", false)]
    [DataRow("inbound", "payload-shadow", false)]
    public void SoapPayloadValidationShouldRetainInScopeNamespacesAndNearestPrefixBindings(
        string section, string scope, bool valid)
    {
        var envelopeTypeNamespace = scope == "envelope" ? "http://www.w3.org/2001/XMLSchema" : "urn:wrong";
        var bodyNamespace = scope == "body-shadow" ? " xmlns:xsd='http://www.w3.org/2001/XMLSchema'" : string.Empty;
        var payloadNamespace = scope == "payload-shadow" ? " xmlns:xsd='http://www.w3.org/2001/XMLSchema'" : string.Empty;
        var content = $"""
            <s:Envelope xmlns:s="http://www.w3.org/2003/05/soap-envelope"
                xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xmlns:xsd="{envelopeTypeNamespace}">
                <s:Body{bodyNamespace}><root{payloadNamespace} xsi:type="xsd:int">{(valid ? "5" : "invalid")}</root></s:Body>
            </s:Envelope>
            """;
        var test = CreateXml(
            """<xs:schema xmlns:xs="http://www.w3.org/2001/XMLSchema"><xs:element name="root" type="xs:anyType"/></xs:schema>""",
            content, section, "soap");
        var body = SchemaValidationTest.Message(test.Context, section).Body;

        SchemaValidationTest.Run(test, section);

        if (valid)
        {
            SchemaValidationTest.Errors(test.Context).Should().BeEmpty();
            test.Context.Variables["after"].Should().Be(true);
            body.Content.Should().Be(content);
            SchemaValidationTest.AssertPreserved(test.Context);
        }
        else
        {
            SchemaValidationTest.AssertRejected(test.Context, section == "inbound" ? 400 : 502,
                "validate-content", section);
            SchemaValidationTest.Errors(test.Context).Should().Contain(error => error.ValidationRule == "IncorrectMessage");
        }
        body.Consumed.Should().BeFalse();
    }

    [TestMethod]
    public void SoapPayloadValidationShouldPreserveNamespacePrefixesUsedOnlyInQNameText()
    {
        const string content = """
            <s:Envelope xmlns:s="http://www.w3.org/2003/05/soap-envelope" xmlns:xsd="http://www.w3.org/2001/XMLSchema">
                <s:Body><root>xsd:int</root></s:Body>
            </s:Envelope>
            """;
        var test = CreateXml(
            """<xs:schema xmlns:xs="http://www.w3.org/2001/XMLSchema"><xs:element name="root" type="xs:QName"/></xs:schema>""",
            content, validateAs: "soap");

        test.RunInbound();

        SchemaValidationTest.Errors(test.Context).Should().BeEmpty();
        test.Context.Variables["after"].Should().Be(true);
        test.Context.Request.Body.Content.Should().Be(content);
        test.Context.Request.Body.Consumed.Should().BeFalse();
        SchemaValidationTest.AssertPreserved(test.Context);
    }

    [TestMethod]
    [DataRow("inbound")]
    [DataRow("outbound")]
    [DataRow("on-error")]
    public void LaxWildcardWarningsShouldNotRejectValidDeclaredXmlDocuments(string section)
    {
        const string content = "<root><extra><nested/></extra></root>";
        var test = CreateXml(LaxXmlSchema, content, section);

        SchemaValidationTest.Run(test, section);

        SchemaValidationTest.Errors(test.Context).Should().BeEmpty();
        test.Context.Variables["after"].Should().Be(true);
        SchemaValidationTest.Message(test.Context, section).Body.Content.Should().Be(content);
        SchemaValidationTest.Message(test.Context, section).Body.Consumed.Should().BeFalse();
        SchemaValidationTest.AssertPreserved(test.Context);
    }

    [TestMethod]
    [DataRow("inbound")]
    [DataRow("outbound")]
    [DataRow("on-error")]
    public void LaxWildcardsShouldStillRejectInvalidValuesForDeclaredChildElements(string section)
    {
        var test = CreateXml(LaxXmlSchema, "<root><known>invalid</known></root>", section);

        SchemaValidationTest.Run(test, section);

        SchemaValidationTest.AssertRejected(test.Context, section == "inbound" ? 400 : 502,
            "validate-content", section);
        SchemaValidationTest.Errors(test.Context).Should().Contain(error => error.ValidationRule == "IncorrectMessage");
    }

    [TestMethod]
    [DataRow("<unknown><extra/></unknown>")]
    [DataRow("<other:root xmlns:other='urn:unregistered'><extra/></other:root>")]
    [DataRow("<unknown xmlns:xsi='http://www.w3.org/2001/XMLSchema-instance' xmlns:xs='http://www.w3.org/2001/XMLSchema' xsi:type='xs:string'>value</unknown>")]
    public void XmlValidationShouldExplicitlyRejectUndeclaredDocumentRootsEvenWithKnownXsiTypes(string content)
    {
        var test = CreateXml(LaxXmlSchema, content);

        test.RunInbound();

        SchemaValidationTest.AssertRejected(test.Context, 400, "validate-content", "inbound");
        SchemaValidationTest.Errors(test.Context).Should().ContainSingle()
            .Which.Details.Should().Contain("not declared");
    }

    [TestMethod]
    [DataRow("unspecified")]
    [DataRow("size")]
    [DataRow("content")]
    public void ShouldRejectUnknownActionsInsteadOfSilentlyAllowingThem(string field)
    {
        var config = SchemaValidationTest.ContentConfig();
        config = field switch
        {
            "unspecified" => config with { UnspecifiedContentTypeAction = "allow" },
            "size" => config with { SizeExceededAction = "deny" },
            _ => config with { Contents = [config.Contents![0] with { Action = "unknown" }] },
        };
        var test = Create(config);

        var error = Assert.ThrowsException<PolicyException>(() => test.RunInbound());

        error.InnerException.Should().BeOfType<ArgumentException>();
        SchemaValidationTest.AssertPreserved(test.Context);
    }

    [TestMethod]
    [DataRow(-1)]
    [DataRow(4194305)]
    public void ShouldRejectInvalidSizeConfiguration(int maxSize)
    {
        var test = Create(SchemaValidationTest.ContentConfig() with { MaxSize = maxSize });

        var error = Assert.ThrowsException<PolicyException>(() => test.RunInbound());

        error.InnerException.Should().BeOfType<ArgumentOutOfRangeException>();
        SchemaValidationTest.AssertPreserved(test.Context);
    }

    [TestMethod]
    [DataRow("Content-Encoding", "gzip")]
    [DataRow("Content-Type", "application/json; charset=utf-16")]
    public void ShouldNotPretendToValidateRepresentationsTheTextEmulatorCannotMeasure(string header, string value)
    {
        var test = Create();
        test.Context.Request.Headers["Content-Type"] = ["application/json"];
        test.Context.Request.Headers[header] = [value];
        test.Context.Request.Body.Content = SchemaValidationTest.ValidBody;

        SchemaValidationTest.AssertUnsupported(() => test.RunInbound(), header);
        SchemaValidationTest.AssertPreserved(test.Context);
    }

    [TestMethod]
    public void ShouldUseEvaluatedExpressionValuesForEveryExpressionAllowedAttribute()
    {
        var test = SchemaValidationTest.Create(SchemaValidationTest.ContentDocument(context =>
            SchemaValidationTest.ContentConfig() with
            {
                MaxSize = (int)context.Variables["max"],
                SizeExceededAction = (string)context.Variables["sizeAction"],
                UnspecifiedContentTypeAction = (string)context.Variables["typeAction"],
                Contents = null,
            }));
        test.Context.Services.Register(SchemaValidationTest.Metadata());
        test.Context.Variables["max"] = 1;
        test.Context.Variables["sizeAction"] = "detect";
        test.Context.Variables["typeAction"] = "detect";
        test.Context.Request.Body.Content = "\u00e9";
        test.Context.Request.Headers["Content-Type"] = ["text/plain"];

        test.RunInbound();

        SchemaValidationTest.Errors(test.Context).Select(error => error.ValidationRule)
            .Should().BeEquivalentTo(new[] { "SizeLimit", "Unspecified" });
        test.Context.Variables["after"].Should().Be(true);
    }

    [TestMethod]
    [DataRow("inbound", false)]
    [DataRow("outbound", false)]
    [DataRow("on-error", false)]
    [DataRow("inbound", true)]
    [DataRow("outbound", true)]
    [DataRow("on-error", true)]
    public void ShouldSupportPredicateCallbacksAndCallbackTerminationInEverySection(string section, bool terminate)
    {
        var test = SchemaValidationTest.Create(
            SchemaValidationTest.ContentDocument(_ => SchemaValidationTest.ContentConfig()));
        var seen = false;
        Action<GatewayContext, ValidateContentConfig> callback = (context, config) =>
        {
            seen = true;
            config.MaxSize.Should().Be(1024);
            if (terminate)
            {
                context.Response.StatusCode = 409;
                context.Response.Body.Content = "callback";
                throw new FinishSectionProcessingException();
            }
        };
        switch (section)
        {
            case "inbound":
                test.SetupInbound().ValidateContent((_, config) => config.MaxSize == 0)
                    .WithCallback((_, _) => Assert.Fail("Wrong predicate."));
                test.SetupInbound().ValidateContent().WithCallback(callback);
                break;
            case "outbound":
                test.SetupOutbound().ValidateContent((_, config) => config.MaxSize == 0)
                    .WithCallback((_, _) => Assert.Fail("Wrong predicate."));
                test.SetupOutbound().ValidateContent().WithCallback(callback);
                break;
            default:
                test.SetupOnError().ValidateContent((_, config) => config.MaxSize == 0)
                    .WithCallback((_, _) => Assert.Fail("Wrong predicate."));
                test.SetupOnError().ValidateContent().WithCallback(callback);
                break;
        }

        SchemaValidationTest.Run(test, section);

        seen.Should().BeTrue();
        test.Context.ResponseTerminated.Should().Be(terminate);
        test.Context.Variables.ContainsKey("after").Should().Be(!terminate);
        if (terminate)
        {
            test.Context.Response.StatusCode.Should().Be(409);
            test.Context.Response.Body.Content.Should().Be("callback");
        }
    }

    [TestMethod]
    [DataRow("inbound", false)]
    [DataRow("outbound", false)]
    [DataRow("on-error", false)]
    [DataRow("inbound", true)]
    [DataRow("outbound", true)]
    [DataRow("on-error", true)]
    public void ShouldPreventLaterScopesAndSectionsUsingTheAcceptedTerminationContract(string section, bool nested)
    {
        SchemaValidationTest.AssertPipelineStops(
            SchemaValidationTest.ContentDocument(_ => SchemaValidationTest.ContentConfig()), section, nested,
            context =>
            {
                SchemaValidationTest.RegisterContentSchema(context);
                var message = SchemaValidationTest.Message(context, section);
                message.Headers["Content-Type"] = ["application/json"];
                message.Body.Content = "{}";
            });
    }

    private const string IntegerXmlSchema =
        """<xs:schema xmlns:xs="http://www.w3.org/2001/XMLSchema"><xs:element name="root" type="xs:int"/></xs:schema>""";

    private const string LaxXmlSchema = """
        <xs:schema xmlns:xs="http://www.w3.org/2001/XMLSchema">
            <xs:element name="root">
                <xs:complexType><xs:sequence>
                    <xs:any minOccurs="0" maxOccurs="unbounded" processContents="lax"/>
                </xs:sequence></xs:complexType>
            </xs:element>
            <xs:element name="known" type="xs:int"/>
        </xs:schema>
        """;

    private static TestDocument CreateXml(
        string schema, string content, string section = "inbound", string validateAs = "xml", string action = "prevent")
    {
        var contentType = validateAs == "xml" ? "application/xml" : "application/soap+xml";
        var config = SchemaValidationTest.ContentConfig(action) with
        {
            Contents = [new ValidateContent
            {
                ValidateAs = validateAs, Type = contentType, SchemaId = "xml-schema", Action = action,
            }],
        };
        var test = SchemaValidationTest.Create(SchemaValidationTest.ContentDocument(_ => config));
        test.Context.Services.Register("xml-schema", new ContentValidationSchema("xml", schema));
        var message = SchemaValidationTest.Message(test.Context, section);
        message.Headers["Content-Type"] = [contentType + "; charset=utf-8"];
        message.Body.Content = content;
        return test;
    }

    private static TestDocument Create(ValidateContentConfig? config = null)
    {
        var test = SchemaValidationTest.Create(
            SchemaValidationTest.ContentDocument(_ => config ?? SchemaValidationTest.ContentConfig()));
        SchemaValidationTest.RegisterContentSchema(test.Context);
        return test;
    }
}

internal sealed class ValidationTestDocument : IDocument
{
    public Action<IInboundContext>? InboundAction { get; init; }
    public Action<IBackendContext>? BackendAction { get; init; }
    public Action<IOutboundContext>? OutboundAction { get; init; }
    public Action<IOnErrorContext>? OnErrorAction { get; init; }

    public void Inbound(IInboundContext context) => InboundAction?.Invoke(context);
    public void Backend(IBackendContext context) => BackendAction?.Invoke(context);
    public void Outbound(IOutboundContext context) => OutboundAction?.Invoke(context);
    public void OnError(IOnErrorContext context) => OnErrorAction?.Invoke(context);
}

internal static class SchemaValidationTest
{
    internal const string JsonSchema =
        """{"type":"object","required":["id","name"],"properties":{"id":{"type":"integer","minimum":1},"name":{"type":"string","minLength":1,"maxLength":4}},"additionalProperties":false}""";
    internal const string ValidBody = """{"id":1,"name":"Ada"}""";

    internal static ValidateContentConfig ContentConfig(string action = "prevent") => new()
    {
        UnspecifiedContentTypeAction = "prevent",
        MaxSize = 1024,
        SizeExceededAction = "prevent",
        ErrorsVariableName = "errors",
        Contents = [new ValidateContent
        {
            Type = "application/json", ValidateAs = "json", SchemaId = "customer", Action = action,
        }],
    };

    internal static ValidationTestDocument ContentDocument(Func<IExpressionContext, ValidateContentConfig> config) => new()
    {
        InboundAction = context =>
        {
            context.ValidateContent(config(context.ExpressionContext));
            context.SetVariable("after", true);
        },
        OutboundAction = context =>
        {
            context.ValidateContent(config(context.ExpressionContext));
            context.SetVariable("after", true);
        },
        OnErrorAction = context =>
        {
            context.ValidateContent(config(context.ExpressionContext));
            context.SetVariable("after", true);
        },
        BackendAction = context => context.SetVariable("later-section", true),
    };

    internal static TestDocument Create(IDocument document)
    {
        var test = document.AsTestDocument();
        Seed(test.Context);
        return test;
    }

    internal static void Seed(GatewayContext context)
    {
        context.Variables["unrelated"] = "preserved";
        context.Response.Headers["X-Unrelated"] = ["preserved"];
        context.Response.Headers["Content-Length"] = ["14"];
        context.Response.Body.Content = "backend-secret";
        context.LastError.Source = "original";
        context.LastError.Reason = "original";
        context.LastError.Message = "original";
        context.LastError.Scope = "preserved-scope";
    }

    internal static ApiParameterValidationMetadata Parameter(string schema, bool required = false) =>
        new() { JsonSchema = schema, Required = required };

    internal static ApiValidationMetadata Metadata() => new()
    {
        RequestContent = new Dictionary<string, ContentValidationSchema>
        {
            ["application/json"] = new("json", JsonSchema),
        },
        RequestHeaders = new Dictionary<string, ApiParameterValidationMetadata>
        {
            ["X-Limit"] = Parameter("""{"type":"integer","minimum":1,"maximum":5}""", true),
            ["Content-Type"] = Parameter("""{"type":"string"}"""),
            ["Content-Length"] = Parameter("""{"type":"integer","minimum":0}"""),
        },
        QueryParameters = new Dictionary<string, ApiParameterValidationMetadata>
        {
            ["count"] = Parameter("""{"type":"integer","minimum":1,"maximum":5}""", true),
            ["tags"] = Parameter("""{"type":"array","minItems":1,"uniqueItems":true,"items":{"type":"string","enum":["red","green"]}}"""),
        },
        PathParameters = new Dictionary<string, ApiParameterValidationMetadata>
        {
            ["id"] = Parameter("""{"type":"integer","minimum":1}""", true),
        },
        Responses = new Dictionary<int, ApiResponseValidationMetadata>
        {
            [200] = new()
            {
                Content = new Dictionary<string, ContentValidationSchema> { ["application/json"] = new("json", JsonSchema) },
                Headers = new Dictionary<string, ApiParameterValidationMetadata>
                {
                    ["X-Limit"] = Parameter("""{"type":"integer","minimum":1,"maximum":5}""", true),
                    ["X-Tags"] = Parameter("""{"type":"array","items":{"type":"string","enum":["red","green"]}}"""),
                    ["X-Unrelated"] = Parameter("""{"type":"string"}"""),
                    ["Content-Type"] = Parameter("""{"type":"string"}"""),
                    ["Content-Length"] = Parameter("""{"type":"integer","minimum":0}"""),
                },
            },
        },
    };

    internal static void RegisterContentSchema(GatewayContext context) =>
        context.Services.Register("customer", new ContentValidationSchema("json", JsonSchema));

    internal static MockMessage Message(GatewayContext context, string section) =>
        section == "inbound" ? context.Request : context.Response;

    internal static void Run(TestDocument test, string section)
    {
        switch (section)
        {
            case "inbound": test.RunInbound(); break;
            case "outbound": test.RunOutbound(); break;
            case "on-error": test.RunOnError(); break;
            default: throw new ArgumentException(section);
        }
    }

    internal static IReadOnlyList<SchemaValidationError> Errors(GatewayContext context)
    {
        context.Variables.Should().ContainKey("errors");
        return context.Variables["errors"].Should().BeAssignableTo<IReadOnlyList<SchemaValidationError>>().Which;
    }

    internal static void AssertPreserved(GatewayContext context)
    {
        context.ResponseTerminated.Should().BeFalse();
        context.Response.StatusCode.Should().Be(200);
        context.Response.Headers.Should().ContainKey("X-Unrelated").WhoseValue.Should().Equal("preserved");
        context.LastError.Source.Should().Be("original");
        context.LastError.Reason.Should().Be("original");
        context.LastError.Message.Should().Be("original");
        context.Variables["unrelated"].Should().Be("preserved");
    }

    internal static void AssertRejected(GatewayContext context, int status, string source, string section)
    {
        context.ResponseTerminated.Should().BeTrue();
        context.Response.StatusCode.Should().Be(status);
        context.Response.StatusReason.Should().Be(status == 400 ? "Bad Request" : "Bad Gateway");
        context.Response.Headers.Keys.Should().Equal("Content-Type");
        context.Response.Headers["Content-Type"].Should().Equal("application/json");
        var body = context.Response.Body.As<JObject>(preserveContent: true);
        body["statusCode"]!.Value<int>().Should().Be(status);
        body["message"]!.Value<string>().Should().NotBeNullOrWhiteSpace();
        context.Response.Body.Content.Should().NotContain("backend-secret");
        context.LastError.Source.Should().Be(source);
        context.LastError.Section.Should().Be(section);
        context.LastError.HttpErrorCode.Should().Be(status);
        context.LastError.Reason.Should().Be(status == 400 ? "Bad request" : "Response not allowed");
        context.LastError.Message.Should().NotBeNullOrWhiteSpace();
        context.LastError.Scope.Should().Be("preserved-scope");
        context.Variables.Should().NotContainKey("after");
        context.Variables["unrelated"].Should().Be("preserved");
    }

    internal static void AssertUnsupported(Action action, string detail)
    {
        var error = Assert.ThrowsException<PolicyException>(action);
        error.InnerException.Should().BeOfType<NotSupportedException>()
            .Which.Message.Should().ContainEquivalentOf(detail);
    }

    internal static void AssertPipelineStops(IDocument document, string section, bool nested,
        Action<GatewayContext> arrange)
    {
        var outer = new ValidationTestDocument
        {
            InboundAction = context =>
            {
                if (nested) context.Base();
                context.SetVariable("outer-after", true);
            },
            OutboundAction = context =>
            {
                if (nested) context.Base();
                context.SetVariable("outer-after", true);
            },
            OnErrorAction = context =>
            {
                if (nested) context.Base();
                context.SetVariable("outer-after", true);
            },
            BackendAction = context => context.SetVariable("later-section", true),
        };
        var pipeline = PolicyPipelineBuilder.Create()
            .AddPolicy(section == "inbound" ? PolicyScope.Global : PolicyScope.Operation, outer)
            .AddPolicy(section == "inbound" ? PolicyScope.Operation : PolicyScope.Global, document)
            .Build();
        Seed(pipeline.Context);
        arrange(pipeline.Context);
        if (section == "inbound")
        {
            if (nested) pipeline.RunInboundNested(); else pipeline.RunInbound();
        }
        else if (section == "outbound")
        {
            if (nested) pipeline.RunOutboundNested(); else pipeline.RunOutbound();
        }
        else
        {
            if (nested) pipeline.RunOnErrorNested(); else pipeline.RunOnError();
        }
        pipeline.RunAll();
        pipeline.RunAllNested();
        pipeline.RunOnError();
        pipeline.RunOnErrorNested();

        pipeline.Context.ResponseTerminated.Should().BeTrue();
        pipeline.Context.Variables.Should().NotContainKey("after").And.NotContainKey("later-section");
        if (nested)
        {
            pipeline.Context.Variables.Should().NotContainKey("outer-after");
        }
        pipeline.Context.Variables["unrelated"].Should().Be("preserved");
    }
}