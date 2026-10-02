// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Globalization;

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring.Expressions;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Document;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Data;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Expressions;

namespace Test.Emulator.Emulator.Policies;

[TestClass]
public class MockResponseTests
{
    class SimpleMockResponse : IDocument
    {
        public void Inbound(IInboundContext context)
        {
            context.MockResponse();
        }

        public void Outbound(IOutboundContext context)
        {
            context.MockResponse();
        }

        public void OnError(IOnErrorContext context)
        {
            context.MockResponse();
        }
    }

    class TerminateSectionMockResponse : IDocument
    {
        public void Inbound(IInboundContext context)
        {
            context.MockResponse();
            context.SetHeader("X-Header", "Value");
        }
    }

    class WithIndexMockResponse : IDocument
    {
        public void Inbound(IInboundContext context)
        {
            context.MockResponse(new MockResponseConfig { Index = 1 });
        }
    }

    class WithCodeMockResponse : IDocument
    {
        public void Inbound(IInboundContext context)
        {
            context.MockResponse(new MockResponseConfig { StatusCode = 201 });
        }
    }

    class WithContentTypeMockResponse : IDocument
    {
        public void Inbound(IInboundContext context)
        {
            context.MockResponse(new MockResponseConfig { ContentType = "plain/text" });
        }
    }

    class WithCodeAndContentTypeMockResponse : IDocument
    {
        public void Inbound(IInboundContext context)
        {
            context.MockResponse(new MockResponseConfig { StatusCode = 201, ContentType = "plain/text" });
        }
    }

    [TestMethod]
    public void MockResponse_Inbound()
    {
        var test = new SimpleMockResponse().AsTestDocument();

        test.RunInbound();

        test.Context.Response.StatusCode.Should().Be(200);
        test.Context.Response.Headers.GetValueOrDefault("Content-Length").Should().Be("0");
    }

    [TestMethod]
    public void MockResponse_Outbound()
    {
        var test = new SimpleMockResponse().AsTestDocument();

        test.RunOutbound();

        test.Context.Response.StatusCode.Should().Be(200);
        test.Context.Response.Headers.GetValueOrDefault("Content-Length").Should().Be("0");
    }

    [TestMethod]
    public void MockResponse_OnError()
    {
        var test = new SimpleMockResponse().AsTestDocument();

        test.RunOnError();

        test.Context.Response.StatusCode.Should().Be(200);
        test.Context.Response.Headers.GetValueOrDefault("Content-Length").Should().Be("0");
    }

    [TestMethod]
    public void MockResponse_TerminateSectionExecution()
    {
        var test = new TerminateSectionMockResponse().AsTestDocument();
        bool executed = false;
        test.SetupInbound().SetHeader().WithCallback((_, _, _) => executed = true);

        test.RunInbound();

        executed.Should().BeFalse();
    }

    [TestMethod]
    public void MockResponse_WithIndexExample()
    {
        var test = new WithIndexMockResponse().AsTestDocument();
        var notUsed = new ResponseExample(500, "{ \"error\": \"error\" }", "application/json");
        var used = new ResponseExample(400, "<validation>not valid</validation>", "application/xml");
        test.SetupResponseExampleStore().Add(test.Context, notUsed, used, notUsed);

        test.RunInbound();

        var response = test.Context.Response;
        response.StatusCode.Should().Be(used.ResponseCode);
        response.Headers.GetValueOrDefault("Content-Type").Should().Be(used.ContentType);
        response.Headers.GetValueOrDefault("Content-Length")
            .Should().Be(used.Sample.Length.ToString(CultureInfo.InvariantCulture));
        response.Body.Content.Should().Be(used.Sample);
    }

    [TestMethod]
    public void MockResponse_WithCodeExample()
    {
        var test = new WithCodeMockResponse().AsTestDocument();
        var notUsed = new ResponseExample(500, "{ \"error\": \"error\" }", "application/json");
        var used = new ResponseExample(201, "<data>complex data</data>", "application/xml");
        test.SetupResponseExampleStore().Add(test.Context, notUsed, used, notUsed);

        test.RunInbound();

        var response = test.Context.Response;
        response.StatusCode.Should().Be(used.ResponseCode);
        response.Headers.GetValueOrDefault("Content-Type").Should().Be(used.ContentType);
        response.Headers.GetValueOrDefault("Content-Length")
            .Should().Be(used.Sample.Length.ToString(CultureInfo.InvariantCulture));
        response.Body.Content.Should().Be(used.Sample);
    }

