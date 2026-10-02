// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring.Expressions;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Document;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Expressions;

namespace Test.Emulator.Emulator.Policies;

[TestClass]
public class ReturnResponseTests
{
    class SimpleReturnResponse : IDocument
    {
        public void Inbound(IInboundContext context)
        {
            context.ReturnResponse(new ReturnResponseConfig
            {
                Status = new StatusConfig { Code = 200, Reason = "OK" },
                Body = new BodyConfig { Content = "Hello from inbound" }
            });
        }

        public void Backend(IBackendContext context)
        {
            context.ReturnResponse(new ReturnResponseConfig
            {
                Status = new StatusConfig { Code = 201, Reason = "Created" }
            });
        }

        public void Outbound(IOutboundContext context)
        {
            context.ReturnResponse(new ReturnResponseConfig
            {
                Status = new StatusConfig { Code = 202, Reason = "Accepted" }
            });
        }

        public void OnError(IOnErrorContext context)
        {
            context.ReturnResponse(new ReturnResponseConfig
            {
                Status = new StatusConfig { Code = 500, Reason = "Internal Server Error" }
            });
        }
    }

    class TerminateSectionReturnResponse : IDocument
    {
        public void Inbound(IInboundContext context)
        {
            context.ReturnResponse(new ReturnResponseConfig
            {
                Status = new StatusConfig { Code = 200, Reason = "OK" }
            });
            context.SetHeader("X-After", "should-not-execute");
        }
    }

    class WithHeadersReturnResponse : IDocument
    {
        public void Inbound(IInboundContext context)
        {
            context.ReturnResponse(new ReturnResponseConfig
            {
                Status = new StatusConfig { Code = 200, Reason = "OK" },
                Headers =
                [
                    new HeaderConfig
                    {
                        Name = "X-Custom",
                        ExistsAction = "override",
                        Values = ["custom-value"]
                    }
                ]
            });
        }
    }

    [TestMethod]
    public void ReturnResponse_Inbound()
    {
        var test = new SimpleReturnResponse().AsTestDocument();

        test.RunInbound();

        test.Context.Response.StatusCode.Should().Be(200);
        test.Context.Response.StatusReason.Should().Be("OK");
        test.Context.Response.Body.Content.Should().Be("Hello from inbound");
    }

    [TestMethod]
    public void ReturnResponse_Backend()
    {
        var test = new SimpleReturnResponse().AsTestDocument();

        test.RunBackend();

        test.Context.Response.StatusCode.Should().Be(201);
        test.Context.Response.StatusReason.Should().Be("Created");
    }

    [TestMethod]
    public void ReturnResponse_Outbound()
    {
        var test = new SimpleReturnResponse().AsTestDocument();

        test.RunOutbound();

        test.Context.Response.StatusCode.Should().Be(202);
        test.Context.Response.StatusReason.Should().Be("Accepted");
    }

    [TestMethod]
    public void ReturnResponse_OnError()
    {
        var test = new SimpleReturnResponse().AsTestDocument();

        test.RunOnError();

        test.Context.Response.StatusCode.Should().Be(500);
        test.Context.Response.StatusReason.Should().Be("Internal Server Error");
    }

    [TestMethod]
    public void ReturnResponse_TerminatesSectionExecution()
    {
        var test = new TerminateSectionReturnResponse().AsTestDocument();
        var headerExecuted = false;
        test.SetupInbound().SetHeader().WithCallback((_, _, _) => headerExecuted = true);

        test.RunInbound();

        headerExecuted.Should().BeFalse();
    }

    [TestMethod]
    public void ReturnResponse_WithHeaders()
    {
        var test = new WithHeadersReturnResponse().AsTestDocument();

        test.RunInbound();

        test.Context.Response.StatusCode.Should().Be(200);
        test.Context.Response.Headers.Should().ContainKey("X-Custom")
            .WhoseValue.Should().ContainInOrder("custom-value");
    }

    [TestMethod]
    public void ReturnResponse_Callback()
    {
        var test = new SimpleReturnResponse().AsTestDocument();
        var callbackExecuted = false;
        test.SetupInbound().ReturnResponse((_, _) => true).WithCallback((context, _) =>
        {
            callbackExecuted = true;
            context.Response.StatusCode = 418;
            context.Response.StatusReason = "I'm a teapot";
        });

        test.RunInbound();

        callbackExecuted.Should().BeTrue();
        test.Context.Response.StatusCode.Should().Be(418);
        test.Context.Response.StatusReason.Should().Be("I'm a teapot");
    }

