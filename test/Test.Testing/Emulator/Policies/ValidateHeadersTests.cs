// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring.Expressions;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Document;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Services;

namespace Test.Emulator.Emulator.Policies;

[TestClass]
public class ValidateHeadersTests
{
    [TestMethod]
    [DataRow("outbound")]
    [DataRow("on-error")]
    public void ShouldValidateResponseHeadersInEveryAuthoredSection(string section)
    {
        var test = Create();
        test.Context.Response.Headers = new Dictionary<string, string[]>
        {
            ["x-limit"] = ["3"], ["X-Unrelated"] = ["preserved"], ["X-Tags"] = ["red", "green"],
        };
        test.Context.Request.Headers["X-Limit"] = ["not validated"];
        var headers = test.Context.Response.Headers;
        var body = test.Context.Response.Body;

        SchemaValidationTest.Run(test, section);

        SchemaValidationTest.Errors(test.Context).Should().BeEmpty();
        test.Context.Response.Headers.Should().BeSameAs(headers);
        test.Context.Response.Body.Should().BeSameAs(body);
        body.Content.Should().Be("backend-secret");
        body.Consumed.Should().BeFalse();
        test.Context.Variables["after"].Should().Be(true);
        test.Context.Request.Headers["X-Limit"].Should().Equal("not validated");
        SchemaValidationTest.AssertPreserved(test.Context);
    }