    [TestMethod]
    public void MockResponse_WithContentExample()
    {
        var test = new WithContentTypeMockResponse().AsTestDocument();
        var notUsed = new ResponseExample(500, "error", "plain/text");
        var used = new ResponseExample(200, "complex data", "plain/text");
        test.SetupResponseExampleStore().Add(test.Context, notUsed, used, notUsed);

        test.RunInbound();

        var response = test.Context.Response;
        response.StatusCode.Should().Be(used.ResponseCode);
        response.Headers.GetValueOrDefault("Content-Type").Should().Be(used.ContentType);
        response.Headers.GetValueOrDefault("Content-Length")
            .Should().Be(used.Sample.Length.ToString(CultureInfo.InvariantCulture));
        response.Body.Content.Should().Be(used.Sample);
    }

    [TestMethod]
    public void MockResponse_WithCodeAndContentExample()
    {
        var test = new WithCodeAndContentTypeMockResponse().AsTestDocument();
        var notUsed = new ResponseExample(500, "error", "plain/text");
        var used = new ResponseExample(201, "complex data", "plain/text");
        test.SetupResponseExampleStore().Add(test.Context, notUsed, used, notUsed);

        test.RunInbound();

        var response = test.Context.Response;
        response.StatusCode.Should().Be(used.ResponseCode);
        response.Headers.GetValueOrDefault("Content-Type").Should().Be(used.ContentType);
        response.Headers.GetValueOrDefault("Content-Length")
            .Should().Be(used.Sample.Length.ToString(CultureInfo.InvariantCulture));
        response.Body.Content.Should().Be(used.Sample);
    }

    [TestMethod]
    [DataRow("inbound")]
    [DataRow("outbound")]
    [DataRow("on-error")]
    public void MockResponse_EmptyResponseReplacesStaleStateWithoutReplacingSharedObjects(string section)
    {
        var test = CreateTest();
        SeedResponse(test.Context);
        var response = test.Context.Response;
        var headers = response.Headers;
        var body = response.Body;
        test.Context.Request.Headers["X-Request"] = ["keep"];
        test.Context.Request.Body.Content = "request body";
        test.Context.Variables["keep"] = "value";

        RunSection(test, section);

        test.Context.Response.Should().BeSameAs(response);
        response.Headers.Should().BeSameAs(headers);
        response.Body.Should().BeSameAs(body);
        response.StatusCode.Should().Be(200);
        response.StatusReason.Should().Be("OK");
        response.Body.Content.Should().BeEmpty();
        response.Headers.Should().ContainSingle();
        response.Headers["Content-Length"].Should().Equal("0");
        test.Context.ResponseTerminated.Should().BeTrue();
        test.Context.Variables.Keys.Should().Equal("keep");
        test.Context.Variables["keep"].Should().Be("value");
        test.Context.Request.Headers["X-Request"].Should().Equal("keep");
        test.Context.Request.Body.Content.Should().Be("request body");
    }

    [TestMethod]
    [DataRow("")]
    [DataRow(null)]
    public void MockResponse_EmptyExampleClearsStaleBody(string? sample)
    {
        var test = CreateTest(new MockResponseConfig { Index = 0 });
        SeedResponse(test.Context);
        test.SetupResponseExampleStore().Add(test.Context,
            new ResponseExample(201, sample!, "application/json"));

        test.RunInbound();

        var response = test.Context.Response;
        response.StatusCode.Should().Be(201);
        response.StatusReason.Should().Be("Created");
        response.Body.Content.Should().BeEmpty();
        response.Headers.Should().HaveCount(2);
        response.Headers["Content-Type"].Should().Equal("application/json");
        response.Headers["Content-Length"].Should().Equal("0");
        test.Context.ResponseTerminated.Should().BeTrue();
    }