    class ConfiguredReturnResponse(ReturnResponseConfig config) : IDocument
    {
        public void Inbound(IInboundContext context)
        {
            context.ReturnResponse(config);
            context.SetVariable("after", true);
        }

        public void Backend(IBackendContext context)
        {
            context.ReturnResponse(config);
            context.SetVariable("after", true);
        }

        public void Outbound(IOutboundContext context)
        {
            context.ReturnResponse(config);
            context.SetVariable("after", true);
        }

        public void OnError(IOnErrorContext context)
        {
            context.ReturnResponse(config);
            context.SetVariable("after", true);
        }
    }

    [TestMethod]
    [DataRow("inbound")]
    [DataRow("backend")]
    [DataRow("outbound")]
    [DataRow("on-error")]
    public void ReturnResponse_CopiesSelectedResponseWithoutSharingMutableState(string section)
    {
        var source = new MockResponse
        {
            StatusCode = 202,
            StatusReason = "Accepted",
            Headers = { ["X-Upstream"] = ["first", "second"] },
            Body = { Content = "upstream body" }
        };
        var test = new ConfiguredReturnResponse(new ReturnResponseConfig
        {
            ResponseVariableName = "upstream"
        }).AsTestDocument();
        test.Context.Variables["upstream"] = source;
        test.Context.Response.StatusCode = 503;
        test.Context.Response.Headers["X-Stale"] = ["stale"];
        test.Context.Response.Body.Content = "stale body";

        ExecutionTest.RunSection(test, section);

        var response = test.Context.Response;
        response.Should().NotBeSameAs(source);
        response.StatusCode.Should().Be(202);
        response.StatusReason.Should().Be("Accepted");
        response.Headers.Should().ContainSingle().Which.Key.Should().Be("X-Upstream");
        response.Headers["X-Upstream"].Should().Equal("first", "second");
        response.Headers["X-Upstream"].Should().NotBeSameAs(source.Headers["X-Upstream"]);
        response.Body.Should().NotBeSameAs(source.Body);
        response.Body.Content.Should().Be("upstream body");
        test.Context.Variables["upstream"].Should().BeSameAs(source);
        test.Context.Variables.Should().NotContainKey("after");
        test.Context.ResponseTerminated.Should().BeTrue();

        response.Headers["X-Upstream"][0] = "changed response";
        response.Body.Content = "changed response body";
        source.Headers["X-Upstream"][1] = "changed source";

        source.Headers["X-Upstream"][0].Should().Be("first");
        source.Body.Content.Should().Be("upstream body");
        response.Headers["X-Upstream"][1].Should().Be("second");
    }

    [TestMethod]
    public void ReturnResponse_CopiesAResponseVariableThatAliasesTheCurrentResponse()
    {
        var test = new ConfiguredReturnResponse(new ReturnResponseConfig
        {
            ResponseVariableName = "upstream",
            Status = new StatusConfig { Code = 201, Reason = "Created" },
            Headers = [new HeaderConfig { Name = "X-Upstream", Values = ["replacement"] }],
            Body = new BodyConfig { Content = "replacement body" }
        }).AsTestDocument();
        var source = test.Context.Response;
        source.StatusCode = 202;
        source.StatusReason = "Accepted";
        source.Headers["X-Upstream"] = ["original"];
        source.Body.Content = "original body";
        test.Context.Variables["upstream"] = source;

        test.RunInbound();

        test.Context.Response.Should().NotBeSameAs(source);
        test.Context.Response.StatusCode.Should().Be(201);
        test.Context.Response.Headers["X-Upstream"].Should().Equal("replacement");
        test.Context.Response.Body.Content.Should().Be("replacement body");
        source.StatusCode.Should().Be(202);
        source.StatusReason.Should().Be("Accepted");
        source.Headers["X-Upstream"].Should().Equal("original");
        source.Body.Content.Should().Be("original body");
    }

