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
public class ValidateStatusCodeTests
{
    [TestMethod]
    [DataRow("outbound")]
    [DataRow("on-error")]
    public void ShouldAcceptSchemaSpecifiedStatusEvenWhenAPolicyOverrideSaysPrevent(string section)
    {
        var test = Create(Config() with
        {
            StatusCodes = [new ValidateStatusCode { Code = 200, Action = "prevent" }],
        });

        SchemaValidationTest.Run(test, section);

        SchemaValidationTest.Errors(test.Context).Should().BeEmpty();
        test.Context.Variables["after"].Should().Be(true);
        test.Context.Response.Body.Content.Should().Be("backend-secret");
        SchemaValidationTest.AssertPreserved(test.Context);
    }

    [TestMethod]
    [DataRow("outbound")]
    [DataRow("on-error")]
    public void ShouldPreventUnspecifiedBackendStatusWithoutLeakingTheBackendResponse(string section)
    {
        var test = Create();
        test.Context.Response.StatusCode = 503;
        test.Context.Response.Headers["Set-Cookie"] = ["backend-secret"];

        SchemaValidationTest.Run(test, section);

        SchemaValidationTest.AssertRejected(test.Context, 502, "validate-status-code", section);
        SchemaValidationTest.Errors(test.Context).Should().ContainSingle()
            .Which.Should().Be(new SchemaValidationError("503", "StatusCode", "Unspecified",
                "Response status code 503 is not allowed by the API schema.", "prevent"));
    }

    [TestMethod]
    [DataRow("detect", 1)]
    [DataRow("ignore", 0)]
    public void ShouldApplyUnspecifiedStatusActionsWithoutMutatingDetectedResponses(string action, int errors)
    {
        var test = Create(Config() with { UnspecifiedStatusCodeAction = action });
        var traces = new List<string>();
        test.Context.Trace = traces.Add;
        test.Context.Response.StatusCode = 503;

        test.RunOutbound();

        SchemaValidationTest.Errors(test.Context).Should().HaveCount(errors);
        test.Context.Variables["after"].Should().Be(true);
        test.Context.Response.StatusCode.Should().Be(503);
        test.Context.Response.Body.Content.Should().Be("backend-secret");
        test.Context.Response.Headers["X-Unrelated"].Should().Equal("preserved");
        test.Context.LastError.Source.Should().Be("original");
        test.Context.ResponseTerminated.Should().BeFalse();
        if (action == "detect") traces.Should().Contain(message => message.Contains("503"));
    }

    [TestMethod]
    public void ShouldOverrideTheActionOnlyForUnspecifiedCodes()
    {
        var config = Config() with
        {
            StatusCodes =
            [
                new ValidateStatusCode { Code = 200, Action = "prevent" },
                new ValidateStatusCode { Code = 503, Action = "detect" },
            ],
        };
        var test = Create(config);
        test.Context.Response.StatusCode = 503;

        test.RunOutbound();

        SchemaValidationTest.Errors(test.Context).Should().ContainSingle().Which.Action.Should().Be("detect");
        test.Context.Response.StatusCode.Should().Be(503);
        test.Context.Variables["after"].Should().Be(true);
        test.Context.ResponseTerminated.Should().BeFalse();
    }

    [TestMethod]
    public void ShouldHonorTheApiDefaultResponse()
    {
        var test = Create();
        test.Context.Services.Register(SchemaValidationTest.Metadata() with
        {
            DefaultResponse = new ApiResponseValidationMetadata(),
        });
        test.Context.Response.StatusCode = 503;

        test.RunOutbound();

        SchemaValidationTest.Errors(test.Context).Should().BeEmpty();
        test.Context.Response.StatusCode.Should().Be(503);
        test.Context.Variables["after"].Should().Be(true);
    }

    [TestMethod]
    public void ShouldRequireApiMetadataRatherThanTreatingPolicyCodesAsApiDeclarations()
    {
        var test = SchemaValidationTest.Create(Document(_ => Config() with
        {
            UnspecifiedStatusCodeAction = "detect",
            StatusCodes = [new ValidateStatusCode { Code = 200, Action = "prevent" }],
        }));

        SchemaValidationTest.AssertUnsupported(() => test.RunOutbound(), "ApiValidationMetadata");
        SchemaValidationTest.AssertPreserved(test.Context);
    }

    [TestMethod]
    public void ShouldSkipExplicitlyIgnoredActualStatusesWithoutMetadata()
    {
        var test = SchemaValidationTest.Create(Document(_ => Config() with
        {
            StatusCodes = [new ValidateStatusCode { Code = 503, Action = "ignore" }],
        }));
        test.Context.Response.StatusCode = 503;

        test.RunOutbound();

        SchemaValidationTest.Errors(test.Context).Should().BeEmpty();
        test.Context.Variables["after"].Should().Be(true);
        test.Context.Response.StatusCode.Should().Be(503);
    }

