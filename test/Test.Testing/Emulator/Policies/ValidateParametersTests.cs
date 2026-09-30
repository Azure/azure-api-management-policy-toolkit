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
public class ValidateParametersTests
{
    [TestMethod]
    public void ShouldValidateHeadersDecodedQueryArraysAndMatchedPathValuesWithoutMutation()
    {
        var test = Create();
        test.Context.Request.Headers = new Dictionary<string, string[]> { ["x-limit"] = ["3"] };
        test.Context.Request.Url.QueryString = "?count=2&tags=red&tags=green";
        test.Context.Request.Body.Content = "unrelated body";
        var headers = test.Context.Request.Headers;
        var query = test.Context.Request.Url.Query;
        var path = test.Context.Request.MatchedParameters;

        test.RunInbound();

        SchemaValidationTest.Errors(test.Context).Should().BeEmpty();
        test.Context.Request.Headers.Should().BeSameAs(headers);
        test.Context.Request.Url.Query.Should().BeSameAs(query);
        query["tags"].Should().Equal("red", "green");
        test.Context.Request.MatchedParameters.Should().BeSameAs(path);
        test.Context.Request.Body.Content.Should().Be("unrelated body");
        test.Context.Request.Body.Consumed.Should().BeFalse();
        test.Context.Variables["after"].Should().Be(true);
        SchemaValidationTest.AssertPreserved(test.Context);
    }

    [TestMethod]
    [DataRow("header", "0")]
    [DataRow("header", "6")]
    [DataRow("header", "invalid")]
    [DataRow("query", "0")]
    [DataRow("query", "6")]
    [DataRow("query", "invalid")]
    [DataRow("path", "0")]
    [DataRow("path", "invalid")]
    public void ShouldRejectValuesThatDoNotConformToTheDeclaredTypeAndBounds(string location, string value)
    {
        var test = Create();
        SetValue(test.Context, location, value);

        test.RunInbound();

        SchemaValidationTest.AssertRejected(test.Context, 400, "validate-parameters", "inbound");
        SchemaValidationTest.Errors(test.Context).Should().Contain(error =>
            error.Type == Type(location) && error.ValidationRule == "IncorrectMessage" && error.Action == "prevent");
    }

    [TestMethod]
    [DataRow("header", "1")]
    [DataRow("header", "5")]
    [DataRow("query", "1")]
    [DataRow("query", "5")]
    [DataRow("path", "1")]
    public void ShouldAcceptValuesAtTheExactDeclaredBounds(string location, string value)
    {
        var test = Create();
        SetValue(test.Context, location, value);

        test.RunInbound();

        SchemaValidationTest.Errors(test.Context).Should().BeEmpty();
        test.Context.Variables["after"].Should().Be(true);
        SchemaValidationTest.AssertPreserved(test.Context);
    }

    [TestMethod]
    public void ShouldExplicitlyRejectUnsupportedObjectParameterSerialization()
    {
        var test = Create();
        test.Context.Services.Register(SchemaValidationTest.Metadata() with
        {
            QueryParameters = new Dictionary<string, ApiParameterValidationMetadata>
            {
                ["count"] = SchemaValidationTest.Parameter("""{"type":"object","properties":{"id":{"type":"integer"}}}"""),
            },
        });
        test.Context.Request.Url.Query["count"] = ["""{"id":1}"""];

        SchemaValidationTest.AssertUnsupported(() => test.RunInbound(), "serialization");
        SchemaValidationTest.AssertPreserved(test.Context);
    }

    [TestMethod]
    [DataRow("header")]
    [DataRow("query")]
    [DataRow("path")]
    public void ShouldEnforceRequiredParametersInEveryLocation(string location)
    {
        var test = Create();
        switch (location)
        {
            case "header": test.Context.Request.Headers.Remove("X-Limit"); break;
            case "query": test.Context.Request.Url.Query.Remove("count"); break;
            default: test.Context.Request.MatchedParameters.Remove("id"); break;
        }

        test.RunInbound();

        SchemaValidationTest.AssertRejected(test.Context, 400, "validate-parameters", "inbound");
        SchemaValidationTest.Errors(test.Context).Should().Contain(error =>
            error.Type == Type(location) && error.ValidationRule == "Required");
    }

    [TestMethod]
    [DataRow("header")]
    [DataRow("query")]
    public void ShouldRejectMultipleScalarValues(string location)
    {
        var test = Create();
        if (location == "header") test.Context.Request.Headers["X-Limit"] = ["1", "2"];
        else test.Context.Request.Url.Query["count"] = ["1", "2"];

        test.RunInbound();

        SchemaValidationTest.AssertRejected(test.Context, 400, "validate-parameters", "inbound");
        SchemaValidationTest.Errors(test.Context).Should().Contain(error =>
            error.Type == Type(location) && error.ValidationRule == "IncorrectMessage");
    }