    sealed class ExternalResponse : IResponse
    {
        public IMessageBody Body { get; init; } = new MockBody { Content = "external body" };
        public IReadOnlyDictionary<string, string[]> Headers { get; init; } =
            new Dictionary<string, string[]> { ["X-External"] = ["external"] };
        public int StatusCode => 203;
        public string StatusReason => "Non-Authoritative Information";
    }

    [TestMethod]
    public void ReturnResponse_AcceptsTheAuthoringResponseContractWithoutConsumingTheBody()
    {
        var source = new ExternalResponse();
        var test = new ConfiguredReturnResponse(new ReturnResponseConfig
        {
            ResponseVariableName = "external"
        }).AsTestDocument();
        test.Context.Variables["external"] = source;

        test.RunInbound();

        test.Context.Response.StatusCode.Should().Be(203);
        test.Context.Response.StatusReason.Should().Be("Non-Authoritative Information");
        test.Context.Response.Headers["X-External"].Should().Equal("external");
        test.Context.Response.Headers["X-External"].Should().NotBeSameAs(source.Headers["X-External"]);
        test.Context.Response.Body.Content.Should().Be("external body");
        ((MockBody)source.Body).Consumed.Should().BeFalse();
    }

    [TestMethod]
    [DataRow("inbound")]
    [DataRow("backend")]
    [DataRow("outbound")]
    [DataRow("on-error")]
    public void ReturnResponse_WithoutAVariableStartsWithTheDefaultResponse(string section)
    {
        var test = new ConfiguredReturnResponse(new ReturnResponseConfig()).AsTestDocument();
        test.Context.Response.StatusCode = 503;
        test.Context.Response.StatusReason = "Service Unavailable";
        test.Context.Response.Headers["X-Stale"] = ["stale"];
        test.Context.Response.Body.Content = "stale";

        ExecutionTest.RunSection(test, section);

        test.Context.Response.StatusCode.Should().Be(200);
        test.Context.Response.StatusReason.Should().Be("OK");
        test.Context.Response.Headers.Should().BeEmpty();
        test.Context.Response.Body.Content.Should().BeNullOrEmpty();
    }

    [TestMethod]
    [DataRow(null, "replacement")]
    [DataRow("override", "replacement")]
    [DataRow("append", "original,replacement")]
    [DataRow("skip", "original")]
    [DataRow("delete", null)]
    public void ReturnResponse_AppliesHeaderActionsToACopyCaseInsensitively(string? action, string? expected)
    {
        var values = new[] { "replacement" };
        var source = new MockResponse
        {
            StatusCode = 202,
            Headers = { ["X-Upstream"] = ["original"] },
            Body = { Content = "upstream body" }
        };
        var test = new ConfiguredReturnResponse(new ReturnResponseConfig
        {
            ResponseVariableName = "upstream",
            Headers =
            [
                new HeaderConfig
                {
                    Name = "x-upstream",
                    ExistsAction = action,
                    Values = action == "delete" ? null : values
                }
            ]
        }).AsTestDocument();
        test.Context.Variables["upstream"] = source;

        test.RunInbound();

        source.Headers["X-Upstream"].Should().Equal("original");
        test.Context.Response.StatusCode.Should().Be(202);
        test.Context.Response.Body.Content.Should().Be("upstream body");
        if (expected is null)
        {
            test.Context.Response.Headers.Should().BeEmpty();
        }
        else
        {
            test.Context.Response.Headers.Should().ContainSingle();
            test.Context.Response.Headers["X-Upstream"].Should().Equal(expected.Split(','));
            values[0] = "changed configuration";
            test.Context.Response.Headers["X-Upstream"].Should().Equal(expected.Split(','));
        }
    }

    [TestMethod]
    [DataRow("override")]
    [DataRow("append")]
    [DataRow("skip")]
    [DataRow("delete")]
    public void ReturnResponse_AppliesHeaderActionsWhenTheHeaderIsMissing(string action)
    {
        var test = new ConfiguredReturnResponse(new ReturnResponseConfig
        {
            Headers =
            [
                new HeaderConfig
                {
                    Name = "X-New",
                    ExistsAction = action,
                    Values = action == "delete" ? null : ["new"]
                }
            ]
        }).AsTestDocument();

        test.RunInbound();

        if (action == "delete")
        {
            test.Context.Response.Headers.Should().BeEmpty();
        }
        else
        {
            test.Context.Response.Headers["X-New"].Should().Equal("new");
        }
    }