    [TestMethod]
    [DataRow(100, "Continue")]
    [DataRow(200, "OK")]
    [DataRow(201, "Created")]
    [DataRow(204, "No Content")]
    [DataRow(400, "Bad Request")]
    [DataRow(403, "Forbidden")]
    [DataRow(404, "Not Found")]
    [DataRow(500, "Internal Server Error")]
    [DataRow(599, "")]
    public void MockResponse_UsesReasonPhraseForConfiguredStatus(int statusCode, string reason)
    {
        var test = CreateTest(new MockResponseConfig { StatusCode = statusCode });
        SeedResponse(test.Context);

        test.RunInbound();

        test.Context.Response.StatusCode.Should().Be(statusCode);
        test.Context.Response.StatusReason.Should().Be(reason);
        test.Context.Response.Body.Content.Should().BeEmpty();
        test.Context.Response.Headers.Should().ContainSingle();
        test.Context.Response.Headers["Content-Length"].Should().Equal("0");
        test.Context.ResponseTerminated.Should().BeTrue();
    }

    [TestMethod]
    [DataRow(-1)]
    [DataRow(0)]
    [DataRow(99)]
    [DataRow(600)]
    [DataRow(int.MaxValue)]
    public void MockResponse_RejectsInvalidStatusBeforeChangingResponse(int statusCode)
    {
        var test = CreateTest(new MockResponseConfig { StatusCode = statusCode });
        SeedResponse(test.Context);

        var error = Assert.ThrowsExactly<PolicyException>(test.RunInbound);

        AssertPolicyError(error);
        error.InnerException.Should().BeOfType<ArgumentOutOfRangeException>()
            .Which.ActualValue.Should().Be(statusCode);
        AssertOriginalResponse(test.Context);
    }

    [TestMethod]
    [DataRow(99)]
    [DataRow(600)]
    public void MockResponse_RejectsInvalidSelectedExampleStatus(int statusCode)
    {
        var test = CreateTest(new MockResponseConfig { Index = 0 });
        SeedResponse(test.Context);
        test.SetupResponseExampleStore().Add(test.Context, new ResponseExample(statusCode, "invalid"));

        var error = Assert.ThrowsExactly<PolicyException>(test.RunInbound);

        AssertPolicyError(error);
        error.InnerException.Should().BeOfType<ArgumentOutOfRangeException>()
            .Which.ActualValue.Should().Be(statusCode);
        AssertOriginalResponse(test.Context);
    }

    [TestMethod]
    [DataRow("ASCII", 5)]
    [DataRow("caf\u00e9", 5)]
    [DataRow("\u4f60\u597d", 6)]
    [DataRow("\ud83d\ude00", 4)]
    [DataRow("", 0)]
    public void MockResponse_ContentLengthCountsUtf8Bytes(string sample, int byteCount)
    {
        var test = CreateTest(new MockResponseConfig { StatusCode = 201, ContentType = "application/json" });
        SeedResponse(test.Context);
        test.SetupResponseExampleStore().Add(test.Context,
            new ResponseExample(200, "wrong status", "application/json"),
            new ResponseExample(201, sample, "application/json"));

        test.RunInbound();

        var response = test.Context.Response;
        response.StatusCode.Should().Be(201);
        response.StatusReason.Should().Be("Created");
        response.Body.Content.Should().Be(sample);
        response.Body.As<byte[]>(preserveContent: true).Should().HaveCount(byteCount);
        response.Headers.Should().HaveCount(2);
        response.Headers["Content-Type"].Should().Equal("application/json");
        response.Headers["Content-Length"].Should().Equal(byteCount.ToString(CultureInfo.InvariantCulture));
    }

    [TestMethod]
    [DataRow("application/json")]
    [DataRow("APPLICATION/JSON")]
    public void MockResponse_SelectsFirstMatchingStatusAndContentType(string contentType)
    {
        var test = CreateTest(new MockResponseConfig { StatusCode = 201, ContentType = contentType });
        test.SetupResponseExampleStore().Add(test.Context,
            new ResponseExample(201, "wrong media type", "application/xml"),
            new ResponseExample(200, "wrong status", "application/json"),
            new ResponseExample(201, "selected", "application/json"),
            new ResponseExample(201, "later match", "application/json"));

        test.RunInbound();

        test.Context.Response.StatusCode.Should().Be(201);
        test.Context.Response.StatusReason.Should().Be("Created");
        test.Context.Response.Body.Content.Should().Be("selected");
        test.Context.Response.Headers["Content-Type"].Should().Equal("application/json");
        test.Context.Response.Headers["Content-Length"].Should().Equal("8");
    }

