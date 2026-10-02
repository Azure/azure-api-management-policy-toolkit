// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Document;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Services;

namespace Test.Emulator.Emulator.Policies;

[TestClass]
public class CorsTests
{
    private const string Origin = "https://allowed.example";

    [TestMethod]
    [DataRow("GET")]
    [DataRow("POST")]
    [DataRow("OPTIONS")]
    public void Cors_NormalRequestsReceiveOnlyActualResponseHeadersAndContinue(string method)
    {
        var test = CreateTest(DefaultConfig() with
        {
            AllowCredentials = true,
            ExposeHeaders = ["X-Visible"],
            PreflightResultMaxAge = 600
        });
        test.Context.Request.Method = method;
        test.Context.Request.Headers["Origin"] = [Origin];
        test.Context.Response.StatusCode = 207;
        test.Context.Response.StatusReason = "Multi-Status";
        test.Context.Response.Headers["X-Preserved"] = ["original"];
        test.Context.Response.Body.Content = "original";

        test.RunInbound();

        test.Context.Response.StatusCode.Should().Be(207);
        test.Context.Response.StatusReason.Should().Be("Multi-Status");
        test.Context.Response.Body.Content.Should().Be("original");
        test.Context.Response.Headers.Should().HaveCount(4);
        test.Context.Response.Headers["X-Preserved"].Should().Equal("original");
        test.Context.Response.Headers["Access-Control-Allow-Origin"].Should().Equal(Origin);
        test.Context.Response.Headers["Access-Control-Allow-Credentials"].Should().Equal("true");
        test.Context.Response.Headers["Access-Control-Expose-Headers"].Should().Equal("X-Visible");
        test.Context.Response.Headers.Should().NotContainKeys(
            "Access-Control-Allow-Methods", "Access-Control-Allow-Headers", "Access-Control-Max-Age");
        test.Context.Variables.Should().ContainKey("after-cors");
        test.Context.ResponseTerminated.Should().BeFalse();
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void Cors_NoOriginNeverAddsAncillaryHeadersOrTerminates(bool preflightHeaders)
    {
        var test = CreateTest(DefaultConfig() with
        {
            AllowedOrigins = ["*"],
            AllowedMethods = ["*"],
            AllowedHeaders = ["*"],
            AllowCredentials = true,
            ExposeHeaders = ["*"],
            PreflightResultMaxAge = 600,
            TerminateUnmatchedRequest = "true"
        });
        test.Context.Request.Method = preflightHeaders ? "OPTIONS" : "GET";
        if (preflightHeaders)
        {
            test.Context.Request.Headers["Access-Control-Request-Method"] = ["POST"];
            test.Context.Request.Headers["Access-Control-Request-Headers"] = ["X-Test"];
        }
        test.Context.Response.StatusCode = 207;
        test.Context.Response.Body.Content = "original";
        test.Context.Response.Headers["X-Preserved"] = ["original"];

        test.RunInbound();

        test.Context.Response.StatusCode.Should().Be(207);
        test.Context.Response.Body.Content.Should().Be("original");
        test.Context.Response.Headers.Should().ContainSingle();
        test.Context.Response.Headers["X-Preserved"].Should().Equal("original");
        AssertNoCorsHeaders(test.Context);
        test.Context.Variables.Should().ContainKey("after-cors");
        test.Context.ResponseTerminated.Should().BeFalse();
    }

    [TestMethod]
    [DataRow("https://ALLOWED.example", "https://allowed.example")]
    [DataRow("https://allowed.example:443/", "https://allowed.example")]
    [DataRow("https://allowed.example/", "https://allowed.example:443")]
    public void Cors_OriginMatchingUsesSchemeHostAndEffectivePort(string allowed, string origin)
    {
        var test = CreateTest(DefaultConfig() with { AllowedOrigins = [allowed] });
        test.Context.Request.Headers["Origin"] = [origin];

        test.RunInbound();

        test.Context.Response.Headers.Should().ContainSingle();
        test.Context.Response.Headers["Access-Control-Allow-Origin"].Should().Equal(origin);
        test.Context.Variables.Should().ContainKey("after-cors");
        test.Context.ResponseTerminated.Should().BeFalse();
    }

    [TestMethod]
    [DataRow(false, false)]
    [DataRow(true, false)]
    [DataRow(false, true)]
    [DataRow(true, true)]
    public void Cors_WildcardsReflectOriginForCredentialsAndExpandRequestedMethodsAndHeaders(
        bool credentials, bool preflight)
    {
        var test = CreateTest(DefaultConfig() with
        {
            AllowedOrigins = ["*"],
            AllowedMethods = ["*"],
            AllowedHeaders = ["*"],
            AllowCredentials = credentials
        });
        if (preflight)
        {
            SeedPreflight(test.Context, method: "PATCH",
                headers: ["X-Trace, Authorization", "x-trace, X-Other"]);
        }
        else
        {
            test.Context.Request.Method = "PATCH";
            test.Context.Request.Headers["Origin"] = [Origin];
        }

        test.RunInbound();

        test.Context.Response.Headers["Access-Control-Allow-Origin"]
            .Should().Equal(credentials ? Origin : "*");
        test.Context.Response.Headers.ContainsKey("Access-Control-Allow-Credentials")
            .Should().Be(credentials);
        if (credentials)
        {
            test.Context.Response.Headers["Access-Control-Allow-Credentials"].Should().Equal("true");
        }
        if (preflight)
        {
            test.Context.Response.Headers["Access-Control-Allow-Methods"].Should().Equal("PATCH");
            test.Context.Response.Headers["Access-Control-Allow-Headers"].Single().Split(',')
                .Should().BeEquivalentTo("X-Trace", "Authorization", "X-Other");
            test.Context.Response.Body.Content.Should().BeEmpty();
        }
        else
        {
            test.Context.Response.Headers.Should().NotContainKeys(
                "Access-Control-Allow-Methods", "Access-Control-Allow-Headers", "Access-Control-Max-Age");
        }
        test.Context.ResponseTerminated.Should().Be(preflight);
        test.Context.Variables.ContainsKey("after-cors").Should().Be(!preflight);
    }

    [TestMethod]
    public void Cors_ValidPreflightValidatesEveryHeaderAndOverwritesThenStops()
    {
        var test = CreateTest(DefaultConfig() with
        {
            AllowCredentials = true,
            ExposeHeaders = ["X-Visible"],
            PreflightResultMaxAge = 600
        });
        SeedPreflight(test.Context, headers: ["x-test", "CONTENT-TYPE"]);
        var response = test.Context.Response;
        var headers = response.Headers;
        response.StatusCode = 502;
        response.StatusReason = "Bad Gateway";
        response.Headers["X-Upstream"] = ["stale"];
        response.Body.Content = "stale";

        test.RunInbound();

        test.Context.Response.Should().BeSameAs(response);
        response.Headers.Should().BeSameAs(headers).And.HaveCount(5);
        response.StatusCode.Should().Be(200);
        response.StatusReason.Should().Be("OK");
        response.Body.Content.Should().BeEmpty();
        response.Headers["Access-Control-Allow-Origin"].Should().Equal(Origin);
        response.Headers["Access-Control-Allow-Credentials"].Should().Equal("true");
        response.Headers["Access-Control-Allow-Methods"].Should().Equal("GET,POST");
        response.Headers["Access-Control-Allow-Headers"].Should().Equal("X-Test,Content-Type");
        response.Headers["Access-Control-Max-Age"].Should().Equal("600");
        response.Headers.Should().NotContainKeys("X-Upstream", "Access-Control-Expose-Headers");
        test.Context.ResponseTerminated.Should().BeTrue();
        test.Context.Variables.Should().NotContainKey("after-cors");
    }

    [TestMethod]
    [DataRow("origin")]
    [DataRow("origin-port")]
    [DataRow("origin-scheme")]
    [DataRow("origin-invalid")]
    [DataRow("origin-multiple")]
    [DataRow("method")]
    [DataRow("method-case")]
    [DataRow("method-multiple")]
    [DataRow("method-empty")]
    [DataRow("method-list")]
    [DataRow("header")]
    [DataRow("header-second-value")]
    [DataRow("header-empty-token")]
    [DataRow("header-invalid-token")]
    public void Cors_RejectedPreflightDoesNotPublishAnyCorsHeaders(string invalid)
    {
        var test = CreateTest(DefaultConfig() with
        {
            AllowCredentials = true,
            ExposeHeaders = ["X-Visible"],
            PreflightResultMaxAge = 600
        });
        SeedPreflight(test.Context);
        switch (invalid)
        {
            case "origin":
                test.Context.Request.Headers["Origin"] = ["https://denied.example"];
                break;
            case "origin-port":
                test.Context.Request.Headers["Origin"] = ["https://allowed.example:8443"];
                break;
            case "origin-scheme":
                test.Context.Request.Headers["Origin"] = ["http://allowed.example"];
                break;
            case "origin-invalid":
                test.Context.Request.Headers["Origin"] = ["not an origin"];
                break;
            case "origin-multiple":
                test.Context.Request.Headers["Origin"] = [Origin, Origin];
                break;
            case "method":
                test.Context.Request.Headers["Access-Control-Request-Method"] = ["DELETE"];
                break;
            case "method-case":
                test.Context.Request.Headers["Access-Control-Request-Method"] = ["post"];
                break;
            case "method-multiple":
                test.Context.Request.Headers["Access-Control-Request-Method"] = ["POST", "GET"];
                break;
            case "method-empty":
                test.Context.Request.Headers["Access-Control-Request-Method"] = [];
                break;
            case "method-list":
                test.Context.Request.Headers["Access-Control-Request-Method"] = ["POST,GET"];
                break;
            case "header":
                test.Context.Request.Headers["Access-Control-Request-Headers"] = ["X-Forbidden"];
                break;
            case "header-second-value":
                test.Context.Request.Headers["Access-Control-Request-Headers"] = ["X-Test", "X-Forbidden"];
                break;
            case "header-empty-token":
                test.Context.Request.Headers["Access-Control-Request-Headers"] = ["X-Test,,Content-Type"];
                break;
            case "header-invalid-token":
                test.Context.Request.Headers["Access-Control-Request-Headers"] = ["X-Test\r\nInjected: value"];
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(invalid));
        }
        test.Context.Response.Headers["X-Upstream"] = ["stale"];
        test.Context.Response.Body.Content = "stale";

        test.RunInbound();

        AssertRejected(test.Context);
    }

