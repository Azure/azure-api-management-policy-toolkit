// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Document;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator;

namespace Test.Emulator.Emulator.Policies;

[TestClass]
public class SetBackendServiceTests
{
    class SetBackendServiceWithBaseUrl : IDocument
    {
        public void Inbound(IInboundContext context)
        {
            context.SetBackendService(new SetBackendServiceConfig
            {
                BaseUrl = "https://new-backend.example.com/api"
            });
        }

        public void Backend(IBackendContext context)
        {
            context.SetBackendService(new SetBackendServiceConfig
            {
                BaseUrl = "https://backend-section.example.com"
            });
        }

        public void Outbound(IOutboundContext context)
        {
            context.SetBackendService(new SetBackendServiceConfig
            {
                BaseUrl = "https://outbound-backend.example.com"
            });
        }

        public void OnError(IOnErrorContext context)
        {
            context.SetBackendService(new SetBackendServiceConfig
            {
                BaseUrl = "https://error-backend.example.com"
            });
        }
    }

    class SetBackendServiceWithBackendId : IDocument
    {
        public void Inbound(IInboundContext context)
        {
            context.SetBackendService(new SetBackendServiceConfig
            {
                BackendId = "my-backend"
            });
        }
    }

    [TestMethod]
    public void SetBackendService_BaseUrl_Inbound()
    {
        var test = new SetBackendServiceWithBaseUrl().AsTestDocument();

        test.RunInbound();

        test.Context.Api.ServiceUrl.Scheme.Should().Be("https");
        test.Context.Api.ServiceUrl.Host.Should().Be("new-backend.example.com");
        test.Context.Api.ServiceUrl.Path.Should().Be("/api");
    }

    [TestMethod]
    public void SetBackendService_BaseUrl_Backend()
    {
        var test = new SetBackendServiceWithBaseUrl().AsTestDocument();

        test.RunBackend();

        test.Context.Api.ServiceUrl.Host.Should().Be("backend-section.example.com");
    }

    [TestMethod]
    public void SetBackendService_BaseUrl_Outbound()
    {
        var test = new SetBackendServiceWithBaseUrl().AsTestDocument();

        test.RunOutbound();

        test.Context.Api.ServiceUrl.Host.Should().Be("outbound-backend.example.com");
    }

    [TestMethod]
    public void SetBackendService_BaseUrl_OnError()
    {
        var test = new SetBackendServiceWithBaseUrl().AsTestDocument();

        test.RunOnError();

        test.Context.Api.ServiceUrl.Host.Should().Be("error-backend.example.com");
    }

    [TestMethod]
    public void SetBackendService_BackendId_ResolvesFromStore()
    {
        var test = new SetBackendServiceWithBackendId().AsTestDocument();
        test.SetupBackendStore().Add("my-backend", "https://resolved-backend.example.com/v2");

        test.RunInbound();

        test.Context.Api.ServiceUrl.Host.Should().Be("resolved-backend.example.com");
        test.Context.Api.ServiceUrl.Path.Should().Be("/v2");
    }

    [TestMethod]
    public void SetBackendService_BackendId_NotFound_Throws()
    {
        var test = new SetBackendServiceWithBackendId().AsTestDocument();

        var act = () => test.RunInbound();

        act.Should().Throw<BadRuntimeConfigurationException>()
            .Which.Message.Should().Contain("my-backend");
    }

    [TestMethod]
    public void SetBackendService_Callback()
    {
        var test = new SetBackendServiceWithBaseUrl().AsTestDocument();
        var callbackExecuted = false;
        test.SetupInbound().SetBackendService().WithCallback((_, config) =>
        {
            callbackExecuted = true;
            config.BaseUrl.Should().Be("https://new-backend.example.com/api");
        });

        test.RunInbound();

        callbackExecuted.Should().BeTrue();
        test.Context.Api.ServiceUrl.Host.Should().NotBe("new-backend.example.com");
    }
}