    [TestMethod]
    public void MockResponse_WithoutContentTypeSelectsFirstExampleForRequestedStatus()
    {
        var test = CreateTest(new MockResponseConfig { StatusCode = 201 });
        test.SetupResponseExampleStore().Add(test.Context,
            new ResponseExample(500, "wrong status", "application/json"),
            new ResponseExample(201, "first", "text/plain"),
            new ResponseExample(201, "second"));

        test.RunInbound();

        test.Context.Response.StatusCode.Should().Be(201);
        test.Context.Response.Body.Content.Should().Be("first");
        test.Context.Response.Headers["Content-Type"].Should().Equal("text/plain");
        test.Context.Response.Headers["Content-Length"].Should().Equal("5");
    }

    [TestMethod]
    [DataRow(201)]
    [DataRow(404)]
    public void MockResponse_DoesNotFallBackToAnExampleWithDifferentSelectors(int statusCode)
    {
        var test = CreateTest(new MockResponseConfig { StatusCode = statusCode, ContentType = "application/json" });
        SeedResponse(test.Context);
        test.SetupResponseExampleStore().Add(test.Context,
            new ResponseExample(200, "wrong status", "application/json"),
            new ResponseExample(201, "wrong media type", "application/xml"));

        test.RunInbound();

        test.Context.Response.StatusCode.Should().Be(statusCode);
        test.Context.Response.StatusReason.Should().Be(statusCode == 201 ? "Created" : "Not Found");
        test.Context.Response.Body.Content.Should().BeEmpty();
        test.Context.Response.Headers.Should().HaveCount(2);
        test.Context.Response.Headers["Content-Type"].Should().Equal("application/json");
        test.Context.Response.Headers["Content-Length"].Should().Equal("0");
        test.Context.ResponseTerminated.Should().BeTrue();
    }

    [TestMethod]
    [DataRow(0, 201, "Created", "first")]
    [DataRow(2, 500, "Internal Server Error", "third")]
    public void MockResponse_IndexSelectsExactZeroBasedExample(
        int index, int statusCode, string reason, string sample)
    {
        var test = CreateTest(new MockResponseConfig
        {
            Index = index, StatusCode = 404, ContentType = "application/xml"
        });
        test.SetupResponseExampleStore().Add(test.Context,
            new ResponseExample(201, "first", "text/plain"),
            new ResponseExample(202, "second", "application/json"),
            new ResponseExample(500, "third", "text/plain"));

        test.RunInbound();

        test.Context.Response.StatusCode.Should().Be(statusCode);
        test.Context.Response.StatusReason.Should().Be(reason);
        test.Context.Response.Body.Content.Should().Be(sample);
        test.Context.Response.Headers["Content-Type"].Should().Equal("text/plain");
        test.Context.Response.Headers["Content-Length"].Should().Equal("5");
    }

    [TestMethod]
    [DataRow(-1, 0)]
    [DataRow(-1, 2)]
    [DataRow(int.MinValue, 2)]
    [DataRow(0, 0)]
    [DataRow(2, 2)]
    [DataRow(int.MaxValue, 2)]
    public void MockResponse_RejectsIndexOutsideAvailableExamples(int index, int exampleCount)
    {
        var test = CreateTest(new MockResponseConfig { Index = index });
        SeedResponse(test.Context);
        var examples = Enumerable.Range(0, exampleCount)
            .Select(i => new ResponseExample(200, $"sample-{i}", "application/json")).ToArray();
        test.SetupResponseExampleStore().Add(test.Context, examples);

        var error = Assert.ThrowsExactly<PolicyException>(test.RunInbound);

        AssertPolicyError(error);
        var indexError = error.InnerException.Should().BeOfType<ArgumentOutOfRangeException>().Which;
        indexError.ParamName.Should().Be(nameof(MockResponseConfig.Index));
        indexError.ActualValue.Should().Be(index);
        AssertOriginalResponse(test.Context);
    }