    [TestMethod]
    [DataRow("GET", true)]
    [DataRow("POST", true)]
    [DataRow("DELETE", false)]
    public void Cors_DefaultMethodsAreGetAndPostAndNoRequestedHeadersNeedNoPermission(
        string method, bool allowed)
    {
        var test = CreateTest(DefaultConfig() with { AllowedMethods = null, AllowedHeaders = [] });
        SeedPreflight(test.Context, method: method);
        test.Context.Request.Headers.Remove("Access-Control-Request-Headers");

        test.RunInbound();

        if (allowed)
        {
            test.Context.Response.Headers["Access-Control-Allow-Origin"].Should().Equal(Origin);
            test.Context.Response.Headers["Access-Control-Allow-Methods"].Should().Equal("GET,POST");
            test.Context.Response.Headers.Should().NotContainKeys(
                "Access-Control-Allow-Headers", "Access-Control-Max-Age");
            test.Context.ResponseTerminated.Should().BeTrue();
            test.Context.Variables.Should().NotContainKey("after-cors");
        }
        else
        {
            AssertRejected(test.Context);
        }
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("true")]
    [DataRow("false")]
    public void Cors_MatchedPreflightAlwaysStopsRegardlessOfUnmatchedSetting(string? terminate)
    {
        var test = CreateTest(DefaultConfig() with { TerminateUnmatchedRequest = terminate });
        SeedPreflight(test.Context);

        test.RunInbound();

        test.Context.Response.Headers["Access-Control-Allow-Origin"].Should().Equal(Origin);
        test.Context.ResponseTerminated.Should().BeTrue();
        test.Context.Variables.Should().NotContainKey("after-cors");
    }

