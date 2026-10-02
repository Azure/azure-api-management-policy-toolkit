// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Document;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator;

using Newtonsoft.Json.Linq;

namespace Test.Emulator.Emulator.Policies;

[TestClass]
public class CheckHeaderTests
{
    class SimpleCheckHeader : IDocument
    {
        public void Inbound(IInboundContext context)
        {
            context.CheckHeader(new CheckHeaderConfig()
            {
                Name = "Test",
                FailCheckHttpCode = 401,
                FailCheckErrorMessage = "Request do not contain test header",
                IgnoreCase = false,
                Values = []
            });
        }
    }

    [TestMethod]
    public void CheckHeader_Callback()
    {
        var test = new SimpleCheckHeader().AsTestDocument();
        var executedCallback = false;
        test.SetupInbound().CheckHeader().WithCallback((_, _) =>
        {
            executedCallback = true;
        });

        test.RunInbound();

        executedCallback.Should().BeTrue();
    }

    [TestMethod]
    public void CheckHeader_PassExistenceCheck()
    {
        var test = new SimpleCheckHeader().AsTestDocument();
        test.Context.Request.Headers["Test"] = ["test"];

        test.RunInbound();

        var response = test.Context.Response;
        response.StatusCode.Should().NotBe(401);
    }

    [TestMethod]
    public void CheckHeader_FailExistenceCheck()
    {
        var test = new SimpleCheckHeader().AsTestDocument();

        test.RunInbound();

        var response = test.Context.Response;
        response.StatusCode.Should().Be(401);
        response.Headers.Should().ContainKey("Content-Type")
            .WhoseValue.Should().ContainSingle()
            .Which.Should().Be("application/json");
        response.Body.Content.Should().NotBeNullOrWhiteSpace();
        var body = response.Body.As<JObject>();
        body.Should().ContainKey("statusCode")
            .WhoseValue.Should().NotBeNull().And
            .Subject.Value<int>().Should().Be(401);
        body.Should().ContainKey("message")
            .WhoseValue.Should().NotBeNull().And
            .Subject.Value<string>().Should().Be("Request do not contain test header");
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(1)]
    [DataRow(3)]
    public void CheckHeader_ExistenceOnlyAcceptsAnyNumberOfValues(int count)
    {
        var test = CreateTest(DefaultConfig());
        test.Context.Request.Headers["test"] = Enumerable.Range(0, count)
            .Select(index => $"value-{index}").ToArray();
        test.Context.Response.Headers["X-Preserved"] = ["original"];
        test.Context.Response.Body.Content = "original";

        test.RunInbound();

        test.Context.Response.StatusCode.Should().Be(200);
        test.Context.Response.Headers["X-Preserved"].Should().Equal("original");
        test.Context.Response.Body.Content.Should().Be("original");
        test.Context.Variables.Should().ContainKey("after-check");
        test.Context.ResponseTerminated.Should().BeFalse();
    }

    [TestMethod]
    [DataRow("Default")]
    [DataRow("Ordinal")]
    [DataRow("OrdinalIgnoreCase")]
    [DataRow("InvariantCultureIgnoreCase")]
    public void CheckHeader_HeaderNamesAreCaseInsensitiveRegardlessOfInjectedComparer(string comparer)
    {
        var test = CreateTest(DefaultConfig() with { Name = "TEST" });
        var values = new[] { "first", "second" };
        var source = new Dictionary<string, string[]>(HeaderComparer(comparer))
        {
            ["test"] = values,
            ["X-Keep"] = ["keep"]
        };
        test.Context.Request.Headers = source;
        var originalComparer = source.Comparer;

        test.RunInbound();

        test.Context.Variables.Should().ContainKey("after-check");
        test.Context.ResponseTerminated.Should().BeFalse();
        test.Context.Request.Headers.Should().BeSameAs(source);
        source.Comparer.Should().BeSameAs(originalComparer);
        source.Keys.Should().Equal("test", "X-Keep");
        source["test"].Should().BeSameAs(values).And.Equal("first", "second");
        source["X-Keep"].Should().Equal("keep");
    }

