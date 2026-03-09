// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Document;

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
}
