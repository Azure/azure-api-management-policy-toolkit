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
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Data;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Expressions;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Services;

namespace Test.Emulator.Emulator.Policies;

[TestClass]
public class ForwardRequestTests
{
    [TestMethod]
    public void CallbackOwnership_CallbackSelectionEvaluatesEachPredicateOnlyOnce()
    {
        var test = new HeaderFlowDocument(
            inbound: context => context.Cors(OverlayCors with { ExposeHeaders = ["*"] }),
            backend: context => context.ForwardRequest(new ForwardRequestConfig { HttpVersion = "mock-only" }))
            .AsTestDocument();
        test.Context.Request.Headers["Origin"] = [OverlayOrigin];
        var rejected = 0;
        var selected = 0;
        var callbacks = 0;
        test.SetupBackend().ForwardRequest((_, _) =>
        {
            rejected++;
            return false;
        }).WithCallback((_, _) => Assert.Fail("Unmatched callback executed."));
        test.SetupBackend().ForwardRequest((_, config) =>
        {
            selected++;
            return config?.HttpVersion == "mock-only";
        }).WithCallback((context, _) =>
        {
            callbacks++;
            FinalHeaderBoundaryTest.SetCallbackOwnedResponse(context, replace: true, ignoreCase: false);
        });

        test.RunRequest(request => request.RunRequest(inner => inner.RunAll()));

        rejected.Should().Be(1);
        selected.Should().Be(1);
        callbacks.Should().Be(1);
        FinalHeaderBoundaryTest.AssertCallbackOwnedResponse(test.Context);
        test.Context.Services.Resolve<IHttpClient>().Should().BeNull();
    }

    [TestMethod]
    public void CallbackOwnership_AThrowingPredicateDoesNotRetireNativePolicyProvenance()
    {
        var test = new HeaderFlowDocument(context => context.Cors(OverlayCors with { ExposeHeaders = ["*"] }))
            .AsTestDocument();
        test.Context.Request.Headers["Origin"] = [OverlayOrigin];
        var expected = new InvalidOperationException("predicate failed before callback selection");
        test.SetupBackend().ForwardRequest((_, _) => throw expected)
            .WithCallback((_, _) => Assert.Fail("Callback must not execute."));

        test.RunRequest(request =>
        {
            request.RunInbound();
            Assert.ThrowsExactly<PolicyException>(request.RunBackend).InnerException.Should().BeSameAs(expected);
            request.Context.Response.Headers["X-After-Predicate"] = ["final"];
        });

        test.Context.Response.Headers["Access-Control-Expose-Headers"].Should().Equal("X-After-Predicate");
        test.Context.Services.Resolve<IHttpClient>().Should().BeNull();
    }

    [TestMethod]
    [DataRow(200, false)]
    [DataRow(503, true)]
    public void TokenObservationAbsent_DoesNotCreateTokenServicesOrReadNonLlmPayloads(int status, bool failOnError)
    {
        var test = CreateTest(new ForwardRequestConfig { FailOnErrorStatusCode = failOnError });
        test.Context.Services.Register<ILlmTokenUsageProvider>(new UnexpectedUsageProvider());
        test.Context.Services.Register<IHttpClient>(new StubHttpClient(_ => new HttpResponseMessage((HttpStatusCode)status)
        {
            Content = new StringContent("not LLM JSON")
        }));

        if (failOnError)
        {
            Assert.ThrowsExactly<PolicyException>(test.RunBackend).InnerException.Should().BeOfType<HttpRequestException>();
        }
        else
        {
            test.RunBackend();
        }

        test.Context.Response.StatusCode.Should().Be(status);
        test.Context.Response.Body.Content.Should().Be("not LLM JSON");
        test.Context.Services.Resolve<TokenLimitService>().Should().BeNull();
        test.Context.Services.Resolve<TokenLimitCounterStore>().Should().BeNull();
    }

