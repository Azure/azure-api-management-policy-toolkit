// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring.Expressions;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Document;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Expressions;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Services;

namespace Test.Emulator.Emulator.Policies;

[TestClass]
public class SetBackendServiceTests
{
    class SimpleSetBackendService : IDocument
    {
        public void Inbound(IInboundContext context)
        {
            context.SetBackendService(new SetBackendServiceConfig
            {
                BaseUrl = "https://backend.example.com",
            });
        }

        public void Backend(IBackendContext context) { }
        public void Outbound(IOutboundContext context) { }
        public void OnError(IOnErrorContext context) { }
    }

    [TestMethod]
    public void SetBackendService_SetsBackendUrl()
    {
        // Arrange
        var test = new SimpleSetBackendService().AsTestDocument();

        // Act
        test.RunInbound();

        // Assert
        test.Context.BackendUrl.Should().Be("https://backend.example.com");
    }

    [TestMethod]
    public void SetBackendService_Callback()
    {
        // Arrange
        var test = new SimpleSetBackendService().AsTestDocument();
        var executedCallback = false;

        test.SetupInbound().SetBackendService().WithCallback((context, config) =>
        {
            executedCallback = true;
            context.Variables["backend-url"] = config.BaseUrl!;
        });

        // Act
        test.RunInbound();

        // Assert
        executedCallback.Should().BeTrue();
        test.Context.Variables.Should().ContainKey("backend-url")
            .WhoseValue.Should().Be("https://backend.example.com");
    }

    internal sealed class RecordingBackendResolver(
        Func<GatewayContext, SetBackendServiceConfig, Uri> resolver) : IBackendResolver
    {
        public SetBackendServiceConfig? LastConfig { get; private set; }
        public int Calls { get; private set; }

        public Uri Resolve(GatewayContext context, SetBackendServiceConfig config)
        {
            LastConfig = config;
            Calls++;
            return resolver(context, config);
        }
    }

    private sealed class ConfiguredDocument(
        Func<IExpressionContext, SetBackendServiceConfig> factory, string section = "inbound") : IDocument
    {
        public void Inbound(IInboundContext context)
        {
            if (section != "inbound") return;
            context.SetBackendService(factory(context.ExpressionContext));
            context.SetVariable("continued", true);
        }

        public void Backend(IBackendContext context)
        {
            if (section != "backend") return;
            context.SetBackendService(factory(context.ExpressionContext));
            context.SetVariable("continued", true);
        }

        public void Outbound(IOutboundContext context)
        {
            if (section != "outbound") return;
            context.SetBackendService(factory(context.ExpressionContext));
            context.SetVariable("continued", true);
        }

        public void OnError(IOnErrorContext context)
        {
            if (section != "on-error") return;
            context.SetBackendService(factory(context.ExpressionContext));
            context.SetVariable("continued", true);
        }
    }

    private static TestDocument CreateTest(SetBackendServiceConfig config, string section = "inbound") =>
        new ConfiguredDocument(_ => config, section).AsTestDocument();

    [TestMethod]
    [DataRow("inbound")]
    [DataRow("backend")]
    [DataRow("outbound")]
    [DataRow("on-error")]
    public void ShouldSetBackendInEveryAuthoredSection(string section)
    {
        var test = CreateTest(new SetBackendServiceConfig { BaseUrl = "https://backend.example.test/base" }, section);

        SendRequestTests.RunSection(test, section);

        test.Context.BackendUrl.Should().Be("https://backend.example.test/base");
        test.Context.Variables["continued"].Should().Be(true);
    }

