// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net;
using System.Text;

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring.Expressions;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Document;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Services;

namespace Test.Emulator.Emulator.Policies;

[TestClass]
public class SendOneWayRequestTests
{
    class SimpleSendOneWayRequest : IDocument
    {
        public void Inbound(IInboundContext context)
        {
            context.SendOneWayRequest(new SendOneWayRequestConfig
            {
                Url = "https://example.com/notify",
                Method = "POST",
            });
        }

        public void Backend(IBackendContext context) { }
        public void Outbound(IOutboundContext context) { }
        public void OnError(IOnErrorContext context) { }
    }

    class OutboundSendOneWayRequest : IDocument
    {
        public void Inbound(IInboundContext context) { }
        public void Backend(IBackendContext context) { }

        public void Outbound(IOutboundContext context)
        {
            context.SendOneWayRequest(new SendOneWayRequestConfig
            {
                Url = "https://example.com/log",
                Method = "POST",
            });
        }

        public void OnError(IOnErrorContext context) { }
    }

    [TestMethod]
    public void SendOneWayRequest_Inbound_ShouldFailWithoutClient()
    {
        var test = new TestDocument(new SimpleSendOneWayRequest());

        Assert.ThrowsExactly<PolicyException>(() => test.RunInbound())
            .InnerException.Should().BeOfType<InvalidOperationException>();
    }

    [TestMethod]
    public void SendOneWayRequest_Inbound_Callback()
    {
        // Arrange
        var test = new TestDocument(new SimpleSendOneWayRequest());
        var executedCallback = false;

        test.SetupInbound().SendOneWayRequest().WithCallback((_, _) =>
        {
            executedCallback = true;
        });

        // Act
        test.RunInbound();

        // Assert
        executedCallback.Should().BeTrue();
    }

    [TestMethod]
    public void SendOneWayRequest_Inbound_CallbackWithPredicate()
    {
        // Arrange
        var test = new TestDocument(new SimpleSendOneWayRequest());
        var executedCallback = false;

        test.SetupInbound()
            .SendOneWayRequest((_, config) => config.Url == "https://example.com/notify")
            .WithCallback((context, _) =>
            {
                executedCallback = true;
                context.Variables["notified"] = true;
            });

        // Act
        test.RunInbound();

        // Assert
        executedCallback.Should().BeTrue();
        test.Context.Variables.Should().ContainKey("notified")
            .WhoseValue.Should().Be(true);
    }

    [TestMethod]
    public void SendOneWayRequest_Outbound_Callback()
    {
        // Arrange
        var test = new TestDocument(new OutboundSendOneWayRequest());
        var executedCallback = false;

        test.SetupOutbound().SendOneWayRequest().WithCallback((_, _) =>
        {
            executedCallback = true;
        });

        // Act
        test.RunOutbound();

        // Assert
        executedCallback.Should().BeTrue();
    }

    private sealed class ConfiguredDocument(
        Func<IExpressionContext, SendOneWayRequestConfig> factory, string section = "inbound") : IDocument
    {
        public void Inbound(IInboundContext context)
        {
            if (section != "inbound") return;
            context.SendOneWayRequest(factory(context.ExpressionContext));
            context.SetVariable("continued", true);
        }

        public void Backend(IBackendContext context)
        {
            if (section != "backend") return;
            context.SendOneWayRequest(factory(context.ExpressionContext));
            context.SetVariable("continued", true);
        }

        public void Outbound(IOutboundContext context)
        {
            if (section != "outbound") return;
            context.SendOneWayRequest(factory(context.ExpressionContext));
            context.SetVariable("continued", true);
        }

        public void OnError(IOnErrorContext context)
        {
            if (section != "on-error") return;
            context.SendOneWayRequest(factory(context.ExpressionContext));
            context.SetVariable("continued", true);
        }
    }

    private static SendOneWayRequestConfig DefaultConfig => new()
    {
        Url = "https://notify.example.test/event",
        Method = "POST"
    };