    [TestMethod]
    [DataRow(false, null, true)]
    [DataRow(false, "true", true)]
    [DataRow(false, "false", false)]
    [DataRow(true, null, true)]
    [DataRow(true, "true", true)]
    [DataRow(true, "false", false)]
    public void Cors_UnmatchedRequestsHaveNoAncillaryHeadersAndHonorTermination(
        bool preflight, string? terminate, bool stopped)
    {
        var test = CreateTest(DefaultConfig() with
        {
            TerminateUnmatchedRequest = terminate,
            AllowCredentials = true,
            ExposeHeaders = ["X-Visible"],
            PreflightResultMaxAge = 600
        });
        if (preflight)
        {
            SeedPreflight(test.Context, origin: "https://denied.example");
        }
        else
        {
            test.Context.Request.Headers["Origin"] = ["https://denied.example"];
        }
        test.Context.Response.StatusCode = 207;
        test.Context.Response.StatusReason = "Multi-Status";
        test.Context.Response.Headers["X-Preserved"] = ["original"];
        test.Context.Response.Body.Content = "original";

        test.RunInbound();

        AssertNoCorsHeaders(test.Context);
        test.Context.ResponseTerminated.Should().Be(stopped);
        test.Context.Variables.ContainsKey("after-cors").Should().Be(!stopped);
        if (stopped)
        {
            test.Context.Response.StatusCode.Should().Be(200);
            test.Context.Response.StatusReason.Should().Be("OK");
            test.Context.Response.Body.Content.Should().BeEmpty();
            test.Context.Response.Headers.Should().BeEmpty();
        }
        else
        {
            test.Context.Response.StatusCode.Should().Be(207);
            test.Context.Response.StatusReason.Should().Be("Multi-Status");
            test.Context.Response.Body.Content.Should().Be("original");
            test.Context.Response.Headers.Should().ContainSingle();
            test.Context.Response.Headers["X-Preserved"].Should().Equal("original");
        }
    }

    [TestMethod]
    public void Cors_UnmatchedFalseCanReachALaterCorsWithoutLeakingHeaders()
    {
        var first = DefaultConfig() with
        {
            AllowedOrigins = ["https://denied.example"],
            TerminateUnmatchedRequest = "false",
            AllowCredentials = true,
            ExposeHeaders = ["X-Visible"],
            PreflightResultMaxAge = 600
        };
        var second = DefaultConfig();
        var test = new ExecutionTestDocument
        {
            InboundAction = context =>
            {
                context.Cors(first);
                context.Cors(second);
                context.SetVariable("after-cors", true);
            }
        }.AsTestDocument();
        SeedPreflight(test.Context);

        test.RunInbound();

        test.Context.Response.Headers.Should().HaveCount(3);
        test.Context.Response.Headers["Access-Control-Allow-Origin"].Should().Equal(Origin);
        test.Context.Response.Headers.Should().NotContainKeys(
            "Access-Control-Allow-Credentials", "Access-Control-Expose-Headers", "Access-Control-Max-Age");
        test.Context.ResponseTerminated.Should().BeTrue();
        test.Context.Variables.Should().NotContainKey("after-cors");
    }