    [TestMethod]
    [DataRow("unknown")]
    [DataRow("duplicate")]
    [DataRow("empty")]
    public void ShouldValidateArrayItemsCardinalityAndUniqueness(string invalid)
    {
        var test = Create();
        test.Context.Request.Url.Query["tags"] = invalid switch
        {
            "unknown" => ["blue"],
            "duplicate" => ["red", "red"],
            _ => [],
        };

        test.RunInbound();

        SchemaValidationTest.AssertRejected(test.Context, 400, "validate-parameters", "inbound");
        SchemaValidationTest.Errors(test.Context).Should().Contain(error => error.Name == "tags");
    }

    [TestMethod]
    [DataRow("header", "prevent", true, 1)]
    [DataRow("query", "detect", false, 1)]
    [DataRow("path", "ignore", false, 0)]
    [DataRow("path", "prevent", true, 1)]
    public void ShouldApplyUnspecifiedRulesToEachParameterLocation(string location, string action,
        bool rejected, int errors)
    {
        var test = Create(Config() with { UnspecifiedParameterAction = action });
        switch (location)
        {
            case "header": test.Context.Request.Headers["extra"] = ["preserved"]; break;
            case "query": test.Context.Request.Url.Query["extra"] = ["preserved"]; break;
            default: test.Context.Request.MatchedParameters["extra"] = "preserved"; break;
        }

        test.RunInbound();

        SchemaValidationTest.Errors(test.Context).Should().HaveCount(errors);
        if (rejected)
        {
            SchemaValidationTest.AssertRejected(test.Context, 400, "validate-parameters", "inbound");
        }
        else
        {
            test.Context.Variables["after"].Should().Be(true);
            SchemaValidationTest.AssertPreserved(test.Context);
        }
    }

    [TestMethod]
    public void ShouldApplyGroupOverridesToSpecifiedAndUnspecifiedParameters()
    {
        var config = Config() with
        {
            UnspecifiedParameterAction = "prevent",
            Headers = new ValidateHeaderParameters
            {
                SpecifiedParameterAction = "detect", UnspecifiedParameterAction = "ignore",
            },
            Query = new ValidateQueryParameters
            {
                SpecifiedParameterAction = "ignore", UnspecifiedParameterAction = "detect",
            },
            Path = new ValidatePathParameters { SpecifiedParameterAction = "ignore" },
        };
        var test = Create(config);
        test.Context.Request.Headers["X-Limit"] = ["bad"];
        test.Context.Request.Headers["extra"] = ["ignored"];
        test.Context.Request.Url.Query["count"] = ["bad"];
        test.Context.Request.Url.Query["extra"] = ["detected"];
        test.Context.Request.MatchedParameters["id"] = "bad";

        test.RunInbound();

        SchemaValidationTest.Errors(test.Context).Select(error => error.Type)
            .Should().BeEquivalentTo(new[] { "RequestHeader", "QueryParameter" });
        SchemaValidationTest.Errors(test.Context).Should().OnlyContain(error => error.Action == "detect");
        test.Context.Variables["after"].Should().Be(true);
        SchemaValidationTest.AssertPreserved(test.Context);
    }

    [TestMethod]
    public void ShouldLetNamedActionsOverrideGroupDefaultsWithoutDeclaringSchemas()
    {
        var config = Config() with
        {
            Headers = new ValidateHeaderParameters
            {
                SpecifiedParameterAction = "prevent", UnspecifiedParameterAction = "prevent",
                Parameters = [new ValidateParameter { Name = "x-limit", Action = "ignore" }],
            },
            Query = new ValidateQueryParameters
            {
                SpecifiedParameterAction = "prevent", UnspecifiedParameterAction = "prevent",
                Parameters = [new ValidateParameter { Name = "count", Action = "ignore" }],
            },
            Path = new ValidatePathParameters
            {
                SpecifiedParameterAction = "prevent",
                Parameters = [new ValidateParameter { Name = "id", Action = "ignore" }],
            },
        };
        var test = Create(config);
        SetValue(test.Context, "header", "bad");
        SetValue(test.Context, "query", "bad");
        SetValue(test.Context, "path", "bad");

        test.RunInbound();

        SchemaValidationTest.Errors(test.Context).Should().BeEmpty();
        test.Context.Variables["after"].Should().Be(true);
        SchemaValidationTest.AssertPreserved(test.Context);

        var unknown = Create(Config() with
        {
            Query = new ValidateQueryParameters
            {
                SpecifiedParameterAction = "ignore", UnspecifiedParameterAction = "ignore",
                Parameters = [new ValidateParameter { Name = "extra", Action = "prevent" }],
            },
        });
        unknown.Context.Request.Url.Query["extra"] = ["any"];
        unknown.RunInbound();
        SchemaValidationTest.Errors(unknown.Context).Should().ContainSingle()
            .Which.ValidationRule.Should().Be("Unspecified");
    }

