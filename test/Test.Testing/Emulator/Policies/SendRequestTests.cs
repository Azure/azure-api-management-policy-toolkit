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
public class SendRequestTests
{
    internal sealed class RecordingHttpClient(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler) : IHttpClient
    {
        public HttpRequestMessage? LastRequest { get; private set; }
        public CancellationToken LastCancellationToken { get; private set; }
        public int Calls { get; private set; }

        public Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken = default)
        {
            LastRequest = request;
            LastCancellationToken = cancellationToken;
            Calls++;
            return handler(request, cancellationToken);
        }
    }

    internal sealed class TrackingContent(string content) : StringContent(content, Encoding.UTF8)
    {
        public bool Disposed { get; private set; }

        protected override void Dispose(bool disposing)
        {
            Disposed = true;
            base.Dispose(disposing);
        }
    }

    internal sealed class ControlledResponseContent(string body) : HttpContent
    {
        private readonly byte[] _bytes = Encoding.UTF8.GetBytes(body);
        private readonly TaskCompletionSource<bool> _started = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<bool> _release = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task Started => _started.Task;
        public CancellationToken BodyCancellationToken { get; private set; }
        public bool Disposed { get; private set; }

        public void Release() => _release.TrySetResult(true);

        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) =>
            SerializeToStreamAsync(stream, context, CancellationToken.None);

        protected override async Task SerializeToStreamAsync(
            Stream stream, TransportContext? context, CancellationToken cancellationToken)
        {
            BodyCancellationToken = cancellationToken;
            _started.TrySetResult(true);
            await _release.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
            await stream.WriteAsync(_bytes, cancellationToken).ConfigureAwait(false);
        }

        protected override bool TryComputeLength(out long length)
        {
            length = _bytes.Length;
            return true;
        }

        protected override void Dispose(bool disposing)
        {
            Disposed = true;
            Release();
            base.Dispose(disposing);
        }
    }

    private sealed class SharedTransportDocument(
        string policy, BodyConfig? body, HeaderConfig[]? headers, string method) : IDocument
    {
        public void Inbound(IInboundContext context)
        {
            switch (policy)
            {
                case "send":
                    context.SendRequest(new SendRequestConfig
                    {
                        ResponseVariableName = "resp",
                        Url = "https://transport.example.test",
                        Mode = "copy",
                        Method = method,
                        Body = body,
                        Headers = headers
                    });
                    break;
                case "one-way":
                    context.SendOneWayRequest(new SendOneWayRequestConfig
                    {
                        Url = "https://transport.example.test",
                        Mode = "copy",
                        Method = method,
                        Body = body,
                        Headers = headers
                    });
                    break;
                case "invoke":
                    context.InvokeRequest(new InvokeRequestConfig
                    {
                        ResponseVariableName = "resp",
                        Url = "https://transport.example.test",
                        Method = method,
                        Body = body,
                        Headers = headers
                    });
                    break;
                case "forward":
                    break;
                default:
                    throw new ArgumentException($"Unknown HTTP policy '{policy}'.", nameof(policy));
            }
        }

        public void Backend(IBackendContext context)
        {
            if (policy == "forward") context.ForwardRequest();
        }
    }

    internal static TestDocument CreateTransportTest(
        string policy, BodyConfig? body = null, HeaderConfig[]? headers = null, string method = "POST")
    {
        var test = new SharedTransportDocument(policy, body, headers, method).AsTestDocument();
        test.Context.Request.Method = method;
        test.Context.Request.Body.Content = "\u00e9";
        return test;
    }

    internal static void RunTransport(TestDocument test, string policy)
    {
        if (policy == "forward") test.RunBackend();
        else test.RunInbound();
    }

    internal static void RunSection(TestDocument test, string section)
    {
        switch (section)
        {
            case "inbound": test.RunInbound(); break;
            case "backend": test.RunBackend(); break;
            case "outbound": test.RunOutbound(); break;
            case "on-error": test.RunOnError(); break;
            default: throw new ArgumentException($"Unknown section '{section}'.", nameof(section));
        }
    }

    private sealed class ConfiguredDocument(
        Func<IExpressionContext, SendRequestConfig> factory, string section = "inbound") : IDocument
    {
        public void Inbound(IInboundContext context)
        {
            if (section != "inbound") return;
            context.SendRequest(factory(context.ExpressionContext));
            context.SetVariable("continued", true);
        }

        public void Backend(IBackendContext context)
        {
            if (section != "backend") return;
            context.SendRequest(factory(context.ExpressionContext));
            context.SetVariable("continued", true);
        }

        public void Outbound(IOutboundContext context)
        {
            if (section != "outbound") return;
            context.SendRequest(factory(context.ExpressionContext));
            context.SetVariable("continued", true);
        }

        public void OnError(IOnErrorContext context)
        {
            if (section != "on-error") return;
            context.SendRequest(factory(context.ExpressionContext));
            context.SetVariable("continued", true);
        }
    }

    private static SendRequestConfig DefaultConfig => new()
    {
        ResponseVariableName = "resp",
        Url = "https://backend.example.test/resource"
    };

    private static TestDocument CreateTest(SendRequestConfig config, string section = "inbound") =>
        new ConfiguredDocument(_ => config, section).AsTestDocument();

    class SimpleSendRequest : IDocument
    {
        public void Inbound(IInboundContext context)
        {
            context.SendRequest(new SendRequestConfig
            {
                ResponseVariableName = "resp",
                Url = "https://example.com/api",
                Method = "GET",
            });
        }

        public void Backend(IBackendContext context) { }
        public void Outbound(IOutboundContext context) { }
        public void OnError(IOnErrorContext context) { }
    }

    class CopyModeSendRequest : IDocument
    {
        public void Inbound(IInboundContext context)
        {
            context.SendRequest(new SendRequestConfig
            {
                ResponseVariableName = "resp",
                Mode = "copy",
                Url = "https://example.com/api",
            });
        }

        public void Backend(IBackendContext context) { }
        public void Outbound(IOutboundContext context) { }
        public void OnError(IOnErrorContext context) { }
    }

    class NewModeSendRequest : IDocument
    {
        public void Inbound(IInboundContext context)
        {
            context.SendRequest(new SendRequestConfig
            {
                ResponseVariableName = "resp",
                Mode = "new",
                Url = "https://example.com/api",
            });
        }

        public void Backend(IBackendContext context) { }
        public void Outbound(IOutboundContext context) { }
        public void OnError(IOnErrorContext context) { }
    }

    [TestMethod]
    public void SendRequest_StoresResponseAsMockResponse()
    {
        // Arrange
        var test = new SimpleSendRequest().AsTestDocument();
        var stubClient = StubHttpClient.Ok("hello");
        test.Context.Services.Register<IHttpClient>(stubClient);

        // Act
        test.RunInbound();

        // Assert
        test.Context.Variables.Should().ContainKey("resp");
        var resp = test.Context.Variables["resp"];
        resp.Should().BeOfType<MockResponse>();
        var mockResponse = (MockResponse)resp;
        mockResponse.StatusCode.Should().Be(200);
        mockResponse.Body.Content.Should().Be("hello");
    }

    [TestMethod]
    public void SendRequest_CopyMode_ClonesCurrentRequest()
    {
        // Arrange
        var test = new CopyModeSendRequest().AsTestDocument();
        test.Context.Request.Headers["X-Custom"] = new[] { "val" };
        test.Context.Request.Body.Content = "";
        var stubClient = StubHttpClient.Ok();
        test.Context.Services.Register<IHttpClient>(stubClient);

        // Act
        test.RunInbound();

        // Assert
        stubClient.LastRequest.Should().NotBeNull();
        stubClient.LastRequest!.Headers.Contains("X-Custom").Should().BeTrue();
    }

    [TestMethod]
    public void SendRequest_NewMode_DoesNotCopyHeaders()
    {
        // Arrange
        var test = new NewModeSendRequest().AsTestDocument();
        test.Context.Request.Headers["X-Custom"] = new[] { "val" };
        var stubClient = StubHttpClient.Ok();
        test.Context.Services.Register<IHttpClient>(stubClient);

        // Act
        test.RunInbound();

        // Assert
        stubClient.LastRequest.Should().NotBeNull();
        stubClient.LastRequest!.Headers.Contains("X-Custom").Should().BeFalse();
    }

    [TestMethod]
    public void SendRequest_NoHttpClient_Throws()
    {
        // Arrange
        var test = new SimpleSendRequest().AsTestDocument();

        // Act & Assert
        var act = () => test.RunInbound();
        act.Should().Throw<PolicyException>()
            .WithInnerException<InvalidOperationException>();
    }

    [TestMethod]
    [DataRow("inbound")]
    [DataRow("backend")]
    [DataRow("outbound")]
    [DataRow("on-error")]
    public void ShouldExecuteTransportInEveryAuthoredSection(string section)
    {
        var test = CreateTest(DefaultConfig, section);
        test.Context.Services.Register<IHttpClient>(StubHttpClient.Ok("received"));
        test.Context.Response.Body.Content = "original";

        RunSection(test, section);

        test.Context.Variables["resp"].Should().BeOfType<MockResponse>()
            .Which.Body.Content.Should().Be("received");
        test.Context.Response.Body.Content.Should().Be("original");
        test.Context.Variables["continued"].Should().Be(true);
    }

    [TestMethod]
    [DataRow("inbound")]
    [DataRow("backend")]
    [DataRow("outbound")]
    [DataRow("on-error")]
    public void ShouldOverrideTransportWithMatchingCallbackInEverySection(string section)
    {
        var test = CreateTest(DefaultConfig, section);
        Action<GatewayContext, SendRequestConfig> callback = (context, _) =>
            context.Variables["resp"] = new MockResponse { StatusCode = 202 };
        switch (section)
        {
            case "inbound": test.SetupInbound().SendRequest((_, c) => c.ResponseVariableName == "resp").WithCallback(callback); break;
            case "backend": test.SetupBackend().SendRequest((_, c) => c.ResponseVariableName == "resp").WithCallback(callback); break;
            case "outbound": test.SetupOutbound().SendRequest((_, c) => c.ResponseVariableName == "resp").WithCallback(callback); break;
            case "on-error": test.SetupOnError().SendRequest((_, c) => c.ResponseVariableName == "resp").WithCallback(callback); break;
        }

        RunSection(test, section);

        test.Context.Variables["resp"].Should().BeOfType<MockResponse>().Which.StatusCode.Should().Be(202);
        test.Context.Variables["continued"].Should().Be(true);
    }

    [TestMethod]
    public void ShouldNotLetUnmatchedCallbackHideMissingClient()
    {
        var test = CreateTest(DefaultConfig);
        test.SetupInbound().SendRequest((_, c) => c.ResponseVariableName == "other")
            .WithCallback((_, _) => Assert.Fail("Unmatched callback ran."));

        var error = Assert.ThrowsExactly<PolicyException>(() => test.RunInbound());

        error.InnerException.Should().BeOfType<InvalidOperationException>();
        test.Context.Variables.Should().NotContainKey("continued");
    }

    [TestMethod]
    public void ShouldApplyHeaderActionsToRequestAndContentHeadersWithoutMutatingSource()
    {
        var test = CreateTest(DefaultConfig with
        {
            Mode = "copy",
            Headers =
            [
                new() { Name = "x-override", Values = ["replacement"] },
                new() { Name = "X-Append", ExistsAction = "append", Values = ["second"] },
                new() { Name = "X-Skip", ExistsAction = "skip", Values = ["ignored"] },
                new() { Name = "X-Delete", ExistsAction = "delete" },
                new() { Name = "Content-Type", Values = ["application/json"] },
                new() { Name = "Content-Language", ExistsAction = "append", Values = ["pl"] }
            ]
        });
        test.Context.Request.Method = "PATCH";
        test.Context.Request.Body.Content = "{\"text\":\"\u00e9\"}";
        test.Context.Request.Headers["X-Override"] = ["old"];
        test.Context.Request.Headers["X-Append"] = ["first"];
        test.Context.Request.Headers["X-Skip"] = ["kept"];
        test.Context.Request.Headers["X-Delete"] = ["removed"];
        test.Context.Request.Headers["Content-Type"] = ["text/plain"];
        test.Context.Request.Headers["Content-Language"] = ["en"];
        test.Context.Request.Headers["Content-Length"] = ["999"];
        var client = new StubHttpClient(request =>
        {
            request.Method.Method.Should().Be("PATCH");
            request.Headers.GetValues("X-Override").Should().Equal("replacement");
            request.Headers.GetValues("X-Append").Should().Equal("first", "second");
            request.Headers.GetValues("X-Skip").Should().Equal("kept");
            request.Headers.Contains("X-Delete").Should().BeFalse();
            request.Content.Should().NotBeNull();
            request.Content!.Headers.ContentType!.MediaType.Should().Be("application/json");
            request.Content.Headers.ContentLanguage.Should().Equal("en", "pl");
            request.Content.Headers.ContentLength.Should().Be(Encoding.UTF8.GetByteCount(test.Context.Request.Body.Content));
            request.Content.ReadAsStringAsync().GetAwaiter().GetResult().Should().Be(test.Context.Request.Body.Content);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        test.Context.Services.Register<IHttpClient>(client);

        test.RunInbound();

        test.Context.Request.Headers["X-Override"].Should().Equal("old");
        test.Context.Request.Headers["Content-Type"].Should().Equal("text/plain");
        test.Context.Request.Headers["Content-Length"].Should().Equal("999");
        test.Context.Request.Body.Consumed.Should().BeFalse();
    }

    [TestMethod]
    [DataRow("inbound", "original")]
    [DataRow("backend", "original")]
    [DataRow("outbound", "")]
    [DataRow("on-error", "original")]
    public void ShouldCopyBodyExceptInOutboundSection(string section, string expectedBody)
    {
        var test = CreateTest(DefaultConfig with { Mode = "copy" }, section);
        test.Context.Request.Body.Content = "original";
        test.Context.Request.Headers["Content-Type"] = ["application/json"];
        test.Context.Services.Register<IHttpClient>(new StubHttpClient(request =>
        {
            var body = request.Content?.ReadAsStringAsync().GetAwaiter().GetResult() ?? "";
            body.Should().Be(expectedBody);
            request.Content?.Headers.ContentLength.Should().Be(Encoding.UTF8.GetByteCount(expectedBody));
            return new HttpResponseMessage(HttpStatusCode.OK);
        }));

        RunSection(test, section);

        test.Context.Request.Body.Consumed.Should().BeFalse();
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    public void ShouldHonorExplicitEmptyBodyInsteadOfCopyingOriginal(string? body)
    {
        var test = CreateTest(DefaultConfig with
        {
            Mode = "copy",
            Body = new BodyConfig { Content = body }
        });
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
    public void ShouldSendByteArrayBodyWithoutStringifyingItsType()
    {
        byte[] body = [0, 1, 2, 255];
        var test = CreateTest(DefaultConfig with { Body = new BodyConfig { Content = body } });
        test.Context.Services.Register<IHttpClient>(new StubHttpClient(request =>
        {
            request.Content!.ReadAsByteArrayAsync().GetAwaiter().GetResult().Should().Equal(body);
            request.Content.Headers.ContentLength.Should().Be(body.Length);
            return new HttpResponseMessage(HttpStatusCode.OK);
        }));

        test.RunInbound();
    }

    [TestMethod]
    public void ShouldEvaluateRequestAuthenticationAndProxyExpressions()
    {
        var test = new ConfiguredDocument(context => new SendRequestConfig
        {
            ResponseVariableName = "resp",
            Mode = (string)context.Variables["mode"],
            Timeout = (int)context.Variables["timeout"],
            Url = (string)context.Variables["url"],
            Method = context.Request.Method,
            Headers =
            [
                new()
                {
                    Name = (string)context.Variables["header"],
                    ExistsAction = (string)context.Variables["action"],
                    Values = [(string)context.Variables["value"]]
                }
            ],
            Body = new BodyConfig
            {
                Content = (context.Request.Body ?? throw new InvalidOperationException("The test requires a request body."))
                    .As<string>(preserveContent: true) + "\u00e9"
            },
            Authentication = new BasicAuthenticationConfig
            {
                Username = (string)context.Variables["user"],
                Password = (string)context.Variables["password"]
            },
            Proxy = new ProxyConfig
            {
                Url = (string)context.Variables["proxy"],
                Username = (string)context.Variables["proxy-user"],
                Password = (string)context.Variables["proxy-password"]
            }
        }).AsTestDocument();
        test.Context.Variables["mode"] = "new";
        test.Context.Variables["timeout"] = 10;
        test.Context.Variables["url"] = "https://expression.example.test/path";
        test.Context.Variables["header"] = "X-Expression";
        test.Context.Variables["action"] = "override";
        test.Context.Variables["value"] = "evaluated";
        test.Context.Variables["user"] = "u\u00e9";
        test.Context.Variables["password"] = "p\u00e9";
        test.Context.Variables["proxy"] = "http://proxy.example.test:8080";
        test.Context.Variables["proxy-user"] = "proxy-user";
        test.Context.Variables["proxy-password"] = "proxy-password";
        test.Context.Request.Method = "POST";
        test.Context.Request.Body.Content = "body-";
        test.Context.Services.Register<IHttpClient>(new StubHttpClient(request =>
        {
            request.RequestUri!.AbsoluteUri.Should().Be("https://expression.example.test/path");
            request.Method.Method.Should().Be("POST");
            request.Headers.GetValues("X-Expression").Should().Equal("evaluated");
            request.Headers.Authorization!.Scheme.Should().Be("Basic");
            request.Headers.Authorization.Parameter.Should().Be(Convert.ToBase64String(Encoding.UTF8.GetBytes("u\u00e9:p\u00e9")));
            request.Headers.Contains("Proxy-Authorization").Should().BeFalse();
            request.Content!.ReadAsStringAsync().GetAwaiter().GetResult().Should().Be("body-\u00e9");
            request.Content.Headers.ContentLength.Should().Be(Encoding.UTF8.GetByteCount("body-\u00e9"));
            request.Options.TryGetValue(HttpTransportOptions.Key, out var options).Should().BeTrue();
            options!.Timeout.Should().Be(TimeSpan.FromSeconds(10));
            options.Proxy!.Url.Should().Be("http://proxy.example.test:8080");
            options.Proxy.Username.Should().Be("proxy-user");
            options.Proxy.Password.Should().Be("proxy-password");
            return new HttpResponseMessage(HttpStatusCode.OK);
        }));

        test.RunInbound();

        test.Context.Request.Headers.Should().NotContainKey("Authorization");
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void ShouldUseInjectedManagedIdentityProviderWithoutMutatingOriginalRequest(bool saveToken)
    {
        var test = new ConfiguredDocument(context => DefaultConfig with
        {
            Authentication = new ManagedIdentityAuthenticationConfig
            {
                Resource = (string)context.Variables["resource"],
                ClientId = (string)context.Variables["identity"],
                OutputTokenVariableName = saveToken ? "token" : null
            }
        }).AsTestDocument();
        test.Context.Variables["resource"] = "https://resource.example.test";
        test.Context.Variables["identity"] = "identity";
        test.Context.ManagedIdentityTokenProvider = (resource, clientId) =>
        {
            resource.Should().Be("https://resource.example.test");
            clientId.Should().Be("identity");
            return "injected-token";
        };
        test.Context.Services.Register<IHttpClient>(new StubHttpClient(request =>
        {
            if (saveToken)
                request.Headers.Authorization.Should().BeNull();
            else
                request.Headers.Authorization!.ToString().Should().Be("Bearer injected-token");
            return new HttpResponseMessage(HttpStatusCode.OK);
        }));

        test.RunInbound();

        if (saveToken) test.Context.Variables["token"].Should().Be("injected-token");
        test.Context.Request.Headers.Should().NotContainKey("Authorization");
    }

    [TestMethod]
    public void ShouldFailExplicitlyWhenManagedIdentityProviderIsUnconfigured()
    {
        var test = CreateTest(DefaultConfig with
        {
            IgnoreError = true,
            Authentication = new ManagedIdentityAuthenticationConfig { Resource = "https://resource.example.test" }
        });
        var client = new RecordingHttpClient((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));
        test.Context.Services.Register<IHttpClient>(client);

        var error = Assert.ThrowsExactly<PolicyException>(() => test.RunInbound());

        error.InnerException.Should().BeOfType<InvalidOperationException>();
        client.Calls.Should().Be(0);
    }

    [TestMethod]
    [DataRow("id")]
    [DataRow("thumbprint")]
    [DataRow("body")]
    public void ShouldExposeSelectedClientCertificateToInjectedTransport(string source)
    {
        using var key = RSA.Create(2048);
        var certificateRequest = new CertificateRequest("CN=http-emulator-test", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var certificate = certificateRequest.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddHours(1));
        var authentication = source switch
        {
            "id" => new CertificateAuthenticationConfig { CertificateId = "certificate" },
            "thumbprint" => new CertificateAuthenticationConfig { Thumbprint = certificate.Thumbprint },
            _ => new CertificateAuthenticationConfig { Body = certificate.Export(X509ContentType.Pfx, "password"), Password = "password" }
        };
        var test = CreateTest(DefaultConfig with { Authentication = authentication });
        test.SetupCertificateStore().WithCertificateById("certificate", certificate)
            .WithCertificateByThumbprint(certificate.Thumbprint, certificate);
        test.Context.Services.Register<IHttpClient>(new StubHttpClient(request =>
        {
            request.Options.TryGetValue(HttpTransportOptions.Key, out var options).Should().BeTrue();
            options!.Certificate!.Thumbprint.Should().Be(certificate.Thumbprint);
            return new HttpResponseMessage(HttpStatusCode.OK);
        }));

        test.RunInbound();

        test.Context.Request.Certificate.Should().BeNull();
        certificate.Thumbprint.Should().NotBeNullOrEmpty();
    }

    private sealed record UnsupportedAuthentication : IAuthenticationConfig;

    [TestMethod]
    public void ShouldRejectUnsupportedAuthenticationRatherThanIgnoringIt()
    {
        var test = CreateTest(DefaultConfig with { Authentication = new UnsupportedAuthentication() });
        test.Context.Services.Register<IHttpClient>(StubHttpClient.Ok());

        Assert.ThrowsExactly<PolicyException>(() => test.RunInbound())
            .InnerException.Should().BeOfType<NotSupportedException>();
    }

    [TestMethod]
    [DataRow("missing")]
    [DataRow("multiple")]
    [DataRow("invalid-body")]
    public void ShouldRejectInvalidClientCertificateConfiguration(string kind)
    {
        var authentication = kind switch
        {
            "missing" => new CertificateAuthenticationConfig { CertificateId = "missing" },
            "multiple" => new CertificateAuthenticationConfig { CertificateId = "id", Thumbprint = "thumbprint" },
            _ => new CertificateAuthenticationConfig { Body = [1, 2, 3], Password = "password" }
        };
        var test = CreateTest(DefaultConfig with { Authentication = authentication });
        var client = new RecordingHttpClient((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));
        test.Context.Services.Register<IHttpClient>(client);

        var error = Assert.ThrowsExactly<PolicyException>(() => test.RunInbound());

        if (kind == "invalid-body")
            error.InnerException.Should().BeOfType<CryptographicException>();
        else
            error.InnerException.Should().BeOfType<InvalidOperationException>();
        client.Calls.Should().Be(0);
    }

    [TestMethod]
    [DataRow("variable-null")]
    [DataRow("variable-empty")]
    [DataRow("variable-whitespace")]
    [DataRow("mode")]
    [DataRow("url-empty")]
    [DataRow("url-relative")]
    [DataRow("url-scheme")]
    [DataRow("url-fragment")]
    [DataRow("method")]
    [DataRow("timeout")]
    [DataRow("header-name")]
    [DataRow("header-action")]
    [DataRow("header-values")]
    [DataRow("header-empty-values")]
    [DataRow("header-newline")]
    [DataRow("template")]
    public void ShouldRejectInvalidConfigurationEvenWhenErrorsAreIgnored(string kind)
    {
        var config = kind switch
        {
            "variable-null" => DefaultConfig with { ResponseVariableName = null! },
            "variable-empty" => DefaultConfig with { ResponseVariableName = "" },
            "variable-whitespace" => DefaultConfig with { ResponseVariableName = " " },
            "mode" => DefaultConfig with { Mode = "unknown" },
            "url-empty" => DefaultConfig with { Url = "" },
            "url-relative" => DefaultConfig with { Url = "/relative" },
            "url-scheme" => DefaultConfig with { Url = "ftp://example.test/path" },
            "url-fragment" => DefaultConfig with { Url = "https://example.test/path#fragment" },
            "method" => DefaultConfig with { Method = "BAD METHOD" },
            "timeout" => DefaultConfig with { Timeout = -1 },
            "header-name" => DefaultConfig with { Headers = [new() { Name = "", Values = ["value"] }] },
            "header-action" => DefaultConfig with { Headers = [new() { Name = "X-Test", ExistsAction = "unknown", Values = ["value"] }] },
            "header-values" => DefaultConfig with { Headers = [new() { Name = "X-Test" }] },
            "header-empty-values" => DefaultConfig with { Headers = [new() { Name = "X-Test", Values = [] }] },
            "header-newline" => DefaultConfig with { Headers = [new() { Name = "X-Test", Values = ["value\r\nInjected: yes"] }] },
            _ => DefaultConfig with { Body = new BodyConfig { Content = "body", Template = "liquid" } }
        };
        var test = CreateTest(config with { IgnoreError = true });
        var client = new RecordingHttpClient((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));
        test.Context.Services.Register<IHttpClient>(client);

        var error = Assert.ThrowsExactly<PolicyException>(() => test.RunInbound());

        if (kind == "template")
            error.InnerException.Should().BeOfType<NotSupportedException>();
        else
            error.InnerException.Should().BeAssignableTo<ArgumentException>();
        client.Calls.Should().Be(0);
        test.Context.Variables.Should().NotContainKey("resp");
    }

    [TestMethod]
    public void ShouldFailForMissingClientEvenWhenIgnoreErrorIsTrue()
    {
        var test = CreateTest(DefaultConfig with { IgnoreError = true });

        Assert.ThrowsExactly<PolicyException>(() => test.RunInbound())
            .InnerException.Should().BeOfType<InvalidOperationException>();
        test.Context.Variables.Should().NotContainKey("resp");
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void ShouldPropagateOrExplicitlyRecordTransportFailure(bool ignoreError)
    {
        var test = CreateTest(DefaultConfig with { IgnoreError = ignoreError });
        var failure = new HttpRequestException("injected transport failure");
        test.Context.Services.Register<IHttpClient>(new RecordingHttpClient((_, _) => Task.FromException<HttpResponseMessage>(failure)));
        var traces = new List<string>();
        test.Context.Trace = traces.Add;

        if (ignoreError)
        {
            test.RunInbound();
            test.Context.Variables.Should().ContainKey("resp").WhoseValue.Should().BeNull();
            test.Context.Variables["continued"].Should().Be(true);
            traces.Should().Contain(message => message.Contains("SendRequest") && message.Contains("injected transport failure"));
        }
        else
        {
            var error = Assert.ThrowsExactly<PolicyException>(() => test.RunInbound());
            error.InnerException.Should().BeSameAs(failure);
            error.Section.Should().Be(nameof(IInboundContext));
            test.Context.Variables.Should().NotContainKey("continued");
        }
    }

    [TestMethod]
    public void ShouldNotSuppressNonTransportClientErrors()
    {
        var test = CreateTest(DefaultConfig with { IgnoreError = true });
        var failure = new InvalidOperationException("bad injected client");
        test.Context.Services.Register<IHttpClient>(new RecordingHttpClient((_, _) => throw failure));

        Assert.ThrowsExactly<PolicyException>(() => test.RunInbound()).InnerException.Should().BeSameAs(failure);
    }

    [TestMethod]
    public void ShouldStoreHttpErrorResponseWithoutTreatingStatusAsTransportFailure()
    {
        var test = CreateTest(DefaultConfig);
        test.Context.Services.Register<IHttpClient>(new StubHttpClient(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError)));

        test.RunInbound();

        test.Context.Variables["resp"].Should().BeOfType<MockResponse>().Which.StatusCode.Should().Be(500);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void ShouldHonorZeroTimeoutAndCallerCancellation(bool callerCancellation)
    {
        using var cancellation = new CancellationTokenSource();
        if (callerCancellation) cancellation.Cancel();
        var test = CreateTest(DefaultConfig with { Timeout = callerCancellation ? 60 : 0 });
        test.Context.Services.Register(new HttpTransportState { CancellationToken = cancellation.Token });
        var client = new RecordingHttpClient((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));
        test.Context.Services.Register<IHttpClient>(client);

        Assert.ThrowsExactly<PolicyException>(() => test.RunInbound())
            .InnerException.Should().BeAssignableTo<OperationCanceledException>();
        client.Calls.Should().Be(0);
    }

    [TestMethod]
    public async Task ShouldCancelAnInFlightRequestEvenWhenClientIgnoresCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        var pending = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        var entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var test = CreateTest(DefaultConfig);
        test.Context.Services.Register(new HttpTransportState { CancellationToken = cancellation.Token });
        var client = new RecordingHttpClient((_, _) =>
        {
            entered.SetResult(true);
            return pending.Task;
        });
        test.Context.Services.Register<IHttpClient>(client);
        var execution = Task.Run(() =>
        {
            try { test.RunInbound(); return null; }
            catch (PolicyException error) { return error; }
        });

        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(2));
            cancellation.Cancel();
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

    [TestMethod]
    public void ShouldReplaceResponseVariableAndDisposeTransportMessages()
    {
        var test = CreateTest(DefaultConfig with { Body = new BodyConfig { Content = "\u00e9" } });
        var oldResponse = new MockResponse { StatusCode = 418 };
        test.Context.Variables["resp"] = oldResponse;
        var content = new TrackingContent("r\u00e9ponse");
        var client = new StubHttpClient(_ => new HttpResponseMessage(HttpStatusCode.Created)
        {
            Content = content,
            ReasonPhrase = "Created by stub"
        });
        test.Context.Services.Register<IHttpClient>(client);

        test.RunInbound();

        var response = test.Context.Variables["resp"].Should().BeOfType<MockResponse>().Subject;
        response.Should().NotBeSameAs(oldResponse);
        response.StatusReason.Should().Be("Created by stub");
        response.Body.Content.Should().Be("r\u00e9ponse");
        response.Headers["Content-Length"].Should().Equal(Encoding.UTF8.GetByteCount("r\u00e9ponse").ToString());
        content.Disposed.Should().BeTrue();
        var readDisposedRequest = () => client.LastRequest!.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
        readDisposedRequest.Should().Throw<ObjectDisposedException>();
    }

    private sealed class FragmentDocument : IDocument
    {
        public void Inbound(IInboundContext context) => context.IncludeFragment("send-http");
    }

    private sealed class RequestFragment : IFragment
    {
        public void Fragment(IFragmentContext context) => context.SendRequest(DefaultConfig);
    }

    [TestMethod]
    public void ShouldExecuteInsideAuthoredFragment()
    {
        var test = new FragmentDocument().AsTestDocument().RegisterFragment("send-http", new RequestFragment());
        test.Context.Services.Register<IHttpClient>(StubHttpClient.Ok("fragment"));

        test.RunInbound();

        test.Context.Variables["resp"].Should().BeOfType<MockResponse>().Which.Body.Content.Should().Be("fragment");
    }

    [TestMethod]
    public void ShouldWrapCallbackErrorsWithoutCallingTransport()
    {
        var test = CreateTest(DefaultConfig);
        var failure = new HttpRequestException("callback failure");
        test.SetupInbound().SendRequest().WithCallback((_, _) => throw failure);

        Assert.ThrowsExactly<PolicyException>(() => test.RunInbound()).InnerException.Should().BeSameAs(failure);
    }

    [TestMethod]
    [DataRow("username-null")]
    [DataRow("username-colon")]
    [DataRow("password-null")]
    public void ShouldRejectInvalidBasicAuthenticationBeforeDispatch(string kind)
    {
        var test = CreateTest(DefaultConfig with
        {
            IgnoreError = true,
            Authentication = new BasicAuthenticationConfig
            {
                Username = kind == "username-null" ? null! : kind == "username-colon" ? "bad:user" : "user",
                Password = kind == "password-null" ? null! : "password"
            }
        });
        var client = new RecordingHttpClient((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));
        test.Context.Services.Register<IHttpClient>(client);

        Assert.ThrowsExactly<PolicyException>(() => test.RunInbound())
            .InnerException.Should().BeAssignableTo<ArgumentException>();
        client.Calls.Should().Be(0);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void ShouldPropagateOrTraceExplicitlyIgnoredManagedIdentityFailure(bool ignore)
    {
        var test = CreateTest(DefaultConfig with
        {
            Authentication = new ManagedIdentityAuthenticationConfig
            {
                Resource = "https://resource.example.test",
                IgnoreError = ignore
            }
        });
        var failure = new HttpRequestException("token provider unavailable");
        test.Context.ManagedIdentityTokenProvider = (_, _) => throw failure;
        var traces = new List<string>();
        test.Context.Trace = traces.Add;
        var client = new StubHttpClient(request =>
        {
            request.Headers.Authorization.Should().BeNull();
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        test.Context.Services.Register<IHttpClient>(client);

        if (ignore)
        {
            test.RunInbound();
            traces.Should().Contain(message => message.Contains("managed identity", StringComparison.OrdinalIgnoreCase));
            test.Context.Variables["continued"].Should().Be(true);
        }
        else
        {
            Assert.ThrowsExactly<PolicyException>(() => test.RunInbound()).InnerException.Should().BeSameAs(failure);
            client.LastRequest.Should().BeNull();
        }
    }

    [TestMethod]
    public void ShouldRejectEmptyManagedIdentityToken()
    {
        var test = CreateTest(DefaultConfig with
        {
            Authentication = new ManagedIdentityAuthenticationConfig { Resource = "https://resource.example.test" }
        });
        test.Context.ManagedIdentityTokenProvider = (_, _) => "";
        test.Context.Services.Register<IHttpClient>(StubHttpClient.Ok());

        Assert.ThrowsExactly<PolicyException>(() => test.RunInbound())
            .InnerException.Should().BeOfType<InvalidOperationException>();
    }

    [TestMethod]
    public async Task ShouldEnforceTimeoutInSecondsForUncooperativeClient()
    {
        var test = CreateTest(DefaultConfig with { Timeout = 1 });
        var pending = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        var client = new RecordingHttpClient((_, _) => pending.Task);
        test.Context.Services.Register<IHttpClient>(client);
        var execution = Task.Run(() =>
        {
            try { test.RunInbound(); return null; }
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

    [TestMethod]
    public async Task ShouldRespectCancellationInStubHttpClient()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://stub.example.test");
        var client = StubHttpClient.Ok();
        var send = () => client.SendAsync(request, cancellation.Token);

        await send.Should().ThrowAsync<OperationCanceledException>();
        client.LastRequest.Should().BeNull();
    }

    [TestMethod]
    [DataRow("delete", null)]
    [DataRow("override-delete", null)]
    [DataRow("delete-override", "application/json")]
    [DataRow("delete-skip", "application/json")]
    public void ShouldApplyContentHeaderActionsAfterSynthesizedDefaults(string sequence, string? expectedContentType)
    {
        HeaderConfig[] headers = sequence switch
        {
            "delete" => [new() { Name = "Content-Type", ExistsAction = "delete" }],
            "override-delete" =>
            [
                new() { Name = "Content-Type", Values = ["application/json"] },
                new() { Name = "content-type", ExistsAction = "delete" }
            ],
            "delete-override" =>
            [
                new() { Name = "Content-Type", ExistsAction = "delete" },
                new() { Name = "Content-Type", Values = ["application/json"] }
            ],
            _ =>
            [
                new() { Name = "Content-Type", ExistsAction = "delete" },
                new() { Name = "Content-Type", ExistsAction = "skip", Values = ["application/json"] }
            ]
        };
        var test = CreateTest(DefaultConfig with
        {
            Body = new BodyConfig { Content = "body" },
            Headers = headers
        });
        test.Context.Services.Register<IHttpClient>(new StubHttpClient(request =>
        {
            request.Content!.Headers.ContentType?.MediaType.Should().Be(expectedContentType);
            return new HttpResponseMessage(HttpStatusCode.OK);
        }));

        test.RunInbound();
    }

    [TestMethod]
    public void ShouldNormalizeDecodedResponseLengthToMockBodyUtf8Representation()
    {
        var test = CreateTest(DefaultConfig);
        test.Context.Services.Register<IHttpClient>(new StubHttpClient(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("\u6771\u4eac", Encoding.Unicode, "text/plain")
        }));

        test.RunInbound();

        var response = test.Context.Variables["resp"].Should().BeOfType<MockResponse>().Subject;
        response.Body.Content.Should().Be("\u6771\u4eac");
        response.Headers["Content-Length"].Should().Equal(
            response.Body.As<byte[]>(preserveContent: true).Length.ToString());
    }

    [TestMethod]
    [DataRow("forward", false, "iso-8859-1")]
    [DataRow("send", false, "iso-8859-1")]
    [DataRow("one-way", false, "iso-8859-1")]
    [DataRow("invoke", false, "iso-8859-1")]
    [DataRow("send", true, "iso-8859-1")]
    [DataRow("one-way", true, "iso-8859-1")]
    [DataRow("invoke", true, "iso-8859-1")]
    [DataRow("send", true, "\"iso-8859-1\"")]
    [DataRow("invoke", true, "utf-16")]
    public void ShouldEncodeStringBodyUsingEffectiveContentCharset(string policy, bool configured, string charset)
    {
        var headers = configured
            ? new HeaderConfig[] { new() { Name = "Content-Type", Values = [$"text/plain; charset={charset}"] } }
            : null;
        var test = CreateTransportTest(policy, configured ? new BodyConfig { Content = "\u00e9" } : null, headers);
        test.Context.Request.Headers["Content-Type"] = [$"text/plain; charset={(configured ? "utf-8" : charset)}"];
        var originalContentType = test.Context.Request.Headers["Content-Type"].ToArray();
        test.Context.Services.Register<IHttpClient>(new StubHttpClient(request =>
        {
            var content = request.Content ?? throw new InvalidOperationException("Expected request content.");
            var effectiveCharset = content.Headers.ContentType?.CharSet
                ?? throw new InvalidOperationException("Expected an effective charset.");
            var encoding = Encoding.GetEncoding(effectiveCharset.Trim('"'));
            var bytes = content.ReadAsByteArrayAsync().GetAwaiter().GetResult();
            bytes.Should().Equal(Encoding.GetEncoding(charset.Trim('"')).GetBytes("\u00e9"));
            encoding.GetString(bytes).Should().Be("\u00e9");
            content.ReadAsStringAsync().GetAwaiter().GetResult().Should().Be("\u00e9");
            content.Headers.ContentLength.Should().Be(bytes.Length);
            return new HttpResponseMessage(HttpStatusCode.OK);
        }));

        RunTransport(test, policy);

        test.Context.Request.Headers["Content-Type"].Should().Equal(originalContentType);
        test.Context.Request.Body.Consumed.Should().BeFalse();
    }

    [TestMethod]
    [DataRow("send")]
    [DataRow("one-way")]
    [DataRow("invoke")]
    public void ShouldPreserveExplicitByteArrayDespiteDeclaredCharset(string policy)
    {
        byte[] bytes = [0xC3, 0xA9, 0, 255];
        var test = CreateTransportTest(policy, new BodyConfig { Content = bytes },
            [new() { Name = "Content-Type", Values = ["text/plain; charset=iso-8859-1"] }]);
        test.Context.Services.Register<IHttpClient>(new StubHttpClient(request =>
        {
            request.Content!.ReadAsByteArrayAsync().GetAwaiter().GetResult().Should().Equal(bytes);
            request.Content.Headers.ContentLength.Should().Be(bytes.Length);
            request.Content.Headers.ContentType!.CharSet.Should().Be("iso-8859-1");
            return new HttpResponseMessage(HttpStatusCode.OK);
        }));

        RunTransport(test, policy);

        bytes.Should().Equal(0xC3, 0xA9, 0, 255);
    }

    [TestMethod]
    [DataRow("forward")]
    [DataRow("send")]
    [DataRow("one-way")]
    [DataRow("invoke")]
    public void ShouldRejectUnsupportedStringBodyCharsetBeforeDispatch(string policy)
    {
        const string charset = "x-apim-unsupported-charset";
        var test = CreateTransportTest(policy);
        test.Context.Request.Headers["Content-Type"] = [$"text/plain; charset={charset}"];
        var client = new RecordingHttpClient((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));
        test.Context.Services.Register<IHttpClient>(client);

        var error = Assert.ThrowsExactly<PolicyException>(() => RunTransport(test, policy));

        error.InnerException.Should().BeOfType<NotSupportedException>()
            .Which.Message.Should().Contain(charset);
        client.Calls.Should().Be(0);
        test.Context.Request.Body.Content.Should().Be("\u00e9");
    }

    [TestMethod]
    [DataRow("send")]
    [DataRow("one-way")]
    [DataRow("invoke")]
    public void ShouldUseUtf8AfterDeletingCopiedCharsetHeader(string policy)
    {
        var test = CreateTransportTest(policy, headers: [new() { Name = "Content-Type", ExistsAction = "delete" }]);
        test.Context.Request.Headers["Content-Type"] = ["text/plain; charset=iso-8859-1"];
        test.Context.Services.Register<IHttpClient>(new StubHttpClient(request =>
        {
            request.Content!.Headers.ContentType.Should().BeNull();
            request.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult().Should().Equal(0xC3, 0xA9);
            request.Content.Headers.ContentLength.Should().Be(2);
            return new HttpResponseMessage(HttpStatusCode.OK);
        }));

        RunTransport(test, policy);
    }

    [TestMethod]
    [DataRow("forward", "none")]
    [DataRow("send", "none")]
    [DataRow("send", "append")]
    [DataRow("send", "skip")]
    [DataRow("send", "override")]
    [DataRow("send", "delete")]
    [DataRow("one-way", "none")]
    [DataRow("one-way", "append")]
    [DataRow("one-way", "skip")]
    [DataRow("one-way", "override")]
    [DataRow("one-way", "delete")]
    [DataRow("invoke", "none")]
    [DataRow("invoke", "append")]
    [DataRow("invoke", "skip")]
    [DataRow("invoke", "override")]
    [DataRow("invoke", "delete")]
    public void ShouldAggregateCopiedCaseVariantsBeforeHeaderActions(string policy, string action)
    {
        var headers = action == "none" ? null : new HeaderConfig[]
        {
            new() { Name = "x-tAg", ExistsAction = action, Values = action == "delete" ? null : ["action"] }
        };
        var test = CreateTransportTest(policy, headers: headers);
        var source = new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["X-Tag"] = ["first", "first-again"],
            ["x-tag"] = ["second"],
            ["X-TAG"] = ["third"],
            ["Content-Language"] = ["en"],
            ["content-language"] = ["pl"]
        };
        test.Context.Request.Headers = source;
        test.Context.Services.Register<IHttpClient>(new StubHttpClient(request =>
        {
            if (action == "delete")
            {
                request.Headers.Contains("X-Tag").Should().BeFalse();
            }
            else
            {
                var expected = action switch
                {
                    "override" => new[] { "action" },
                    "append" => ["first", "first-again", "second", "third", "action"],
                    _ => ["first", "first-again", "second", "third"]
                };
                request.Headers.GetValues("X-Tag").Should().Equal(expected);
            }

            request.Content!.Headers.ContentLanguage.Should().Equal("en", "pl");
            return new HttpResponseMessage(HttpStatusCode.OK);
        }));

        RunTransport(test, policy);

        test.Context.Request.Headers.Should().BeSameAs(source);
        source.Keys.Should().Equal("X-Tag", "x-tag", "X-TAG", "Content-Language", "content-language");
        source["X-Tag"].Should().Equal("first", "first-again");
        source["x-tag"].Should().Equal("second");
        source["X-TAG"].Should().Equal("third");
    }

    [TestMethod]
    [DataRow("forward", "HEAD", 200, 321L)]
    [DataRow("send", "HEAD", 200, 321L)]
    [DataRow("invoke", "HEAD", 200, 321L)]
    [DataRow("forward", "GET", 304, 321L)]
    [DataRow("send", "GET", 304, 321L)]
    [DataRow("invoke", "GET", 304, 321L)]
    [DataRow("forward", "GET", 205, 0L)]
    [DataRow("send", "GET", 205, 0L)]
    [DataRow("invoke", "GET", 205, 0L)]
    public void ShouldPreserveValidBodylessRepresentationLengths(string policy, string method, int status, long length)
    {
        var test = CreateTransportTest(policy, method: method);
        test.Context.Services.Register<IHttpClient>(new StubHttpClient(_ =>
        {
            var response = new HttpResponseMessage((HttpStatusCode)status) { Content = new ByteArrayContent([]) };
            response.Content.Headers.ContentLength = length;
            response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/plain");
            return response;
        }));

        RunTransport(test, policy);

        var result = policy == "forward" ? test.Context.Response
            : test.Context.Variables["resp"].Should().BeOfType<MockResponse>().Subject;
        result.StatusCode.Should().Be(status);
        result.Body.Content.Should().BeNullOrEmpty();
        result.Headers["Content-Length"].Should().Equal(length.ToString());
        result.Headers["Content-Type"].Should().Equal("text/plain");
    }

    private sealed class BodylessResponseContent : HttpContent
    {
        public bool ReadAttempted { get; private set; }

        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context)
        {
            ReadAttempted = true;
            throw new InvalidOperationException("A bodyless HTTP response must not be read.");
        }

        protected override bool TryComputeLength(out long length)
        {
            length = 0;
            return false;
        }
    }

    [TestMethod]
    [DataRow("forward", "HEAD", 200)]
    [DataRow("send", "HEAD", 200)]
    [DataRow("invoke", "HEAD", 200)]
    [DataRow("forward", "GET", 304)]
    [DataRow("send", "GET", 304)]
    [DataRow("invoke", "GET", 304)]
    [DataRow("forward", "GET", 204)]
    [DataRow("send", "GET", 204)]
    [DataRow("invoke", "GET", 204)]
    [DataRow("send", "GET", 103)]
    [DataRow("send", "GET", 205)]
    [DataRow("send", "CONNECT", 200)]
    public void ShouldNotReadBodylessResponsesOrSynthesizeLength(string policy, string method, int status)
    {
        var test = CreateTransportTest(policy, method: method);
        var content = new BodylessResponseContent();
        test.Context.Services.Register<IHttpClient>(new StubHttpClient(_ => new HttpResponseMessage((HttpStatusCode)status)
        {
            Content = content
        }));

        RunTransport(test, policy);

        var result = policy == "forward" ? test.Context.Response
            : test.Context.Variables["resp"].Should().BeOfType<MockResponse>().Subject;
        content.ReadAttempted.Should().BeFalse();
        result.Body.Content.Should().BeNullOrEmpty();
        result.Headers.Should().NotContainKey("Content-Length");
    }

    [TestMethod]
    public async Task ShouldApplySendRequestTotalTimeoutToDelayedResponseBody()
    {
        var test = CreateTest(DefaultConfig with { Timeout = 1 });
        var content = new ControlledResponseContent("late-body");
        var client = new RecordingHttpClient((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = content
        }));
        test.Context.Services.Register<IHttpClient>(client);
        var execution = Task.Run(() =>
        {
            try { test.RunInbound(); return null; }
            catch (PolicyException error) { return error; }
        });

        try
        {
            await content.Started.WaitAsync(TimeSpan.FromSeconds(3));
            var error = await execution.WaitAsync(TimeSpan.FromSeconds(3));
            error.Should().NotBeNull();
            error!.InnerException.Should().BeAssignableTo<OperationCanceledException>();
            client.LastCancellationToken.IsCancellationRequested.Should().BeTrue();
            content.BodyCancellationToken.IsCancellationRequested.Should().BeTrue();
            test.Context.Variables.Should().NotContainKey("resp");
        }
        finally
        {
            content.Release();
            await execution;
        }
    }
}