    [TestMethod]
    [DataRow("inbound")]
    [DataRow("backend")]
    [DataRow("outbound")]
    [DataRow("on-error")]
    public void ShouldOverrideResolutionWithMatchingCallbacksInEverySection(string section)
    {
        var test = CreateTest(new SetBackendServiceConfig { BackendId = "mock" }, section);
        Action<GatewayContext, SetBackendServiceConfig> callback = (context, _) =>
            context.BackendUrl = "https://callback.example.test";
        switch (section)
        {
            case "inbound": test.SetupInbound().SetBackendService((_, c) => c.BackendId == "mock").WithCallback(callback); break;
            case "backend": test.SetupBackend().SetBackendService((_, c) => c.BackendId == "mock").WithCallback(callback); break;
            case "outbound": test.SetupOutbound().SetBackendService((_, c) => c.BackendId == "mock").WithCallback(callback); break;
            case "on-error": test.SetupOnError().SetBackendService((_, c) => c.BackendId == "mock").WithCallback(callback); break;
        }

        SendRequestTests.RunSection(test, section);

        test.Context.BackendUrl.Should().Be("https://callback.example.test");
        test.Context.Variables["continued"].Should().Be(true);
    }

    [TestMethod]
    public void ShouldResolveBackendIdThroughInjectedService()
    {
        var test = CreateTest(new SetBackendServiceConfig { BackendId = "orders" });
        var resolver = new RecordingBackendResolver((context, config) =>
        {
            context.Should().BeSameAs(test.Context);
            config.BackendId.Should().Be("orders");
            return new Uri("https://orders.example.test/v1");
        });
        test.Context.Services.Register<IBackendResolver>(resolver);

        test.RunInbound();

        resolver.Calls.Should().Be(1);
        test.Context.BackendUrl.Should().Be("https://orders.example.test/v1");
    }

    [TestMethod]
    public void ShouldEvaluateBaseUrlExpression()
    {
        var test = new ConfiguredDocument(context => new SetBackendServiceConfig
        {
            BaseUrl = (string)context.Variables["base"]
        }).AsTestDocument();
        test.Context.Variables["base"] = "https://expression.example.test/base";

        test.RunInbound();

        test.Context.BackendUrl.Should().Be("https://expression.example.test/base");
    }

    [TestMethod]
    public void ShouldPassAllEvaluatedServiceFabricAndDaprOptionsToResolver()
    {
        var test = new ConfiguredDocument(context => new SetBackendServiceConfig
        {
            BackendId = (string)context.Variables["backend"],
            SfResolveCondition = (bool)context.Variables["sf-condition"],
            SfServiceInstanceName = (string)context.Variables["sf-instance"],
            SfPartitionKey = (string)context.Variables["sf-partition"],
            SfListenerName = (string)context.Variables["sf-listener"],
            SfReplicaType = (string)context.Variables["sf-replica"],
            DaprAppId = (string)context.Variables["dapr-app"],
            DaprMethod = (string)context.Variables["dapr-method"],
            DaprNamespace = (string)context.Variables["dapr-namespace"]
        }).AsTestDocument();
        test.Context.Variables["backend"] = "configured-backend";
        test.Context.Variables["sf-condition"] = false;
        test.Context.Variables["sf-instance"] = "fabric:/service";
        test.Context.Variables["sf-partition"] = "42";
        test.Context.Variables["sf-listener"] = "listener";
        test.Context.Variables["sf-replica"] = "primary";
        test.Context.Variables["dapr-app"] = "orders";
        test.Context.Variables["dapr-method"] = "create";
        test.Context.Variables["dapr-namespace"] = "services";
        var resolver = new RecordingBackendResolver((_, config) =>
        {
            config.BackendId.Should().Be("configured-backend");
            config.SfResolveCondition.Should().BeFalse();
            config.SfServiceInstanceName.Should().Be("fabric:/service");
            config.SfPartitionKey.Should().Be("42");
            config.SfListenerName.Should().Be("listener");
            config.SfReplicaType.Should().Be("primary");
            config.DaprAppId.Should().Be("orders");
            config.DaprMethod.Should().Be("create");
            config.DaprNamespace.Should().Be("services");
            return new Uri("http://resolved.example.test:3500/v1.0/invoke/orders/method/create");
        });
        test.Context.Services.Register<IBackendResolver>(resolver);

        test.RunInbound();

        test.Context.BackendUrl.Should().Be("http://resolved.example.test:3500/v1.0/invoke/orders/method/create");
        resolver.Calls.Should().Be(1);
    }