    [TestMethod]
    [DataRow(false, "expected", true)]
    [DataRow(false, "EXPECTED", false)]
    [DataRow(true, "EXPECTED", true)]
    [DataRow(true, "unexpected", false)]
    [DataRow(true, "expected suffix", false)]
    [DataRow(false, "", false)]
    public void CheckHeader_AnyExactValueMatchUsesConfiguredCaseSensitivity(
        bool ignoreCase, string value, bool pass)
    {
        var test = CreateTest(DefaultConfig() with
        {
            IgnoreCase = ignoreCase,
            Values = ["allowed", "expected"]
        });
        test.Context.Request.Headers["test"] = ["other", value, "last"];

        test.RunInbound();

        test.Context.Response.StatusCode.Should().Be(pass ? 200 : 401);
        test.Context.ResponseTerminated.Should().Be(!pass);
        test.Context.Variables.ContainsKey("after-check").Should().Be(pass);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void CheckHeader_ValueComparisonDoesNotUseCultureOrUnicodeNormalization(bool ignoreCase)
    {
        var test = CreateTest(DefaultConfig() with
        {
            IgnoreCase = ignoreCase,
            Values = ["caf\u00E9"]
        });
        test.Context.Request.Headers["Test"] = ["cafe\u0301"];

        test.RunInbound();

        test.Context.Response.StatusCode.Should().Be(401);
        test.Context.ResponseTerminated.Should().BeTrue();
        test.Context.Variables.Should().NotContainKey("after-check");
    }

    [TestMethod]
    public void CheckHeader_EmptyHeaderValuesDoNotMatchConfiguredValues()
    {
        var test = CreateTest(DefaultConfig() with { Values = ["expected"] });
        test.Context.Request.Headers["Test"] = [];

        test.RunInbound();

        test.Context.Response.StatusCode.Should().Be(401);
        test.Context.ResponseTerminated.Should().BeTrue();
        test.Context.Variables.Should().NotContainKey("after-check");
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void CheckHeader_CombinesInjectedCaseVariantEntriesWithoutChangingStorage(bool pass)
    {
        var test = CreateTest(DefaultConfig() with
        {
            Name = "tEsT",
            IgnoreCase = true,
            Values = ["expected"]
        });
        var upperValues = new[] { "other" };
        var lowerValues = new[] { pass ? "EXPECTED" : "different", "last" };
        var source = new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["Test"] = upperValues,
            ["test"] = lowerValues,
            ["TEST"] = [],
            ["X-Keep"] = ["keep"]
        };
        test.Context.Request.Headers = source;

        test.RunInbound();

        test.Context.Response.StatusCode.Should().Be(pass ? 200 : 401);
        test.Context.ResponseTerminated.Should().Be(!pass);
        test.Context.Variables.ContainsKey("after-check").Should().Be(pass);
        test.Context.Request.Headers.Should().BeSameAs(source);
        source.Comparer.Should().BeSameAs(StringComparer.Ordinal);
        source.Keys.Should().Equal("Test", "test", "TEST", "X-Keep");
        source["Test"].Should().BeSameAs(upperValues).And.Equal("other");
        source["test"].Should().BeSameAs(lowerValues).And.Equal(lowerValues);
        source["TEST"].Should().BeEmpty();
    }

    [TestMethod]
    [DataRow("quoted \"value\"")]
    [DataRow("C:\\temp\\file")]
    [DataRow("\r\n\t\b\f\0")]
    [DataRow("\", \"injected\": true, \"message\": \"")]
    public void CheckHeader_FailureMessageIsSafelySerializedAsJson(string message)
    {
        var test = CreateTest(DefaultConfig() with { FailCheckErrorMessage = message });
        var response = test.Context.Response;
        var headers = response.Headers;
        response.StatusCode = 502;
        response.Headers["X-Upstream"] = ["stale"];
        response.Body.Content = "stale";

        test.RunInbound();

        test.Context.Response.Should().BeSameAs(response);
        response.Headers.Should().BeSameAs(headers).And.HaveCount(1);
        response.Headers["content-type"].Should().Equal("application/json");
        response.StatusCode.Should().Be(401);
        var body = response.Body.As<JObject>();
        body.Properties().Should().HaveCount(2);
        body.Value<int>("statusCode").Should().Be(401);
        body.Value<string>("message").Should().Be(message);
        test.Context.ResponseTerminated.Should().BeTrue();
        test.Context.Variables.Should().NotContainKey("after-check");
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void CheckHeader_InvokesOnlyTheFirstMatchingSuccessOrFailureHook(bool pass)
    {
        var test = CreateTest(DefaultConfig() with { Values = ["expected"] });
        test.Context.Request.Headers["Test"] = [pass ? "expected" : "other"];
        var calls = new List<string>();
        test.SetupInbound().CheckHeader((_, config) => config.Name == "Other")
            .OnCheckPass((_, _) => calls.Add("unmatched-pass"));
        test.SetupInbound().CheckHeader((_, config) => config.Name == "Other")
            .OnCheckFail((_, _) => calls.Add("unmatched-fail"));
        test.SetupInbound().CheckHeader((_, config) => config.Name == "Test")
            .OnCheckPass((context, _) =>
            {
                calls.Add("pass");
                context.Variables["check-hook"] = "passed";
            });
        test.SetupInbound().CheckHeader((_, config) => config.Name == "Test")
            .OnCheckFail((context, config) =>
            {
                context.Response.StatusCode.Should().Be(config.FailCheckHttpCode);
                context.Response.Body.As<JObject>().Value<string>("message")
                    .Should().Be(config.FailCheckErrorMessage);
                calls.Add("fail");
                context.Response.Headers["X-Check-Hook"] = ["failed"];
            });
        test.SetupInbound().CheckHeader().OnCheckPass((_, _) => calls.Add("second-pass"));
        test.SetupInbound().CheckHeader().OnCheckFail((_, _) => calls.Add("second-fail"));

        test.RunInbound();

        calls.Should().Equal(pass ? "pass" : "fail");
        test.Context.Variables.ContainsKey("after-check").Should().Be(pass);
        test.Context.ResponseTerminated.Should().Be(!pass);
        if (pass)
        {
            test.Context.Variables["check-hook"].Should().Be("passed");
        }
        else
        {
            test.Context.Response.Headers["X-Check-Hook"].Should().Equal("failed");
        }
    }

    [TestMethod]
    public void CheckHeader_FailureHookCanCustomizeResponseButStillTerminates()
    {
        var test = CreateTest(DefaultConfig());
        test.SetupInbound().CheckHeader().OnCheckFail((context, _) =>
        {
            context.Response.StatusCode = 409;
            context.Response.Body.Content = "hook response";
        });

        test.RunInbound();

        test.Context.Response.StatusCode.Should().Be(409);
        test.Context.Response.Body.Content.Should().Be("hook response");
        test.Context.ResponseTerminated.Should().BeTrue();
        test.Context.Variables.Should().NotContainKey("after-check");
    }

    [TestMethod]
    public void CheckHeader_CallbackOverrideBypassesDefaultValidationAndHooks()
    {
        var config = DefaultConfig() with { Values = null! };
        var test = CreateTest(config);
        var callbackCalled = false;
        var hookCalled = false;
        test.SetupInbound().CheckHeader((_, candidate) => ReferenceEquals(candidate, config))
            .WithCallback((context, candidate) =>
            {
                candidate.Should().BeSameAs(config);
                callbackCalled = true;
                context.Response.StatusCode = 202;
                context.Response.Body.Content = "callback";
            });
        test.SetupInbound().CheckHeader().OnCheckPass((_, _) => hookCalled = true);
        test.SetupInbound().CheckHeader().OnCheckFail((_, _) => hookCalled = true);

        test.RunInbound();

        callbackCalled.Should().BeTrue();
        hookCalled.Should().BeFalse();
        test.Context.Response.StatusCode.Should().Be(202);
        test.Context.Response.Body.Content.Should().Be("callback");
        test.Context.Response.Headers.Should().BeEmpty();
        test.Context.Variables.Should().ContainKey("after-check");
        test.Context.ResponseTerminated.Should().BeFalse();
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void CheckHeader_NonmatchingCallbackUsesDefaultBehavior(bool pass)
    {
        var test = CreateTest(DefaultConfig());
        if (pass)
        {
            test.Context.Request.Headers["Test"] = ["first", "second"];
        }
        var callbackCalled = false;
        test.SetupInbound().CheckHeader((_, config) => config.Name == "Other")
            .WithCallback((_, _) => callbackCalled = true);

        test.RunInbound();

        callbackCalled.Should().BeFalse();
        test.Context.Response.StatusCode.Should().Be(pass ? 200 : 401);
        test.Context.ResponseTerminated.Should().Be(!pass);
        test.Context.Variables.ContainsKey("after-check").Should().Be(pass);
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void CheckHeader_CallbackErrorsAndTerminationAreNotSilentlyIgnored(bool terminate)
    {
        var test = CreateTest(DefaultConfig());
        test.SetupInbound().CheckHeader().WithCallback((context, _) =>
        {
            context.Response.StatusCode = 418;
            context.Response.Body.Content = "callback";
            if (terminate)
            {
                throw new FinishSectionProcessingException();
            }
            throw new InvalidOperationException("callback failure");
        });

        if (terminate)
        {
            test.RunInbound();
        }
        else
        {
            var error = Assert.ThrowsExactly<PolicyException>(test.RunInbound);
            error.Policy.Should().Be(nameof(IInboundContext.CheckHeader));
            error.InnerException.Should().BeOfType<InvalidOperationException>()
                .Which.Message.Should().Be("callback failure");
        }

        test.Context.Response.StatusCode.Should().Be(418);
        test.Context.Response.Body.Content.Should().Be("callback");
        test.Context.ResponseTerminated.Should().Be(terminate);
        test.Context.Variables.Should().NotContainKey("after-check");
    }

    [TestMethod]
    [DataRow("null-name")]
    [DataRow("empty-name")]
    [DataRow("whitespace-name")]
    [DataRow("invalid-name")]
    [DataRow("null-values")]
    [DataRow("null-value")]
    [DataRow("null-message")]
    [DataRow("low-status")]
    [DataRow("high-status")]
    public void CheckHeader_InvalidConfigFailsExplicitlyWithoutPublishingAResponse(string invalid)
    {
        var config = invalid switch
        {
            "null-name" => DefaultConfig() with { Name = null! },
            "empty-name" => DefaultConfig() with { Name = "" },
            "whitespace-name" => DefaultConfig() with { Name = " " },
            "invalid-name" => DefaultConfig() with { Name = "Bad\r\nName" },
            "null-values" => DefaultConfig() with { Values = null! },
            "null-value" => DefaultConfig() with { Values = [null!] },
            "null-message" => DefaultConfig() with { FailCheckErrorMessage = null! },
            "low-status" => DefaultConfig() with { FailCheckHttpCode = 99 },
            "high-status" => DefaultConfig() with { FailCheckHttpCode = 600 },
            _ => throw new ArgumentOutOfRangeException(nameof(invalid))
        };
        var test = CreateTest(config);
        test.Context.Request.Headers["Test"] = ["expected"];
        test.Context.Response.StatusCode = 202;
        test.Context.Response.Headers["X-Original"] = ["original"];
        test.Context.Response.Body.Content = "original";

        var error = Assert.ThrowsExactly<PolicyException>(test.RunInbound);

        error.Policy.Should().Be(nameof(IInboundContext.CheckHeader));
        error.Section.Should().Be(nameof(IInboundContext));
        error.InnerException.Should().BeAssignableTo<ArgumentException>();
        test.Context.Response.StatusCode.Should().Be(202);
        test.Context.Response.Headers.Should().ContainSingle();
        test.Context.Response.Headers["X-Original"].Should().Equal("original");
        test.Context.Response.Body.Content.Should().Be("original");
        test.Context.ResponseTerminated.Should().BeFalse();
        test.Context.Variables.Should().NotContainKey("after-check");
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void CheckHeader_EvaluatesEveryConfigExpressionBeforeChecking(bool ignoreCase)
    {
        var test = new ExecutionTestDocument
        {
            InboundAction = context =>
            {
                var variables = context.ExpressionContext.Variables;
                context.CheckHeader(new CheckHeaderConfig
                {
                    Name = (string)variables["name"],
                    Values = [(string)variables["value"]],
                    IgnoreCase = (bool)variables["ignore-case"],
                    FailCheckHttpCode = (int)variables["code"],
                    FailCheckErrorMessage = (string)variables["message"]
                });
                context.SetVariable("after-check", true);
            }
        }.AsTestDocument();
        test.Context.Variables["name"] = "X-Expression";
        test.Context.Variables["value"] = "Expected";
        test.Context.Variables["ignore-case"] = ignoreCase;
        test.Context.Variables["code"] = 403;
        test.Context.Variables["message"] = "expression \"failure\"";
        test.Context.Request.Headers = new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["x-expression"] = ["other", "EXPECTED"]
        };

        test.RunInbound();

        test.Context.Response.StatusCode.Should().Be(ignoreCase ? 200 : 403);
        test.Context.ResponseTerminated.Should().Be(!ignoreCase);
        test.Context.Variables.ContainsKey("after-check").Should().Be(ignoreCase);
        if (!ignoreCase)
        {
            test.Context.Response.Body.As<JObject>().Value<string>("message")
                .Should().Be("expression \"failure\"");
        }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void CheckHeader_FailureStopsEveryLaterCoordinatedScopeAndSection(bool nested)
    {
        var pipeline = PolicyPipelineBuilder.Create()
            .AddPolicy(PolicyScope.Global, CreateDocument(DefaultConfig()))
            .AddPolicy(PolicyScope.Operation, new ExecutionTestDocument
            {
                InboundAction = context => context.SetVariable("later-inbound", true),
                BackendAction = context => context.SetVariable("later-backend", true),
                OutboundAction = context => context.SetVariable("later-outbound", true),
                OnErrorAction = context => context.SetVariable("later-error", true)
            })
            .Build();

        if (nested)
        {
            pipeline.RunAllNested();
            pipeline.RunOnErrorNested();
        }
        else
        {
            pipeline.RunAll();
            pipeline.RunOnError();
        }

        pipeline.Context.Response.StatusCode.Should().Be(401);
        pipeline.Context.ResponseTerminated.Should().BeTrue();
        pipeline.Context.Variables.Should().BeEmpty();
    }

    [TestMethod]
    public void CheckHeader_TerminationDoesNotBlockLaterStandaloneSectionInvocations()
    {
        var test = CreateTest(DefaultConfig());

        test.RunInbound();
        test.RunBackend();

        test.Context.Response.StatusCode.Should().Be(401);
        test.Context.ResponseTerminated.Should().BeTrue();
        test.Context.Variables.Should().NotContainKey("after-check");
        test.Context.Variables.Should().ContainKey("backend");
    }

    [TestMethod]
    public void CheckHeader_SectionRegistrationIsInboundOnly()
    {
        var handler = typeof(MockPoliciesProvider<>).Assembly.GetType(
            "Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Policies.CheckHeaderHandler",
            throwOnError: true)!;

        handler.GetCustomAttributes(typeof(SectionAttribute), inherit: false)
            .Cast<SectionAttribute>().Select(attribute => attribute.Scope)
            .Should().Equal(nameof(IInboundContext));
    }

    [TestMethod]
    [DataRow("backend")]
    [DataRow("outbound")]
    [DataRow("on-error")]
    public void CheckHeader_FragmentCannotBypassInboundSectionRestriction(string section)
    {
        var test = new ExecutionTestDocument
        {
            BackendAction = context => context.IncludeFragment("check-inbound-only"),
            OutboundAction = context => context.IncludeFragment("check-inbound-only"),
            OnErrorAction = context => context.IncludeFragment("check-inbound-only")
        }.AsTestDocument().RegisterFragment("check-inbound-only", new CheckHeaderFragment());
        test.Context.Request.Headers["Test"] = ["expected"];

        var error = Assert.ThrowsExactly<PolicyException>(() => ExecutionTest.RunSection(test, section));

        error.Policy.Should().Be(nameof(IInboundContext.CheckHeader));
        error.Section.Should().Be(ExecutionTest.SectionName(section));
        error.InnerException.Should().BeOfType<NotImplementedException>();
        test.Context.Response.Headers.Should().BeEmpty();
        test.Context.ResponseTerminated.Should().BeFalse();
    }

    private static CheckHeaderConfig DefaultConfig() => new()
    {
        Name = "Test",
        FailCheckHttpCode = 401,
        FailCheckErrorMessage = "Request do not contain test header",
        IgnoreCase = false,
        Values = []
    };

    private static TestDocument CreateTest(CheckHeaderConfig config) => CreateDocument(config).AsTestDocument();

    private static IDocument CreateDocument(CheckHeaderConfig config) => new ExecutionTestDocument
    {
        InboundAction = context =>
        {
            context.CheckHeader(config);
            context.SetVariable("after-check", true);
        },
        BackendAction = context => context.SetVariable("backend", true),
        OutboundAction = context => context.SetVariable("outbound", true),
        OnErrorAction = context => context.SetVariable("on-error", true)
    };

    private static IEqualityComparer<string>? HeaderComparer(string comparer) => comparer switch
    {
        "Default" => null,
        "Ordinal" => StringComparer.Ordinal,
        "OrdinalIgnoreCase" => StringComparer.OrdinalIgnoreCase,
        "InvariantCultureIgnoreCase" => StringComparer.InvariantCultureIgnoreCase,
        _ => throw new ArgumentOutOfRangeException(nameof(comparer))
    };

    private sealed class CheckHeaderFragment : IFragment
    {
        public void Fragment(IFragmentContext context) => context.CheckHeader(DefaultConfig());
    }
}