    [TestMethod]
    [DataRow(0u)]
    [DataRow(99u)]
    [DataRow(600u)]
    [DataRow(uint.MaxValue)]
    public void ShouldRejectInvalidConfiguredStatusCodes(uint code)
    {
        var test = Create(Config() with { StatusCodes = [new ValidateStatusCode { Code = code, Action = "ignore" }] });

        var error = Assert.ThrowsException<PolicyException>(() => test.RunOutbound());

        error.InnerException.Should().BeOfType<ArgumentOutOfRangeException>();
        SchemaValidationTest.AssertPreserved(test.Context);
    }

    [TestMethod]
    [DataRow("default")]
    [DataRow("override")]
    [DataRow("duplicate")]
    public void ShouldSurfaceInvalidActionsAndDuplicateOverrides(string invalid)
    {
        var config = invalid switch
        {
            "default" => Config() with { UnspecifiedStatusCodeAction = "allow" },
            "override" => Config() with { StatusCodes = [new ValidateStatusCode { Code = 200, Action = "deny" }] },
            _ => Config() with
            {
                StatusCodes =
                [
                    new ValidateStatusCode { Code = 200, Action = "ignore" },
                    new ValidateStatusCode { Code = 200, Action = "detect" },
                ],
            },
        };
        var test = Create(config);

        var error = Assert.ThrowsException<PolicyException>(() => test.RunOutbound());

        error.InnerException.Should().BeOfType<ArgumentException>();
        SchemaValidationTest.AssertPreserved(test.Context);
    }

    [TestMethod]
    public void ShouldEvaluateDefaultAndOverrideActionExpressions()
    {
        var test = SchemaValidationTest.Create(Document(context => Config() with
        {
            UnspecifiedStatusCodeAction = (string)context.Variables["defaultAction"],
            StatusCodes = [new ValidateStatusCode { Code = 503, Action = (string)context.Variables["overrideAction"] }],
        }));
        test.Context.Services.Register(SchemaValidationTest.Metadata());
        test.Context.Variables["defaultAction"] = "prevent";
        test.Context.Variables["overrideAction"] = "detect";
        test.Context.Response.StatusCode = 503;

        test.RunOutbound();

        SchemaValidationTest.Errors(test.Context).Should().ContainSingle().Which.Action.Should().Be("detect");
        test.Context.Response.StatusCode.Should().Be(503);
        test.Context.Variables["after"].Should().Be(true);
    }

    [TestMethod]
    [DataRow("outbound", false)]
    [DataRow("on-error", false)]
    [DataRow("outbound", true)]
    [DataRow("on-error", true)]
    public void ShouldSupportPredicateCallbacksAndCallbackTermination(string section, bool terminate)
    {
        var test = SchemaValidationTest.Create(Document(_ => Config()));
        var seen = false;
        Action<GatewayContext, ValidateStatusCodeConfig> callback = (context, _) =>
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
            test.SetupOutbound().ValidateStatusCode((_, config) => config.ErrorVariableName == "wrong")
                .WithCallback((_, _) => Assert.Fail("Wrong predicate."));
            test.SetupOutbound().ValidateStatusCode().WithCallback(callback);
        }
        else
        {
            test.SetupOnError().ValidateStatusCode((_, config) => config.ErrorVariableName == "wrong")
                .WithCallback((_, _) => Assert.Fail("Wrong predicate."));
            test.SetupOnError().ValidateStatusCode().WithCallback(callback);
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
            context.Response.StatusCode = 503;
        });
    }

    private static ValidateStatusCodeConfig Config() => new()
    {
        UnspecifiedStatusCodeAction = "prevent", ErrorVariableName = "errors",
    };

    private static ValidationTestDocument Document(Func<IExpressionContext, ValidateStatusCodeConfig> config) => new()
    {
        OutboundAction = context =>
        {
            context.ValidateStatusCode(config(context.ExpressionContext));
            context.SetVariable("after", true);
        },
        OnErrorAction = context =>
        {
            context.ValidateStatusCode(config(context.ExpressionContext));
            context.SetVariable("after", true);
        },
        BackendAction = context => context.SetVariable("later-section", true),
    };

    private static TestDocument Create(ValidateStatusCodeConfig? config = null)
    {
        var test = SchemaValidationTest.Create(Document(_ => config ?? Config()));
        test.Context.Services.Register(SchemaValidationTest.Metadata());
        return test;
    }
}