    [TestMethod]
    [DataRow("backend-id")]
    [DataRow("sf-condition")]
    [DataRow("sf-instance")]
    [DataRow("sf-partition")]
    [DataRow("sf-listener")]
    [DataRow("sf-replica")]
    [DataRow("dapr-app")]
    [DataRow("dapr-method")]
    [DataRow("dapr-namespace")]
    public void ShouldRejectUnconfiguredResolutionInsteadOfIgnoringOptions(string option)
    {
        var baseConfig = new SetBackendServiceConfig { BaseUrl = "https://backend.example.test" };
        var config = option switch
        {
            "backend-id" => new SetBackendServiceConfig { BackendId = "unconfigured" },
            "sf-condition" => baseConfig with { SfResolveCondition = false },
            "sf-instance" => baseConfig with { SfServiceInstanceName = "fabric:/service" },
            "sf-partition" => baseConfig with { SfPartitionKey = "partition" },
            "sf-listener" => baseConfig with { SfListenerName = "listener" },
            "sf-replica" => baseConfig with { SfReplicaType = "primary" },
            "dapr-app" => baseConfig with { DaprAppId = "app" },
            "dapr-method" => baseConfig with { DaprAppId = "app", DaprMethod = "method" },
            _ => baseConfig with { DaprAppId = "app", DaprNamespace = "namespace" }
        };
        var test = CreateTest(config);
        test.Context.BackendUrl = "https://previous.example.test";

        var error = Assert.ThrowsExactly<PolicyException>(() => test.RunInbound());

        error.InnerException.Should().BeOfType<InvalidOperationException>();
        test.Context.BackendUrl.Should().Be("https://previous.example.test");
        test.Context.Variables.Should().NotContainKey("continued");
    }

    [TestMethod]
    [DataRow("missing")]
    [DataRow("both")]
    [DataRow("base-empty")]
    [DataRow("base-relative")]
    [DataRow("base-scheme")]
    [DataRow("base-fragment")]
    [DataRow("id-empty")]
    [DataRow("dapr-method")]
    [DataRow("dapr-namespace")]
    [DataRow("sf-empty")]
    public void ShouldRejectInvalidBackendConfigurationWithoutMutatingPreviousTarget(string kind)
    {
        var config = kind switch
        {
            "missing" => new SetBackendServiceConfig(),
            "both" => new SetBackendServiceConfig { BaseUrl = "https://base.example.test", BackendId = "id" },
            "base-empty" => new SetBackendServiceConfig { BaseUrl = " " },
            "base-relative" => new SetBackendServiceConfig { BaseUrl = "/relative" },
            "base-scheme" => new SetBackendServiceConfig { BaseUrl = "ftp://backend.example.test" },
            "base-fragment" => new SetBackendServiceConfig { BaseUrl = "https://backend.example.test/#fragment" },
            "id-empty" => new SetBackendServiceConfig { BackendId = " " },
            "dapr-method" => new SetBackendServiceConfig { BaseUrl = "http://localhost:3500", DaprMethod = "method" },
            "dapr-namespace" => new SetBackendServiceConfig { BaseUrl = "http://localhost:3500", DaprNamespace = "namespace" },
            _ => new SetBackendServiceConfig { BackendId = "id", SfListenerName = " " }
        };
        var test = CreateTest(config);
        test.Context.BackendUrl = "https://previous.example.test";
        var resolver = new RecordingBackendResolver((_, _) => new Uri("https://resolved.example.test"));
        test.Context.Services.Register<IBackendResolver>(resolver);

        Assert.ThrowsExactly<PolicyException>(() => test.RunInbound())
            .InnerException.Should().BeAssignableTo<ArgumentException>();
        test.Context.BackendUrl.Should().Be("https://previous.example.test");
        resolver.Calls.Should().Be(0);
    }

