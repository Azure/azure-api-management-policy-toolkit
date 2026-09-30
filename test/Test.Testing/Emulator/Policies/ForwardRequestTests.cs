// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
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
public class ForwardRequestTests
{
    class SimpleForwardRequest : IDocument
    {
        public void Inbound(IInboundContext context) { }

        public void Backend(IBackendContext context)
        {
            context.ForwardRequest();
        }

        public void Outbound(IOutboundContext context) { }
        public void OnError(IOnErrorContext context) { }
    }

    [TestMethod]
    public void ForwardRequest_Callback()
    {
        var test = new SimpleForwardRequest().AsTestDocument();
        var executedCallback = false;
        test.SetupBackend().ForwardRequest().WithCallback((_, _) =>
        {
            executedCallback = true;
        });

        test.RunBackend();

        executedCallback.Should().BeTrue();
    }

    [TestMethod]
    public void ForwardRequest_NoHttpClient_FailsExplicitly()
    {
        var test = new SimpleForwardRequest().AsTestDocument();

        Assert.ThrowsExactly<PolicyException>(() => test.RunBackend())
            .InnerException.Should().BeOfType<InvalidOperationException>();
    }

    [TestMethod]
    public void ForwardRequest_WithHttpClient_CopiesResponseToContext()
    {
        // Arrange
        var test = new SimpleForwardRequest().AsTestDocument();
        test.Context.Request.Body.Content = "";
        var stubClient = new StubHttpClient(req =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.Created)
            {
                Content = new StringContent("response-body"),
            };
            response.Headers.Add("X-Custom-Response", "resp-value");
            return response;
        });
        test.Context.Services.Register<IHttpClient>(stubClient);

        // Act
        test.RunBackend();

        // Assert
        test.Context.Response.StatusCode.Should().Be(201);
        test.Context.Response.Body.Content.Should().Be("response-body");
        test.Context.Response.Headers.Should().ContainKey("X-Custom-Response")
            .WhoseValue.Should().Contain("resp-value");
    }

    [TestMethod]
    public void ForwardRequest_UsesBackendUrl_WhenSet()
    {
        // Arrange
        var test = new SimpleForwardRequest().AsTestDocument();
        test.Context.BackendUrl = "https://backend.example.com";
        test.Context.Request.Body.Content = "";
        var stubClient = new StubHttpClient(req => new HttpResponseMessage(HttpStatusCode.OK));
        test.Context.Services.Register<IHttpClient>(stubClient);

        // Act
        test.RunBackend();

        // Assert
        stubClient.LastRequest.Should().NotBeNull();
        stubClient.LastRequest!.RequestUri!.ToString().Should().StartWith("https://backend.example.com");
    }

    private sealed class ConfiguredDocument(Func<IExpressionContext, ForwardRequestConfig?> factory) : IDocument
    {
        public void Backend(IBackendContext context)
        {
            context.ForwardRequest(factory(context.ExpressionContext));
            context.SetVariable("continued", true);
        }
    }

    private static TestDocument CreateTest(ForwardRequestConfig? config = null) =>
        new ConfiguredDocument(_ => config).AsTestDocument();

    [TestMethod]
    public void ShouldForwardMethodPathQueryBodyAndContentHeaders()
    {
        var test = CreateTest();
        test.Context.Request = new MockRequest(new Uri("https://gateway.example.test/orders/42?item=one"))
        {
            Method = "PATCH"
        };
        test.Context.BackendUrl = "https://backend.example.test/service/?fixed=yes";
        test.Context.Request.Body.Content = "{\"text\":\"\u00e9\"}";
        test.Context.Request.Headers["X-Request"] = ["one", "two"];
        test.Context.Request.Headers["Content-Type"] = ["application/json"];
        test.Context.Request.Headers["Content-Language"] = ["pl"];
        test.Context.Request.Headers["Content-Length"] = ["999"];
        test.Context.Services.Register<IHttpClient>(new StubHttpClient(request =>
        {
            request.RequestUri!.AbsoluteUri.Should().Be("https://backend.example.test/service/orders/42?fixed=yes&item=one");
            request.Method.Method.Should().Be("PATCH");
            request.Headers.GetValues("X-Request").Should().Equal("one", "two");
            request.Content!.Headers.ContentType!.MediaType.Should().Be("application/json");
            request.Content.Headers.ContentLanguage.Should().Equal("pl");
            request.Content.Headers.ContentLength.Should().Be(Encoding.UTF8.GetByteCount(test.Context.Request.Body.Content));
            request.Content.ReadAsStringAsync().GetAwaiter().GetResult().Should().Be(test.Context.Request.Body.Content);
            return new HttpResponseMessage(HttpStatusCode.OK);
        }));

        test.RunBackend();

        test.Context.Request.Body.Consumed.Should().BeFalse();
        test.Context.Request.Headers["Content-Length"].Should().Equal("999");
    }

    [TestMethod]
    public void ShouldPreserveOriginalTargetWhenNoBackendOverrideExists()
    {
        var test = CreateTest();
        test.Context.Request = new MockRequest(new Uri("https://original.example.test/path?query=value"));
        var client = StubHttpClient.Ok();
        test.Context.Services.Register<IHttpClient>(client);

        test.RunBackend();

        client.LastRequest!.RequestUri!.AbsoluteUri.Should().Be("https://original.example.test/path?query=value");
    }

    [TestMethod]
    public void ShouldReplaceEntireResponseAndDisposeTransportMessages()
    {
        var test = CreateTest();
        test.Context.Request.Body.Content = "request";
        test.Context.Response.Headers["X-Stale"] = ["remove"];
        test.Context.Response.Headers["Content-Type"] = ["old/type"];
        test.Context.Response.Body.Content = "old-body";
        var content = new SendRequestTests.TrackingContent("r\u00e9ponse");
        var client = new StubHttpClient(_ =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.Accepted) { Content = content };
            response.Headers.Add("X-New", "new");
            return response;
        });
        test.Context.Services.Register<IHttpClient>(client);

        test.RunBackend();

        test.Context.Response.Headers.Should().NotContainKey("X-Stale");
        test.Context.Response.Headers["X-New"].Should().Equal("new");
        test.Context.Response.Headers["Content-Type"].Should().Equal("text/plain; charset=utf-8");
        test.Context.Response.Headers["Content-Length"].Should().Equal(Encoding.UTF8.GetByteCount("r\u00e9ponse").ToString());
        test.Context.Response.Body.Content.Should().Be("r\u00e9ponse");
        content.Disposed.Should().BeTrue();
        var readDisposedRequest = () => client.LastRequest!.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
        readDisposedRequest.Should().Throw<ObjectDisposedException>();
    }

    [TestMethod]
    public void ShouldClearStaleBodyAndHeadersWhenBackendHasNoBody()
    {
        var test = CreateTest();
        test.Context.Response.Body.Content = "stale";
        test.Context.Response.Headers["X-Stale"] = ["stale"];
        test.Context.Services.Register<IHttpClient>(new StubHttpClient(_ => new HttpResponseMessage(HttpStatusCode.NoContent)));

        test.RunBackend();

        test.Context.Response.Body.Content.Should().BeNullOrEmpty();
        test.Context.Response.Headers.Should().NotContainKey("X-Stale");
    }

    [TestMethod]
    [DataRow(400, true)]
    [DataRow(599, true)]
    [DataRow(500, false)]
    [DataRow(200, true)]
    public void ShouldOnlyFailOnConfiguredHttpErrorStatuses(int status, bool failOnError)
    {
        var test = CreateTest(new ForwardRequestConfig { FailOnErrorStatusCode = failOnError });
        test.Context.Services.Register<IHttpClient>(new StubHttpClient(_ => new HttpResponseMessage((HttpStatusCode)status)
        {
            Content = new StringContent("backend-body")
        }));

        if (failOnError && status is >= 400 and <= 599)
        {
            var error = Assert.ThrowsExactly<PolicyException>(() => test.RunBackend());
            error.Policy.Should().Be(nameof(IBackendContext.ForwardRequest));
            error.InnerException.Should().BeOfType<HttpRequestException>()
                .Which.StatusCode.Should().Be((HttpStatusCode)status);
            test.Context.Variables.Should().NotContainKey("continued");
        }
        else
        {
            test.RunBackend();
            test.Context.Variables["continued"].Should().Be(true);
        }

        test.Context.Response.StatusCode.Should().Be(status);
        test.Context.Response.Body.Content.Should().Be("backend-body");
    }

    [TestMethod]
    public void ShouldPropagateClientFailureWithoutReplacingResponse()
    {
        var test = CreateTest();
        var failure = new HttpRequestException("backend unavailable");
        test.Context.Response.StatusCode = 418;
        test.Context.Response.Body.Content = "unchanged";
        test.Context.Services.Register<IHttpClient>(new SendRequestTests.RecordingHttpClient((_, _) => throw failure));

        var error = Assert.ThrowsExactly<PolicyException>(() => test.RunBackend());

        error.InnerException.Should().BeSameAs(failure);
        error.Section.Should().Be(nameof(IBackendContext));
        test.Context.Response.StatusCode.Should().Be(418);
        test.Context.Response.Body.Content.Should().Be("unchanged");
    }

    [TestMethod]
    [DataRow("1", 1, HttpVersionPolicy.RequestVersionExact)]
    [DataRow("2", 2, HttpVersionPolicy.RequestVersionExact)]
    [DataRow("2or1", 2, HttpVersionPolicy.RequestVersionOrLower)]
    public void ShouldExposeRequestedHttpVersionToTransport(string version, int major, HttpVersionPolicy policy)
    {
        var test = CreateTest(new ForwardRequestConfig { HttpVersion = version });
        test.Context.Services.Register<IHttpClient>(new StubHttpClient(request =>
        {
            request.Version.Major.Should().Be(major);
            request.VersionPolicy.Should().Be(policy);
            return new HttpResponseMessage(HttpStatusCode.OK);
        }));

        test.RunBackend();
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void ShouldEvaluateTimeoutContinueAndRedirectExpressions(bool milliseconds)
    {
        var test = new ConfiguredDocument(context => new ForwardRequestConfig
        {
            Timeout = milliseconds ? null : (uint)context.Variables["timeout"],
            TimeoutMs = milliseconds ? (uint)context.Variables["timeout"] : null,
            ContinueTimeout = (uint)context.Variables["continue"],
            FollowRedirects = (bool)context.Variables["redirect"],
            BufferRequestBody = true,
            BufferResponse = false
        }).AsTestDocument();
        test.Context.Variables["timeout"] = milliseconds ? 10000u : 10u;
        test.Context.Variables["continue"] = 2u;
        test.Context.Variables["redirect"] = true;
        test.Context.Services.Register<IHttpClient>(new StubHttpClient(request =>
        {
            request.Options.TryGetValue(HttpTransportOptions.Key, out var options).Should().BeTrue();
            options!.Timeout.Should().Be(TimeSpan.FromSeconds(10));
            options.ContinueTimeout.Should().Be(TimeSpan.FromSeconds(2));
            options.FollowRedirects.Should().BeTrue();
            options.BufferRequestBody.Should().BeTrue();
            options.BufferResponse.Should().BeFalse();
            return new HttpResponseMessage(HttpStatusCode.OK);
        }));

        test.RunBackend();
    }

    [TestMethod]
    [DataRow("both-timeouts")]
    [DataRow("http-version")]
    [DataRow("timeout-range")]
    public void ShouldRejectInvalidForwardConfigurationBeforeTransport(string kind)
    {
        var config = kind switch
        {
            "both-timeouts" => new ForwardRequestConfig { Timeout = 1, TimeoutMs = 1000 },
            "http-version" => new ForwardRequestConfig { HttpVersion = "3" },
            _ => new ForwardRequestConfig { Timeout = uint.MaxValue }
        };
        var test = CreateTest(config);
        var client = new SendRequestTests.RecordingHttpClient((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));
        test.Context.Services.Register<IHttpClient>(client);

        Assert.ThrowsExactly<PolicyException>(() => test.RunBackend())
            .InnerException.Should().BeAssignableTo<ArgumentException>();
        client.Calls.Should().Be(0);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void ShouldCancelBeforeSendingForZeroTimeoutOrCanceledCaller(bool callerCancellation)
    {
        using var cancellation = new CancellationTokenSource();
        if (callerCancellation) cancellation.Cancel();
        var test = CreateTest(new ForwardRequestConfig { TimeoutMs = callerCancellation ? 1000u : 0u });
        test.Context.Services.Register(new HttpTransportState { CancellationToken = cancellation.Token });
        var client = new SendRequestTests.RecordingHttpClient((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));
        test.Context.Services.Register<IHttpClient>(client);

        Assert.ThrowsExactly<PolicyException>(() => test.RunBackend())
            .InnerException.Should().BeAssignableTo<OperationCanceledException>();
        client.Calls.Should().Be(0);
    }

    [TestMethod]
    public async Task ShouldEnforceTimeoutEvenWhenInjectedClientIgnoresToken()
    {
        var test = CreateTest(new ForwardRequestConfig { TimeoutMs = 20 });
        var pending = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        var client = new SendRequestTests.RecordingHttpClient((_, _) => pending.Task);
        test.Context.Services.Register<IHttpClient>(client);
        var execution = Task.Run(() =>
        {
            try { test.RunBackend(); return null; }
            catch (PolicyException error) { return error; }
        });
        try
        {
            var error = await execution.WaitAsync(TimeSpan.FromSeconds(2));
            error.Should().NotBeNull();
            error!.InnerException.Should().BeAssignableTo<OperationCanceledException>();
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
        public void Backend(IBackendContext context) => context.IncludeFragment("forward-http");
    }

    private sealed class ForwardFragment : IFragment
    {
        public void Fragment(IFragmentContext context) => context.ForwardRequest();
    }

    [TestMethod]
    public void ShouldForwardInsideBackendFragment()
    {
        var test = new FragmentDocument().AsTestDocument().RegisterFragment("forward-http", new ForwardFragment());
        test.Context.Services.Register<IHttpClient>(StubHttpClient.Ok("fragment"));

        test.RunBackend();

        test.Context.Response.Body.Content.Should().Be("fragment");
    }

    [TestMethod]
    public void ShouldAllowCallbackErrorSimulationWithoutClient()
    {
        var test = CreateTest();
        var failure = new HttpRequestException("callback failed");
        test.SetupBackend().ForwardRequest((_, config) => config is null).WithCallback((_, _) => throw failure);

        Assert.ThrowsExactly<PolicyException>(() => test.RunBackend()).InnerException.Should().BeSameAs(failure);
    }

    [TestMethod]
    public void ShouldExposeExistingBackendAuthenticationCertificateWithoutTakingOwnership()
    {
        using var key = RSA.Create(2048);
        var certificateRequest = new CertificateRequest("CN=forward-emulator-test", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var certificate = certificateRequest.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddHours(1));
        var test = CreateTest();
        test.Context.Request.Certificate = certificate;
        test.Context.Request.Headers["Authorization"] = ["Bearer existing-token"];
        test.Context.Services.Register<IHttpClient>(new StubHttpClient(request =>
        {
            request.Headers.Authorization!.ToString().Should().Be("Bearer existing-token");
            request.Options.TryGetValue(HttpTransportOptions.Key, out var options).Should().BeTrue();
            options!.Certificate.Should().BeSameAs(certificate);
            return new HttpResponseMessage(HttpStatusCode.OK);
        }));

        test.RunBackend();

        test.Context.Request.Certificate.Should().BeSameAs(certificate);
        certificate.Thumbprint.Should().NotBeNullOrEmpty();
    }

    [TestMethod]
    public async Task ShouldApplyForwardTimeoutToHeadersOnlyNotDelayedResponseBody()
    {
        var test = CreateTest(new ForwardRequestConfig { TimeoutMs = 100 });
        var content = new SendRequestTests.ControlledResponseContent("delayed-body");
        var client = new SendRequestTests.RecordingHttpClient(async (_, cancellation) =>
        {
            await Task.Delay(19, cancellation).ConfigureAwait(false);
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
        });
        test.Context.Services.Register<IHttpClient>(client);
        var execution = Task.Run(() =>
        {
            try { test.RunBackend(); return null; }
            catch (PolicyException error) { return error; }
        });

        try
        {
            await content.Started.WaitAsync(TimeSpan.FromSeconds(3));
            await Task.Delay(350);
            execution.IsCompleted.Should().BeFalse("headers arrived within 100ms, but the response body is still pending");
            client.LastCancellationToken.IsCancellationRequested.Should().BeFalse();
            content.BodyCancellationToken.IsCancellationRequested.Should().BeFalse();
            content.Release();
            (await execution.WaitAsync(TimeSpan.FromSeconds(3))).Should().BeNull();
            test.Context.Response.Body.Content.Should().Be("delayed-body");
            content.Disposed.Should().BeTrue();
        }
        finally
        {
            content.Release();
            await execution;
        }
    }

    [TestMethod]
    public async Task ShouldRetainCallerCancellationDuringForwardResponseBodyAfterHeaderDeadline()
    {
        using var cancellation = new CancellationTokenSource();
        var test = CreateTest(new ForwardRequestConfig { TimeoutMs = 100 });
        test.Context.Services.Register(new HttpTransportState { CancellationToken = cancellation.Token });
        var content = new SendRequestTests.ControlledResponseContent("pending-body");
        var client = new SendRequestTests.RecordingHttpClient((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = content }));
        test.Context.Services.Register<IHttpClient>(client);
        var execution = Task.Run(() =>
        {
            try { test.RunBackend(); return null; }
            catch (PolicyException error) { return error; }
        });

        try
        {
            await content.Started.WaitAsync(TimeSpan.FromSeconds(3));
            await Task.Delay(150);
            execution.IsCompleted.Should().BeFalse();
            client.LastCancellationToken.IsCancellationRequested.Should().BeFalse();
            cancellation.Cancel();
            var error = await execution.WaitAsync(TimeSpan.FromSeconds(3));
            error.Should().NotBeNull();
            error!.InnerException.Should().BeAssignableTo<OperationCanceledException>();
            client.LastCancellationToken.IsCancellationRequested.Should().BeTrue();
            content.BodyCancellationToken.IsCancellationRequested.Should().BeTrue();
        }
        finally
        {
            content.Release();
            await execution;
        }
    }
}