    [TestMethod]
    [DataRow("query")]
    [DataRow("path")]
    public void ShouldCompareQueryAndPathNamesOrdinallyEvenWithAnIgnoreCaseDictionary(string location)
    {
        var test = Create();
        if (location == "query")
        {
            test.Context.Request.Url.Query = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
            {
                ["COUNT"] = ["2"],
            };
        }
        else
        {
            test.Context.Request.MatchedParameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["ID"] = "2",
            };
        }

        test.RunInbound();

        SchemaValidationTest.AssertRejected(test.Context, 400, "validate-parameters", "inbound");
        SchemaValidationTest.Errors(test.Context).Should().Contain(error =>
            error.Type == Type(location) && error.ValidationRule == "Required");
    }

    [TestMethod]
    public void ShouldUseEveryAuthoredExpressionAllowedActionValue()
    {
        var test = SchemaValidationTest.Create(Document(context => new ValidateParametersConfig
        {
            ErrorsVariableName = "errors",
            SpecifiedParameterAction = (string)context.Variables["specified"],
            UnspecifiedParameterAction = (string)context.Variables["unspecified"],
            Headers = new ValidateHeaderParameters
            {
                SpecifiedParameterAction = (string)context.Variables["headerSpecified"],
                UnspecifiedParameterAction = (string)context.Variables["headerUnspecified"],
                Parameters = [new ValidateParameter { Name = "X-Override", Action = (string)context.Variables["headerOverride"] }],
            },
            Query = new ValidateQueryParameters
            {
                SpecifiedParameterAction = (string)context.Variables["querySpecified"],
                UnspecifiedParameterAction = (string)context.Variables["queryUnspecified"],
                Parameters = [new ValidateParameter { Name = "count", Action = (string)context.Variables["queryOverride"] }],
            },
            Path = new ValidatePathParameters
            {
                SpecifiedParameterAction = (string)context.Variables["pathSpecified"],
                Parameters = [new ValidateParameter { Name = "id", Action = (string)context.Variables["pathOverride"] }],
            },
        }));
        test.Context.Services.Register(SchemaValidationTest.Metadata());
        foreach (var name in new[] { "specified", "unspecified", "pathSpecified" }) test.Context.Variables[name] = "prevent";
        foreach (var name in new[] { "headerSpecified", "querySpecified", "queryUnspecified" }) test.Context.Variables[name] = "detect";
        foreach (var name in new[] { "headerUnspecified", "headerOverride", "queryOverride", "pathOverride" }) test.Context.Variables[name] = "ignore";
        SeedRequest(test.Context);
        test.Context.Request.Headers["X-Limit"] = ["bad"];
        test.Context.Request.Headers["X-Override"] = ["ignored"];
        test.Context.Request.Headers["extra"] = ["ignored"];
        test.Context.Request.Url.Query["count"] = ["bad"];
        test.Context.Request.Url.Query["extra"] = ["detected"];
        test.Context.Request.MatchedParameters["id"] = "bad";

        test.RunInbound();

        SchemaValidationTest.Errors(test.Context).Select(error => error.Type)
            .Should().BeEquivalentTo(new[] { "RequestHeader", "QueryParameter" });
        test.Context.Variables["after"].Should().Be(true);
        SchemaValidationTest.AssertPreserved(test.Context);
    }

    [TestMethod]
    public void ShouldRequireApiMetadataEvenWhenANamedRuleExists()
    {
        var test = SchemaValidationTest.Create(Document(_ => Config() with
        {
            SpecifiedParameterAction = "detect",
            Query = new ValidateQueryParameters
            {
                SpecifiedParameterAction = "ignore", UnspecifiedParameterAction = "ignore",
                Parameters = [new ValidateParameter { Name = "count", Action = "prevent" }],
            },
        }));
        SeedRequest(test.Context);

        SchemaValidationTest.AssertUnsupported(() => test.RunInbound(), "ApiValidationMetadata");
        SchemaValidationTest.AssertPreserved(test.Context);
    }

    [TestMethod]
    public void ShouldSkipAllIgnoredRulesWithoutMetadata()
    {
        var test = SchemaValidationTest.Create(Document(_ => Config() with
        {
            SpecifiedParameterAction = "ignore", UnspecifiedParameterAction = "ignore",
        }));

        test.RunInbound();

        SchemaValidationTest.Errors(test.Context).Should().BeEmpty();
        test.Context.Variables["after"].Should().Be(true);
        SchemaValidationTest.AssertPreserved(test.Context);
    }

    [TestMethod]
    [DataRow("root-specified")]
    [DataRow("root-unspecified")]
    [DataRow("header-specified")]
    [DataRow("header-unspecified")]
    [DataRow("query-specified")]
    [DataRow("query-unspecified")]
    [DataRow("path-specified")]
    [DataRow("header-child")]
    [DataRow("query-child")]
    [DataRow("path-child")]
    public void ShouldRejectUnknownActionsAtEveryConfigurationLevel(string location)
    {
        var config = location switch
        {
            "root-specified" => Config() with { SpecifiedParameterAction = "allow" },
            "root-unspecified" => Config() with { UnspecifiedParameterAction = "deny" },
            "header-specified" => Config() with
            {
                Headers = new() { SpecifiedParameterAction = "bad", UnspecifiedParameterAction = "ignore" },
            },
            "header-unspecified" => Config() with
            {
                Headers = new() { SpecifiedParameterAction = "ignore", UnspecifiedParameterAction = "bad" },
            },
            "query-specified" => Config() with
            {
                Query = new() { SpecifiedParameterAction = "bad", UnspecifiedParameterAction = "ignore" },
            },
            "query-unspecified" => Config() with
            {
                Query = new() { SpecifiedParameterAction = "ignore", UnspecifiedParameterAction = "bad" },
            },
            "path-specified" => Config() with { Path = new() { SpecifiedParameterAction = "bad" } },
            "header-child" => Config() with
            {
                Headers = new()
                {
                    SpecifiedParameterAction = "ignore", UnspecifiedParameterAction = "ignore",
                    Parameters = [new() { Name = "X-Limit", Action = "bad" }],
                },
            },
            "query-child" => Config() with
            {
                Query = new()
                {
                    SpecifiedParameterAction = "ignore", UnspecifiedParameterAction = "ignore",
                    Parameters = [new() { Name = "count", Action = "bad" }],
                },
            },
            _ => Config() with
            {
                Path = new()
                {
                    SpecifiedParameterAction = "ignore", Parameters = [new() { Name = "id", Action = "bad" }],
                },
            },
        };
        var test = Create(config);

        var error = Assert.ThrowsException<PolicyException>(() => test.RunInbound());

        error.InnerException.Should().BeOfType<ArgumentException>();
        SchemaValidationTest.AssertPreserved(test.Context);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void ShouldSupportPredicateCallbacksAndErrorSimulation(bool terminate)
    {
        var test = SchemaValidationTest.Create(Document(_ => Config()));
        var seen = false;
        test.SetupInbound().ValidateParameters((_, config) => config.ErrorsVariableName == "wrong")
            .WithCallback((_, _) => Assert.Fail("Wrong predicate."));
        test.SetupInbound().ValidateParameters().WithCallback((context, _) =>
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
        SchemaValidationTest.AssertPipelineStops(Document(_ => Config()), "inbound", nested, context =>
        {
            context.Services.Register(SchemaValidationTest.Metadata());
            SeedRequest(context);
            context.Request.Url.Query["count"] = ["bad"];
        });
    }

    private static ValidateParametersConfig Config() => new()
    {
        SpecifiedParameterAction = "prevent", UnspecifiedParameterAction = "ignore", ErrorsVariableName = "errors",
    };

    private static ValidationTestDocument Document(Func<IExpressionContext, ValidateParametersConfig> config) => new()
    {
        InboundAction = context =>
        {
            context.ValidateParameters(config(context.ExpressionContext));
            context.SetVariable("after", true);
        },
        BackendAction = context => context.SetVariable("later-section", true),
        OutboundAction = context => context.SetVariable("later-section", true),
        OnErrorAction = context => context.SetVariable("later-section", true),
    };

    private static TestDocument Create(ValidateParametersConfig? config = null)
    {
        var test = SchemaValidationTest.Create(Document(_ => config ?? Config()));
        test.Context.Services.Register(SchemaValidationTest.Metadata());
        SeedRequest(test.Context);
        return test;
    }

    private static void SeedRequest(GatewayContext context)
    {
        context.Request.Headers["X-Limit"] = ["3"];
        context.Request.Url.Query["count"] = ["2"];
        context.Request.MatchedParameters["id"] = "4";
    }

    private static void SetValue(GatewayContext context, string location, string value)
    {
        switch (location)
        {
            case "header": context.Request.Headers["X-Limit"] = [value]; break;
            case "query": context.Request.Url.Query["count"] = [value]; break;
            default: context.Request.MatchedParameters["id"] = value; break;
        }
    }

    private static string Type(string location) => location switch
    {
        "header" => "RequestHeader",
        "query" => "QueryParameter",
        _ => "PathParameter",
    };
}
