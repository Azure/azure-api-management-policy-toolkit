// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net;

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring.Expressions;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Document;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Services;

namespace Test.Emulator.Emulator.Policies;

[TestClass]
public class ProxyTests
{
    private static ProxyConfig DefaultConfig => new()
    {
        Url = "http://proxy.example.test:8080",
        Username = "proxy-user",
        Password = "proxy-password"
    };

    private sealed class ConfiguredDocument(
        Func<IExpressionContext, ProxyConfig> factory, string policy = "forward", ProxyConfig? localProxy = null) : IDocument
    {
        public void Inbound(IInboundContext context)
        {
            context.Proxy(factory(context.ExpressionContext));
            switch (policy)
            {
                case "send":
                    context.SendRequest(new SendRequestConfig
                    {
                        ResponseVariableName = "resp",
                        Url = "https://origin.example.test",
                        Proxy = localProxy
                    });
                    break;
                case "one-way":
                    context.SendOneWayRequest(new SendOneWayRequestConfig { Url = "https://origin.example.test", Proxy = localProxy });
                    break;
                case "invoke":
                    context.InvokeRequest(new InvokeRequestConfig { Url = "https://origin.example.test", ResponseVariableName = "resp" });
                    break;
            }
        }

        public void Backend(IBackendContext context)
        {
            if (policy == "forward") context.ForwardRequest();
        }
    }

    [TestMethod]
    [DataRow("forward")]
    [DataRow("send")]
    [DataRow("one-way")]
    [DataRow("invoke")]
    public void ShouldExposeProxyRoutingAndCredentialsToEveryCoupledHttpPolicy(string policy)
    {
        var test = new ConfiguredDocument(_ => DefaultConfig, policy).AsTestDocument();
        test.Context.BackendUrl = "https://backend.example.test";
        test.Context.Services.Register<IHttpClient>(new StubHttpClient(request =>
        {
            request.RequestUri!.Host.Should().NotBe("proxy.example.test");
            request.Headers.Contains("Proxy-Authorization").Should().BeFalse();
            request.Options.TryGetValue(HttpTransportOptions.Key, out var options).Should().BeTrue();
            options!.Proxy.Should().BeEquivalentTo(DefaultConfig);
            return new HttpResponseMessage(HttpStatusCode.OK);
        }));

        test.RunInbound();
        if (policy == "forward") test.RunBackend();

        test.Context.Services.Resolve<HttpTransportState>()!.Proxy.Should().BeEquivalentTo(DefaultConfig);
    }

    [TestMethod]
    public void ShouldEvaluateAllProxyExpressionsBeforeForwarding()
    {
        var test = new ConfiguredDocument(context => new ProxyConfig
        {
            Url = (string)context.Variables["url"],
            Username = (string)context.Variables["username"],
            Password = (string)context.Variables["password"]
        }).AsTestDocument();
        test.Context.Variables["url"] = "http://expression-proxy.example.test:3128";
        test.Context.Variables["username"] = "evaluated-user";
        test.Context.Variables["password"] = "evaluated-password";
        test.Context.Services.Register<IHttpClient>(new StubHttpClient(request =>
        {
            request.Options.TryGetValue(HttpTransportOptions.Key, out var options).Should().BeTrue();
            options!.Proxy!.Url.Should().Be("http://expression-proxy.example.test:3128");
            options.Proxy.Username.Should().Be("evaluated-user");
            options.Proxy.Password.Should().Be("evaluated-password");
            return new HttpResponseMessage(HttpStatusCode.OK);
        }));

        test.RunInbound();
        test.RunBackend();
    }

    [TestMethod]
    [DataRow("send")]
    [DataRow("one-way")]
    public void ShouldAllowPerRequestProxyOverrideWithoutChangingGlobalRouting(string policy)
    {
        var localProxy = new ProxyConfig { Url = "http://local-proxy.example.test:3128" };
        var test = new ConfiguredDocument(_ => DefaultConfig, policy, localProxy).AsTestDocument();
        test.Context.Services.Register<IHttpClient>(new StubHttpClient(request =>
        {
            request.Options.TryGetValue(HttpTransportOptions.Key, out var options).Should().BeTrue();
            options!.Proxy.Should().BeEquivalentTo(localProxy);
            return new HttpResponseMessage(HttpStatusCode.OK);
        }));

        test.RunInbound();

        test.Context.Services.Resolve<HttpTransportState>()!.Proxy.Should().BeEquivalentTo(DefaultConfig);
    }

    [TestMethod]
    public void ShouldLetMatchingCallbackOverrideProxyConfiguration()
    {
        var test = new ConfiguredDocument(_ => DefaultConfig).AsTestDocument();
        test.SetupInbound().Proxy((_, config) => config.Url == DefaultConfig.Url)
            .WithCallback((context, _) => context.Variables["callback"] = true);
        test.Context.Services.Register<IHttpClient>(new StubHttpClient(request =>
        {
            request.Options.TryGetValue(HttpTransportOptions.Key, out var options).Should().BeTrue();
            options!.Proxy.Should().BeNull();
            return new HttpResponseMessage(HttpStatusCode.OK);
        }));

        test.RunInbound();
        test.RunBackend();

        test.Context.Variables["callback"].Should().Be(true);
    }