    [TestMethod]
    public void ReturnResponse_AppliesRepeatedHeaderActionsInOrder()
    {
        var test = new ConfiguredReturnResponse(new ReturnResponseConfig
        {
            Headers =
            [
                new HeaderConfig { Name = "Set-Cookie", Values = ["a=1"] },
                new HeaderConfig { Name = "set-cookie", ExistsAction = "append", Values = ["b=2"] },
                new HeaderConfig { Name = "SET-COOKIE", ExistsAction = "skip", Values = ["c=3"] }
            ]
        }).AsTestDocument();

        test.RunInbound();

        test.Context.Response.Headers.Should().ContainSingle();
        test.Context.Response.Headers["Set-Cookie"].Should().Equal("a=1", "b=2");
    }

    [TestMethod]
    [DataRow("inbound")]
    [DataRow("backend")]
    [DataRow("outbound")]
    [DataRow("on-error")]
    public void ReturnResponse_EvaluatesChildPolicyExpressionsInEachSection(string section)
    {
        var document = new ExecutionTestDocument
        {
            InboundAction = context => context.ReturnResponse(ExpressedResponse(context.ExpressionContext)),
            BackendAction = context => context.ReturnResponse(ExpressedResponse(context.ExpressionContext)),
            OutboundAction = context => context.ReturnResponse(ExpressedResponse(context.ExpressionContext)),
            OnErrorAction = context => context.ReturnResponse(ExpressedResponse(context.ExpressionContext))
        };
        var test = document.AsTestDocument();
        test.Context.Variables["code"] = 206;
        test.Context.Variables["reason"] = "Partial Content";
        test.Context.Variables["header"] = "X-Expression";
        test.Context.Variables["action"] = "append";
        test.Context.Variables["value"] = "expressed value";
        test.Context.Variables["body"] = "expressed body";

        ExecutionTest.RunSection(test, section);

        test.Context.Response.StatusCode.Should().Be(206);
        test.Context.Response.StatusReason.Should().Be("Partial Content");
        test.Context.Response.Headers["X-Expression"].Should().Equal("expressed value");
        test.Context.Response.Body.Content.Should().Be("expressed body");
    }

    private static ReturnResponseConfig ExpressedResponse(IExpressionContext context) => new()
    {
        Status = new StatusConfig
        {
            Code = (int)context.Variables["code"],
            Reason = (string)context.Variables["reason"]
        },
        Headers =
        [
            new HeaderConfig
            {
                Name = (string)context.Variables["header"],
                ExistsAction = (string)context.Variables["action"],
                Values = [(string)context.Variables["value"]]
            }
        ],
        Body = new BodyConfig { Content = context.Variables["body"] }
    };

    [TestMethod]
    [DataRow("text", "text")]
    [DataRow(42, "42")]
    [DataRow(null, null)]
    public void ReturnResponse_SupportsLiteralObjectAndNullBodies(object? content, string? expected)
    {
        var test = new ConfiguredReturnResponse(new ReturnResponseConfig
        {
            Body = new BodyConfig { Content = content }
        }).AsTestDocument();

        test.RunInbound();

        test.Context.Response.Body.Content.Should().Be(expected);
    }

    sealed class ThrowingBody
    {
        public override string ToString() => throw new InvalidOperationException("body conversion failure");
    }

    [TestMethod]
    public void ReturnResponse_BodyConversionErrorsDoNotPublishAPartialResponse()
    {
        var test = new ConfiguredReturnResponse(new ReturnResponseConfig
        {
            Status = new StatusConfig { Code = 201, Reason = "Created" },
            Body = new BodyConfig { Content = new ThrowingBody() }
        }).AsTestDocument();
        test.Context.Response.Body.Content = "original";

        var error = Assert.ThrowsExactly<PolicyException>(test.RunInbound);

        error.InnerException.Should().BeOfType<InvalidOperationException>()
            .Which.Message.Should().Be("body conversion failure");
        test.Context.Response.StatusCode.Should().Be(200);
        test.Context.Response.Body.Content.Should().Be("original");
        test.Context.ResponseTerminated.Should().BeFalse();
    }