    private static TestDocument CreateTest(SendOneWayRequestConfig config, string section = "inbound") =>
        new ConfiguredDocument(_ => config, section).AsTestDocument();

    [TestMethod]
    [DataRow("inbound")]
    [DataRow("backend")]
    [DataRow("outbound")]
    [DataRow("on-error")]
    public void ShouldDispatchInEveryAuthoredSectionWithoutReplacingResponse(string section)
    {
        var test = CreateTest(DefaultConfig, section);
        var response = new SendRequestTests.TrackingContent("ignored");
        var client = new StubHttpClient(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError) { Content = response });
        test.Context.Services.Register<IHttpClient>(client);
        test.Context.Response.StatusCode = 418;
        test.Context.Response.Body.Content = "unchanged";

        SendRequestTests.RunSection(test, section);

        client.LastRequest!.RequestUri!.AbsoluteUri.Should().Be(DefaultConfig.Url);
        test.Context.Response.StatusCode.Should().Be(418);
        test.Context.Response.Body.Content.Should().Be("unchanged");
        test.Context.Variables["continued"].Should().Be(true);
        response.Disposed.Should().BeTrue();
    }

    [TestMethod]
    [DataRow("inbound")]
    [DataRow("backend")]
    [DataRow("outbound")]
    [DataRow("on-error")]
    public void ShouldOverrideTransportWithCallbacksInEveryAuthoredSection(string section)
    {
        var test = CreateTest(DefaultConfig, section);
        Action<GatewayContext, SendOneWayRequestConfig> callback = (context, _) => context.Variables["callback"] = true;
        switch (section)
        {
            case "inbound": test.SetupInbound().SendOneWayRequest().WithCallback(callback); break;
            case "backend": test.SetupBackend().SendOneWayRequest().WithCallback(callback); break;
            case "outbound": test.SetupOutbound().SendOneWayRequest().WithCallback(callback); break;
            case "on-error": test.SetupOnError().SendOneWayRequest().WithCallback(callback); break;
        }

        SendRequestTests.RunSection(test, section);

        test.Context.Variables["callback"].Should().Be(true);
        test.Context.Variables["continued"].Should().Be(true);
    }

    [TestMethod]
    public async Task ShouldReturnBeforeResponseAndRetainRequestUntilCompletion()
    {
        var pending = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        var test = CreateTest(DefaultConfig with { Body = new BodyConfig { Content = "still-alive" } });
        var state = new HttpTransportState();
        test.Context.Services.Register(state);
        var client = new SendRequestTests.RecordingHttpClient((_, _) => pending.Task);
        test.Context.Services.Register<IHttpClient>(client);
        var content = new SendRequestTests.TrackingContent("never-read");
        var execution = Task.Run(() => test.RunInbound());

        try
        {
            await execution.WaitAsync(TimeSpan.FromSeconds(2));
            pending.Task.IsCompleted.Should().BeFalse();
            state.PendingOneWayRequests.Should().ContainSingle();
            test.Context.Variables["continued"].Should().Be(true);
            client.LastRequest!.Content!.ReadAsStringAsync().GetAwaiter().GetResult().Should().Be("still-alive");
            content.Disposed.Should().BeFalse();
            pending.SetResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = content });
            await state.DrainAsync().WaitAsync(TimeSpan.FromSeconds(2));
            content.Disposed.Should().BeTrue();
            var readDisposedRequest = () => client.LastRequest.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            readDisposedRequest.Should().Throw<ObjectDisposedException>();
        }
        finally
        {
            pending.TrySetResult(new HttpResponseMessage(HttpStatusCode.OK));
            await execution;
            await state.DrainAsync();
        }
    }

    private sealed class UnreadableContent : HttpContent
    {
        public bool Disposed { get; private set; }

        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) =>
            throw new InvalidOperationException("A one-way response body must not be read.");

        protected override bool TryComputeLength(out long length)
        {
            length = 0;
            return false;
        }

        protected override void Dispose(bool disposing)
        {
            Disposed = true;
            base.Dispose(disposing);
        }
    }

    [TestMethod]
    public void ShouldDisposeCompletedResponseWithoutReadingItsBody()
    {
        var test = CreateTest(DefaultConfig);
        var content = new UnreadableContent();
        test.Context.Services.Register<IHttpClient>(new StubHttpClient(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = content }));

        test.RunInbound();

        content.Disposed.Should().BeTrue();
    }

    [TestMethod]
    public async Task ShouldObserveTraceAndRethrowAsynchronousClientFailureWhenDrained()
    {
        var pending = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        var test = CreateTest(DefaultConfig);
        var state = new HttpTransportState();
        test.Context.Services.Register(state);
        test.Context.Services.Register<IHttpClient>(new SendRequestTests.RecordingHttpClient((_, _) => pending.Task));
        var traces = new List<string>();
        test.Context.Trace = traces.Add;
        var execution = Task.Run(() =>
        {
            try { test.RunInbound(); return null; }
            catch (PolicyException error) { return error; }
        });
        try
        {
            (await execution.WaitAsync(TimeSpan.FromSeconds(2))).Should().BeNull();
            pending.SetException(new HttpRequestException("asynchronous notification failed"));
            var drain = () => state.DrainAsync();
            await drain.Should().ThrowAsync<HttpRequestException>().WithMessage("asynchronous notification failed");
            traces.Should().Contain(message => message.Contains("SendOneWayRequest") && message.Contains("asynchronous notification failed"));
        }
        finally
        {
            pending.TrySetResult(new HttpResponseMessage(HttpStatusCode.OK));
            await execution;
        }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void ShouldPropagateImmediateClientExceptions(bool faultedTask)
    {
        var test = CreateTest(DefaultConfig);
        var failure = new HttpRequestException("immediate failure");
        test.Context.Services.Register<IHttpClient>(new SendRequestTests.RecordingHttpClient((_, _) =>
            faultedTask ? Task.FromException<HttpResponseMessage>(failure) : throw failure));

        var error = Assert.ThrowsExactly<PolicyException>(() => test.RunInbound());

        error.InnerException.Should().BeSameAs(failure);
        test.Context.Variables.Should().NotContainKey("continued");
    }

    [TestMethod]
    [DataRow("inbound", "original")]
    [DataRow("backend", "original")]
    [DataRow("outbound", "")]
    [DataRow("on-error", "original")]
    public void ShouldCopyContentHeadersAndBodyWithOutboundException(string section, string expectedBody)
    {
        var test = CreateTest(DefaultConfig with { Mode = "copy", Method = null }, section);
        test.Context.Request.Method = "PATCH";
        test.Context.Request.Body.Content = "original";
        test.Context.Request.Headers["Content-Type"] = ["application/json"];
        test.Context.Request.Headers["Content-Length"] = ["1000"];
        test.Context.Services.Register<IHttpClient>(new StubHttpClient(request =>
        {
            request.Method.Method.Should().Be("PATCH");
            request.Content!.Headers.ContentType!.MediaType.Should().Be("application/json");
            request.Content.ReadAsStringAsync().GetAwaiter().GetResult().Should().Be(expectedBody);
            request.Content.Headers.ContentLength.Should().Be(Encoding.UTF8.GetByteCount(expectedBody));
            return new HttpResponseMessage(HttpStatusCode.OK);
        }));

        SendRequestTests.RunSection(test, section);

        test.Context.Request.Body.Consumed.Should().BeFalse();
    }

    [TestMethod]
    public void ShouldEvaluateExpressionsAndApplyBodyHeadersAuthenticationAndProxy()
    {
        var test = new ConfiguredDocument(context => new SendOneWayRequestConfig
        {
            Mode = (string)context.Variables["mode"],
            Timeout = (int)context.Variables["timeout"],
            Url = (string)context.Variables["url"],
            Method = context.Request.Method,
            Body = new BodyConfig
            {
                Content = (context.Request.Body ?? throw new InvalidOperationException("The test requires a request body."))
                    .As<string>(preserveContent: true) + "\u00e9"
            },
            Headers =
            [
                new() { Name = "X-Existing", ExistsAction = "delete" },
                new() { Name = "X-Expression", Values = [(string)context.Variables["value"]] },
                new() { Name = "Content-Type", Values = ["application/json"] }
            ],
            Authentication = new BasicAuthenticationConfig { Username = "user", Password = "password" },
            Proxy = new ProxyConfig { Url = (string)context.Variables["proxy"] }
        }).AsTestDocument();
        test.Context.Variables["mode"] = "copy";
        test.Context.Variables["timeout"] = 10;
        test.Context.Variables["url"] = "https://notify.example.test/expression";
        test.Context.Variables["value"] = "evaluated";
        test.Context.Variables["proxy"] = "http://proxy.example.test:8080";
        test.Context.Request.Method = "POST";
        test.Context.Request.Headers["X-Existing"] = ["remove"];
        test.Context.Request.Body.Content = "body-";
        test.Context.Services.Register<IHttpClient>(new StubHttpClient(request =>
        {
            request.RequestUri!.AbsoluteUri.Should().Be("https://notify.example.test/expression");
            request.Headers.Contains("X-Existing").Should().BeFalse();
            request.Headers.GetValues("X-Expression").Should().Equal("evaluated");
            request.Headers.Authorization!.Scheme.Should().Be("Basic");
            request.Content!.Headers.ContentType!.MediaType.Should().Be("application/json");
            request.Content.ReadAsStringAsync().GetAwaiter().GetResult().Should().Be("body-\u00e9");
            request.Content.Headers.ContentLength.Should().Be(Encoding.UTF8.GetByteCount("body-\u00e9"));
            request.Options.TryGetValue(HttpTransportOptions.Key, out var options).Should().BeTrue();
            options!.Proxy!.Url.Should().Be("http://proxy.example.test:8080");
            options.Timeout.Should().Be(TimeSpan.FromSeconds(10));
            return new HttpResponseMessage(HttpStatusCode.OK);
        }));

        test.RunInbound();
    }

    [TestMethod]
    [DataRow("mode")]
    [DataRow("timeout")]
    [DataRow("url")]
    [DataRow("method")]
    [DataRow("header")]
    [DataRow("template")]
    public void ShouldRejectInvalidConfigurationBeforeDispatch(string kind)
    {
        var config = kind switch
        {
            "mode" => DefaultConfig with { Mode = "invalid" },
            "timeout" => DefaultConfig with { Timeout = -1 },
            "url" => DefaultConfig with { Url = "ftp://example.test" },
            "method" => DefaultConfig with { Method = "BAD METHOD" },
            "header" => DefaultConfig with { Headers = [new() { Name = "X-Test", ExistsAction = "invalid", Values = ["value"] }] },
            _ => DefaultConfig with { Body = new BodyConfig { Content = "body", Template = "liquid" } }
        };
        var test = CreateTest(config);
        var client = new SendRequestTests.RecordingHttpClient((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));
        test.Context.Services.Register<IHttpClient>(client);

        var error = Assert.ThrowsExactly<PolicyException>(() => test.RunInbound());

        if (kind == "template")
            error.InnerException.Should().BeOfType<NotSupportedException>();
        else
            error.InnerException.Should().BeAssignableTo<ArgumentException>();
        client.Calls.Should().Be(0);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void ShouldHonorZeroTimeoutOrCallerCancellationBeforeDispatch(bool callerCancellation)
    {
        using var cancellation = new CancellationTokenSource();
        if (callerCancellation) cancellation.Cancel();
        var test = CreateTest(DefaultConfig with { Timeout = callerCancellation ? 60 : 0 });
        test.Context.Services.Register(new HttpTransportState { CancellationToken = cancellation.Token });
        var client = new SendRequestTests.RecordingHttpClient((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));
        test.Context.Services.Register<IHttpClient>(client);

        Assert.ThrowsExactly<PolicyException>(() => test.RunInbound())
            .InnerException.Should().BeAssignableTo<OperationCanceledException>();
        client.Calls.Should().Be(0);
    }

    [TestMethod]
    public async Task ShouldApplyTimeoutToPendingDispatchWithoutBlockingPolicySection()
    {
        var pending = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        var test = CreateTest(DefaultConfig with { Timeout = 1 });
        var state = new HttpTransportState();
        test.Context.Services.Register(state);
        var client = new SendRequestTests.RecordingHttpClient((_, _) => pending.Task);
        test.Context.Services.Register<IHttpClient>(client);
        var execution = Task.Run(() => test.RunInbound());
        try
        {
            await execution.WaitAsync(TimeSpan.FromSeconds(2));
            var drain = () => state.DrainAsync().WaitAsync(TimeSpan.FromSeconds(2));
            await drain.Should().ThrowAsync<OperationCanceledException>();
            client.LastCancellationToken.IsCancellationRequested.Should().BeTrue();
        }
        finally
        {
            pending.TrySetResult(new HttpResponseMessage(HttpStatusCode.OK));
            await execution;
        }
    }

    private sealed class FragmentDocument : IDocument
    {
        public void Inbound(IInboundContext context) => context.IncludeFragment("one-way-http");
    }

    private sealed class NotificationFragment : IFragment
    {
        public void Fragment(IFragmentContext context) => context.SendOneWayRequest(DefaultConfig);
    }

    [TestMethod]
    public void ShouldDispatchInsideAuthoredFragment()
    {
        var test = new FragmentDocument().AsTestDocument().RegisterFragment("one-way-http", new NotificationFragment());
        var client = StubHttpClient.Ok();
        test.Context.Services.Register<IHttpClient>(client);

        test.RunInbound();

        client.LastRequest!.RequestUri!.AbsoluteUri.Should().Be(DefaultConfig.Url);
    }

    [TestMethod]
    public void ShouldWrapCallbackExceptionsWithoutTransport()
    {
        var test = CreateTest(DefaultConfig);
        var failure = new HttpRequestException("callback failure");
        test.SetupInbound().SendOneWayRequest().WithCallback((_, _) => throw failure);

        Assert.ThrowsExactly<PolicyException>(() => test.RunInbound()).InnerException.Should().BeSameAs(failure);
    }

    [TestMethod]
    public void ShouldUseInjectedManagedIdentityAuthentication()
    {
        var test = CreateTest(DefaultConfig with
        {
            Authentication = new ManagedIdentityAuthenticationConfig { Resource = "https://resource.example.test" }
        });
        test.Context.ManagedIdentityTokenProvider = (_, _) => "notification-token";
        test.Context.Services.Register<IHttpClient>(new StubHttpClient(request =>
        {
            request.Headers.Authorization!.ToString().Should().Be("Bearer notification-token");
            return new HttpResponseMessage(HttpStatusCode.OK);
        }));

        test.RunInbound();

        test.Context.Request.Headers.Should().NotContainKey("Authorization");
    }

    [TestMethod]
    public void ShouldRejectUnconfiguredAuthenticationProvider()
    {
        var test = CreateTest(DefaultConfig with
        {
            Authentication = new ManagedIdentityAuthenticationConfig { Resource = "https://resource.example.test" }
        });
        var client = new SendRequestTests.RecordingHttpClient((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));
        test.Context.Services.Register<IHttpClient>(client);

        Assert.ThrowsExactly<PolicyException>(() => test.RunInbound())
            .InnerException.Should().BeOfType<InvalidOperationException>();
        client.Calls.Should().Be(0);
    }
}