    [TestMethod]
    public void MockResponse_UsesExamplesOnlyFromCurrentApiAndOperation()
    {
        var test = CreateTest();
        test.SetupResponseExampleStore().Add("other-api", test.Context.Operation.Id,
            new ResponseExample(200, "wrong API", "text/plain"));
        test.SetupResponseExampleStore().Add(test.Context.Api.Id, "other-operation",
            new ResponseExample(200, "wrong operation", "text/plain"));
        test.SetupResponseExampleStore().Add(test.Context, new ResponseExample(200, "selected", "text/plain"));

        test.RunInbound();

        test.Context.Response.Body.Content.Should().Be("selected");
        test.Context.Response.Headers["Content-Length"].Should().Equal("8");
    }

    [TestMethod]
    [DataRow("inbound", false)]
    [DataRow("inbound", true)]
    [DataRow("outbound", false)]
    [DataRow("outbound", true)]
    [DataRow("on-error", false)]
    [DataRow("on-error", true)]
    public void MockResponse_CallbackOverridesResponseButStillTerminates(string section, bool throwsTermination)
    {
        var config = new MockResponseConfig { StatusCode = 201 };
        var test = CreateTest(config);
        var callbacks = new List<string>();
        SetupMock(test, section, (_, _) => false).WithCallback((_, _) => Assert.Fail("Nonmatching callback ran."));
        SetupMock(test, section, (context, candidate) => ReferenceEquals(context, test.Context) && candidate == config)
            .WithCallback((context, candidate) =>
            {
                candidate.Should().BeSameAs(config);
                callbacks.Add("first");
                context.Response.StatusCode = 202;
                context.Response.StatusReason = "callback reason";
                context.Response.Headers["X-Callback"] = ["selected"];
                context.Response.Body.Content = "callback body";
                if (throwsTermination)
                {
                    throw new FinishSectionProcessingException();
                }
            });
        SetupMock(test, section, (_, _) => true).WithCallback((_, _) => Assert.Fail("Later callback ran."));

        RunSection(test, section);

        callbacks.Should().Equal("first");
        test.Context.Response.StatusCode.Should().Be(202);
        test.Context.Response.StatusReason.Should().Be("callback reason");
        test.Context.Response.Body.Content.Should().Be("callback body");
        test.Context.Response.Headers.Should().ContainSingle();
        test.Context.Response.Headers["X-Callback"].Should().Equal("selected");
        test.Context.ResponseTerminated.Should().BeTrue();
        test.Context.Variables.Should().NotContainKey("after-mock-response");
    }

    [TestMethod]
    [DataRow("inbound")]
    [DataRow("outbound")]
    [DataRow("on-error")]
    public void MockResponse_NonmatchingCallbackUsesDefaultBehavior(string section)
    {
        var test = CreateTest();
        SetupMock(test, section, (_, _) => false).WithCallback((_, _) => Assert.Fail("Callback ran."));

        RunSection(test, section);

        test.Context.Response.StatusCode.Should().Be(200);
        test.Context.Response.StatusReason.Should().Be("OK");
        test.Context.Response.Body.Content.Should().BeEmpty();
        test.Context.ResponseTerminated.Should().BeTrue();
        test.Context.Variables.Should().NotContainKey("after-mock-response");
    }

    [TestMethod]
    [DataRow("inbound")]
    [DataRow("outbound")]
    [DataRow("on-error")]
    public void MockResponse_CallbackErrorsKeepPolicyAndSectionMetadata(string section)
    {
        var test = CreateTest();
        SeedResponse(test.Context);
        var failure = new InvalidOperationException("mock response callback failed");
        SetupMock(test, section, (_, _) => true).WithCallback((_, _) => throw failure);

        var error = Assert.ThrowsExactly<PolicyException>(() => RunSection(test, section));

        AssertPolicyError(error, section);
        error.InnerException.Should().BeSameAs(failure);
        AssertOriginalResponse(test.Context);
    }