    [TestMethod]
    public void ReturnResponse_DoesNotSilentlyTreatLiquidTemplatesAsLiteralBodies()
    {
        var test = new ConfiguredReturnResponse(new ReturnResponseConfig
        {
            Body = new BodyConfig { Content = "{{ body }}", Template = "liquid" }
        }).AsTestDocument();

        var error = Assert.ThrowsExactly<PolicyException>(test.RunInbound);

        error.InnerException.Should().BeOfType<NotSupportedException>()
            .Which.Message.Should().Contain("liquid");
        test.Context.ResponseTerminated.Should().BeFalse();
    }

    [TestMethod]
    public void ReturnResponse_RejectsNullHeaderEntries()
    {
        var test = new ConfiguredReturnResponse(new ReturnResponseConfig
        {
            Headers = [null!]
        }).AsTestDocument();

        var error = Assert.ThrowsExactly<PolicyException>(test.RunInbound);

        error.InnerException.Should().BeOfType<ArgumentNullException>();
        test.Context.ResponseTerminated.Should().BeFalse();
    }

    [TestMethod]
    [DataRow("inbound")]
    [DataRow("backend")]
    [DataRow("outbound")]
    [DataRow("on-error")]
    public void ReturnResponse_MissingVariablesSurfacePolicyDiagnosticsWithoutTerminating(string section)
    {
        var config = new ReturnResponseConfig { ResponseVariableName = "missing" };
        var test = new ConfiguredReturnResponse(config).AsTestDocument();

        var error = Assert.ThrowsExactly<PolicyException>(() => ExecutionTest.RunSection(test, section));

        error.Policy.Should().Be(nameof(IInboundContext.ReturnResponse));
        error.Section.Should().Be(ExecutionTest.SectionName(section));
        error.PolicyArgs.Should().ContainSingle().Which.Should().BeSameAs(config);
        error.InnerException.Should().BeOfType<KeyNotFoundException>()
            .Which.Message.Should().Contain("missing");
        test.Context.ResponseTerminated.Should().BeFalse();
    }

    [TestMethod]
    [DataRow("not a response")]
    [DataRow(42)]
    [DataRow(null)]
    public void ReturnResponse_RejectsWrongTypeResponseVariables(object? value)
    {
        var test = new ConfiguredReturnResponse(new ReturnResponseConfig
        {
            ResponseVariableName = "invalid"
        }).AsTestDocument();
        test.Context.Variables["invalid"] = value!;

        var error = Assert.ThrowsExactly<PolicyException>(test.RunInbound);

        error.InnerException.Should().BeOfType<ArgumentException>()
            .Which.Message.Should().Contain("invalid").And.Contain("response");
        test.Context.ResponseTerminated.Should().BeFalse();
    }

    [TestMethod]
    [DataRow("")]
    [DataRow(" ")]
    public void ReturnResponse_RejectsEmptyResponseVariableNames(string name)
    {
        var test = new ConfiguredReturnResponse(new ReturnResponseConfig
        {
            ResponseVariableName = name
        }).AsTestDocument();

        var error = Assert.ThrowsExactly<PolicyException>(test.RunInbound);

        error.InnerException.Should().BeOfType<ArgumentException>();
        test.Context.ResponseTerminated.Should().BeFalse();
    }

    [TestMethod]
    [DataRow("inbound")]
    [DataRow("backend")]
    [DataRow("outbound")]
    [DataRow("on-error")]
    public void ReturnResponse_RejectsNullConfigs(string section)
    {
        var test = new ConfiguredReturnResponse(null!).AsTestDocument();

        var error = Assert.ThrowsExactly<PolicyException>(() => ExecutionTest.RunSection(test, section));

        error.Policy.Should().Be(nameof(IInboundContext.ReturnResponse));
        error.Section.Should().Be(ExecutionTest.SectionName(section));
        error.InnerException.Should().BeOfType<ArgumentException>();
        test.Context.ResponseTerminated.Should().BeFalse();
    }

