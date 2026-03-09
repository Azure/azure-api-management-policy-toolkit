// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Document;

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
    public void SendOneWayRequest_Inbound_ShouldExecuteWithoutError()
    {
        // Arrange
        var test = new TestDocument(new SimpleSendOneWayRequest());

        // Act & Assert - no-op should not throw
        test.RunInbound();
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
}
