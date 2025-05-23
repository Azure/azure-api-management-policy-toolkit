// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text;

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring.Expressions;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Document;

namespace Test.Emulator.Emulator.Policies;

[TestClass]
public class LogToEventHubTests
{
    class SimpleLogToEventHub : IDocument
    {
        public void Inbound(IInboundContext context)
        {
            context.LogToEventHub(new LogToEventHubConfig { LoggerId = "test-inbound", Value = "test-value" });
        }

        public void Backend(IBackendContext context)
        {
            context.LogToEventHub(new LogToEventHubConfig { LoggerId = "test-backend", Value = "test-value" });
        }

        public void Outbound(IOutboundContext context)
        {
            context.LogToEventHub(new LogToEventHubConfig { LoggerId = "test-outbound", Value = "test-value" });
        }

        public void OnError(IOnErrorContext context)
        {
            context.LogToEventHub(new LogToEventHubConfig { LoggerId = "test-onerror", Value = "test-value" });
        }
    }

    class WithPartitioningLogToEventHub : IDocument
    {
        public void Inbound(IInboundContext context)
        {
            context.LogToEventHub(new LogToEventHubConfig
            {
                LoggerId = "test-inbound", Value = "test-value", PartitionId = "test-id", PartitionKey = "test-key"
            });
        }
    }

    class HugeMessageLogToEventHub : IDocument
    {
        public void Inbound(IInboundContext context)
        {
            context.LogToEventHub(new LogToEventHubConfig
            {
                LoggerId = "test-inbound", Value = GetValue(context.ExpressionContext)
            });
        }

        public string GetValue(IExpressionContext context)
        {
            return new string('a', 204001); // 204,000 bytes of UTF-8
        }
    }

    [TestMethod]
    public void LogToEventHub_Callback()
    {
        var test = new SimpleLogToEventHub().AsTestDocument();
        var executedCallback = false;
        test.SetupInbound().LogToEventHub().WithCallback((_, _) =>
        {
            executedCallback = true;
        });

        test.RunInbound();

        executedCallback.Should().BeTrue();
    }

    [TestMethod]
    public void LogToEventHub_NotSetupLogger()
    {
        var test = new SimpleLogToEventHub().AsTestDocument();

        test.RunInbound();
    }

    [TestMethod]
    public void LogToEventHub_Inbound_SetupLogger()
    {
        var test = new SimpleLogToEventHub().AsTestDocument();
        var logger = test.SetupLoggerStore().Add("test-inbound");

        test.RunInbound();

        var eventHubEvent = logger.Events.SingleOrDefault();
        eventHubEvent.Should().NotBeNull();
        eventHubEvent?.Value.Should().Be("test-value");
    }

    [TestMethod]
    public void LogToEventHub_Outbound_SetupLogger()
    {
        var test = new SimpleLogToEventHub().AsTestDocument();
        var logger = test.SetupLoggerStore().Add("test-outbound");

        test.RunOutbound();

        var eventHubEvent = logger.Events.SingleOrDefault();
        eventHubEvent.Should().NotBeNull();
        eventHubEvent?.Value.Should().Be("test-value");
    }

    [TestMethod]
    public void LogToEventHub_Backend_SetupLogger()
    {
        var test = new SimpleLogToEventHub().AsTestDocument();
        var logger = test.SetupLoggerStore().Add("test-backend");

        test.RunBackend();

        var eventHubEvent = logger.Events.SingleOrDefault();
        eventHubEvent.Should().NotBeNull();
        eventHubEvent?.Value.Should().Be("test-value");
    }

    [TestMethod]
    public void LogToEventHub_OnError_SetupLogger()
    {
        var test = new SimpleLogToEventHub().AsTestDocument();
        var logger = test.SetupLoggerStore().Add("test-onerror");

        test.RunOnError();

        var eventHubEvent = logger.Events.SingleOrDefault();
        eventHubEvent.Should().NotBeNull();
        eventHubEvent?.Value.Should().Be("test-value");
    }

    [TestMethod]
    public void LogToEventHub_WithPartition()
    {
        var test = new WithPartitioningLogToEventHub().AsTestDocument();
        var logger = test.SetupLoggerStore().Add("test-inbound");

        test.RunInbound();

        var eventHubEvent = logger.Events.SingleOrDefault();
        eventHubEvent.Should().NotBeNull();
        eventHubEvent?.Value.Should().Be("test-value");
        eventHubEvent?.PartitionId.Should().Be("test-id");
        eventHubEvent?.PartitionKey.Should().Be("test-key");
    }

    [TestMethod]
    public void LogToEventHub_TrimMessage()
    {
        var test = new HugeMessageLogToEventHub().AsTestDocument();
        var logger = test.SetupLoggerStore().Add("test-inbound");

        test.RunInbound();

        var eventHubEvent = logger.Events.SingleOrDefault();
        eventHubEvent.Should().NotBeNull();
        eventHubEvent?.Value.Should().NotBeNull();
        Encoding.UTF8.GetByteCount(eventHubEvent?.Value!).Should().Be(204000);
    }
}