    [TestMethod]
    [DataRow(99)]
    [DataRow(600)]
    public void ReturnResponse_RejectsInvalidStatusCodesWithoutPublishingAPartialResponse(int code)
    {
        var test = new ConfiguredReturnResponse(new ReturnResponseConfig
        {
            Status = new StatusConfig { Code = code, Reason = "Invalid" }
        }).AsTestDocument();
        test.Context.Response.StatusCode = 202;
        test.Context.Response.Body.Content = "original";

        var error = Assert.ThrowsExactly<PolicyException>(test.RunInbound);

        error.InnerException.Should().BeOfType<ArgumentOutOfRangeException>();
        test.Context.Response.StatusCode.Should().Be(202);
        test.Context.Response.Body.Content.Should().Be("original");
        test.Context.ResponseTerminated.Should().BeFalse();
    }

    [TestMethod]
    [DataRow(100)]
    [DataRow(599)]
    public void ReturnResponse_AcceptsStatusCodeBoundaries(int code)
    {
        var test = new ConfiguredReturnResponse(new ReturnResponseConfig
        {
            Status = new StatusConfig { Code = code, Reason = "Boundary" }
        }).AsTestDocument();

        test.RunInbound();

        test.Context.Response.StatusCode.Should().Be(code);
        test.Context.Response.StatusReason.Should().Be("Boundary");
        test.Context.ResponseTerminated.Should().BeTrue();
    }

    [TestMethod]
    public void ReturnResponse_RejectsNullStatusReasonsWithoutPublishingAPartialResponse()
    {
        var test = new ConfiguredReturnResponse(new ReturnResponseConfig
        {
            Status = new StatusConfig { Code = 201, Reason = null! }
        }).AsTestDocument();

        var error = Assert.ThrowsExactly<PolicyException>(test.RunInbound);

        error.InnerException.Should().BeOfType<ArgumentNullException>();
        test.Context.Response.StatusCode.Should().Be(200);
        test.Context.ResponseTerminated.Should().BeFalse();
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow(" ")]
    public void ReturnResponse_RejectsInvalidHeaderNames(string? name)
    {
        var test = new ConfiguredReturnResponse(new ReturnResponseConfig
        {
            Headers = [new HeaderConfig { Name = name!, Values = ["value"] }]
        }).AsTestDocument();

        var error = Assert.ThrowsExactly<PolicyException>(test.RunInbound);

        error.InnerException.Should().BeAssignableTo<ArgumentException>();
        test.Context.ResponseTerminated.Should().BeFalse();
    }

    [TestMethod]
    [DataRow("invalid")]
    [DataRow("")]
    [DataRow("APPEND")]
    public void ReturnResponse_RejectsUnknownHeaderActions(string action)
    {
        var test = new ConfiguredReturnResponse(new ReturnResponseConfig
        {
            Status = new StatusConfig { Code = 201, Reason = "Created" },
            Headers = [new HeaderConfig { Name = "X-Invalid", ExistsAction = action, Values = ["value"] }]
        }).AsTestDocument();
        test.Context.Response.Body.Content = "original";

        var error = Assert.ThrowsExactly<PolicyException>(test.RunInbound);

        error.InnerException.Should().BeOfType<ArgumentException>()
            .Which.Message.Should().Contain($"'{action}'");
        test.Context.Response.StatusCode.Should().Be(200);
        test.Context.Response.Body.Content.Should().Be("original");
        test.Context.ResponseTerminated.Should().BeFalse();
    }

    [TestMethod]
    [DataRow("override")]
    [DataRow("append")]
    [DataRow("skip")]
    public void ReturnResponse_RequiresValuesForNonDeleteHeaderActions(string action)
    {
        var test = new ConfiguredReturnResponse(new ReturnResponseConfig
        {
            Headers = [new HeaderConfig { Name = "X-Invalid", ExistsAction = action }]
        }).AsTestDocument();
        test.Context.Response.Headers["X-Invalid"] = ["existing"];

        var error = Assert.ThrowsExactly<PolicyException>(test.RunInbound);

        error.InnerException.Should().BeOfType<ArgumentNullException>();
        test.Context.Response.Headers["X-Invalid"].Should().Equal("existing");
        test.Context.ResponseTerminated.Should().BeFalse();
    }

