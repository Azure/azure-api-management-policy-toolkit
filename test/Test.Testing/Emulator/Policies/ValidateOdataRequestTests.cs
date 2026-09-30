// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Document;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Expressions;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Services;

namespace Test.Emulator.Emulator.Policies;

[TestClass]
public class ValidateOdataRequestTests
{
    [TestMethod]
    [DataRow("Products?$top=2&$skip=0&$count=true&$select=id,name&$orderby=name%20desc&$format=json")]
    [DataRow("Products(1)")]
    [DataRow("$metadata")]
    [DataRow("")]
    public void ShouldValidateSafeOdataRequestsAgainstTheExplicitModel(string path)
    {
        var test = Create();
        test.Context.Request.Url = new MockUrl(new Uri("https://contoso.example/odata/" + path));
        test.Context.Request.Headers = new Dictionary<string, string[]> { ["oDaTa-vErSiOn"] = ["4.01"] };
        var headers = test.Context.Request.Headers;
        var query = test.Context.Request.Url.Query;

        test.RunInbound();

        SchemaValidationTest.Errors(test.Context).Should().BeEmpty();
        test.Context.Request.Headers.Should().BeSameAs(headers);
        test.Context.Request.Url.Query.Should().BeSameAs(query);
        test.Context.Request.Body.Consumed.Should().BeFalse();
        test.Context.Variables["after"].Should().Be(true);
        SchemaValidationTest.AssertPreserved(test.Context);
    }

    [TestMethod]
    [DataRow("Products('plain')", "plain")]
    [DataRow("Products('a%2Fb')", "a/b")]
    [DataRow("Products('a%2fb')", "a/b")]
    [DataRow("Products('a%252Fb')", "a%2Fb")]
    [DataRow("Products('a%27%27b%2Fc')", "a'b/c")]
    [DataRow("Products('a%28b%29%2Fc')", "a(b)/c")]
    [DataRow("Products('a%3Fb%23c%2Fd')", "a?b#c/d")]
    public void EncodedStringKeysShouldBeDecodedOnceWithinTheirOriginalStructuralSegment(
        string path, string expectedKey)
    {
        var test = CreateStringKey(expectedKey);
        test.Context.Request.Url = new MockUrl(new Uri("https://contoso.example/odata/" + path));
        var originalPath = test.Context.Request.Url.Path;
        var query = test.Context.Request.Url.Query;
        var headers = test.Context.Request.Headers;

        test.RunInbound();

        SchemaValidationTest.Errors(test.Context).Should().BeEmpty();
        test.Context.Variables["after"].Should().Be(true);
        test.Context.Request.Url.Path.Should().Be(originalPath);
        test.Context.Request.Url.Query.Should().BeSameAs(query);
        test.Context.Request.Headers.Should().BeSameAs(headers);
        test.Context.Request.Body.Consumed.Should().BeFalse();
        SchemaValidationTest.AssertPreserved(test.Context);
    }

    [TestMethod]
    [DataRow("Products('a%2Fb)")]
    [DataRow("Products(a%2Fb)")]
    [DataRow("Products('a%2Fb')extra")]
    [DataRow("Products('a'%2F'b')")]
    [DataRow("Products('a')%2Fname")]
    [DataRow("Products%2F('a')")]
    [DataRow("Products('a%2Fb')%2F")]
    [DataRow("Products('a%252Fb')")]
    public void MalformedOrAmbiguousEncodedResourcesShouldNotBeAcceptedAsKeysOrNavigation(string path)
    {
        var test = CreateStringKey("a/b");
        test.Context.Request.Url = new MockUrl(new Uri("https://contoso.example/odata/" + path));
        var originalPath = test.Context.Request.Url.Path;

        test.RunInbound();

        SchemaValidationTest.AssertRejected(test.Context, 400, "validate-odata-request", "inbound");
        SchemaValidationTest.Errors(test.Context).Should().Contain(error =>
            error.Type == "ODataRequest" && error.ValidationRule == "IncorrectMessage");
        test.Context.Request.Url.Path.Should().Be(originalPath);
    }