    [TestMethod]
    [DataRow("relative")]
    [DataRow("scheme")]
    [DataRow("null")]
    public void ShouldRejectInvalidResolverResult(string kind)
    {
        var test = CreateTest(new SetBackendServiceConfig { BackendId = "id" });
        test.Context.BackendUrl = "https://previous.example.test";
        test.Context.Services.Register<IBackendResolver>(new RecordingBackendResolver((_, _) => kind switch
        {
            "relative" => new Uri("relative", UriKind.Relative),
            "scheme" => new Uri("ftp://resolved.example.test"),
            _ => null!
        }));

        Assert.ThrowsExactly<PolicyException>(() => test.RunInbound())
            .InnerException.Should().BeOfType<InvalidOperationException>();
        test.Context.BackendUrl.Should().Be("https://previous.example.test");
    }

    [TestMethod]
    public void ShouldPropagateResolverExceptionWithoutReplacingBackend()
    {
        var test = CreateTest(new SetBackendServiceConfig { BackendId = "id" });
        var failure = new InvalidOperationException("backend was not found");
        test.Context.BackendUrl = "https://previous.example.test";
        test.Context.Services.Register<IBackendResolver>(new RecordingBackendResolver((_, _) => throw failure));

        Assert.ThrowsExactly<PolicyException>(() => test.RunInbound()).InnerException.Should().BeSameAs(failure);
        test.Context.BackendUrl.Should().Be("https://previous.example.test");
    }

    private sealed class RoutedDocument : IDocument
    {
        public void Inbound(IInboundContext context) =>
            context.SetBackendService(new SetBackendServiceConfig { BackendId = "orders" });

        public void Backend(IBackendContext context) => context.ForwardRequest();
    }

    [TestMethod]
    public void ShouldUseResolvedBackendForSubsequentForwardRequest()
    {
        var test = new RoutedDocument().AsTestDocument();
        test.Context.Request = new MockRequest(new Uri("https://gateway.example.test/items/42?filter=active"));
        test.Context.Services.Register<IBackendResolver>(new RecordingBackendResolver((_, _) => new Uri("https://orders.example.test/api/")));
        var client = StubHttpClient.Ok();
        test.Context.Services.Register<IHttpClient>(client);

        test.RunInbound();
        test.RunBackend();

        client.LastRequest!.RequestUri!.AbsoluteUri.Should().Be("https://orders.example.test/api/items/42?filter=active");
    }

    private sealed class FragmentDocument : IDocument
    {
        public void Inbound(IInboundContext context) => context.IncludeFragment("backend-http");
    }

    private sealed class BackendFragment : IFragment
    {
        public void Fragment(IFragmentContext context) =>
            context.SetBackendService(new SetBackendServiceConfig { BaseUrl = "https://fragment.example.test" });
    }

    [TestMethod]
    public void ShouldSetBackendInsideAuthoredFragment()
    {
        var test = new FragmentDocument().AsTestDocument().RegisterFragment("backend-http", new BackendFragment());

        test.RunInbound();

        test.Context.BackendUrl.Should().Be("https://fragment.example.test");
    }

    [TestMethod]
    public void ShouldAllowCallbackToOverrideDefaultConfigurationValidation()
    {
        var test = CreateTest(new SetBackendServiceConfig());
        test.SetupInbound().SetBackendService().WithCallback((context, _) => context.BackendUrl = "https://callback.example.test");

        test.RunInbound();

        test.Context.BackendUrl.Should().Be("https://callback.example.test");
    }

    [TestMethod]
    public void ShouldPropagateCallbackErrors()
    {
        var test = CreateTest(new SetBackendServiceConfig { BackendId = "id" });
        var failure = new InvalidOperationException("callback failed");
        test.SetupInbound().SetBackendService().WithCallback((_, _) => throw failure);

        Assert.ThrowsExactly<PolicyException>(() => test.RunInbound()).InnerException.Should().BeSameAs(failure);
    }
}