    [TestMethod]
    [DataRow("inbound", false, false)]
    [DataRow("inbound", false, true)]
    [DataRow("inbound", true, false)]
    [DataRow("inbound", true, true)]
    [DataRow("outbound", false, false)]
    [DataRow("outbound", false, true)]
    [DataRow("outbound", true, false)]
    [DataRow("outbound", true, true)]
    [DataRow("on-error", false, false)]
    [DataRow("on-error", false, true)]
    [DataRow("on-error", true, false)]
    [DataRow("on-error", true, true)]
    public void MockResponse_TerminatesAllCoordinatedScopesAndSections(
        string section, bool nested, bool callback)
    {
        var terminal = CreateDocument();
        var outer = CreateTracingDocument();
        if (nested)
        {
            outer.InboundAction = context =>
            {
                context.SetVariable("outer-before", true);
                context.Base();
                context.SetVariable("outer-after", true);
            };
            outer.OutboundAction = context =>
            {
                context.SetVariable("outer-before", true);
                context.Base();
                context.SetVariable("outer-after", true);
            };
            outer.OnErrorAction = context =>
            {
                context.SetVariable("outer-before", true);
                context.Base();
                context.SetVariable("outer-after", true);
            };
        }

        var firstScope = section == "inbound" ? PolicyScope.Global : PolicyScope.Operation;
        var secondScope = section == "inbound" ? PolicyScope.Operation : PolicyScope.Global;
        var pipeline = PolicyPipelineBuilder.Create()
            .AddPolicy(firstScope, nested ? outer : terminal)
            .AddPolicy(secondScope, nested ? terminal : outer)
            .Build();
        if (callback)
        {
            var setup = new TestDocument(terminal) { Context = pipeline.Context };
            SetupMock(setup, section, (_, _) => true)
                .WithCallback((context, _) => context.Response.Body.Content = "callback response");
        }

        switch (section)
        {
            case "inbound":
                if (nested) pipeline.RunAllNested();
                else pipeline.RunAll();
                break;
            case "outbound":
                if (nested) pipeline.RunOutboundNested();
                else pipeline.RunOutbound();
                break;
            case "on-error":
                if (nested) pipeline.RunOnErrorNested();
                else pipeline.RunOnError();
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(section));
        }
        pipeline.RunAll();
        pipeline.RunAllNested();
        pipeline.RunOnError();
        pipeline.RunOnErrorNested();