    [TestMethod]
    [DataRow("Products('plain')/name")]
    [DataRow("Products('a%2Fb')/name")]
    public void RealNavigationSeparatorsShouldRemainExplicitlyUnsupported(string path)
    {
        var test = CreateStringKey("plain");
        test.Context.Request.Url = new MockUrl(new Uri("https://contoso.example/odata/" + path));

        SchemaValidationTest.AssertUnsupported(() => test.RunInbound(), "navigation");
        SchemaValidationTest.AssertPreserved(test.Context);
    }

    [TestMethod]
    [DataRow("/Products('plain')")]
    [DataRow("Products('plain')/")]
    [DataRow("Products('plain')//")]
    public void EmptyResourceSegmentsShouldNotBeSilentlyNormalizedAway(string path)
    {
        var test = CreateStringKey("plain");
        test.Context.Request.Url = new MockUrl(new Uri("https://contoso.example/odata/" + path));

        test.RunInbound();

        SchemaValidationTest.AssertRejected(test.Context, 400, "validate-odata-request", "inbound");
        SchemaValidationTest.Errors(test.Context).Should().Contain(error =>
            error.Details.Contains("empty segment"));
    }

    [TestMethod]
    [DataRow("Unknown", "GET")]
    [DataRow("Products(0)", "GET")]
    [DataRow("Products('bad')", "GET")]
    [DataRow("Products?$top=-1", "GET")]
    [DataRow("Products?$skip=invalid", "GET")]
    [DataRow("Products?$count=1", "GET")]
    [DataRow("Products?$select=missing", "GET")]
    [DataRow("Products?$orderby=missing%20desc", "GET")]
    [DataRow("Products?$format=xml", "GET")]
    [DataRow("Products?unknown=value", "GET")]
    [DataRow("Products?$top=1&$top=2", "GET")]
    [DataRow("Products", "DELETE")]
    [DataRow("Products(1)", "POST")]
    public void ShouldRejectNonconformingUrlsKeysQueryOptionsAndMethods(string path, string method)
    {
        var test = Create();
        test.Context.Request.Url = new MockUrl(new Uri("https://contoso.example/odata/" + path));
        test.Context.Request.Method = method;

        test.RunInbound();

        SchemaValidationTest.AssertRejected(test.Context, 400, "validate-odata-request", "inbound");
        SchemaValidationTest.Errors(test.Context).Should().Contain(error =>
            error.Type == "ODataRequest" && error.Action == "prevent");
    }

    [TestMethod]
    [DataRow("3.0")]
    [DataRow("4.02")]
    [DataRow("invalid")]
    [DataRow("")]
    public void ShouldRejectUnsupportedOrMalformedRequestVersions(string version)
    {
        var test = Create();
        test.Context.Request.Headers["OData-Version"] = [version];

        test.RunInbound();

        SchemaValidationTest.AssertRejected(test.Context, 400, "validate-odata-request", "inbound");
        SchemaValidationTest.Errors(test.Context).Should().ContainSingle()
            .Which.ValidationRule.Should().Be("ODataVersion");
    }

    [TestMethod]
    public void ShouldRejectVersionsOutsideTheConfiguredRangeAndInconsistentMaxVersionHeaders()
    {
        var ranged = Create(Config() with { MaxOdataVersion = "4.0" });
        ranged.Context.Request.Headers["OData-Version"] = ["4.01"];
        ranged.RunInbound();
        SchemaValidationTest.AssertRejected(ranged.Context, 400, "validate-odata-request", "inbound");

        var inconsistent = Create();
        inconsistent.Context.Request.Headers["OData-Version"] = ["4.01"];
        inconsistent.Context.Request.Headers["OData-MaxVersion"] = ["4.0"];
        inconsistent.RunInbound();
        SchemaValidationTest.AssertRejected(inconsistent.Context, 400, "validate-odata-request", "inbound");
        SchemaValidationTest.Errors(inconsistent.Context).Should().Contain(error => error.ValidationRule == "ODataVersion");
    }