    [TestMethod]
    [DataRow("inbound")]
    [DataRow("backend")]
    [DataRow("outbound")]
    [DataRow("on-error")]
    public void ReturnResponse_CallbackOverridesTheConfigAndStillTerminates(string section)
    {
        var test = new ConfiguredReturnResponse(new ReturnResponseConfig
        {
            ResponseVariableName = "missing",
            Status = new StatusConfig { Code = 600, Reason = "invalid" }
        }).AsTestDocument();
        var calls = 0;
        void Callback(GatewayContext context, ReturnResponseConfig config)
        {
            calls++;
            context.Response.StatusCode = 204;
        }

        test.SetupInbound().ReturnResponse((_, _) => true).WithCallback(Callback);
        test.SetupBackend().ReturnResponse((_, _) => true).WithCallback(Callback);
        test.SetupOutbound().ReturnResponse((_, _) => true).WithCallback(Callback);
        test.SetupOnError().ReturnResponse((_, _) => true).WithCallback(Callback);

        ExecutionTest.RunSection(test, section);

        calls.Should().Be(1);
        test.Context.Response.StatusCode.Should().Be(204);
        test.Context.Variables.Should().NotContainKey("after");
        test.Context.ResponseTerminated.Should().BeTrue();
    }

    [TestMethod]
    public void ReturnResponse_UnmatchedCallbackUsesDefaultBehavior()
    {
        var test = new ConfiguredReturnResponse(new ReturnResponseConfig
        {
            Status = new StatusConfig { Code = 201, Reason = "Created" }
        }).AsTestDocument();
        test.SetupInbound().ReturnResponse((_, config) => config.Status?.Code == 202)
            .WithCallback((_, _) => Assert.Fail("The predicate did not match."));

        test.RunInbound();

        test.Context.Response.StatusCode.Should().Be(201);
        test.Context.ResponseTerminated.Should().BeTrue();
    }

    [TestMethod]
    [DataRow("inbound")]
    [DataRow("backend")]
    [DataRow("outbound")]
    [DataRow("on-error")]
    public void ReturnResponse_CallbackErrorsSurfaceWithoutTerminating(string section)
    {
        var test = new ConfiguredReturnResponse(new ReturnResponseConfig()).AsTestDocument();
        static void Callback(GatewayContext context, ReturnResponseConfig config) =>
            throw new InvalidOperationException("callback failure");
        test.SetupInbound().ReturnResponse((_, _) => true).WithCallback(Callback);
        test.SetupBackend().ReturnResponse((_, _) => true).WithCallback(Callback);
        test.SetupOutbound().ReturnResponse((_, _) => true).WithCallback(Callback);
        test.SetupOnError().ReturnResponse((_, _) => true).WithCallback(Callback);

        var error = Assert.ThrowsExactly<PolicyException>(() => ExecutionTest.RunSection(test, section));

        error.Policy.Should().Be(nameof(IInboundContext.ReturnResponse));
        error.Section.Should().Be(ExecutionTest.SectionName(section));
        error.InnerException.Should().BeOfType<InvalidOperationException>()
            .Which.Message.Should().Be("callback failure");
        test.Context.ResponseTerminated.Should().BeFalse();
    }

    [TestMethod]
    public void ReturnResponse_CallbackCanTerminateEarlyWithoutLosingPipelineTermination()
    {
        var test = new ConfiguredReturnResponse(new ReturnResponseConfig()).AsTestDocument();
        test.SetupInbound().ReturnResponse((_, _) => true).WithCallback((context, _) =>
        {
            context.Response.StatusCode = 204;
            throw new FinishSectionProcessingException();
        });

        test.RunInbound();

        test.Context.Response.StatusCode.Should().Be(204);
        test.Context.ResponseTerminated.Should().BeTrue();
        test.Context.Variables.Should().NotContainKey("after");
    }

    [TestMethod]
    public void ReturnResponse_DoesNotPreventLaterStandaloneSectionInvocations()
    {
        var test = new ExecutionTestDocument
        {
            InboundAction = context => context.ReturnResponse(new ReturnResponseConfig()),
            BackendAction = context => context.SetVariable("backend", true),
            OutboundAction = context => context.SetVariable("outbound", true),
            OnErrorAction = context => context.SetVariable("on-error", true)
        }.AsTestDocument();

        test.RunInbound();
        test.RunBackend();
        test.RunOutbound();
        test.RunOnError();

        test.Context.ResponseTerminated.Should().BeTrue();
        test.Context.Variables.Keys.Should().Contain(["backend", "outbound", "on-error"]);
    }
}