    [TestMethod]
    public void ShouldNotLetUnmatchedCallbackSuppressRealProxyConfiguration()
    {
        var test = new ConfiguredDocument(_ => DefaultConfig).AsTestDocument();
        test.SetupInbound().Proxy((_, config) => config.Url == "http://other.example.test")
            .WithCallback((_, _) => Assert.Fail("Unmatched callback ran."));

        test.RunInbound();

        test.Context.Services.Resolve<HttpTransportState>()!.Proxy.Should().BeEquivalentTo(DefaultConfig);
    }

    [TestMethod]
    [DataRow("missing")]
    [DataRow("empty")]
    [DataRow("relative")]
    [DataRow("scheme")]
    [DataRow("fragment")]
    [DataRow("embedded-credentials")]
    [DataRow("username-only")]
    [DataRow("password-only")]
    public void ShouldRejectInvalidProxyWithoutReplacingPreviousConfiguration(string kind)
    {
        var initial = new ConfiguredDocument(_ => DefaultConfig).AsTestDocument();
        initial.RunInbound();
        var invalid = kind switch
        {
            "missing" => new ProxyConfig { Url = null! },
            "empty" => new ProxyConfig { Url = " " },
            "relative" => new ProxyConfig { Url = "/relative" },
            "scheme" => new ProxyConfig { Url = "ftp://proxy.example.test" },
            "fragment" => new ProxyConfig { Url = "http://proxy.example.test/#fragment" },
            "embedded-credentials" => new ProxyConfig { Url = "http://user:password@proxy.example.test" },
            "username-only" => new ProxyConfig { Url = "http://proxy.example.test", Username = "user" },
            _ => new ProxyConfig { Url = "http://proxy.example.test", Password = "password" }
        };
        var test = new TestDocument(new ConfiguredDocument(_ => invalid)) { Context = initial.Context };

        Assert.ThrowsExactly<PolicyException>(() => test.RunInbound())
            .InnerException.Should().BeAssignableTo<ArgumentException>();
        test.Context.Services.Resolve<HttpTransportState>()!.Proxy.Should().BeEquivalentTo(DefaultConfig);
    }

    private sealed class TwoProxiesDocument : IDocument
    {
        public void Inbound(IInboundContext context)
        {
            context.Proxy(new ProxyConfig { Url = "http://first-proxy.example.test" });
            context.SendRequest(new SendRequestConfig { ResponseVariableName = "first", Url = "https://origin.example.test" });
            context.Proxy(new ProxyConfig { Url = "http://second-proxy.example.test" });
            context.SendRequest(new SendRequestConfig { ResponseVariableName = "second", Url = "https://origin.example.test" });
        }
    }

    [TestMethod]
    public void ShouldSnapshotProxyPerRequestInsteadOfMutatingEarlierTransportOptions()
    {
        var test = new TwoProxiesDocument().AsTestDocument();
        var options = new List<HttpTransportOptions>();
        test.Context.Services.Register<IHttpClient>(new StubHttpClient(request =>
        {
            request.Options.TryGetValue(HttpTransportOptions.Key, out var value).Should().BeTrue();
            options.Add(value!);
            return new HttpResponseMessage(HttpStatusCode.OK);
        }));

        test.RunInbound();

        options.Should().HaveCount(2);
        options[0].Proxy!.Url.Should().Be("http://first-proxy.example.test");
        options[1].Proxy!.Url.Should().Be("http://second-proxy.example.test");
    }

    private sealed class FragmentDocument : IDocument
    {
        public void Inbound(IInboundContext context) => context.IncludeFragment("proxy-http");
        public void Backend(IBackendContext context) => context.ForwardRequest();
    }

    private sealed class ProxyFragment : IFragment
    {
        public void Fragment(IFragmentContext context) => context.Proxy(DefaultConfig);
    }

    [TestMethod]
    public void ShouldApplyProxyFromAuthoredInboundFragment()
    {
        var test = new FragmentDocument().AsTestDocument().RegisterFragment("proxy-http", new ProxyFragment());
        test.Context.Services.Register<IHttpClient>(new StubHttpClient(request =>
        {
            request.Options.TryGetValue(HttpTransportOptions.Key, out var options).Should().BeTrue();
            options!.Proxy.Should().BeEquivalentTo(DefaultConfig);
            return new HttpResponseMessage(HttpStatusCode.OK);
        }));

        test.RunInbound();
        test.RunBackend();
    }

    [TestMethod]
    public void ShouldPropagateCallbackExceptions()
    {
        var test = new ConfiguredDocument(_ => DefaultConfig).AsTestDocument();
        var failure = new HttpRequestException("proxy callback failed");
        test.SetupInbound().Proxy().WithCallback((_, _) => throw failure);

        var error = Assert.ThrowsExactly<PolicyException>(() => test.RunInbound());

        error.InnerException.Should().BeSameAs(failure);
        error.Section.Should().Be(nameof(IInboundContext));
    }
}