        pipeline.Context.ResponseTerminated.Should().BeTrue();
        pipeline.Context.Response.StatusCode.Should().Be(200);
        pipeline.Context.Response.Body.Content.Should().Be(callback ? "callback response" : string.Empty);
        pipeline.Context.Variables.Keys.Should().BeEquivalentTo(nested ? new[] { "outer-before" } : []);
    }

    [TestMethod]
    [DataRow("inbound", false)]
    [DataRow("inbound", true)]
    [DataRow("outbound", false)]
    [DataRow("outbound", true)]
    [DataRow("on-error", false)]
    [DataRow("on-error", true)]
    public void MockResponse_CallbackPromotesSectionOnlyInvokeRequestToPipelineTermination(
        string section, bool nested)
    {
        Action invokeRequest = () => Assert.Fail("The terminal section was not entered.");
        var terminal = CreateTracingDocument();
        var outer = CreateTracingDocument();
        switch (section)
        {
            case "inbound":
                terminal.InboundAction = context =>
                {
                    invokeRequest = () => context.InvokeRequest(new InvokeRequestConfig());
                    context.MockResponse();
                    context.SetVariable("after-mock-response", true);
                };
                outer.InboundAction = context =>
                {
                    context.SetVariable("outer-before", true);
                    context.Base();
                    context.SetVariable("outer-after", true);
                };
                break;
            case "outbound":
                terminal.OutboundAction = context =>
                {
                    invokeRequest = () => context.InvokeRequest(new InvokeRequestConfig());
                    context.MockResponse();
                    context.SetVariable("after-mock-response", true);
                };
                outer.OutboundAction = context =>
                {
                    context.SetVariable("outer-before", true);
                    context.Base();
                    context.SetVariable("outer-after", true);
                };
                break;
            case "on-error":
                terminal.OnErrorAction = context =>
                {
                    invokeRequest = () => context.InvokeRequest(new InvokeRequestConfig());
                    context.MockResponse();
                    context.SetVariable("after-mock-response", true);
                };
                outer.OnErrorAction = context =>
                {
                    context.SetVariable("outer-before", true);
                    context.Base();
                    context.SetVariable("outer-after", true);
                };
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(section));
        }
        var firstScope = section == "inbound" ? PolicyScope.Global : PolicyScope.Operation;
        var lastScope = section == "inbound" ? PolicyScope.Operation : PolicyScope.Global;
        var pipeline = PolicyPipelineBuilder.Create()
            .AddPolicy(firstScope, nested ? outer : terminal)
            .AddPolicy(PolicyScope.Api, nested ? terminal : CreateTracingDocument())
            .AddPolicy(lastScope, CreateTracingDocument())
            .ConfigureContext(SeedResponse)
            .Build();
        var response = pipeline.Context.Response;
        var headers = response.Headers;
        var body = response.Body;
        var setup = new TestDocument(terminal) { Context = pipeline.Context };
        var invokeCalls = 0;
        setup.SetupInbound().InvokeRequest().WithCallback((_, _) => invokeCalls++);
        setup.SetupOutbound().InvokeRequest().WithCallback((_, _) => invokeCalls++);
        setup.SetupOnError().InvokeRequest().WithCallback((_, _) => invokeCalls++);
        SetupMock(setup, section, (_, _) => true).WithCallback((context, _) =>
        {
            context.Variables["terminal-callback"] = true;
            context.Response.StatusCode = 201;
            context.Response.StatusReason = "Created";
            context.Response.Headers.Clear();
            context.Response.Headers["Content-Type"] = ["text/plain"];
            context.Response.Headers["Content-Length"] = ["5"];
            context.Response.Body.Content = "caf\u00e9";
            invokeRequest();
            context.Variables["after-invoke-request"] = true;
        });

        switch (section)
        {
            case "inbound":
                if (nested) pipeline.RunAllNested();
                else pipeline.RunAll();
                break;
            case "outbound":
                if (nested) pipeline.RunOutboundNested();
                else pipeline.RunOutbound();
                break;
            case "on-error":
                if (nested) pipeline.RunOnErrorNested();
                else pipeline.RunOnError();
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(section));
        }
        pipeline.RunAll();
        pipeline.RunAllNested();
        pipeline.RunOnError();
        pipeline.RunOnErrorNested();

        invokeCalls.Should().Be(1);
        pipeline.Context.ResponseTerminated.Should().BeTrue();
        pipeline.Context.Variables.Keys.Should().BeEquivalentTo(
            nested ? new[] { "outer-before", "terminal-callback" } : ["terminal-callback"]);
        pipeline.Context.Response.Should().BeSameAs(response);
        response.Headers.Should().BeSameAs(headers);
        response.Body.Should().BeSameAs(body);
        response.StatusCode.Should().Be(201);
        response.StatusReason.Should().Be("Created");
        response.Body.Content.Should().Be("caf\u00e9");
        response.Headers.Should().HaveCount(2);
        response.Headers["Content-Type"].Should().Equal("text/plain");
        response.Headers["Content-Length"].Should().Equal("5");
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void MockResponse_SectionOnlyInvokeRequestOutsideTerminalCallbackRemainsSectionOnly(bool nested)
    {
        var invoke = new PolicyDocument
        {
            InboundAction = context =>
            {
                context.InvokeRequest(new InvokeRequestConfig());
                context.SetVariable("after-invoke-request", true);
            },
            OutboundAction = context => context.Base()
        };
        var outer = CreateTracingDocument();
        outer.InboundAction = context =>
        {
            context.SetVariable("outer-before", true);
            context.Base();
            context.SetVariable("outer-after", true);
        };
        var pipeline = PolicyPipelineBuilder.Create()
            .AddPolicy(PolicyScope.Global, nested ? outer : invoke)
            .AddPolicy(PolicyScope.Operation, nested ? invoke : CreateTracingDocument())
            .ConfigureContext(SeedResponse)
            .Build();
        var setup = new TestDocument(invoke) { Context = pipeline.Context };
        var invokeCalls = 0;
        setup.SetupInbound().InvokeRequest().WithCallback((_, _) => invokeCalls++);

        if (nested) pipeline.RunAllNested();
        else pipeline.RunAll();

        invokeCalls.Should().Be(1);
        AssertOriginalResponse(pipeline.Context);
        pipeline.Context.Variables.Keys.Should().BeEquivalentTo(nested
            ? new[] { "outer-before", "outer-after", "backend", "outbound" }
            : ["inbound", "backend", "outbound"]);
    }

    [TestMethod]
    public void MockResponse_TerminationDoesNotDisableIndependentSectionExecution()
    {
        var test = CreateTest();
        test.RunInbound();
        var independent = new TestDocument(CreateTracingDocument()) { Context = test.Context };

        independent.RunBackend();
        independent.RunOutbound();
        independent.RunOnError();

        test.Context.ResponseTerminated.Should().BeTrue();
        test.Context.Variables.Keys.Should().BeEquivalentTo("backend", "outbound", "on-error");
        test.Context.Response.StatusCode.Should().Be(200);
        test.Context.Response.StatusReason.Should().Be("OK");
    }

    private static TestDocument CreateTest(MockResponseConfig? config = null) =>
        CreateDocument(config).AsTestDocument();

    private static PolicyDocument CreateDocument(MockResponseConfig? config = null) => new()
    {
        InboundAction = context =>
        {
            context.MockResponse(config);
            context.SetVariable("after-mock-response", true);
        },
        OutboundAction = context =>
        {
            context.MockResponse(config);
            context.SetVariable("after-mock-response", true);
        },
        OnErrorAction = context =>
        {
            context.MockResponse(config);
            context.SetVariable("after-mock-response", true);
        }
    };

    private static PolicyDocument CreateTracingDocument() => new()
    {
        InboundAction = context => context.SetVariable("inbound", true),
        BackendAction = context => context.SetVariable("backend", true),
        OutboundAction = context => context.SetVariable("outbound", true),
        OnErrorAction = context => context.SetVariable("on-error", true)
    };

    private static void SeedResponse(GatewayContext context)
    {
        context.Response.StatusCode = 502;
        context.Response.StatusReason = "stale reason";
        context.Response.Headers = new Dictionary<string, string[]>
        {
            ["content-type"] = ["text/stale"],
            ["CONTENT-LENGTH"] = ["999"],
            ["X-Stale"] = ["discard"]
        };
        context.Response.Body.Content = "stale body";
    }

    private static void AssertOriginalResponse(GatewayContext context)
    {
        context.Response.StatusCode.Should().Be(502);
        context.Response.StatusReason.Should().Be("stale reason");
        context.Response.Body.Content.Should().Be("stale body");
        context.Response.Headers.Should().HaveCount(3);
        context.Response.Headers["content-type"].Should().Equal("text/stale");
        context.Response.Headers["CONTENT-LENGTH"].Should().Equal("999");
        context.Response.Headers["X-Stale"].Should().Equal("discard");
        context.ResponseTerminated.Should().BeFalse();
        context.Variables.Should().NotContainKey("after-mock-response");
    }

    private static void AssertPolicyError(PolicyException error, string section = "inbound")
    {
        error.Policy.Should().Be(nameof(IInboundContext.MockResponse));
        error.Section.Should().Be(section switch
        {
            "inbound" => nameof(IInboundContext),
            "outbound" => nameof(IOutboundContext),
            "on-error" => nameof(IOnErrorContext),
            _ => throw new ArgumentOutOfRangeException(nameof(section))
        });
    }

    private static MockMockResponseProvider.Setup SetupMock(
        TestDocument test,
        string section,
        Func<GatewayContext, MockResponseConfig?, bool> predicate) => section switch
    {
        "inbound" => test.SetupInbound().MockResponse(predicate),
        "outbound" => test.SetupOutbound().MockResponse(predicate),
        "on-error" => test.SetupOnError().MockResponse(predicate),
        _ => throw new ArgumentOutOfRangeException(nameof(section))
    };

    private static void RunSection(TestDocument test, string section)
    {
        switch (section)
        {
            case "inbound": test.RunInbound(); break;
            case "outbound": test.RunOutbound(); break;
            case "on-error": test.RunOnError(); break;
            default: throw new ArgumentOutOfRangeException(nameof(section));
        }
    }

    private sealed class PolicyDocument : IDocument
    {
        public Action<IInboundContext>? InboundAction { get; set; }
        public Action<IBackendContext>? BackendAction { get; set; }
        public Action<IOutboundContext>? OutboundAction { get; set; }
        public Action<IOnErrorContext>? OnErrorAction { get; set; }

        public void Inbound(IInboundContext context) => InboundAction?.Invoke(context);
        public void Backend(IBackendContext context) => BackendAction?.Invoke(context);
        public void Outbound(IOutboundContext context) => OutboundAction?.Invoke(context);
        public void OnError(IOnErrorContext context) => OnErrorAction?.Invoke(context);
    }
}