    [TestMethod]
    [DataRow("outbound", "0")]
    [DataRow("outbound", "6")]
    [DataRow("outbound", "invalid")]
    [DataRow("on-error", "")]
    public void ShouldRejectValuesThatDoNotConformToTheResponseSchema(string section, string value)
    {
        var test = Create();
        test.Context.Response.Headers["X-Limit"] = [value];

        SchemaValidationTest.Run(test, section);

        SchemaValidationTest.AssertRejected(test.Context, 502, "validate-headers", section);
        SchemaValidationTest.Errors(test.Context).Should().Contain(error =>
            error.Name == "X-Limit" && error.Type == "ResponseHeader" &&
            error.ValidationRule == "IncorrectMessage" && error.Action == "prevent");
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void ShouldEnforceRequiredHeadersAndScalarCardinality(bool missing)
    {
        var test = Create();
        if (missing) test.Context.Response.Headers.Remove("X-Limit");
        else test.Context.Response.Headers["X-Limit"] = ["1", "2"];

        test.RunOutbound();

        SchemaValidationTest.AssertRejected(test.Context, 502, "validate-headers", "outbound");
        SchemaValidationTest.Errors(test.Context).Should().Contain(error => error.Name == "X-Limit");
    }

    [TestMethod]
    [DataRow("prevent", true, 1)]
    [DataRow("detect", false, 1)]
    [DataRow("ignore", false, 0)]
    public void ShouldApplyUnspecifiedHeaderActions(string action, bool rejected, int errors)
    {
        var test = Create(Config() with { UnspecifiedHeaderAction = action });
        test.Context.Response.Headers["X-Unknown"] = ["backend-secret"];

        test.RunOutbound();

        SchemaValidationTest.Errors(test.Context).Should().HaveCount(errors);
        if (rejected)
        {
            SchemaValidationTest.AssertRejected(test.Context, 502, "validate-headers", "outbound");
        }
        else
        {
            test.Context.Variables["after"].Should().Be(true);
            test.Context.Response.Headers["X-Unknown"].Should().Equal("backend-secret");
            SchemaValidationTest.AssertPreserved(test.Context);
        }
    }

    [TestMethod]
    public void ShouldUseNamedOverridesWithoutTreatingThemAsSchemaDeclarations()
    {
        var config = Config() with
        {
            Headers = [new ValidateHeader { Name = "x-unknown", Action = "prevent" }],
        };
        var test = Create(config);
        test.Context.Response.Headers["X-Unknown"] = ["anything"];

        test.RunOutbound();

        SchemaValidationTest.AssertRejected(test.Context, 502, "validate-headers", "outbound");
        SchemaValidationTest.Errors(test.Context).Should().ContainSingle()
            .Which.ValidationRule.Should().Be("Unspecified");
    }

    [TestMethod]
    public void ShouldLetChildActionsOverrideBothSpecifiedAndUnspecifiedDefaults()
    {
        var config = Config() with
        {
            UnspecifiedHeaderAction = "prevent",
            Headers =
            [
                new ValidateHeader { Name = "x-limit", Action = "detect" },
                new ValidateHeader { Name = "X-Unknown", Action = "ignore" },
            ],
        };
        var test = Create(config);
        var traces = new List<string>();
        test.Context.Trace = traces.Add;
        test.Context.Response.Headers["X-Limit"] = ["bad"];
        test.Context.Response.Headers["X-Unknown"] = ["preserved"];

        test.RunOutbound();

        SchemaValidationTest.Errors(test.Context).Should().ContainSingle()
            .Which.Should().Match<SchemaValidationError>(error => error.Name == "X-Limit" && error.Action == "detect");
        traces.Should().Contain(message => message.Contains("validate-headers"));
        test.Context.Response.Body.Content.Should().Be("backend-secret");
        test.Context.Variables["after"].Should().Be(true);
        SchemaValidationTest.AssertPreserved(test.Context);
    }

    [TestMethod]
    public void ShouldSelectResponseMetadataByStatusAndHonorTheDefaultResponse()
    {
        var test = Create();
        var metadata = SchemaValidationTest.Metadata();
        test.Context.Services.Register(metadata with
        {
            DefaultResponse = new ApiResponseValidationMetadata
            {
                Headers = new Dictionary<string, ApiParameterValidationMetadata>
                {
                    ["X-Limit"] = SchemaValidationTest.Parameter("""{"type":"string","enum":["fallback"]}""", true),
                },
            },
        });
        test.Context.Response.StatusCode = 201;
        test.Context.Response.Headers["X-Limit"] = ["fallback"];

        test.RunOutbound();

        SchemaValidationTest.Errors(test.Context).Should().BeEmpty();
        test.Context.Response.StatusCode.Should().Be(201);
        test.Context.Variables["after"].Should().Be(true);
    }

    [TestMethod]
    public void ShouldUseEvaluatedExpressionValuesForParentAndChildActions()
    {
        var test = SchemaValidationTest.Create(Document(context => Config() with
        {
            SpecifiedHeaderAction = (string)context.Variables["specified"],
            UnspecifiedHeaderAction = (string)context.Variables["unspecified"],
            Headers = [new ValidateHeader { Name = "X-Override", Action = (string)context.Variables["override"] }],
        }));
        test.Context.Services.Register(SchemaValidationTest.Metadata());
        test.Context.Variables["specified"] = "detect";
        test.Context.Variables["unspecified"] = "detect";
        test.Context.Variables["override"] = "ignore";
        test.Context.Response.Headers["X-Limit"] = ["6"];
        test.Context.Response.Headers["X-Unknown"] = ["any"];
        test.Context.Response.Headers["X-Override"] = ["any"];

        test.RunOutbound();

        SchemaValidationTest.Errors(test.Context).Select(error => error.Name)
            .Should().BeEquivalentTo(new[] { "X-Limit", "X-Unknown" });
        test.Context.Variables["after"].Should().Be(true);
        SchemaValidationTest.AssertPreserved(test.Context);
    }

    [TestMethod]
    public void ShouldExplicitlyRequireApiMetadataForDetectAsWellAsPrevent()
    {
        var test = SchemaValidationTest.Create(Document(_ => Config() with { SpecifiedHeaderAction = "detect" }));

        SchemaValidationTest.AssertUnsupported(() => test.RunOutbound(), "ApiValidationMetadata");
        SchemaValidationTest.AssertPreserved(test.Context);
    }

    [TestMethod]
    public void ShouldNotRequireMetadataWhenAllValidationIsExplicitlyIgnored()
    {
        var test = SchemaValidationTest.Create(Document(_ => Config() with
        {
            SpecifiedHeaderAction = "ignore", UnspecifiedHeaderAction = "ignore",
        }));

        test.RunOutbound();

        SchemaValidationTest.Errors(test.Context).Should().BeEmpty();
        test.Context.Variables["after"].Should().Be(true);
        SchemaValidationTest.AssertPreserved(test.Context);
    }

    [TestMethod]
    [DataRow("specified")]
    [DataRow("unspecified")]
    [DataRow("child")]
    [DataRow("duplicate")]
    public void ShouldSurfaceInvalidActionsAndCaseInsensitiveDuplicateOverrides(string invalid)
    {
        var config = invalid switch
        {
            "specified" => Config() with { SpecifiedHeaderAction = "allow" },
            "unspecified" => Config() with { UnspecifiedHeaderAction = "deny" },
            "child" => Config() with { Headers = [new ValidateHeader { Name = "X-Limit", Action = "bad" }] },
            _ => Config() with
            {
                Headers =
                [
                    new ValidateHeader { Name = "X-Limit", Action = "ignore" },
                    new ValidateHeader { Name = "x-limit", Action = "detect" },
                ],
            },
        };
        var test = Create(config);

        var error = Assert.ThrowsException<PolicyException>(() => test.RunOutbound());

        error.InnerException.Should().BeOfType<ArgumentException>();
        SchemaValidationTest.AssertPreserved(test.Context);
    }

    [TestMethod]
    [DataRow("outbound", false)]
    [DataRow("on-error", false)]
    [DataRow("outbound", true)]
    [DataRow("on-error", true)]
    public void ShouldSupportPredicateCallbacksAndErrorSimulation(string section, bool terminate)
    {
        var test = SchemaValidationTest.Create(Document(_ => Config()));
        var seen = false;
        Action<GatewayContext, ValidateHeadersConfig> callback = (context, _) =>
        {
            seen = true;
            if (terminate)
            {
                context.Response.StatusCode = 409;
                throw new FinishSectionProcessingException();
            }
        };
        if (section == "outbound")
        {
            test.SetupOutbound().ValidateHeaders((_, config) => config.ErrorsVariableName == "wrong")
                .WithCallback((_, _) => Assert.Fail("Wrong predicate."));
            test.SetupOutbound().ValidateHeaders().WithCallback(callback);
        }
        else
        {
            test.SetupOnError().ValidateHeaders((_, config) => config.ErrorsVariableName == "wrong")
                .WithCallback((_, _) => Assert.Fail("Wrong predicate."));
            test.SetupOnError().ValidateHeaders().WithCallback(callback);
        }

        SchemaValidationTest.Run(test, section);

        seen.Should().BeTrue();
        test.Context.ResponseTerminated.Should().Be(terminate);
        test.Context.Variables.ContainsKey("after").Should().Be(!terminate);
    }

    [TestMethod]
    [DataRow("outbound", false)]
    [DataRow("on-error", false)]
    [DataRow("outbound", true)]
    [DataRow("on-error", true)]
    public void ShouldPreventLaterScopesAndSections(string section, bool nested)
    {
        SchemaValidationTest.AssertPipelineStops(Document(_ => Config()), section, nested, context =>
        {
            context.Services.Register(SchemaValidationTest.Metadata());
            context.Response.Headers["X-Limit"] = ["bad"];
        });
    }

    private static ValidateHeadersConfig Config() => new()
    {
        SpecifiedHeaderAction = "prevent", UnspecifiedHeaderAction = "ignore", ErrorsVariableName = "errors",
    };

    private static ValidationTestDocument Document(Func<IExpressionContext, ValidateHeadersConfig> config) => new()
    {
        OutboundAction = context =>
        {
            context.ValidateHeaders(config(context.ExpressionContext));
            context.SetVariable("after", true);
        },
        OnErrorAction = context =>
        {
            context.ValidateHeaders(config(context.ExpressionContext));
            context.SetVariable("after", true);
        },
        BackendAction = context => context.SetVariable("later-section", true),
    };

    private static TestDocument Create(ValidateHeadersConfig? config = null)
    {
        var test = SchemaValidationTest.Create(Document(_ => config ?? Config()));
        test.Context.Services.Register(SchemaValidationTest.Metadata());
        test.Context.Response.Headers["X-Limit"] = ["3"];
        return test;
    }
}