    [TestMethod]
    [DataRow(false, false)]
    [DataRow(false, true)]
    [DataRow(true, false)]
    [DataRow(true, true)]
    public void TokenObservationAtCopy_PublishesUsageBeforeContinuationOrFailOnError(bool azure, bool failOnError)
    {
        var tokens = new TokenLimitCounterStore();
        var config = new TokenLimitConfig
        {
            CounterKey = "copy",
            EstimatePromptToken = false,
            TokensPerMinute = 10,
            TokensConsumedVariableName = "consumed",
            TokensConsumedHeaderName = "X-Tokens"
        };
        var test = new TestDocument(new CopyObservationDocument(config, azure, failOnError));
        test.Context.Services.Register(tokens);
        test.Context.Services.Register<IHttpClient>(new StubHttpClient(_ =>
            new HttpResponseMessage(failOnError ? HttpStatusCode.ServiceUnavailable : HttpStatusCode.OK)
            {
                Content = new StringContent("""{"usage":{"prompt_tokens":4,"completion_tokens":2}}""",
                    Encoding.UTF8, "application/json")
            }));
        var headers = test.Context.Response.Headers;
        test.RunInbound();

        if (failOnError)
        {
            Assert.ThrowsExactly<PolicyException>(test.RunBackend).InnerException.Should().BeOfType<HttpRequestException>();
        }
        else
        {
            test.RunBackend();
            test.Context.Variables["consumed-at-continuation"].Should().Be(6L);
        }

        test.Context.Variables["consumed"].Should().Be(6L);
        test.Context.Response.Headers.Should().BeSameAs(headers);
        test.Context.Response.Headers["X-Tokens"].Should().Equal("6");
        tokens.GetRateTokens("copy", DateTimeOffset.UtcNow).Should().Be(0);
        test.RunRequest(request => request.RunOnError());
        tokens.GetRateTokens("copy", DateTimeOffset.UtcNow).Should().Be(6);
        test.Context.Response.StatusCode.Should().Be(502);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void TokenObservationAtCallbackReturn_PreservesOverrideAndCapturesBeforePostprocessing(bool azure)
    {
        var tokens = new TokenLimitCounterStore();
        var config = new TokenLimitConfig
        {
            CounterKey = "mock-copy",
            EstimatePromptToken = false,
            TokensPerMinute = 10,
            TokensConsumedVariableName = "consumed",
            TokensConsumedHeaderName = "X-Tokens"
        };
        var test = new TestDocument(new CallbackObservationDocument(config, azure));
        test.Context.Services.Register(tokens).Register<ILlmTokenUsageProvider>(new UnexpectedUsageProvider());
        var callbacks = 0;
        test.SetupBackend().ForwardRequest((_, callbackConfig) => callbackConfig?.HttpVersion == "unselected")
            .WithCallback((_, _) => Assert.Fail("The unmatched callback ran."));
        test.SetupBackend().ForwardRequest((_, callbackConfig) => callbackConfig?.HttpVersion == "mock-only")
            .WithCallback((context, _) =>
            {
                callbacks++;
                context.Response.StatusCode = 503;
                context.Response.Body.Content = """{"usage":{"prompt_tokens":4,"completion_tokens":2}}""";
            });
        var headers = test.Context.Response.Headers;

        test.RunAll();

        callbacks.Should().Be(1);
        test.Context.Services.Resolve<IHttpClient>().Should().BeNull();
        test.Context.Variables["consumed-at-continuation"].Should().Be(6L);
        test.Context.Variables["consumed"].Should().Be(6L);
        test.Context.Response.Headers.Should().BeSameAs(headers);
        test.Context.Response.Headers["X-Tokens"].Should().Equal("6");
        test.Context.Response.Body.Content.Should().Be("<formatted-after-callback />");
        test.Context.Response.StatusCode.Should().Be(503);
        tokens.GetRateTokens("mock-copy", DateTimeOffset.UtcNow).Should().Be(6);
    }

    [TestMethod]
    [DataRow("<non-llm />")]
    [DataRow("[]")]
    public void TokenObservationAbsent_CallbackOverrideDoesNotRequireLlmUsageOrTransport(string body)
    {
        var test = CreateTest(new ForwardRequestConfig { HttpVersion = "mock-only", FailOnErrorStatusCode = true });
        test.Context.Request.Body.Content = """{"stream":true}""";
        test.Context.Services.Register<ILlmTokenUsageProvider>(new UnexpectedUsageProvider());
        var callbacks = 0;
        test.SetupBackend().ForwardRequest().WithCallback((context, _) =>
        {
            callbacks++;
            context.Response.StatusCode = 503;
            context.Response.Headers["Content-Type"] = ["text/event-stream"];
            context.Response.Body.Content = body;
        });

        test.RunBackend();

        callbacks.Should().Be(1);
        test.Context.Response.StatusCode.Should().Be(503);
        test.Context.Response.Body.Content.Should().Be(body);
        test.Context.Variables["continued"].Should().Be(true);
        test.Context.Services.Resolve<IHttpClient>().Should().BeNull();
        test.Context.Services.Resolve<TokenLimitService>().Should().BeNull();
        test.Context.Services.Resolve<TokenLimitCounterStore>().Should().BeNull();
    }

    private sealed class CallbackObservationDocument(TokenLimitConfig config, bool azure) : IDocument
    {
        public void Inbound(IInboundContext context)
        {
            if (azure) context.AzureOpenAiTokenLimit(config);
            else context.LlmTokenLimit(config);
        }

        public void Backend(IBackendContext context)
        {
            context.ForwardRequest(new ForwardRequestConfig { HttpVersion = "mock-only", FailOnErrorStatusCode = true });
            ((GatewayContext)context.ExpressionContext).Variables["consumed-at-continuation"] =
                context.ExpressionContext.Variables["consumed"];
            ((GatewayContext)context.ExpressionContext).Response.Body.Content = "<formatted-after-callback />";
        }
    }

    private sealed class UnexpectedUsageProvider : ILlmTokenUsageProvider
    {
        public LlmTokenUsage? GetUsage(GatewayContext context) =>
            throw new InvalidOperationException("ForwardRequest without a token policy must not query token usage.");
    }

    private sealed class CopyObservationDocument(TokenLimitConfig config, bool azure, bool failOnError) : IDocument
    {
        public void Inbound(IInboundContext context)
        {
            if (azure) context.AzureOpenAiTokenLimit(config);
            else context.LlmTokenLimit(config);
        }

        public void Backend(IBackendContext context)
        {
            context.ForwardRequest(new ForwardRequestConfig { FailOnErrorStatusCode = failOnError });
            ((GatewayContext)context.ExpressionContext).Variables["consumed-at-continuation"] =
                context.ExpressionContext.Variables["consumed"];
            ((GatewayContext)context.ExpressionContext).Response.Body.Content = """{"answer":"backend rewrite"}""";
        }

        public void OnError(IOnErrorContext context)
        {
            context.SetStatus(new StatusConfig { Code = 502, Reason = "Handled" });
            context.SetBody("""{"error":"rewritten"}""");
        }
    }

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

    private const string OverlayOrigin = "https://overlay-client.example.test";

    private static CorsConfig OverlayCors => new()
    {
        AllowedOrigins = [OverlayOrigin],
        AllowedHeaders = [],
        AllowCredentials = true,
        ExposeHeaders = ["X-Remaining"]
    };

    private static RateLimitByKeyConfig OverlayRate => new()
    {
        CounterKey = "overlay-customer",
        Calls = 10,
        RenewalPeriod = 60,
        RemainingCallsHeaderName = "X-Remaining",
        TotalCallsHeaderName = "X-Total"
    };

    internal sealed class HeaderFlowDocument(
        Action<IInboundContext>? inbound = null,
        Action<IBackendContext>? backend = null,
        Action<IOutboundContext>? outbound = null) : IDocument
    {
        public void Inbound(IInboundContext context) => inbound?.Invoke(context);
        public void Backend(IBackendContext context)
        {
            if (backend is null) context.ForwardRequest();
            else backend(context);
        }
        public void Outbound(IOutboundContext context) => outbound?.Invoke(context);
    }

    private static Dictionary<string, string[]> OverlayHeaders(bool ignoreCase) => new(
        ignoreCase ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal)
    {
        ["access-control-allow-origin"] = ["mock-lower"],
        ["ACCESS-CONTROL-ALLOW-ORIGIN"] = ["mock-upper"],
        ["x-remaining"] = ["999"],
        ["X-REMAINING"] = ["998"],
        ["X-Stale"] = ["mock-only"]
    };

    private static HttpResponseMessage ConflictingBackendResponse()
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("backend") };
        response.Headers.TryAddWithoutValidation("access-control-allow-origin", "backend-origin");
        response.Headers.TryAddWithoutValidation("x-remaining", "backend-remaining");
        response.Headers.TryAddWithoutValidation("X-Total", "backend-total");
        response.Headers.TryAddWithoutValidation("X-Backend", "keep");
        return response;
    }

    [TestMethod]
    [DataRow(false, false, false)]
    [DataRow(false, true, false)]
    [DataRow(true, false, false)]
    [DataRow(true, true, false)]
    [DataRow(false, false, true)]
    [DataRow(false, true, true)]
    [DataRow(true, false, true)]
    [DataRow(true, true, true)]
    public void PolicyHeaders_SurviveFlatAndNestedBackendReplacement(
        bool nested, bool ignoreCase, bool duplicateScopes)
    {
        var headers = OverlayHeaders(ignoreCase);
        var comparer = headers.Comparer;
        var pipeline = PolicyPipelineBuilder.Create()
            .AddPolicy(PolicyScope.Global, new HeaderFlowDocument(
                inbound: context =>
                {
                    context.Cors(OverlayCors);
                    context.RateLimitByKey(OverlayRate);
                    context.Base();
                },
                backend: context => context.Base(),
                outbound: context => context.Base()))
            .AddPolicy(PolicyScope.Api, new HeaderFlowDocument(
                inbound: context =>
                {
                    if (duplicateScopes)
                    {
                        context.Cors(OverlayCors with { AllowCredentials = false, ExposeHeaders = ["X-Inner"] });
                        context.RateLimitByKey(OverlayRate);
                    }
                    context.Base();
                },
                outbound: context => context.Base()))
            .ConfigureContext(context =>
            {
                context.Request.Headers["oRiGiN"] = [OverlayOrigin];
                context.Response.Headers = headers;
                context.Services.Register<IHttpClient>(new StubHttpClient(_ => ConflictingBackendResponse()));
            })
            .Build();

        if (nested) pipeline.RunAllNested();
        else pipeline.RunAll();

        pipeline.Context.Response.Headers.Should().BeSameAs(headers);
        headers.Comparer.Should().BeSameAs(comparer);
        headers.Where(header => header.Key.Equals("Access-Control-Allow-Origin", StringComparison.OrdinalIgnoreCase))
            .Should().ContainSingle().Which.Value.Should().Equal(OverlayOrigin);
        headers.Where(header => header.Key.Equals("X-Remaining", StringComparison.OrdinalIgnoreCase))
            .Should().ContainSingle().Which.Value.Should().Equal(duplicateScopes ? "8" : "9");
        headers["X-Total"].Should().Equal("10");
        headers["X-Backend"].Should().Equal("keep");
        headers.Should().NotContainKey("X-Stale");
        headers.ContainsKey("Access-Control-Allow-Credentials").Should().Be(!duplicateScopes);
        headers["Access-Control-Expose-Headers"].Should().Equal(duplicateScopes ? "X-Inner" : "X-Remaining");
        pipeline.Context.Response.Body.Content.Should().Be("backend");
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void PolicyHeaders_DoNotPreserveUnregisteredStaleNames(bool ignoreCase)
    {
        var test = new HeaderFlowDocument().AsTestDocument();
        var headers = OverlayHeaders(ignoreCase);
        test.Context.Response.Headers = headers;
        test.Context.Services.Register<IHttpClient>(StubHttpClient.Ok("new"));

        test.RunAll();

        test.Context.Response.Headers.Should().BeSameAs(headers);
        headers.Should().NotContainKeys("X-Stale", "X-Remaining", "Access-Control-Allow-Origin");
        test.Context.Services.Resolve<TokenLimitService>().Should().BeNull();
        test.Context.Services.Resolve<PolicyCounterService>().Should().BeNull();
    }

    [TestMethod]
    public void PolicyHeaders_CloneRestoredArraysAndIgnoreFreshRequestStaleValues()
    {
        var enabled = true;
        var test = new HeaderFlowDocument(context =>
        {
            if (!enabled) return;
            context.Cors(OverlayCors);
            context.RateLimitByKey(OverlayRate);
        }).AsTestDocument();
        test.Context.Request.Headers["Origin"] = [OverlayOrigin];
        test.Context.Services.Register<IHttpClient>(StubHttpClient.Ok("new"));
        test.RunInbound();
        var origin = test.Context.Response.Headers["Access-Control-Allow-Origin"];
        var remaining = test.Context.Response.Headers["X-Remaining"];

        test.RunBackend();

        test.Context.Response.Headers["Access-Control-Allow-Origin"].Should().NotBeSameAs(origin).And.Equal(OverlayOrigin);
        test.Context.Response.Headers["X-Remaining"].Should().NotBeSameAs(remaining).And.Equal("9");
        origin[0] = "mutated-old-origin";
        remaining[0] = "mutated-old-count";
        test.Context.Response.Headers["Access-Control-Allow-Origin"].Should().Equal(OverlayOrigin);
        test.Context.Response.Headers["X-Remaining"].Should().Equal("9");
        test.RunRequest(_ => { });
        enabled = false;
        test.Context.RequestId = Guid.NewGuid();

        test.RunAll();

        test.Context.Response.Headers.Should().NotContainKeys(
            "Access-Control-Allow-Origin", "Access-Control-Allow-Credentials", "Access-Control-Expose-Headers",
            "X-Remaining", "X-Total");
    }

    private sealed class ResponseHeaderMutationDocument(string action, string name) : IDocument
    {
        public void Outbound(IOutboundContext context)
        {
            if (action == "remove") context.RemoveHeader(name);
            else
            {
                context.SetHeader(name.ToLowerInvariant(), ["first-override"]);
                context.SetHeader(name.ToUpperInvariant(), ["last-override"]);
            }
        }
    }

    [TestMethod]
    [DataRow("remove", "X-Remaining", false)]
    [DataRow("remove", "X-Remaining", true)]
    [DataRow("set", "X-Remaining", false)]
    [DataRow("set", "X-Remaining", true)]
    [DataRow("remove", "Access-Control-Allow-Origin", false)]
    [DataRow("set", "Access-Control-Allow-Origin", false)]
    public void PolicyHeaders_HonorResponseMutationBeforeAnyLaterForward(
        string action, string name, bool ignoreCase)
    {
        var test = new HeaderFlowDocument(context =>
        {
            context.Cors(OverlayCors);
            context.RateLimitByKey(OverlayRate);
        }).AsTestDocument();
        test.Context.Request.Headers["Origin"] = [OverlayOrigin];
        test.Context.Response.Headers = OverlayHeaders(ignoreCase);
        test.Context.Services.Register<IHttpClient>(StubHttpClient.Ok());
        test.RunInbound();
        var mutation = new TestDocument(new ResponseHeaderMutationDocument(action, name)) { Context = test.Context };

        mutation.RunOutbound();
        test.RunBackend();
        test.RunBackend();

        var matching = test.Context.Response.Headers
            .Where(header => header.Key.Equals(name, StringComparison.OrdinalIgnoreCase)).ToArray();
        if (action == "remove") matching.Should().BeEmpty();
        else matching.Should().ContainSingle().Which.Value.Should().Equal("last-override");
        test.Context.Response.Headers.Should().NotContainKey("X-Stale");
    }

    [TestMethod]
    [DataRow("remove")]
    [DataRow("set")]
    public void PolicyHeaders_ExplicitInboundRequestMutationsDoNotAlterGeneratedResponse(string action)
    {
        var test = new HeaderFlowDocument(context =>
        {
            context.RateLimitByKey(OverlayRate);
            if (action == "remove") context.RemoveHeader("X-Remaining");
            else context.SetHeader("x-remaining", ["request-value"]);
        }).AsTestDocument();
        test.Context.Request.Headers["X-Remaining"] = ["request-old"];
        test.Context.Services.Register<IHttpClient>(StubHttpClient.Ok());

        test.RunAll();

        test.Context.Response.Headers["X-Remaining"].Should().Equal("9");
        if (action == "remove") test.Context.Request.Headers.Should().NotContainKey("X-Remaining");
        else test.Context.Request.Headers["X-Remaining"].Should().Equal("request-value");
    }

    [TestMethod]
    [DataRow("remove")]
    [DataRow("set")]
    public void PolicyHeaders_InboundCallbackResponseMutationsAreNotReplayedStale(string action)
    {
        var test = new HeaderFlowDocument(context =>
        {
            context.RateLimitByKey(OverlayRate);
            if (action == "remove") context.RemoveHeader("X-Remaining");
            else context.SetHeader("x-remaining", ["callback-response"]);
        }).AsTestDocument();
        if (action == "remove")
        {
            test.SetupInbound().RemoveHeader().WithCallback((context, name) =>
                new TestDocument(new ResponseHeaderMutationDocument("remove", name)) { Context = context }.RunOutbound());
        }
        else
        {
            test.SetupInbound().SetHeader().WithCallback((context, name, values) =>
                context.Response.Headers[name] = values);
        }
        test.Context.Services.Register<IHttpClient>(StubHttpClient.Ok());

        test.RunAll();

        if (action == "remove") test.Context.Response.Headers.Should().NotContainKey("X-Remaining");
        else test.Context.Response.Headers["X-Remaining"].Should().Equal("callback-response");
    }

    [TestMethod]
    public void PolicyHeaders_ResponseSetterDoesNotRegisterUnrelatedStaleHeaders()
    {
        var test = new HeaderFlowDocument(context => context.RateLimitByKey(OverlayRate)).AsTestDocument();
        test.Context.Services.Register<IHttpClient>(StubHttpClient.Ok());
        test.RunInbound();
        var mutation = new TestDocument(new HeaderFlowDocument(
            outbound: context => context.SetHeader("X-Unregistered", ["stale-response"])))
        { Context = test.Context };
        mutation.RunOutbound();

        test.RunBackend();

        test.Context.Response.Headers["X-Remaining"].Should().Equal("9");
        test.Context.Response.Headers.Should().NotContainKey("X-Unregistered");
    }

    [TestMethod]
    public void PolicyHeaders_ForwardCallbackRemainsAnExplicitResponseOverride()
    {
        var test = new HeaderFlowDocument(context => context.RateLimitByKey(OverlayRate)).AsTestDocument();
        test.SetupBackend().ForwardRequest().WithCallback((context, _) =>
        {
            context.Response.Headers.Clear();
            context.Response.Headers["X-Mock"] = ["only"];
            context.Response.StatusCode = 202;
        });

        test.RunAll();

        test.Context.Response.Headers.Should().ContainSingle().Which.Key.Should().Be("X-Mock");
        test.Context.Response.StatusCode.Should().Be(202);
        test.Context.Services.Resolve<IHttpClient>().Should().BeNull();
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void PolicyHeaders_PreserveRateAliasAndDeferredOutputUpdates(bool deferred)
    {
        var test = new HeaderFlowDocument(context =>
        {
            context.Cors(OverlayCors);
            if (deferred) context.RateLimitByKey(OverlayRate with { IncrementAfterResponse = true });
            else context.RateLimit(new RateLimitConfig
            {
                Calls = 10,
                RenewalPeriod = 60,
                RemainingCallsHeaderName = "X-Remaining",
                TotalCallsHeaderName = "X-Total"
            });
            context.QuotaByKey(new QuotaByKeyConfig
            {
                CounterKey = "overlay-bandwidth",
                Calls = 100,
                Bandwidth = 10,
                RenewalPeriod = 3600
            });
        }).AsTestDocument();
        test.Context.Request.Headers["Origin"] = [OverlayOrigin];
        test.Context.Services.Register<IHttpClient>(StubHttpClient.Ok("backend"));
        test.RunInbound();

        test.RunBackend();
        test.Context.Response.Headers["X-Remaining"].Should().Equal(deferred ? "10" : "9");
        test.CompleteLimiterResponse();
        test.SetupRateLimitStore().GetBandwidth("quota-by-key:overlay-bandwidth").Should().Be(7);
        test.Context.Response.Headers["X-Remaining"].Should().Equal("9");
        test.RunBackend();

        test.Context.Response.Headers["X-Remaining"].Should().Equal("9");
        test.Context.Response.Headers["X-Total"].Should().Equal("10");
        test.Context.Response.Headers["Access-Control-Allow-Origin"].Should().Equal(OverlayOrigin);
    }

    private sealed class OverlayUsageProvider(Action<GatewayContext> observe) : ILlmTokenUsageProvider
    {
        public LlmTokenUsage? GetUsage(GatewayContext context)
        {
            observe(context);
            return new LlmTokenUsage { PromptTokens = 2, CompletionTokens = 1 };
        }
    }

    [TestMethod]
    [DataRow(false, false)]
    [DataRow(false, true)]
    [DataRow(true, false)]
    [DataRow(true, true)]
    public void PolicyHeaders_RestoreTokenOutputsBeforeUsageObservationAndOutbound(bool azure, bool ignoreCase)
    {
        var tokens = new TokenLimitCounterStore();
        var config = new TokenLimitConfig
        {
            CounterKey = "overlay-token",
            EstimatePromptToken = false,
            TokensPerMinute = 100,
            TokenQuota = 200,
            TokenQuotaPeriod = "Daily",
            RemainingTokensHeaderName = "X-Token-Remaining",
            RemainingQuotaTokensHeaderName = "X-Token-Quota",
            TokensConsumedHeaderName = "X-Token-Consumed"
        };
        var observed = 0;
        var test = new HeaderFlowDocument(
            inbound: context =>
            {
                context.Cors(OverlayCors);
                context.RateLimitByKey(OverlayRate);
                if (azure) context.AzureOpenAiTokenLimit(config);
                else context.LlmTokenLimit(config);
            },
            outbound: context =>
            {
                context.ExpressionContext.Response.Headers["X-Token-Remaining"].Should().Equal("100");
                context.ExpressionContext.Response.Headers["X-Token-Quota"].Should().Equal("200");
                context.ExpressionContext.Response.Headers["X-Token-Consumed"].Should().Equal("3");
            }).AsTestDocument();
        test.Context.Request.Headers["Origin"] = [OverlayOrigin];
        test.Context.Response.Headers = OverlayHeaders(ignoreCase);
        test.Context.Services.Register(tokens);
        test.Context.Services.Register<ILlmTokenUsageProvider>(new OverlayUsageProvider(context =>
        {
            observed++;
            context.Response.Headers["Access-Control-Allow-Origin"].Should().Equal(OverlayOrigin);
            context.Response.Headers["X-Remaining"].Should().Equal("9");
            context.Response.Headers["X-Token-Remaining"].Should().Equal("100");
            context.Response.Headers["X-Token-Quota"].Should().Equal("200");
        }));
        test.Context.Services.Register<IHttpClient>(new StubHttpClient(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{}", Encoding.UTF8, "application/json")
        }));

        test.RunAll();

        observed.Should().Be(1);
        test.Context.Response.Headers["X-Token-Remaining"].Should().Equal("97");
        test.Context.Response.Headers["X-Token-Quota"].Should().Equal("197");
        test.Context.Response.Headers["X-Token-Consumed"].Should().Equal("3");
        tokens.GetRateTokens("overlay-token", DateTimeOffset.UtcNow).Should().Be(3);
    }

    [TestMethod]
    [DataRow(false, "remove", "X-Token-Remaining", false)]
    [DataRow(false, "remove", "X-Token-Remaining", true)]
    [DataRow(false, "set", "X-Token-Remaining", false)]
    [DataRow(false, "set", "X-Token-Remaining", true)]
    [DataRow(true, "remove", "X-Token-Remaining", false)]
    [DataRow(true, "remove", "X-Token-Remaining", true)]
    [DataRow(true, "set", "X-Token-Remaining", false)]
    [DataRow(true, "set", "X-Token-Remaining", true)]
    [DataRow(false, "remove", "X-Token-Quota", false)]
    [DataRow(false, "remove", "X-Token-Quota", true)]
    [DataRow(false, "set", "X-Token-Quota", false)]
    [DataRow(false, "set", "X-Token-Quota", true)]
    [DataRow(true, "remove", "X-Token-Quota", false)]
    [DataRow(true, "remove", "X-Token-Quota", true)]
    [DataRow(true, "set", "X-Token-Quota", false)]
    [DataRow(true, "set", "X-Token-Quota", true)]
    public void PolicyHeaders_HonorResponseMutationsOfTokenOutputsBeforeRepeatedForwarding(
        bool azure, string action, string name, bool ignoreCase)
    {
        var config = new TokenLimitConfig
        {
            CounterKey = "mutated-token",
            EstimatePromptToken = false,
            TokensPerMinute = 100,
            TokenQuota = 200,
            TokenQuotaPeriod = "Daily",
            RemainingTokensHeaderName = "X-Token-Remaining",
            RemainingQuotaTokensHeaderName = "X-Token-Quota"
        };
        var test = new HeaderFlowDocument(context =>
        {
            if (azure) context.AzureOpenAiTokenLimit(config);
            else context.LlmTokenLimit(config);
        }).AsTestDocument();
        test.Context.Response.Headers = new Dictionary<string, string[]>(
            ignoreCase ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
        test.Context.Services.Register<IHttpClient>(new StubHttpClient(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"usage":{"prompt_tokens":2,"completion_tokens":1}}""",
                    Encoding.UTF8, "application/json")
            }));
        test.RunInbound();
        new TestDocument(new ResponseHeaderMutationDocument(action, name)) { Context = test.Context }.RunOutbound();

        test.RunBackend();
        test.RunBackend();

        var matching = test.Context.Response.Headers
            .Where(header => header.Key.Equals(name, StringComparison.OrdinalIgnoreCase)).ToArray();
        if (action == "remove") matching.Should().BeEmpty();
        else matching.Should().ContainSingle().Which.Value.Should().Equal("last-override");
        var untouched = name == "X-Token-Remaining" ? "X-Token-Quota" : "X-Token-Remaining";
        test.Context.Response.Headers[untouched].Should().Equal(untouched == "X-Token-Quota" ? "200" : "100");
    }

    [TestMethod]
    public void PolicyHeaders_CannotBeSharedBetweenGatewayContextsByServiceCopy()
    {
        var source = new HeaderFlowDocument(context => context.Cors(OverlayCors)).AsTestDocument();
        source.Context.Request.Headers["Origin"] = [OverlayOrigin];
        source.RunInbound();
        var target = new HeaderFlowDocument().AsTestDocument();
        source.Context.Services.CopyTo(target.Context.Services);
        var forwarded = false;
        target.Context.Services.Register<IHttpClient>(new StubHttpClient(_ =>
        {
            forwarded = true;
            return new HttpResponseMessage(HttpStatusCode.OK);
        }));
        target.Context.Response.Headers["X-Target"] = ["unchanged"];

        var error = Assert.ThrowsExactly<PolicyException>(target.RunBackend);

        error.Policy.Should().Be(nameof(IBackendContext.ForwardRequest));
        error.InnerException.Should().BeOfType<InvalidOperationException>();
        forwarded.Should().BeFalse();
        target.Context.Response.Headers.Should().ContainSingle().Which.Value.Should().Equal("unchanged");
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void PolicyHeaders_RepeatedCaseVariantCallbackValuesRemainOrderedAndCloned(bool ignoreCase)
    {
        var test = new HeaderFlowDocument(context => context.RateLimitByKey(OverlayRate)).AsTestDocument();
        test.Context.Response.Headers = new Dictionary<string, string[]>(
            ignoreCase ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
        test.Context.Services.Register<IHttpClient>(StubHttpClient.Ok());
        test.RunInbound();
        var first = new[] { "callback-first", "callback-first" };
        var second = new[] { "callback-last" };
        test.Context.Response.Headers["X-Remaining"] = first;
        test.Context.Response.Headers["x-remaining"] = second;

        test.RunBackend();

        var restored = test.Context.Response.Headers
            .Where(header => header.Key.Equals("X-Remaining", StringComparison.OrdinalIgnoreCase))
            .Should().ContainSingle().Which.Value;
        restored.Should().Equal(ignoreCase ? second : [.. first, .. second]);
        restored.Should().NotBeSameAs(first).And.NotBeSameAs(second);
        first[0] = "changed";
        second[0] = "changed";
        restored.Should().NotContain("changed");
    }

    [TestMethod]
    [DataRow(null, false)]
    [DataRow(null, true)]
    [DataRow("X-Retry", false)]
    [DataRow("X-Retry", true)]
    public void PolicyHeaders_RateRecoveryDoesNotRestoreAnObsoleteRegisteredRetryHeader(
        string? retryName, bool ignoreCase)
    {
        var allowed = false;
        var test = new HeaderFlowDocument(context => context.RateLimit(new RateLimitConfig
        {
            Calls = allowed ? 10 : 0,
            RenewalPeriod = 60,
            RetryAfterHeaderName = retryName,
            RemainingCallsHeaderName = "X-Remaining",
            TotalCallsHeaderName = "X-Total"
        })).AsTestDocument();
        test.Context.Response.Headers = new Dictionary<string, string[]>(
            ignoreCase ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
        test.RunInbound();
        var retry = retryName ?? "Retry-After";
        test.Context.Response.Headers[retry].Should().Equal("60");
        allowed = true;
        test.Context.ResponseTerminated = false;
        test.RunInbound();
        test.Context.Response.Headers.Keys.Should().NotContain(
            name => name.Equals(retry, StringComparison.OrdinalIgnoreCase));
        test.Context.Services.Register<IHttpClient>(StubHttpClient.Ok());

        test.RunBackend();
        test.RunBackend();

        test.Context.Response.Headers["X-Remaining"].Should().Equal("9");
        test.Context.Response.Headers["X-Total"].Should().Equal("10");
        test.Context.Response.Headers.Keys.Should().NotContain(
            name => name.Equals(retry, StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void PolicyHeaders_PreserveAnExplicitEmptyRegisteredHeaderValue(bool ignoreCase)
    {
        var test = new HeaderFlowDocument(context => context.RateLimitByKey(OverlayRate)).AsTestDocument();
        test.Context.Response.Headers = new Dictionary<string, string[]>(
            ignoreCase ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
        test.Context.Services.Register<IHttpClient>(StubHttpClient.Ok());
        test.RunInbound();
        new TestDocument(new HeaderFlowDocument(outbound: context => context.SetHeader("x-remaining", [])))
        { Context = test.Context }.RunOutbound();

        test.RunBackend();

        test.Context.Response.Headers
            .Where(header => header.Key.Equals("X-Remaining", StringComparison.OrdinalIgnoreCase))
            .Should().ContainSingle().Which.Value.Should().BeEmpty();
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void PolicyHeaders_RemovalDoesNotClaimLaterBackendHeadersAsGenerated(bool ignoreCase)
    {
        var calls = 0;
        var test = new HeaderFlowDocument(context => context.RateLimitByKey(OverlayRate)).AsTestDocument();
        test.Context.Response.Headers = new Dictionary<string, string[]>(
            ignoreCase ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
        test.Context.Services.Register<IHttpClient>(new StubHttpClient(_ =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK);
            if (++calls == 1) response.Headers.TryAddWithoutValidation("X-Remaining", "backend-only");
            return response;
        }));
        test.RunInbound();
        new TestDocument(new ResponseHeaderMutationDocument("remove", "X-Remaining")) { Context = test.Context }
            .RunOutbound();

        test.RunBackend();
        test.Context.Response.Headers["X-Remaining"].Should().Equal("backend-only");
        test.RunBackend();

        test.Context.Response.Headers.Should().NotContainKey("X-Remaining");
        test.Context.Response.Headers["X-Total"].Should().Equal("10");
    }

    private static HttpResponseMessage WildcardBackend(string header = "X-Actual", bool includeExposure = true)
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("payload", Encoding.UTF8, "text/plain")
        };
        response.Headers.TryAddWithoutValidation(header, "backend");
        response.Headers.TryAddWithoutValidation("Set-Cookie", "private=1");
        response.Headers.TryAddWithoutValidation("Set-Cookie2", "private=2");
        if (includeExposure)
        {
            response.Headers.TryAddWithoutValidation("access-control-expose-headers", "X-Backend-Claim");
        }
        return response;
    }

    private static string[] WildcardExposure(GatewayContext context) =>
        context.Response.Headers.Single(header =>
                header.Key.Equals("Access-Control-Expose-Headers", StringComparison.OrdinalIgnoreCase))
            .Value.Single().Split(',');

    [TestMethod]
    [DataRow(false, false, false)]
    [DataRow(false, true, false)]
    [DataRow(true, false, false)]
    [DataRow(true, true, false)]
    [DataRow(false, false, true)]
    [DataRow(false, true, true)]
    [DataRow(true, false, true)]
    [DataRow(true, true, true)]
    public void CredentialedWildcard_ExposesActualBackendAndPolicyHeadersInEveryPipeline(
        bool nested, bool ignoreCase, bool seeded)
    {
        var headers = new Dictionary<string, string[]>(
            ignoreCase ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
        if (seeded)
        {
            headers["X-Stale"] = ["old"];
            headers["x-stale"] = ["old-case"];
        }
        var comparer = headers.Comparer;
        var pipeline = PolicyPipelineBuilder.Create()
            .AddPolicy(PolicyScope.Global, new HeaderFlowDocument(
                inbound: context =>
                {
                    context.Cors(OverlayCors with { ExposeHeaders = ["*"] });
                    context.RateLimitByKey(OverlayRate);
                    context.Base();
                },
                backend: context => context.Base(),
                outbound: context => context.Base()))
            .AddPolicy(PolicyScope.Api, new HeaderFlowDocument(
                inbound: context => context.Base(),
                outbound: context => context.Base()))
            .ConfigureContext(context =>
            {
                context.Request.Headers["Origin"] = [OverlayOrigin];
                context.Response.Headers = headers;
                context.Services.Register<IHttpClient>(new StubHttpClient(_ => WildcardBackend()));
            }).Build();

        if (nested) pipeline.RunAllNested();
        else pipeline.RunAll();

        pipeline.Context.Response.Headers.Should().BeSameAs(headers);
        headers.Comparer.Should().BeSameAs(comparer);
        headers.Keys.Should().NotContain(name => name.Equals("X-Stale", StringComparison.OrdinalIgnoreCase));
        WildcardExposure(pipeline.Context).Should().BeEquivalentTo(
            "X-Actual", "Content-Type", "Content-Length", "X-Remaining", "X-Total");
        headers["Set-Cookie"].Should().Equal("private=1");
        headers["Set-Cookie2"].Should().Equal("private=2");
        headers["X-Remaining"].Should().Equal("9");
    }

    [TestMethod]
    public void CredentialedWildcard_PreservesExplicitNamesButNeverExposesCookieNames()
    {
        var test = new HeaderFlowDocument(context => context.Cors(OverlayCors with
        {
            ExposeHeaders = ["X-Explicit", "*", "x-explicit", "set-cookie", "SET-COOKIE2"]
        })).AsTestDocument();
        test.Context.Request.Headers["Origin"] = [OverlayOrigin];
        test.Context.Response.Headers["X-Stale"] = ["old"];
        test.Context.Services.Register<IHttpClient>(new StubHttpClient(_ => WildcardBackend()));

        test.RunAll();

        WildcardExposure(test.Context).Should().Equal("X-Explicit", "X-Actual", "Content-Type", "Content-Length");
    }

    [TestMethod]
    [DataRow(false, false, "remove")]
    [DataRow(false, true, "remove")]
    [DataRow(true, false, "remove")]
    [DataRow(true, true, "remove")]
    [DataRow(false, false, "set")]
    [DataRow(false, true, "set")]
    [DataRow(true, false, "set")]
    [DataRow(true, true, "set")]
    [DataRow(false, false, "remove-then-set")]
    [DataRow(false, true, "remove-then-set")]
    [DataRow(true, false, "remove-then-set")]
    [DataRow(true, true, "remove-then-set")]
    public void CredentialedWildcard_ExplicitResponseMutationWinsBeforeRepeatedForwarding(
        bool ignoreCase, bool seeded, string action)
    {
        var test = new HeaderFlowDocument(context =>
            context.Cors(OverlayCors with { ExposeHeaders = ["*"] })).AsTestDocument();
        var headers = new Dictionary<string, string[]>(
            ignoreCase ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
        test.Context.Response.Headers = headers;
        if (seeded) headers["X-Stale"] = ["old"];
        test.Context.Request.Headers["Origin"] = [OverlayOrigin];
        test.Context.Services.Register<IHttpClient>(new StubHttpClient(_ => WildcardBackend()));
        test.RunInbound();
        var name = "Access-Control-Expose-Headers";
        if (action is "remove" or "remove-then-set")
        {
            new TestDocument(new ResponseHeaderMutationDocument("remove", name)) { Context = test.Context }.RunOutbound();
        }
        if (action is "set" or "remove-then-set")
        {
            new TestDocument(new ResponseHeaderMutationDocument("set", name)) { Context = test.Context }.RunOutbound();
        }

        test.RunBackend();
        test.RunBackend();

        test.Context.Response.Headers.Should().BeSameAs(headers);
        if (action == "remove")
        {
            headers.Keys.Should().NotContain(key => key.Equals(name, StringComparison.OrdinalIgnoreCase));
        }
        else
        {
            WildcardExposure(test.Context).Should().Equal("last-override");
        }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void CredentialedWildcard_RecomputesEachBackendReplacementWithoutTreatingItsOutputAsAnOverride(bool ignoreCase)
    {
        var calls = 0;
        var test = new HeaderFlowDocument(context =>
            context.Cors(OverlayCors with { ExposeHeaders = ["*"] })).AsTestDocument();
        test.Context.Response.Headers = new Dictionary<string, string[]>(
            ignoreCase ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal)
        { ["X-Stale"] = ["old"] };
        test.Context.Request.Headers["Origin"] = [OverlayOrigin];
        test.Context.Services.Register<IHttpClient>(new StubHttpClient(_ =>
            WildcardBackend(++calls == 1 ? "X-First" : "X-Second")));
        test.RunInbound();

        test.RunBackend();
        WildcardExposure(test.Context).Should().BeEquivalentTo("X-First", "Content-Type", "Content-Length");
        test.RunBackend();

        WildcardExposure(test.Context).Should().BeEquivalentTo("X-Second", "Content-Type", "Content-Length");
        test.Context.Response.Headers.Should().NotContainKeys("X-First", "X-Stale");
    }

    [TestMethod]
    [DataRow(false, "no-origin")]
    [DataRow(true, "no-origin")]
    [DataRow(false, "unmatched")]
    [DataRow(true, "unmatched")]
    [DataRow(false, "explicit")]
    [DataRow(true, "explicit")]
    [DataRow(false, "noncredentialed")]
    [DataRow(true, "noncredentialed")]
    public void CredentialedWildcard_ReconfigurationRetiresPendingDynamicExposure(bool seeded, string reconfiguration)
    {
        var test = new HeaderFlowDocument(context =>
            context.Cors(OverlayCors with { ExposeHeaders = ["*"] })).AsTestDocument();
        test.Context.Request.Headers["Origin"] = [OverlayOrigin];
        if (seeded) test.Context.Response.Headers["X-Stale"] = ["old"];
        test.RunInbound();
        if (reconfiguration == "no-origin") test.Context.Request.Headers.Remove("Origin");
        if (reconfiguration == "unmatched")
        {
            test.Context.Request.Headers["Origin"] = ["https://denied.example.test"];
        }
        var nextConfig = OverlayCors with
        {
            TerminateUnmatchedRequest = "false",
            AllowCredentials = reconfiguration != "noncredentialed",
            ExposeHeaders = reconfiguration == "explicit" ? ["X-Manual"] : ["*"]
        };
        new TestDocument(new HeaderFlowDocument(context => context.Cors(nextConfig))) { Context = test.Context }.RunInbound();
        test.Context.Services.Register<IHttpClient>(new StubHttpClient(_ => WildcardBackend(includeExposure: false)));

        test.RunBackend();

        if (reconfiguration is "no-origin" or "unmatched")
        {
            test.Context.Response.Headers.Should().NotContainKey("Access-Control-Expose-Headers");
        }
        else
        {
            WildcardExposure(test.Context).Should().Equal(reconfiguration == "explicit" ? "X-Manual" : "*");
        }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void CredentialedWildcard_FreshRequestCannotReuseAnOldResolver(bool seeded)
    {
        var enabled = true;
        var test = new HeaderFlowDocument(context =>
        {
            if (enabled) context.Cors(OverlayCors with { ExposeHeaders = ["*"] });
        }).AsTestDocument();
        test.Context.Request.Headers["Origin"] = [OverlayOrigin];
        if (seeded) test.Context.Response.Headers["X-Stale"] = ["old"];
        test.Context.Services.Register<IHttpClient>(new StubHttpClient(_ => WildcardBackend(includeExposure: false)));
        test.RunAll();
        WildcardExposure(test.Context).Should().Contain("X-Actual");
        test.Context.RequestId = Guid.NewGuid();
        enabled = false;

        test.RunAll();

        test.Context.Response.Headers.Should().NotContainKey("Access-Control-Expose-Headers");
    }

    [TestMethod]
    [DataRow(false, false)]
    [DataRow(false, true)]
    [DataRow(true, false)]
    [DataRow(true, true)]
    public void CredentialedWildcard_ForwardCallbackDoesNotResurrectRemovedOrStaleExposure(bool seeded, bool remove)
    {
        var test = new HeaderFlowDocument(context =>
            context.Cors(OverlayCors with { ExposeHeaders = ["*"] })).AsTestDocument();
        test.Context.Request.Headers["Origin"] = [OverlayOrigin];
        if (seeded) test.Context.Response.Headers["X-Stale"] = ["old"];
        test.RunInbound();
        if (remove)
        {
            new TestDocument(new ResponseHeaderMutationDocument("remove", "Access-Control-Expose-Headers"))
            { Context = test.Context }.RunOutbound();
        }
        test.SetupBackend().ForwardRequest().WithCallback((context, _) =>
        {
            context.Response.Headers.Clear();
            context.Response.Headers["X-Actual"] = ["callback"];
        });

        test.RunBackend();

        test.Context.Response.Headers.Should().ContainSingle().Which.Key.Should().Be("X-Actual");
        test.Context.Services.Resolve<IHttpClient>().Should().BeNull();
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void CredentialedWildcard_IncludesTokenOutputsAddedAtBackendObservation(bool azure)
    {
        var config = new TokenLimitConfig
        {
            CounterKey = "wildcard-token",
            EstimatePromptToken = false,
            TokensPerMinute = 100,
            RemainingTokensHeaderName = "X-Token-Remaining",
            TokensConsumedHeaderName = "X-Token-Consumed"
        };
        var test = new HeaderFlowDocument(context =>
        {
            context.Cors(OverlayCors with { ExposeHeaders = ["*"] });
            if (azure) context.AzureOpenAiTokenLimit(config);
            else context.LlmTokenLimit(config);
        }).AsTestDocument();
        test.Context.Request.Headers["Origin"] = [OverlayOrigin];
        test.Context.Services.Register<IHttpClient>(new StubHttpClient(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"usage":{"prompt_tokens":2,"completion_tokens":1}}""",
                Encoding.UTF8, "application/json")
        }));

        test.RunAll();

        WildcardExposure(test.Context).Should().BeEquivalentTo(
            "Content-Type", "Content-Length", "X-Token-Remaining", "X-Token-Consumed");
        test.Context.Response.Headers["X-Token-Consumed"].Should().Equal("3");
    }

    [TestMethod]
    [DataRow(false, false)]
    [DataRow(false, true)]
    [DataRow(true, false)]
    [DataRow(true, true)]
    public void CredentialedWildcard_ExplicitSetterCanReplaceSuppressionAfterAnEarlierForward(bool ignoreCase, bool seeded)
    {
        var test = new HeaderFlowDocument(context =>
            context.Cors(OverlayCors with { ExposeHeaders = ["*"] })).AsTestDocument();
        test.Context.Request.Headers["Origin"] = [OverlayOrigin];
        test.Context.Response.Headers = new Dictionary<string, string[]>(
            ignoreCase ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
        if (seeded) test.Context.Response.Headers["X-Stale"] = ["old"];
        test.Context.Services.Register<IHttpClient>(new StubHttpClient(_ => WildcardBackend()));
        test.RunInbound();
        new TestDocument(new ResponseHeaderMutationDocument("remove", "Access-Control-Expose-Headers"))
        { Context = test.Context }.RunOutbound();
        test.RunBackend();
        test.Context.Response.Headers.Should().NotContainKey("Access-Control-Expose-Headers");
        new TestDocument(new ResponseHeaderMutationDocument("set", "Access-Control-Expose-Headers"))
        { Context = test.Context }.RunOutbound();

        test.RunBackend();

        WildcardExposure(test.Context).Should().Equal("last-override");
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void CredentialedWildcard_AnExplicitEmptySetterValueDisablesExpansion(bool seeded)
    {
        var test = new HeaderFlowDocument(context =>
            context.Cors(OverlayCors with { ExposeHeaders = ["*"] })).AsTestDocument();
        test.Context.Request.Headers["Origin"] = [OverlayOrigin];
        if (seeded) test.Context.Response.Headers["X-Stale"] = ["old"];
        test.Context.Services.Register<IHttpClient>(new StubHttpClient(_ => WildcardBackend()));
        test.RunInbound();
        new TestDocument(new HeaderFlowDocument(outbound: context =>
            context.SetHeader("access-control-expose-headers", [])))
        { Context = test.Context }.RunOutbound();

        test.RunBackend();

        test.Context.Response.Headers.Single(header =>
            header.Key.Equals("Access-Control-Expose-Headers", StringComparison.OrdinalIgnoreCase))
            .Value.Should().BeEmpty();
    }

    [TestMethod]
    public void CredentialedWildcard_UsageProviderResponseOverrideWinsOverFinalExpansion()
    {
        var test = new HeaderFlowDocument(context =>
        {
            context.Cors(OverlayCors with { ExposeHeaders = ["*"] });
            context.LlmTokenLimit(new TokenLimitConfig
            {
                CounterKey = "wildcard-provider",
                EstimatePromptToken = false,
                TokensPerMinute = 100,
                TokensConsumedHeaderName = "X-Consumed"
            });
        }).AsTestDocument();
        test.Context.Request.Headers["Origin"] = [OverlayOrigin];
        test.Context.Services.Register<IHttpClient>(StubHttpClient.Ok("{}"));
        test.Context.Services.Register<ILlmTokenUsageProvider>(new OverlayUsageProvider(context =>
            new TestDocument(new HeaderFlowDocument(outbound: section =>
                section.SetHeader("Access-Control-Expose-Headers", ["provider-override"])))
            { Context = context }.RunOutbound()));

        test.RunAll();

        WildcardExposure(test.Context).Should().Equal("provider-override");
        test.Context.Response.Headers["X-Consumed"].Should().Equal("3");
    }
}