    [TestMethod]
    [DataRow("Default")]
    [DataRow("Ordinal")]
    [DataRow("OrdinalIgnoreCase")]
    [DataRow("InvariantCultureIgnoreCase")]
    public void Cors_UsesCaseInsensitiveRequestNamesWithoutChangingInjectedStorage(string comparer)
    {
        var test = CreateTest(DefaultConfig());
        var originValues = new[] { Origin };
        var source = new Dictionary<string, string[]>(HeaderComparer(comparer))
        {
            ["origin"] = originValues,
            ["access-control-request-method"] = ["POST"],
            ["access-control-request-headers"] = ["x-test", "CONTENT-TYPE"],
            ["X-Keep"] = ["keep"]
        };
        var originalComparer = source.Comparer;
        test.Context.Request.Headers = source;
        test.Context.Request.Method = "OPTIONS";

        test.RunInbound();

        test.Context.Response.Headers["Access-Control-Allow-Origin"].Should().Equal(Origin);
        test.Context.ResponseTerminated.Should().BeTrue();
        test.Context.Request.Headers.Should().BeSameAs(source);
        source.Comparer.Should().BeSameAs(originalComparer);
        source.Keys.Should().Equal(
            "origin", "access-control-request-method", "access-control-request-headers", "X-Keep");
        source["origin"].Should().BeSameAs(originValues).And.Equal(Origin);
        source["access-control-request-headers"].Should().Equal("x-test", "CONTENT-TYPE");
        source["X-Keep"].Should().Equal("keep");
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void Cors_ValidatesAllInjectedCaseVariantRequestHeaderLists(bool forbidden)
    {
        var test = CreateTest(DefaultConfig());
        var first = new[] { "X-Test" };
        var second = new[] { forbidden ? "X-Forbidden" : "content-type, x-test" };
        var source = new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["origin"] = [Origin],
            ["access-control-request-method"] = ["POST"],
            ["Access-Control-Request-Headers"] = first,
            ["access-control-request-headers"] = second
        };
        test.Context.Request.Method = "OPTIONS";
        test.Context.Request.Headers = source;

        test.RunInbound();

        if (forbidden)
        {
            AssertRejected(test.Context);
        }
        else
        {
            test.Context.Response.Headers["Access-Control-Allow-Origin"].Should().Equal(Origin);
            test.Context.Response.Headers["Access-Control-Allow-Headers"].Should().Equal("X-Test,Content-Type");
            test.Context.ResponseTerminated.Should().BeTrue();
        }
        test.Context.Request.Headers.Should().BeSameAs(source);
        source.Comparer.Should().BeSameAs(StringComparer.Ordinal);
        source.Should().HaveCount(4);
        source["Access-Control-Request-Headers"].Should().BeSameAs(first).And.Equal("X-Test");
        source["access-control-request-headers"].Should().BeSameAs(second).And.Equal(second);
    }

    [TestMethod]
    [DataRow("Origin", "origin", Origin)]
    [DataRow("Origin", "origin", "https://denied.example")]
    [DataRow("Access-Control-Request-Method", "access-control-request-method", "POST")]
    [DataRow("Access-Control-Request-Method", "access-control-request-method", "DELETE")]
    public void Cors_RejectsAmbiguousSingletonHeadersAcrossInjectedCaseVariants(
        string name, string duplicate, string value)
    {
        var test = CreateTest(DefaultConfig());
        SeedPreflight(test.Context);
        var source = new Dictionary<string, string[]>(test.Context.Request.Headers, StringComparer.Ordinal)
        {
            [duplicate] = [value]
        };
        test.Context.Request.Headers = source;

        test.RunInbound();

        AssertRejected(test.Context);
        source[name].Should().Equal(name == "Origin" ? Origin : "POST");
        source[duplicate].Should().Equal(value);
        test.Context.Request.Headers.Should().BeSameAs(source);
    }

    [TestMethod]
    public void Cors_NormalResponseReplacesCaseVariantCorsHeadersWithoutReplacingInjectedDictionary()
    {
        var test = CreateTest(DefaultConfig() with { AllowCredentials = true });
        test.Context.Request.Headers["Origin"] = [Origin];
        var source = new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["Access-Control-Allow-Origin"] = ["stale"],
            ["access-control-allow-origin"] = ["conflicting"],
            ["ACCESS-CONTROL-ALLOW-CREDENTIALS"] = ["false"],
            ["access-control-allow-methods"] = ["DELETE"],
            ["access-control-expose-headers"] = ["X-Stale"],
            ["access-control-max-age"] = ["1"],
            ["X-Keep"] = ["keep"]
        };
        test.Context.Response.Headers = source;

        test.RunInbound();

