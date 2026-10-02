// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text;

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring.Expressions;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Document;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator;

namespace Test.Emulator.Emulator.Policies;

[TestClass]
public class LogToEventHubTests
{
    private const int MaxMessageBytes = 204000;

    class ConfiguredLogToEventHub(LogToEventHubConfig config) : IDocument
    {
        public void Inbound(IInboundContext context) => context.LogToEventHub(config);
        public void Outbound(IOutboundContext context) => context.LogToEventHub(config);
        public void Backend(IBackendContext context) => context.LogToEventHub(config);
        public void OnError(IOnErrorContext context) => context.LogToEventHub(config);
    }

    class ExpressionLogToEventHub : IDocument
    {
        public void Inbound(IInboundContext context)
        {
            context.LogToEventHub(new LogToEventHubConfig
            {
                LoggerId = GetLoggerId(context.ExpressionContext),
                Value = GetValue(context.ExpressionContext),
                PartitionKey = GetPartitionKey(context.ExpressionContext)
            });
        }

        [Expression]
        static string GetLoggerId(IExpressionContext context) => (string)context.Variables["logger"];

        [Expression]
        static string GetValue(IExpressionContext context) => (string)context.Variables["message"];

        [Expression]
        static string GetPartitionKey(IExpressionContext context) => context.RequestId.ToString();
    }

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
                LoggerId = "test-inbound",
                Value = "test-value",
                PartitionId = "test-id",
                PartitionKey = "test-key"
            });
        }
    }

    class HugeMessageLogToEventHub : IDocument
    {
        public void Inbound(IInboundContext context)
        {
            context.LogToEventHub(new LogToEventHubConfig
            {
                LoggerId = "test-inbound",
                Value = GetValue(context.ExpressionContext)
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

        var act = () => test.RunInbound();

        var error = act.Should().Throw<PolicyException>().Which;
        error.Policy.Should().Be(nameof(IInboundContext.LogToEventHub));
        error.InnerException.Should().BeOfType<InvalidOperationException>()
            .Which.Message.Should().Contain("test-inbound").And.Contain("SetupLoggerStore");
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

    [TestMethod]
    [DataRow("a", 1)]
    [DataRow("\u00E9", 2)]
    [DataRow("\u20AC", 3)]
    [DataRow("\uD83D\uDE00", 4)]
    public void LogToEventHub_ExactByteBoundary_PreservesEntireMessage(string character, int byteWidth)
    {
        var message = new string('a', MaxMessageBytes - byteWidth) + character;
        var test = new ConfiguredLogToEventHub(CreateConfig() with { Value = message }).AsTestDocument();
        var logger = test.SetupLoggerStore().Add("test-inbound");

        test.RunInbound();

        var value = logger.Events.Should().ContainSingle().Which.Value;
        value.Should().Be(message);
        Encoding.UTF8.GetByteCount(value).Should().Be(MaxMessageBytes);
    }

    [TestMethod]
    [DataRow("\u00E9", 2)]
    [DataRow("\u20AC", 3)]
    [DataRow("\uD83D\uDE00", 4)]
    public void LogToEventHub_CompleteCharacterAtLimit_IsPreservedWhenTruncating(string character, int byteWidth)
    {
        var expected = new string('a', MaxMessageBytes - byteWidth) + character;
        var config = CreateConfig() with { Value = expected + "overflow", PartitionKey = "test-key" };
        var test = new ConfiguredLogToEventHub(config).AsTestDocument();
        var logger = test.SetupLoggerStore().Add("test-inbound");

        test.RunInbound();

        var hubEvent = logger.Events.Should().ContainSingle().Which;
        hubEvent.Value.Should().Be(expected);
        hubEvent.PartitionKey.Should().Be("test-key");
        Encoding.UTF8.GetByteCount(hubEvent.Value).Should().Be(MaxMessageBytes);
    }

    [TestMethod]
    [DataRow("\u00E9", 1)]
    [DataRow("\u20AC", 1)]
    [DataRow("\u20AC", 2)]
    [DataRow("\uD83D\uDE00", 1)]
    [DataRow("\uD83D\uDE00", 2)]
    [DataRow("\uD83D\uDE00", 3)]
    public void LogToEventHub_PartialCharacterAtLimit_IsOmitted(string character, int bytesAvailable)
    {
        var expected = new string('a', MaxMessageBytes - bytesAvailable);
        var config = CreateConfig() with { Value = expected + character + "overflow", PartitionId = "0" };
        var test = new ConfiguredLogToEventHub(config).AsTestDocument();
        var logger = test.SetupLoggerStore().Add("test-inbound");

        test.RunInbound();

        var hubEvent = logger.Events.Should().ContainSingle().Which;
        hubEvent.Value.Should().Be(expected).And.NotContain("\uFFFD");
        hubEvent.PartitionId.Should().Be("0");
        Encoding.UTF8.GetByteCount(hubEvent.Value).Should().Be(MaxMessageBytes - bytesAvailable)
            .And.BeLessThanOrEqualTo(MaxMessageBytes);
    }

    [TestMethod]
    public void LogToEventHub_EmptyMessage_IsRecorded()
    {
        var test = new ConfiguredLogToEventHub(CreateConfig() with { Value = "" }).AsTestDocument();
        var logger = test.SetupLoggerStore().Add("test-inbound");

        test.RunInbound();

        logger.Events.Should().ContainSingle().Which.Value.Should().BeEmpty();
    }

    [TestMethod]
    public void LogToEventHub_Expressions_AreEvaluatedForEachInvocation()
    {
        var test = new ExpressionLogToEventHub().AsTestDocument();
        var firstLogger = test.SetupLoggerStore().Add("first");
        var secondLogger = test.SetupLoggerStore().Add("second");
        test.Context.Variables["logger"] = "first";
        test.Context.Variables["message"] = "first-message";

        test.RunInbound();
        test.Context.Variables["logger"] = "second";
        test.Context.Variables["message"] = "second-message";
        test.RunInbound();

        firstLogger.Events.Should().ContainSingle().Which.Value.Should().Be("first-message");
        secondLogger.Events.Should().ContainSingle().Which.Value.Should().Be("second-message");
        firstLogger.Events[0].PartitionKey.Should().Be(test.Context.RequestId.ToString());
        secondLogger.Events[0].PartitionKey.Should().Be(test.Context.RequestId.ToString());
    }

    [TestMethod]
    [DataRow("inbound")]
    [DataRow("outbound")]
    [DataRow("backend")]
    [DataRow("on-error")]
    public void LogToEventHub_Callback_OverridesRecordingInEverySection(string section)
    {
        var config = CreateConfig();
        var test = new ConfiguredLogToEventHub(config).AsTestDocument();
        var logger = test.SetupLoggerStore().Add("test-inbound");
        SetupCallback(test, section).WithCallback((context, observedConfig) =>
        {
            observedConfig.Should().BeSameAs(config);
            context.Variables["callback"] = section;
        });

        RunSection(test, section);

        test.Context.Variables["callback"].Should().Be(section);
        logger.Events.Should().BeEmpty();
    }

    [TestMethod]
    public void LogToEventHub_UnmatchedCallback_UsesDefaultRecording()
    {
        var test = new ConfiguredLogToEventHub(CreateConfig()).AsTestDocument();
        var logger = test.SetupLoggerStore().Add("test-inbound");
        var executedCallback = false;
        test.SetupInbound().LogToEventHub((_, config) => config.LoggerId == "other")
            .WithCallback((_, _) => executedCallback = true);

        test.RunInbound();

        executedCallback.Should().BeFalse();
        logger.Events.Should().ContainSingle().Which.Value.Should().Be("test-value");
    }

    [TestMethod]
    public void LogToEventHub_CallbackFailure_IsSurfacedWithoutRecording()
    {
        var test = new ConfiguredLogToEventHub(CreateConfig()).AsTestDocument();
        var logger = test.SetupLoggerStore().Add("test-inbound");
        test.SetupInbound().LogToEventHub().WithCallback((_, _) =>
            throw new InvalidOperationException("Event Hub sink failed."));

        var act = () => test.RunInbound();

        var error = act.Should().Throw<PolicyException>().Which;
        error.Policy.Should().Be(nameof(IInboundContext.LogToEventHub));
        error.Section.Should().Be(nameof(IInboundContext));
        error.InnerException.Should().BeOfType<InvalidOperationException>()
            .Which.Message.Should().Be("Event Hub sink failed.");
        logger.Events.Should().BeEmpty();
    }

    [TestMethod]
    [DynamicData(nameof(InvalidConfigurations))]
    public void LogToEventHub_InvalidConfig_IsSurfacedWithoutRecording(LogToEventHubConfig config, string parameterName)
    {
        var test = new ConfiguredLogToEventHub(config).AsTestDocument();
        var logger = test.SetupLoggerStore().Add("test-inbound");

        var act = () => test.RunInbound();

        var error = act.Should().Throw<PolicyException>().Which;
        error.Policy.Should().Be(nameof(IInboundContext.LogToEventHub));
        error.Section.Should().Be(nameof(IInboundContext));
        error.InnerException.Should().BeAssignableTo<ArgumentException>()
            .Which.ParamName.Should().Be(parameterName);
        logger.Events.Should().BeEmpty();
    }

    public static IEnumerable<object[]> InvalidConfigurations()
    {
        var config = CreateConfig();
        yield return [null!, "args"];
        foreach (var loggerId in new string?[] { null, "", " " })
        {
            yield return [config with { LoggerId = loggerId! }, nameof(LogToEventHubConfig.LoggerId)];
        }

        yield return [config with { Value = null! }, nameof(LogToEventHubConfig.Value)];
    }

    private static LogToEventHubConfig CreateConfig() => new() { LoggerId = "test-inbound", Value = "test-value" };

    private static MockLogToEventHubProvider.Setup SetupCallback(TestDocument test, string section) => section switch
    {
        "inbound" => test.SetupInbound().LogToEventHub(),
        "outbound" => test.SetupOutbound().LogToEventHub(),
        "backend" => test.SetupBackend().LogToEventHub(),
        "on-error" => test.SetupOnError().LogToEventHub(),
        _ => throw new ArgumentException("Unknown test section.", nameof(section))
    };

    private static void RunSection(TestDocument test, string section)
    {
        switch (section)
        {
            case "inbound": test.RunInbound(); break;
            case "outbound": test.RunOutbound(); break;
            case "backend": test.RunBackend(); break;
            case "on-error": test.RunOnError(); break;
            default: throw new ArgumentException("Unknown test section.", nameof(section));
        }
    }
}