    [TestMethod]
    [DataRow(20, false)]
    [DataRow(19, true)]
    public void ShouldCheckUtf8PayloadSizeAtTheExactThreshold(int maxSize, bool rejected)
    {
        var test = Create(Config() with { MaxSize = maxSize });
        test.Context.Request.Method = "POST";
        test.Context.Request.Headers["Content-Type"] = ["application/json"];
        test.Context.Request.Headers["Content-Length"] = ["1"];
        test.Context.Request.Body.Content = "{\"id\":1,\"name\":\"\u00e9\"}";

        test.RunInbound();

        if (rejected)
        {
            SchemaValidationTest.AssertRejected(test.Context, 400, "validate-odata-request", "inbound");
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
    [DataRow(null)]
    [DataRow("{}")]
    [DataRow("""{"id":"bad","name":"Ada"}""")]
    [DataRow("not-json")]
    public void ShouldValidateWritePayloadsRatherThanOnlyTheirSize(string? body)
    {
        var test = Create();
        test.Context.Request.Method = "POST";
        test.Context.Request.Headers["Content-Type"] = ["application/json"];
        test.Context.Request.Body.Content = body;

        test.RunInbound();

        SchemaValidationTest.AssertRejected(test.Context, 400, "validate-odata-request", "inbound");
        SchemaValidationTest.Errors(test.Context).Should().NotBeEmpty();
        test.Context.Request.Body.Content.Should().Be(body);
    }

    [TestMethod]
    public void ShouldExplicitlyRequireOdataModelMetadata()
    {
        var test = SchemaValidationTest.Create(Document(Config()));
        test.Context.Request.Url = new MockUrl(new Uri("https://contoso.example/odata/Products"));

        SchemaValidationTest.AssertUnsupported(() => test.RunInbound(), "OdataValidationMetadata");
        SchemaValidationTest.AssertPreserved(test.Context);
    }

    [TestMethod]
    [DataRow("Products?$filter=id%20eq%201", "$filter")]
    [DataRow("Products?$expand=children", "$expand")]
    [DataRow("Products?$apply=groupby((name))", "$apply")]
    [DataRow("Products(1)/name", "navigation")]
    [DataRow("$batch", "batch")]
    public void ShouldExplicitlySurfaceOdataRulesThatNeedAFullEdmValidator(string path, string limitation)
    {
        var test = Create();
        test.Context.Request.Url = new MockUrl(new Uri("https://contoso.example/odata/" + path));

        SchemaValidationTest.AssertUnsupported(() => test.RunInbound(), limitation);
        SchemaValidationTest.AssertPreserved(test.Context);
    }

    [TestMethod]
    public void ShouldNotInventAMissingWritePayloadSchema()
    {
        var test = Create();
        var metadata = Metadata();
        test.Context.Services.Register(metadata with
        {
            EntitySets = new Dictionary<string, OdataEntitySetMetadata>
            {
                ["Products"] = metadata.EntitySets["Products"] with { PayloadSchema = null },
            },
        });
        test.Context.Request.Method = "POST";
        test.Context.Request.Headers["Content-Type"] = ["application/json"];
        test.Context.Request.Body.Content = SchemaValidationTest.ValidBody;

        SchemaValidationTest.AssertUnsupported(() => test.RunInbound(), "payload schema");
        SchemaValidationTest.AssertPreserved(test.Context);
    }

    [TestMethod]
    public void ShouldUseTheAuthoredSingularErrorVariableName()
    {
        var test = Create(Config() with { ErrorVariableName = "odata-errors" });
        test.Context.Request.Headers["OData-Version"] = ["invalid"];

        test.RunInbound();

        SchemaValidationTest.AssertRejected(test.Context, 400, "validate-odata-request", "inbound");
        test.Context.Variables.Should().ContainKey("odata-errors").And.NotContainKey("errors");
        test.Context.Variables["odata-errors"].Should().BeAssignableTo<IReadOnlyList<SchemaValidationError>>()
            .Which.Should().ContainSingle().Which.ValidationRule.Should().Be("ODataVersion");
    }

    [TestMethod]
    public void ShouldRejectUrlsOutsideTheInjectedServiceRoot()
    {
        var test = Create();
        test.Context.Request.Url = new MockUrl(new Uri("https://contoso.example/other/Products"));

        test.RunInbound();

        SchemaValidationTest.AssertRejected(test.Context, 400, "validate-odata-request", "inbound");
        SchemaValidationTest.Errors(test.Context).Should().Contain(error => error.Type == "ODataRequest");
    }

    [TestMethod]
    public void ShouldPassTheDefaultVersionAndRealRequestToAnInjectedEdmValidator()
    {
        var validator = new TestValidator(_ => []);
        var test = SchemaValidationTest.Create(Document(Config() with { DefaultOdataVersion = "4.01" }));
        test.Context.Services.Register<IOdataRequestValidator>(validator);
        test.Context.Request.Url = new MockUrl(new Uri("https://contoso.example/odata/Products?$filter=id%20eq%201"));

        test.RunInbound();

        validator.Calls.Should().Be(1);
        validator.Request.Should().NotBeNull();
        validator.Request!.Version.Should().Be("4.01");
        validator.Request.Query["$filter"].Should().Equal("id eq 1");
        validator.Request.Url.AbsolutePath.Should().Be("/odata/Products");
        SchemaValidationTest.Errors(test.Context).Should().BeEmpty();
        test.Context.Variables["after"].Should().Be(true);
    }

    [TestMethod]
    public void ShouldRejectErrorsReturnedByAnInjectedEdmValidator()
    {
        var test = SchemaValidationTest.Create(Document(Config()));
        test.Context.Services.Register<IOdataRequestValidator>(new TestValidator(_ => ["Unknown EDM property."]));

        test.RunInbound();

        SchemaValidationTest.AssertRejected(test.Context, 400, "validate-odata-request", "inbound");
        SchemaValidationTest.Errors(test.Context).Should().ContainSingle()
            .Which.Details.Should().Contain("Unknown EDM property");
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void ShouldSurfaceValidatorFailuresAndNullResultsInsteadOfReturningSuccess(bool nullResult)
    {
        var validator = new TestValidator(_ =>
            nullResult ? null! : throw new InvalidOperationException("EDM validator failed."));
        var test = SchemaValidationTest.Create(Document(Config()));
        test.Context.Services.Register<IOdataRequestValidator>(validator);

        var error = Assert.ThrowsException<PolicyException>(() => test.RunInbound());

        error.InnerException.Should().BeOfType<InvalidOperationException>();
        test.Context.Variables.Should().NotContainKey("after");
        SchemaValidationTest.AssertPreserved(test.Context);
    }

    [TestMethod]
    [DataRow("size")]
    [DataRow("version")]
    public void ShouldRunLocalConstraintsBeforeInvokingAnExternalValidator(string failure)
    {
        var validator = new TestValidator(_ => []);
        var test = SchemaValidationTest.Create(Document(Config() with { MaxSize = 1 }));
        test.Context.Services.Register<IOdataRequestValidator>(validator);
        if (failure == "size") test.Context.Request.Body.Content = "\u00e9";
        else test.Context.Request.Headers["OData-Version"] = ["bad"];

        test.RunInbound();

        SchemaValidationTest.AssertRejected(test.Context, 400, "validate-odata-request", "inbound");
        validator.Calls.Should().Be(0);
    }

    [TestMethod]
    [DataRow("range")]
    [DataRow("default")]
    [DataRow("min")]
    [DataRow("max")]
    [DataRow("negative-size")]
    public void ShouldSurfaceInvalidPolicyConfiguration(string invalid)
    {
        var config = invalid switch
        {
            "range" => Config() with { MinOdataVersion = "4.01", MaxOdataVersion = "4.0" },
            "default" => Config() with { DefaultOdataVersion = "invalid" },
            "min" => Config() with { MinOdataVersion = "invalid" },
            "max" => Config() with { MaxOdataVersion = "invalid" },
            _ => Config() with { MaxSize = -1 },
        };
        var test = Create(config);

        var error = Assert.ThrowsException<PolicyException>(() => test.RunInbound());

        error.InnerException.Should().BeAssignableTo<ArgumentException>();
        SchemaValidationTest.AssertPreserved(test.Context);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void ShouldSupportPredicateCallbacksAndErrorSimulation(bool terminate)
    {
        var test = SchemaValidationTest.Create(Document(Config()));
        var seen = false;
        test.SetupInbound().ValidateOdataRequest((_, config) => config.ErrorVariableName == "wrong")
            .WithCallback((_, _) => Assert.Fail("Wrong predicate."));
        test.SetupInbound().ValidateOdataRequest().WithCallback((context, _) =>
        {
            seen = true;
            if (terminate)
            {
                context.Response.StatusCode = 409;
                throw new FinishSectionProcessingException();
            }
        });

        test.RunInbound();

        seen.Should().BeTrue();
        test.Context.ResponseTerminated.Should().Be(terminate);
        test.Context.Variables.ContainsKey("after").Should().Be(!terminate);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void ShouldPreventLaterScopesAndSections(bool nested)
    {
        SchemaValidationTest.AssertPipelineStops(Document(Config()), "inbound", nested, context =>
        {
            context.Services.Register(Metadata());
            context.Request.Url = new MockUrl(new Uri("https://contoso.example/odata/Products?$top=-1"));
        });
    }

    private static ValidateOdataRequestConfig Config() => new()
    {
        ErrorVariableName = "errors",
        DefaultOdataVersion = "4.0",
        MinOdataVersion = "4.0",
        MaxOdataVersion = "4.01",
        MaxSize = 1024,
    };

    private static ValidationTestDocument Document(ValidateOdataRequestConfig config) => new()
    {
        InboundAction = context =>
        {
            context.ValidateOdataRequest(config);
            context.SetVariable("after", true);
        },
        BackendAction = context => context.SetVariable("later-section", true),
        OutboundAction = context => context.SetVariable("later-section", true),
        OnErrorAction = context => context.SetVariable("later-section", true),
    };

    private static OdataValidationMetadata Metadata() => new()
    {
        ServiceRoot = new Uri("https://contoso.example/odata/"),
        EntitySets = new Dictionary<string, OdataEntitySetMetadata>
        {
            ["Products"] = new()
            {
                KeyProperty = "id",
                Properties = new Dictionary<string, ApiParameterValidationMetadata>
                {
                    ["id"] = SchemaValidationTest.Parameter("""{"type":"integer","minimum":1}"""),
                    ["name"] = SchemaValidationTest.Parameter("""{"type":"string","minLength":1}"""),
                },
                PayloadSchema = new ContentValidationSchema("json", SchemaValidationTest.JsonSchema),
            },
        },
    };

    private static TestDocument Create(ValidateOdataRequestConfig? config = null)
    {
        var test = SchemaValidationTest.Create(Document(config ?? Config()));
        test.Context.Services.Register(Metadata());
        test.Context.Request.Url = new MockUrl(new Uri("https://contoso.example/odata/Products"));
        return test;
    }

    private static TestDocument CreateStringKey(string expectedKey)
    {
        var test = Create();
        var metadata = Metadata();
        test.Context.Services.Register(metadata with
        {
            EntitySets = new Dictionary<string, OdataEntitySetMetadata>
            {
                ["Products"] = metadata.EntitySets["Products"] with
                {
                    Properties = new Dictionary<string, ApiParameterValidationMetadata>
                    {
                        ["id"] = SchemaValidationTest.Parameter(
                            "{\"type\":\"string\",\"const\":" + JsonSerializer.Serialize(expectedKey) + "}"),
                        ["name"] = SchemaValidationTest.Parameter("""{"type":"string"}"""),
                    },
                },
            },
        });
        return test;
    }

    private sealed class TestValidator(Func<OdataValidationRequest, IReadOnlyList<string>> validate) : IOdataRequestValidator
    {
        public int Calls { get; private set; }
        public OdataValidationRequest? Request { get; private set; }

        public IReadOnlyList<string> Validate(OdataValidationRequest request)
        {
            Calls++;
            Request = request;
            return validate(request);
        }
    }
}