        test.Context.Response.Headers.Should().BeSameAs(source);
        source.Comparer.Should().BeSameAs(StringComparer.Ordinal);
        source.Should().HaveCount(3);
        source["Access-Control-Allow-Origin"].Should().Equal(Origin);
        source["Access-Control-Allow-Credentials"].Should().Equal("true");
        source["X-Keep"].Should().Equal("keep");
        test.Context.ResponseTerminated.Should().BeFalse();
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void Cors_ExposedWildcardIsNotUsedForCredentialedRequests(bool credentials)
    {
        var test = CreateTest(DefaultConfig() with { AllowCredentials = credentials, ExposeHeaders = ["*"] });
        test.Context.Request.Headers["Origin"] = [Origin];
        test.Context.Response.Headers["X-Visible"] = ["visible"];
        test.Context.Response.Headers["Set-Cookie"] = ["secret=value"];

        test.RunInbound();

        test.Context.Response.Headers["Access-Control-Expose-Headers"]
            .Should().Equal(credentials ? "X-Visible" : "*");
        test.Context.Response.Headers["Set-Cookie"].Should().Equal("secret=value");
        test.Context.ResponseTerminated.Should().BeFalse();
    }

    [TestMethod]
    [DataRow("null-origins")]
    [DataRow("empty-origins")]
    [DataRow("null-origin")]
    [DataRow("empty-origin")]
    [DataRow("invalid-origin")]
    [DataRow("origin-with-path")]
    [DataRow("mixed-origin-wildcard")]
    [DataRow("empty-methods")]
    [DataRow("null-method")]
    [DataRow("invalid-method")]
    [DataRow("null-headers")]
    [DataRow("null-header")]
    [DataRow("empty-header")]
    [DataRow("invalid-header")]
    [DataRow("null-exposed-header")]
    [DataRow("invalid-exposed-header")]
    [DataRow("invalid-terminate")]
    [DataRow("empty-terminate")]
    public void Cors_InvalidConfigFailsExplicitlyBeforePublishingHeadersEvenWithoutOrigin(string invalid)
    {
        var config = invalid switch
        {
            "null-origins" => DefaultConfig() with { AllowedOrigins = null! },
            "empty-origins" => DefaultConfig() with { AllowedOrigins = [] },
            "null-origin" => DefaultConfig() with { AllowedOrigins = [null!] },
            "empty-origin" => DefaultConfig() with { AllowedOrigins = [""] },
            "invalid-origin" => DefaultConfig() with { AllowedOrigins = ["not an origin"] },
            "origin-with-path" => DefaultConfig() with { AllowedOrigins = [Origin + "/path"] },
            "mixed-origin-wildcard" => DefaultConfig() with { AllowedOrigins = ["*", Origin] },
            "empty-methods" => DefaultConfig() with { AllowedMethods = [] },
            "null-method" => DefaultConfig() with { AllowedMethods = [null!] },
            "invalid-method" => DefaultConfig() with { AllowedMethods = ["POST,GET"] },
            "null-headers" => DefaultConfig() with { AllowedHeaders = null! },
            "null-header" => DefaultConfig() with { AllowedHeaders = [null!] },
            "empty-header" => DefaultConfig() with { AllowedHeaders = [" "] },
            "invalid-header" => DefaultConfig() with { AllowedHeaders = ["X-Test\r\nInjected: value"] },
            "null-exposed-header" => DefaultConfig() with { ExposeHeaders = [null!] },
            "invalid-exposed-header" => DefaultConfig() with { ExposeHeaders = ["Bad Header"] },
            "invalid-terminate" => DefaultConfig() with { TerminateUnmatchedRequest = "sometimes" },
            "empty-terminate" => DefaultConfig() with { TerminateUnmatchedRequest = "" },
            _ => throw new ArgumentOutOfRangeException(nameof(invalid))
        };
        var test = CreateTest(config);
        test.Context.Response.StatusCode = 207;
        test.Context.Response.Headers["X-Original"] = ["original"];
        test.Context.Response.Body.Content = "original";

        var error = Assert.ThrowsExactly<PolicyException>(test.RunInbound);

        error.Policy.Should().Be(nameof(IInboundContext.Cors));
        error.Section.Should().Be(nameof(IInboundContext));
        error.InnerException.Should().BeAssignableTo<ArgumentException>();
        test.Context.Response.StatusCode.Should().Be(207);
        test.Context.Response.Headers.Should().ContainSingle();
        test.Context.Response.Headers["X-Original"].Should().Equal("original");
        test.Context.Response.Body.Content.Should().Be("original");
        test.Context.ResponseTerminated.Should().BeFalse();
        test.Context.Variables.Should().NotContainKey("after-cors");
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void Cors_ConfigExpressionsControlCredentialsMaxAgeAndUnmatchedTermination(bool unmatched)
    {
        var test = new ExecutionTestDocument
        {
            InboundAction = context =>
            {
                var variables = context.ExpressionContext.Variables;
                context.Cors(DefaultConfig() with
                {
                    AllowedOrigins = ["*"],
                    AllowCredentials = (bool)variables["credentials"],
                    PreflightResultMaxAge = (uint)variables["max-age"],
                    TerminateUnmatchedRequest = (string)variables["terminate"]
                });
                context.SetVariable("after-cors", true);
            }
        }.AsTestDocument();
        test.Context.Variables["credentials"] = true;
        test.Context.Variables["max-age"] = uint.MaxValue;
        test.Context.Variables["terminate"] = "FALSE";
        SeedPreflight(test.Context, method: unmatched ? "DELETE" : "POST");

        test.RunInbound();

        if (unmatched)
        {
            AssertNoCorsHeaders(test.Context);
            test.Context.Variables.Should().ContainKey("after-cors");
            test.Context.ResponseTerminated.Should().BeFalse();
        }
        else
        {
            test.Context.Response.Headers["Access-Control-Allow-Origin"].Should().Equal(Origin);
            test.Context.Response.Headers["Access-Control-Allow-Credentials"].Should().Equal("true");
            test.Context.Response.Headers["Access-Control-Max-Age"].Should().Equal("4294967295");
            test.Context.Variables.Should().NotContainKey("after-cors");
            test.Context.ResponseTerminated.Should().BeTrue();
        }
    }

    [TestMethod]
    public void Cors_CallbackOverrideBypassesDefaultValidationAndPreflightBehavior()
    {
        var config = DefaultConfig() with { AllowedOrigins = null!, AllowedHeaders = null! };
        var test = CreateTest(config);
        SeedPreflight(test.Context);
        var callbackCalled = false;
        test.SetupInbound().Cors((context, candidate) =>
                context.Request.Method == "OPTIONS" && ReferenceEquals(candidate, config))
            .WithCallback((context, candidate) =>
            {
                candidate.Should().BeSameAs(config);
                callbackCalled = true;
                context.Response.StatusCode = 202;
                context.Response.Body.Content = "callback";
                context.Response.Headers["X-Callback"] = ["called"];
            });

        test.RunInbound();

        callbackCalled.Should().BeTrue();
        test.Context.Response.StatusCode.Should().Be(202);
        test.Context.Response.Body.Content.Should().Be("callback");
        test.Context.Response.Headers["X-Callback"].Should().Equal("called");
        AssertNoCorsHeaders(test.Context);
        test.Context.Variables.Should().ContainKey("after-cors");
        test.Context.ResponseTerminated.Should().BeFalse();
    }

    [TestMethod]
    public void Cors_NonmatchingCallbackUsesDefaultPreflightBehavior()
    {
        var test = CreateTest(DefaultConfig());
        SeedPreflight(test.Context);
        var callbackCalled = false;
        test.SetupInbound().Cors((_, config) => config.AllowedOrigins.Contains("https://other.example"))
            .WithCallback((_, _) => callbackCalled = true);

        test.RunInbound();

        callbackCalled.Should().BeFalse();
        test.Context.Response.Headers["Access-Control-Allow-Origin"].Should().Equal(Origin);
        test.Context.ResponseTerminated.Should().BeTrue();
        test.Context.Variables.Should().NotContainKey("after-cors");
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void Cors_CallbackErrorsAndTerminationAreNotSilentlyIgnored(bool terminate)
    {
        var test = CreateTest(DefaultConfig());
        SeedPreflight(test.Context);
        test.SetupInbound().Cors().WithCallback((context, _) =>
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
            error.Policy.Should().Be(nameof(IInboundContext.Cors));
            error.InnerException.Should().BeOfType<InvalidOperationException>()
                .Which.Message.Should().Be("callback failure");
        }

        test.Context.Response.StatusCode.Should().Be(418);
        test.Context.Response.Body.Content.Should().Be("callback");
        AssertNoCorsHeaders(test.Context);
        test.Context.ResponseTerminated.Should().Be(terminate);
        test.Context.Variables.Should().NotContainKey("after-cors");
    }

    [TestMethod]
    [DataRow(false, false)]
    [DataRow(true, false)]
    [DataRow(false, true)]
    [DataRow(true, true)]
    public void Cors_PreflightStopsEveryLaterCoordinatedScopeAndSection(bool nested, bool unmatched)
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
            .ConfigureContext(context =>
                SeedPreflight(context, origin: unmatched ? "https://denied.example" : Origin))
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

        pipeline.Context.Response.StatusCode.Should().Be(200);
        pipeline.Context.Response.Body.Content.Should().BeEmpty();
        pipeline.Context.ResponseTerminated.Should().BeTrue();
        pipeline.Context.Variables.Should().BeEmpty();
        if (unmatched)
        {
            AssertNoCorsHeaders(pipeline.Context);
        }
        else
        {
            pipeline.Context.Response.Headers["Access-Control-Allow-Origin"].Should().Equal(Origin);
        }
    }

    [TestMethod]
    public void Cors_SectionRegistrationIsInboundOnly()
    {
        var handler = typeof(MockPoliciesProvider<>).Assembly.GetType(
            "Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Policies.CorsHandler",
            throwOnError: true)!;

        handler.GetCustomAttributes(typeof(SectionAttribute), inherit: false)
            .Cast<SectionAttribute>().Select(attribute => attribute.Scope)
            .Should().Equal(nameof(IInboundContext));
    }

    [TestMethod]
    [DataRow("backend")]
    [DataRow("outbound")]
    [DataRow("on-error")]
    public void Cors_FragmentCannotBypassInboundSectionRestriction(string section)
    {
        var test = new ExecutionTestDocument
        {
            BackendAction = context => context.IncludeFragment("cors-inbound-only"),
            OutboundAction = context => context.IncludeFragment("cors-inbound-only"),
            OnErrorAction = context => context.IncludeFragment("cors-inbound-only")
        }.AsTestDocument().RegisterFragment("cors-inbound-only", new CorsFragment());
        test.Context.Request.Headers["Origin"] = [Origin];

        var error = Assert.ThrowsExactly<PolicyException>(() => ExecutionTest.RunSection(test, section));

        error.Policy.Should().Be(nameof(IInboundContext.Cors));
        error.Section.Should().Be(ExecutionTest.SectionName(section));
        error.InnerException.Should().BeOfType<NotImplementedException>();
        test.Context.Response.Headers.Should().BeEmpty();
        test.Context.ResponseTerminated.Should().BeFalse();
    }

    [TestMethod]
    [DataRow("no-origin", false)]
    [DataRow("no-origin", true)]
    [DataRow("unmatched-origin", false)]
    [DataRow("unmatched-origin", true)]
    [DataRow("unmatched-method", false)]
    [DataRow("unmatched-method", true)]
    [DataRow("unmatched-header", false)]
    [DataRow("unmatched-header", true)]
    public void Cors_LaterNoMatchRemovesOnlyItsEarlierGeneratedOutputs(string unmatched, bool ignoreCase)
    {
        var test = CreateTest(DefaultConfig() with { AllowCredentials = true, ExposeHeaders = ["X-Visible"] });
        var headers = new Dictionary<string, string[]>(
            ignoreCase ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
        test.Context.Response.Headers = headers;
        test.Context.Request.Headers["Origin"] = [Origin];
        test.RunInbound();
        headers["X-Unrelated"] = ["keep"];
        switch (unmatched)
        {
            case "no-origin":
                test.Context.Request.Headers.Remove("Origin");
                break;
            case "unmatched-origin":
                test.Context.Request.Headers["Origin"] = ["https://denied.example"];
                break;
            case "unmatched-method":
                SeedPreflight(test.Context, method: "DELETE");
                break;
            case "unmatched-header":
                SeedPreflight(test.Context, headers: ["X-Not-Allowed"]);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(unmatched));
        }
        var reconfigured = new TestDocument(CreateDocument(DefaultConfig() with
        {
            TerminateUnmatchedRequest = "false"
        }))
        { Context = test.Context };

        reconfigured.RunInbound();

        test.Context.Response.Headers.Should().BeSameAs(headers);
        AssertNoCorsHeaders(test.Context);
        headers["X-Unrelated"].Should().Equal("keep");
        test.Context.ResponseTerminated.Should().BeFalse();
        test.Context.Services.Register<IHttpClient>(StubHttpClient.Ok("backend"));
        new TestDocument(new ForwardRequestTests.HeaderFlowDocument()) { Context = test.Context }.RunBackend();
        AssertNoCorsHeaders(test.Context);
        headers.Should().NotContainKey("X-Unrelated");
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void Cors_ReconfigurationDoesNotRestoreObsoleteCredentialsOrExposedHeaders(bool ignoreCase)
    {
        var test = CreateTest(DefaultConfig() with { AllowCredentials = true, ExposeHeaders = ["X-Old"] });
        var headers = new Dictionary<string, string[]>(
            ignoreCase ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
        test.Context.Response.Headers = headers;
        test.Context.Request.Headers["Origin"] = [Origin];
        test.RunInbound();
        new TestDocument(CreateDocument(DefaultConfig())) { Context = test.Context }.RunInbound();
        test.Context.Services.Register<IHttpClient>(StubHttpClient.Ok("backend"));

        new TestDocument(new ForwardRequestTests.HeaderFlowDocument()) { Context = test.Context }.RunBackend();

        headers["Access-Control-Allow-Origin"].Should().Equal(Origin);
        headers.Should().NotContainKeys("Access-Control-Allow-Credentials", "Access-Control-Expose-Headers");
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void Cors_NoMatchDoesNotClaimUnregisteredMockedCorsHeaders(bool noOrigin)
    {
        var test = CreateTest(DefaultConfig() with { TerminateUnmatchedRequest = "false" });
        test.Context.Response.Headers = new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["access-control-allow-origin"] = ["unregistered"],
            ["Access-Control-Allow-Credentials"] = ["unregistered"]
        };
        if (!noOrigin) test.Context.Request.Headers["Origin"] = ["https://denied.example"];

        test.RunInbound();

        test.Context.Response.Headers["access-control-allow-origin"].Should().Equal("unregistered");
        test.Context.Response.Headers["Access-Control-Allow-Credentials"].Should().Equal("unregistered");
        test.Context.Services.Register<IHttpClient>(StubHttpClient.Ok("backend"));
        new TestDocument(new ForwardRequestTests.HeaderFlowDocument()) { Context = test.Context }.RunBackend();
        AssertNoCorsHeaders(test.Context);
    }

    [TestMethod]
    public void Cors_NoMatchDoesNotRemoveAnotherPolicyOutputThatReplacedItsHeader()
    {
        var test = new ForwardRequestTests.HeaderFlowDocument(context =>
        {
            context.Cors(DefaultConfig());
            context.RateLimitByKey(new RateLimitByKeyConfig
            {
                CounterKey = "cors-name",
                Calls = 10,
                RenewalPeriod = 60,
                RemainingCallsHeaderName = "Access-Control-Allow-Origin"
            });
        }).AsTestDocument();
        test.Context.Request.Headers["Origin"] = [Origin];
        test.RunInbound();
        test.Context.Request.Headers.Remove("Origin");
        new TestDocument(CreateDocument(DefaultConfig())) { Context = test.Context }.RunInbound();
        test.Context.Services.Register<IHttpClient>(StubHttpClient.Ok());

        test.RunBackend();

        test.Context.Response.Headers["Access-Control-Allow-Origin"].Should().Equal("9");
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void Cors_TerminalPreflightNeverInvokesTransportWithRegisteredHeaders(bool unmatched)
    {
        var forwarded = 0;
        var test = new ForwardRequestTests.HeaderFlowDocument(context => context.Cors(DefaultConfig())).AsTestDocument();
        SeedPreflight(test.Context, origin: unmatched ? "https://denied.example" : Origin);
        test.Context.Services.Register<IHttpClient>(new StubHttpClient((_, _) =>
        {
            forwarded++;
            throw new InvalidOperationException("A terminal preflight must not forward.");
        }));

        test.RunAll();

        forwarded.Should().Be(0);
        test.Context.ResponseTerminated.Should().BeTrue();
        test.Context.Response.StatusCode.Should().Be(200);
        if (unmatched) AssertNoCorsHeaders(test.Context);
        else test.Context.Response.Headers["Access-Control-Allow-Origin"].Should().Equal(Origin);
    }

    private static CorsConfig DefaultConfig() => new()
    {
        AllowedOrigins = [Origin],
        AllowedMethods = ["GET", "POST"],
        AllowedHeaders = ["X-Test", "Content-Type"]
    };

    private static TestDocument CreateTest(CorsConfig config) => CreateDocument(config).AsTestDocument();

    private static IDocument CreateDocument(CorsConfig config) => new ExecutionTestDocument
    {
        InboundAction = context =>
        {
            context.Cors(config);
            context.SetVariable("after-cors", true);
        },
        BackendAction = context => context.SetVariable("backend", true),
        OutboundAction = context => context.SetVariable("outbound", true),
        OnErrorAction = context => context.SetVariable("on-error", true)
    };

    private static void SeedPreflight(
        GatewayContext context, string origin = Origin, string method = "POST", string[]? headers = null)
    {
        context.Request.Method = "OPTIONS";
        context.Request.Headers["Origin"] = [origin];
        context.Request.Headers["Access-Control-Request-Method"] = [method];
        context.Request.Headers["Access-Control-Request-Headers"] = headers ?? ["X-Test, Content-Type"];
    }

    private static void AssertNoCorsHeaders(GatewayContext context) =>
        context.Response.Headers.Keys.Should().NotContain(
            name => name.StartsWith("Access-Control-", StringComparison.OrdinalIgnoreCase));

    private static void AssertRejected(GatewayContext context)
    {
        context.Response.StatusCode.Should().Be(200);
        context.Response.StatusReason.Should().Be("OK");
        context.Response.Body.Content.Should().BeEmpty();
        context.Response.Headers.Should().BeEmpty();
        context.ResponseTerminated.Should().BeTrue();
        context.Variables.Should().NotContainKey("after-cors");
    }

    private static IEqualityComparer<string>? HeaderComparer(string comparer) => comparer switch
    {
        "Default" => null,
        "Ordinal" => StringComparer.Ordinal,
        "OrdinalIgnoreCase" => StringComparer.OrdinalIgnoreCase,
        "InvariantCultureIgnoreCase" => StringComparer.InvariantCultureIgnoreCase,
        _ => throw new ArgumentOutOfRangeException(nameof(comparer))
    };

    private sealed class CorsFragment : IFragment
    {
        public void Fragment(IFragmentContext context) => context.Cors(DefaultConfig());
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void Cors_StandaloneCredentialedWildcardPreservesImmediateExpansionAndEmptyAbsence(bool seeded)
    {
        var test = CreateTest(DefaultConfig() with { AllowCredentials = true, ExposeHeaders = ["*"] });
        test.Context.Request.Headers["Origin"] = [Origin];
        test.Context.Response.Headers = new Dictionary<string, string[]>(StringComparer.Ordinal);
        if (seeded)
        {
            test.Context.Response.Headers["X-Stale"] = ["old"];
            test.Context.Response.Headers["x-stale"] = ["old-case"];
            test.Context.Response.Headers["sEt-CoOkIe"] = ["private=1"];
        }

        test.RunInbound();

        test.Context.Response.Headers["Access-Control-Allow-Origin"].Should().Equal(Origin);
        test.Context.Response.Headers["Access-Control-Allow-Credentials"].Should().Equal("true");
        if (seeded)
        {
            test.Context.Response.Headers["Access-Control-Expose-Headers"].Should().Equal("X-Stale");
            test.Context.Response.Headers["sEt-CoOkIe"].Should().Equal("private=1");
        }
        else
        {
            test.Context.Response.Headers.Should().HaveCount(2).And.NotContainKey("Access-Control-Expose-Headers");
        }
    }
}