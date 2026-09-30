// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net;
using System.Text;

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring.Expressions;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Document;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Expressions;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Services;

namespace Test.Emulator.Emulator.Policies;

[TestClass]
public class InvokeRequestTests
{
    class TerminalInvokeRequestDocument : IDocument
    {
        public void Inbound(IInboundContext context)
        {
            context.SetVariable("before", "set");
            context.InvokeRequest(new InvokeRequestConfig
            {
                Url = "https://example.com/terminal"
            });
            context.SetVariable("after", "should-not-run");
        }

        public void Outbound(IOutboundContext context) { }
        public void Backend(IBackendContext context) { }
        public void OnError(IOnErrorContext context) { }
    }

    class VariableInvokeRequestDocument : IDocument
    {
        public void Inbound(IInboundContext context)
        {
            context.InvokeRequest(new InvokeRequestConfig
            {
                ResponseVariableName = "invoke-response",
                Url = "https://example.com/variable"
            });
            context.SetVariable("after", "ran");
        }

        public void Outbound(IOutboundContext context) { }
        public void Backend(IBackendContext context) { }
        public void OnError(IOnErrorContext context) { }
    }

    [TestMethod]
    public void ShouldWriteResponseAndStopSectionWhenNoResponseVariableIsConfigured()
    {
        var test = new TerminalInvokeRequestDocument().AsTestDocument();
        test.Context.Services.Register<IHttpClient>(new StubHttpClient(_ => new HttpResponseMessage(HttpStatusCode.Accepted)
        {
            Content = new StringContent("proxied", Encoding.UTF8, "text/plain")
        }));

        test.RunInbound();

        test.Context.Response.StatusCode.Should().Be((int)HttpStatusCode.Accepted);
        test.Context.Response.Body.As<string>().Should().Be("proxied");
        test.Context.Variables.Should().ContainKey("before");
        test.Context.Variables.Should().NotContainKey("after");
    }

    [TestMethod]
    public void ShouldExecuteCallbackAndStillStopSectionForTerminalInvokeRequest()
    {
        var test = new TerminalInvokeRequestDocument().AsTestDocument();
        test.SetupInbound()
            .InvokeRequest()
            .WithCallback((context, _) =>
            {
                context.Response.StatusCode = 204;
                context.Variables["callback"] = "hit";
            });

        test.RunInbound();

        test.Context.Response.StatusCode.Should().Be(204);
        test.Context.Variables.Should().ContainKey("callback").WhoseValue.Should().Be("hit");
        test.Context.Variables.Should().NotContainKey("after");
    }

