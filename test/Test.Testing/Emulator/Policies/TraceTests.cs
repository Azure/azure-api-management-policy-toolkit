// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Document;

namespace Test.Emulator.Emulator.Policies;

[TestClass]
public class TraceTests
{
    class SimpleTrace : IDocument
    {
        public void Inbound(IInboundContext context)
        {
            context.Trace(new TraceConfig
            {
                Source = "inbound-source",
                Message = "inbound-message",
                Severity = "information",
                Metadata = [new TraceMetadata { Name = "k1", Value = "v1" }]
            });
        }

        public void Outbound(IOutboundContext context)
        {
            context.Trace(new TraceConfig
            {
                Source = "outbound-source",
                Message = "outbound-message",
                Severity = "warning"
            });
        }

        public void Backend(IBackendContext context)
        {
            context.Trace(new TraceConfig
            {
                Source = "backend-source",
                Message = "backend-message",
                Severity = "error"
            });
        }
    }

    class MultiTraceInbound : IDocument
    {
        public void Inbound(IInboundContext context)
        {
            context.Trace(new TraceConfig
            {
                Source = "skip",
                Message = "first"
            });

            context.Trace(new TraceConfig
            {
                Source = "target",
                Message = "second"
            });
        }
    }

    [TestMethod]
    public void Trace_Inbound_HandleSimple_WithCallback()
    {
        // Arrange
        var test = new SimpleTrace().AsTestDocument();
        bool callbackExecuted = false;
        TraceConfig? captured = null;

        test.SetupInbound().Trace().WithCallback((_, config) =>
        {
            callbackExecuted = true;
            captured = config;
        });

        // Act
        test.RunInbound();

        // Assert
        callbackExecuted.Should().BeTrue();
        captured.Should().NotBeNull();
        captured!.Source.Should().Be("inbound-source");
        captured.Message.Should().Be("inbound-message");
        captured.Severity.Should().Be("information");
        captured.Metadata.Should().HaveCount(1);
        captured.Metadata![0].Name.Should().Be("k1");
        captured.Metadata[0].Value.Should().Be("v1");
    }

    [TestMethod]
    public void Trace_Inbound_HandleSimple_WithoutSetup()
    {
        var test = new SimpleTrace().AsTestDocument();

        test.RunInbound();
    }

    [TestMethod]
    public void Trace_Outbound_HandleSimple_WithCallback()
    {
        // Arrange
        var test = new SimpleTrace().AsTestDocument();
        bool callbackExecuted = false;
        TraceConfig? captured = null;

        test.SetupOutbound().Trace().WithCallback((_, config) =>
        {
            callbackExecuted = true;
            captured = config;
        });

        // Act
        test.RunOutbound();

        // Assert
        callbackExecuted.Should().BeTrue();
        captured.Should().NotBeNull();
        captured!.Source.Should().Be("outbound-source");
        captured.Message.Should().Be("outbound-message");
        captured.Severity.Should().Be("warning");
    }

    [TestMethod]
    public void Trace_Backend_HandleSimple_WithCallback()
    {
        // Arrange
        var test = new SimpleTrace().AsTestDocument();
        bool callbackExecuted = false;
        TraceConfig? captured = null;

        test.SetupBackend().Trace().WithCallback((_, config) =>
        {
            callbackExecuted = true;
            captured = config;
        });

        // Act
        test.RunBackend();

        // Assert
        callbackExecuted.Should().BeTrue();
        captured.Should().NotBeNull();
        captured!.Source.Should().Be("backend-source");
        captured.Message.Should().Be("backend-message");
        captured.Severity.Should().Be("error");
    }

    [TestMethod]
    public void Trace_Inbound_HandleMultiTraceInbound_WithPredicateCallback()
    {
        // Arrange
        var test = new MultiTraceInbound().AsTestDocument();
        bool callbackExecuted = false;
        string? lastMessage = null;

        test.SetupInbound().Trace((_, config) => config.Source == "target").WithCallback((_, config) =>
        {
            callbackExecuted = true;
            lastMessage = config.Message;
        });

        // Act
        test.RunInbound();

        // Assert
        callbackExecuted.Should().BeTrue();
        lastMessage.Should().Be("second");
    }
}