    [TestMethod]
    public void ShouldStoreResponseVariableAndContinueWhenConfigured()
    {
        var test = new VariableInvokeRequestDocument().AsTestDocument();
        test.Context.Services.Register<IHttpClient>(new StubHttpClient(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"ok\":true}", Encoding.UTF8, "application/json")
        }));

        test.RunInbound();

        test.Context.Variables.Should().ContainKey("invoke-response");
        var response = test.Context.Variables["invoke-response"].Should().BeOfType<MockResponse>().Subject;
        response.StatusCode.Should().Be(200);
        response.Body.As<string>().Should().Be("{\"ok\":true}");
        test.Context.Variables.Should().ContainKey("after").WhoseValue.Should().Be("ran");
    }

    private sealed class ConfiguredDocument(
        Func<IExpressionContext, InvokeRequestConfig> factory, string section = "inbound") : IDocument
    {
        public void Inbound(IInboundContext context)
        {
            if (section != "inbound") return;
            context.InvokeRequest(factory(context.ExpressionContext));
            context.SetVariable("continued", true);
        }

        public void Backend(IBackendContext context)
        {
            if (section != "backend") return;
            context.InvokeRequest(factory(context.ExpressionContext));
            context.SetVariable("continued", true);
        }

        public void Outbound(IOutboundContext context)
        {
            if (section != "outbound") return;
            context.InvokeRequest(factory(context.ExpressionContext));
            context.SetVariable("continued", true);
        }

        public void OnError(IOnErrorContext context)
        {
            if (section != "on-error") return;
            context.InvokeRequest(factory(context.ExpressionContext));
            context.SetVariable("continued", true);
        }
    }

    private static InvokeRequestConfig DefaultConfig => new()
    {
        Url = "https://invoke.example.test/resource",
        ResponseVariableName = "resp"
    };

    private static TestDocument CreateTest(InvokeRequestConfig config, string section = "inbound") =>
        new ConfiguredDocument(_ => config, section).AsTestDocument();

    [TestMethod]
    [DataRow("inbound", false)]
    [DataRow("backend", false)]
    [DataRow("outbound", false)]
    [DataRow("on-error", false)]
    [DataRow("inbound", true)]
    [DataRow("backend", true)]
    [DataRow("outbound", true)]
    [DataRow("on-error", true)]
    public void ShouldDistinguishVariableAndSectionOnlyStopInEveryAuthoredSection(string section, bool variable)
    {
        var test = CreateTest(DefaultConfig with { ResponseVariableName = variable ? "resp" : null }, section);
        test.Context.Response.Body.Content = "original";
        test.Context.Services.Register<IHttpClient>(StubHttpClient.Ok("invoked"));

        SendRequestTests.RunSection(test, section);

        test.Context.ResponseTerminated.Should().BeFalse();
        if (variable)
        {
            test.Context.Variables["resp"].Should().BeOfType<MockResponse>().Which.Body.Content.Should().Be("invoked");
            test.Context.Response.Body.Content.Should().Be("original");
            test.Context.Variables["continued"].Should().Be(true);
        }
        else
        {
            test.Context.Response.Body.Content.Should().Be("invoked");
            test.Context.Variables.Should().NotContainKey("continued");
        }
    }

    [TestMethod]
    [DataRow("inbound", false)]
    [DataRow("backend", false)]
    [DataRow("outbound", false)]
    [DataRow("on-error", false)]
    [DataRow("inbound", true)]
    [DataRow("backend", true)]
    [DataRow("outbound", true)]
    [DataRow("on-error", true)]
    public void ShouldOverrideTransportWithCallbacksAndKeepTerminationSemantics(string section, bool variable)
    {
        var test = CreateTest(DefaultConfig with { ResponseVariableName = variable ? "resp" : null }, section);
        Action<GatewayContext, InvokeRequestConfig> callback = (context, config) =>
        {
            if (config.ResponseVariableName is not null)
                context.Variables[config.ResponseVariableName] = new MockResponse { StatusCode = 202 };
            else
                context.Response.StatusCode = 202;
        };
        switch (section)
        {
            case "inbound": test.SetupInbound().InvokeRequest((_, c) => c.Url == DefaultConfig.Url).WithCallback(callback); break;
            case "backend": test.SetupBackend().InvokeRequest((_, c) => c.Url == DefaultConfig.Url).WithCallback(callback); break;
            case "outbound": test.SetupOutbound().InvokeRequest((_, c) => c.Url == DefaultConfig.Url).WithCallback(callback); break;
            case "on-error": test.SetupOnError().InvokeRequest((_, c) => c.Url == DefaultConfig.Url).WithCallback(callback); break;
        }

        SendRequestTests.RunSection(test, section);

        test.Context.ResponseTerminated.Should().BeFalse();
        test.Context.Variables.ContainsKey("continued").Should().Be(variable);
        if (variable)
            test.Context.Variables["resp"].Should().BeOfType<MockResponse>().Which.StatusCode.Should().Be(202);
        else
            test.Context.Response.StatusCode.Should().Be(202);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void ShouldFailExplicitlyWithoutClientRatherThanReturningSuccess(bool variable)
    {
        var test = CreateTest(DefaultConfig with { ResponseVariableName = variable ? "resp" : null });

        Assert.ThrowsExactly<PolicyException>(() => test.RunInbound())
            .InnerException.Should().BeOfType<InvalidOperationException>();
        test.Context.ResponseTerminated.Should().BeFalse();
        test.Context.Variables.Should().NotContainKey("continued");
        test.Context.Variables.Should().NotContainKey("resp");
    }

    [TestMethod]
    public void ShouldResolveBackendIdExpressionAndPreserveDefaultBackend()
    {
        var test = new ConfiguredDocument(context => new InvokeRequestConfig
        {
            BackendId = (string)context.Variables["backend"],
            ResponseVariableName = "resp"
        }).AsTestDocument();
        test.Context.Variables["backend"] = "orders";
        test.Context.Request = new MockRequest(new Uri("https://gateway.example.test/orders/42?expand=true"));
        test.Context.BackendUrl = "https://default.example.test";
        var resolver = new SetBackendServiceTests.RecordingBackendResolver((_, config) =>
        {
            config.BackendId.Should().Be("orders");
            return new Uri("https://orders.example.test/base/");
        });
        test.Context.Services.Register<IBackendResolver>(resolver);
        var client = StubHttpClient.Ok("resolved");
        test.Context.Services.Register<IHttpClient>(client);

        test.RunInbound();

        resolver.Calls.Should().Be(1);
        client.LastRequest!.RequestUri!.AbsoluteUri.Should().Be("https://orders.example.test/base/orders/42?expand=true");
        test.Context.BackendUrl.Should().Be("https://default.example.test");
    }

    [TestMethod]
    public void ShouldResolveRelativeUrlAgainstExplicitBackendId()
    {
        var test = CreateTest(DefaultConfig with { BackendId = "orders", Url = "/special?custom=yes" });
        test.Context.Services.Register<IBackendResolver>(new SetBackendServiceTests.RecordingBackendResolver(
            (_, _) => new Uri("https://orders.example.test/base/")));
        var client = StubHttpClient.Ok();
        test.Context.Services.Register<IHttpClient>(client);

        test.RunInbound();

        client.LastRequest!.RequestUri!.AbsoluteUri.Should().Be("https://orders.example.test/base/special?custom=yes");
    }

    [TestMethod]
    public void ShouldUseCurrentBackendWhenNoExplicitTargetIsConfigured()
    {
        var test = CreateTest(new InvokeRequestConfig { ResponseVariableName = "resp" });
        test.Context.Request = new MockRequest(new Uri("https://gateway.example.test/path?query=value"));
        test.Context.BackendUrl = "https://current.example.test/service/";
        var client = StubHttpClient.Ok();
        test.Context.Services.Register<IHttpClient>(client);

        test.RunInbound();

        client.LastRequest!.RequestUri!.AbsoluteUri.Should().Be("https://current.example.test/service/path?query=value");
    }

    [TestMethod]
    public void ShouldEvaluateMethodUrlHeaderAndBodyExpressions()
    {
        var test = new ConfiguredDocument(context => new InvokeRequestConfig
        {
            ResponseVariableName = "resp",
            Method = context.Request.Method,
            Url = (string)context.Variables["url"],
            Headers =
            [
                new()
                {
                    Name = (string)context.Variables["header"],
                    ExistsAction = (string)context.Variables["action"],
                    Values = [(string)context.Variables["value"]]
                },
                new() { Name = "Content-Type", Values = ["application/json"] },
                new() { Name = "X-Delete", ExistsAction = "delete" }
            ],
            Body = new BodyConfig
            {
                Content = (context.Request.Body ?? throw new InvalidOperationException("The test requires a request body."))
                    .As<string>(preserveContent: true) + "\u00e9"
            }
        }).AsTestDocument();
        test.Context.Variables["url"] = "https://expression.example.test/invoke";
        test.Context.Variables["header"] = "X-Expression";
        test.Context.Variables["action"] = "override";
        test.Context.Variables["value"] = "evaluated";
        test.Context.Request.Method = "PUT";
        test.Context.Request.Headers["X-Expression"] = ["old"];
        test.Context.Request.Headers["X-Delete"] = ["remove"];
        test.Context.Request.Headers["Content-Length"] = ["999"];
        test.Context.Request.Body.Content = "body-";
        test.Context.Services.Register<IHttpClient>(new StubHttpClient(request =>
        {
            request.RequestUri!.AbsoluteUri.Should().Be("https://expression.example.test/invoke");
            request.Method.Method.Should().Be("PUT");
            request.Headers.GetValues("X-Expression").Should().Equal("evaluated");
            request.Headers.Contains("X-Delete").Should().BeFalse();
            request.Content!.Headers.ContentType!.MediaType.Should().Be("application/json");
            request.Content.ReadAsStringAsync().GetAwaiter().GetResult().Should().Be("body-\u00e9");
            request.Content.Headers.ContentLength.Should().Be(Encoding.UTF8.GetByteCount("body-\u00e9"));
            return new HttpResponseMessage(HttpStatusCode.OK);
        }));

        test.RunInbound();

        test.Context.Request.Headers["X-Expression"].Should().Equal("old");
        test.Context.Request.Body.Consumed.Should().BeFalse();
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    public void ShouldHonorExplicitEmptyBodyOverride(string? body)
    {
        var test = CreateTest(DefaultConfig with { Body = new BodyConfig { Content = body } });
        test.Context.Request.Body.Content = "must-not-copy";
        test.Context.Services.Register<IHttpClient>(new StubHttpClient(request =>
        {
            request.Content!.ReadAsStringAsync().GetAwaiter().GetResult().Should().Be("");
            request.Content.Headers.ContentLength.Should().Be(0);
            return new HttpResponseMessage(HttpStatusCode.OK);
        }));

        test.RunInbound();
    }

    [TestMethod]
    public void ShouldReplaceEntireTerminalResponseWithoutTerminatingPipeline()
    {
        var test = CreateTest(DefaultConfig with { ResponseVariableName = null });
        test.Context.Response.Body.Content = "old";
        test.Context.Response.Headers["X-Stale"] = ["remove"];
        var content = new SendRequestTests.TrackingContent("r\u00e9ponse");
        test.Context.Services.Register<IHttpClient>(new StubHttpClient(_ => new HttpResponseMessage(HttpStatusCode.Created)
        {
            Content = content,
            ReasonPhrase = "Created by stub"
        }));

        test.RunInbound();

        test.Context.Response.StatusCode.Should().Be(201);
        test.Context.Response.StatusReason.Should().Be("Created by stub");
        test.Context.Response.Body.Content.Should().Be("r\u00e9ponse");
        test.Context.Response.Headers.Should().NotContainKey("X-Stale");
        test.Context.Response.Headers["Content-Length"].Should().Equal(Encoding.UTF8.GetByteCount("r\u00e9ponse").ToString());
        test.Context.ResponseTerminated.Should().BeFalse();
        content.Disposed.Should().BeTrue();
    }

    [TestMethod]
    [DataRow("url-empty")]
    [DataRow("url-relative")]
    [DataRow("url-scheme")]
    [DataRow("backend-empty")]
    [DataRow("absolute-url-and-backend")]
    [DataRow("method")]
    [DataRow("variable")]
    [DataRow("header")]
    [DataRow("template")]
    public void ShouldRejectInvalidInvokeConfigurationBeforeSending(string kind)
    {
        var config = kind switch
        {
            "url-empty" => DefaultConfig with { Url = "" },
            "url-relative" => DefaultConfig with { Url = "/relative" },
            "url-scheme" => DefaultConfig with { Url = "ftp://invoke.example.test" },
            "backend-empty" => DefaultConfig with { Url = null, BackendId = " " },
            "absolute-url-and-backend" => DefaultConfig with { BackendId = "id" },
            "method" => DefaultConfig with { Method = "BAD METHOD" },
            "variable" => DefaultConfig with { ResponseVariableName = " " },
            "header" => DefaultConfig with { Headers = [new() { Name = "X-Test", ExistsAction = "invalid", Values = ["value"] }] },
            _ => DefaultConfig with { Body = new BodyConfig { Content = "body", Template = "liquid" } }
        };
        var test = CreateTest(config);
        var client = new SendRequestTests.RecordingHttpClient((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));
        test.Context.Services.Register<IHttpClient>(client);
        test.Context.Services.Register<IBackendResolver>(new SetBackendServiceTests.RecordingBackendResolver(
            (_, _) => new Uri("https://resolved.example.test")));

        var error = Assert.ThrowsExactly<PolicyException>(() => test.RunInbound());

        if (kind == "template")
            error.InnerException.Should().BeOfType<NotSupportedException>();
        else
            error.InnerException.Should().BeAssignableTo<ArgumentException>();
        client.Calls.Should().Be(0);
    }

    [TestMethod]
    public void ShouldRejectUnconfiguredBackendIdInsteadOfIgnoringIt()
    {
        var test = CreateTest(DefaultConfig with { Url = null, BackendId = "unconfigured" });
        var client = new SendRequestTests.RecordingHttpClient((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));
        test.Context.Services.Register<IHttpClient>(client);

        Assert.ThrowsExactly<PolicyException>(() => test.RunInbound())
            .InnerException.Should().BeOfType<InvalidOperationException>();
        client.Calls.Should().Be(0);
    }

    [TestMethod]
    public void ShouldPropagateClientExceptionWithoutReplacingResponseOrStoppingPipeline()
    {
        var test = CreateTest(DefaultConfig with { ResponseVariableName = null });
        var failure = new HttpRequestException("invoke failed");
        test.Context.Response.Body.Content = "unchanged";
        test.Context.Services.Register<IHttpClient>(new SendRequestTests.RecordingHttpClient((_, _) => throw failure));

        Assert.ThrowsExactly<PolicyException>(() => test.RunInbound()).InnerException.Should().BeSameAs(failure);
        test.Context.Response.Body.Content.Should().Be("unchanged");
        test.Context.ResponseTerminated.Should().BeFalse();
    }

    [TestMethod]
    public void ShouldHonorCallerCancellationBeforeInvoking()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var test = CreateTest(DefaultConfig);
        test.Context.Services.Register(new HttpTransportState { CancellationToken = cancellation.Token });
        var client = new SendRequestTests.RecordingHttpClient((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));
        test.Context.Services.Register<IHttpClient>(client);

        Assert.ThrowsExactly<PolicyException>(() => test.RunInbound())
            .InnerException.Should().BeAssignableTo<OperationCanceledException>();
        client.Calls.Should().Be(0);
    }

    private sealed class OuterPipelineDocument : IDocument
    {
        public void Inbound(IInboundContext context)
        {
            context.Base();
            context.SetVariable("outer-after-base", true);
        }

        public void Backend(IBackendContext context) => context.SetVariable("backend-ran", true);
        public void Outbound(IOutboundContext context) => context.SetVariable("outbound-ran", true);
    }

    private sealed class TerminalPipelineDocument(bool pipelineWide) : IDocument
    {
        public void Inbound(IInboundContext context)
        {
            if (pipelineWide)
                context.ReturnResponse(new ReturnResponseConfig());
            else
                context.InvokeRequest(new InvokeRequestConfig { Url = "https://invoke.example.test/terminal" });
            context.SetVariable("terminal-after", true);
        }

        public void Outbound(IOutboundContext context) => context.Base();
    }

    private sealed class FollowingPipelineDocument : IDocument
    {
        public void Inbound(IInboundContext context) => context.SetVariable("following-scope", true);
        public void Outbound(IOutboundContext context) => context.Base();
    }

    [TestMethod]
    [DataRow(false, false)]
    [DataRow(false, true)]
    [DataRow(true, false)]
    [DataRow(true, true)]
    public void ShouldKeepInvokeSectionStopDistinctFromReturnResponseAcrossFlatAndNestedPipelines(bool nested, bool pipelineWide)
    {
        var pipeline = PolicyPipelineBuilder.Create()
            .AddPolicy(PolicyScope.Global, new OuterPipelineDocument())
            .AddPolicy(PolicyScope.Api, new TerminalPipelineDocument(pipelineWide))
            .AddPolicy(PolicyScope.Operation, new FollowingPipelineDocument())
            .ConfigureContext(context => context.Services.Register<IHttpClient>(StubHttpClient.Ok("invoked")))
            .Build();

        if (nested) pipeline.RunAllNested();
        else pipeline.RunAll();

        pipeline.Context.ResponseTerminated.Should().Be(pipelineWide);
        pipeline.Context.Variables.Should().NotContainKey("terminal-after");
        pipeline.Context.Variables.ContainsKey("outer-after-base").Should().Be(!nested || !pipelineWide);
        pipeline.Context.Variables.ContainsKey("following-scope").Should().Be(!nested && !pipelineWide);
        pipeline.Context.Variables.ContainsKey("backend-ran").Should().Be(!pipelineWide);
        pipeline.Context.Variables.ContainsKey("outbound-ran").Should().Be(!pipelineWide);
    }

    private sealed class FragmentDocument : IDocument
    {
        public void Inbound(IInboundContext context)
        {
            context.IncludeFragment("invoke-http");
            context.SetVariable("after-fragment", true);
        }
    }

    private sealed class InvokeFragment(bool variable) : IFragment
    {
        public void Fragment(IFragmentContext context) =>
            context.InvokeRequest(DefaultConfig with { ResponseVariableName = variable ? "resp" : null });
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void ShouldPreserveSectionStopSemanticsInsideAuthoredFragment(bool variable)
    {
        var test = new FragmentDocument().AsTestDocument().RegisterFragment("invoke-http", new InvokeFragment(variable));
        test.Context.Services.Register<IHttpClient>(StubHttpClient.Ok("fragment"));

        test.RunInbound();

        test.Context.ResponseTerminated.Should().BeFalse();
        test.Context.Variables.ContainsKey("after-fragment").Should().Be(variable);
    }

    [TestMethod]
    public void ShouldPropagateCallbackExceptionWithoutTerminatingPipeline()
    {
        var test = CreateTest(DefaultConfig);
        var failure = new HttpRequestException("callback failed");
        test.SetupInbound().InvokeRequest().WithCallback((_, _) => throw failure);

        Assert.ThrowsExactly<PolicyException>(() => test.RunInbound()).InnerException.Should().BeSameAs(failure);
        test.Context.ResponseTerminated.Should().BeFalse();
    }

    [TestMethod]
    [DataRow("inbound")]
    [DataRow("backend")]
    [DataRow("outbound")]
    [DataRow("on-error")]
    public void ShouldCopyCurrentBodyAndContentHeadersInEverySection(string section)
    {
        var test = CreateTest(DefaultConfig, section);
        test.Context.Request.Method = "POST";
        test.Context.Request.Body.Content = "body-\u00e9";
        test.Context.Request.Headers["Content-Type"] = ["application/json"];
        test.Context.Request.Headers["Content-Length"] = ["999"];
        test.Context.Services.Register<IHttpClient>(new StubHttpClient(request =>
        {
            request.Method.Method.Should().Be("POST");
            request.Content!.Headers.ContentType!.MediaType.Should().Be("application/json");
            request.Content.ReadAsStringAsync().GetAwaiter().GetResult().Should().Be("body-\u00e9");
            request.Content.Headers.ContentLength.Should().Be(Encoding.UTF8.GetByteCount("body-\u00e9"));
            return new HttpResponseMessage(HttpStatusCode.OK);
        }));

        SendRequestTests.RunSection(test, section);

        test.Context.Request.Body.Consumed.Should().BeFalse();
    }

    [TestMethod]
    public void ShouldPropagateBackendResolverExceptionWithoutReplacingDefaultBackend()
    {
        var test = CreateTest(DefaultConfig with { Url = null, BackendId = "orders" });
        test.Context.BackendUrl = "https://default.example.test";
        var failure = new InvalidOperationException("backend resolution failed");
        test.Context.Services.Register<IBackendResolver>(new SetBackendServiceTests.RecordingBackendResolver((_, _) => throw failure));
        test.Context.Services.Register<IHttpClient>(StubHttpClient.Ok());

        Assert.ThrowsExactly<PolicyException>(() => test.RunInbound()).InnerException.Should().BeSameAs(failure);
        test.Context.BackendUrl.Should().Be("https://default.example.test");
    }
}