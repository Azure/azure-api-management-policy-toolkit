// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring.Expressions;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Document;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator;

namespace Test.Emulator.Emulator.Policies;

[TestClass]
public class TraceTests
{
    class TraceDocument(TraceConfig config) : IDocument
    {
        public void Inbound(IInboundContext context) => context.Trace(config);
        public void Outbound(IOutboundContext context) => context.Trace(config);
        public void Backend(IBackendContext context) => context.Trace(config);
        public void OnError(IOnErrorContext context) => context.Trace(config);
    }

    class ExpressionTrace : IDocument
    {
        public void Inbound(IInboundContext context)
        {
            context.Trace(new TraceConfig
            {
                Source = "expression",
                Message = GetMessage(context.ExpressionContext),
                Metadata =
                [
                    new TraceMetadata
                    {
                        Name = "request",
                        Value = GetRequestId(context.ExpressionContext)
                    }
                ]
            });
        }

        [Expression]
        static string GetMessage(IExpressionContext context) => (string)context.Variables["message"];

        [Expression]
        static string GetRequestId(IExpressionContext context) => context.RequestId.ToString();
    }

    [TestMethod]
    [DataRow("inbound")]
    [DataRow("outbound")]
    [DataRow("backend")]
    [DataRow("on-error")]
    public void Trace_RequiredConfig_RecordsEverySection(string section)
    {
        var test = new TraceDocument(CreateConfig()).AsTestDocument();
        var store = test.SetupLoggerStore();

        RunSection(test, section);

        var trace = store.Traces.Should().ContainSingle().Which;
        trace.Source.Should().Be("test-source");
        trace.Message.Should().Be("test-message");
        trace.Severity.Should().Be("verbose");
        trace.Metadata.Should().BeEmpty();
        test.Context.Tracing.Should().BeFalse();
    }

    [TestMethod]
    [DataRow("verbose")]
    [DataRow("information")]
    [DataRow("error")]
    public void Trace_RecordsSeverityAndMetadata(string severity)
    {
        var config = CreateConfig() with
        {
            Severity = severity,
            Metadata =
            [
                new TraceMetadata { Name = "operation", Value = "create-order" },
                new TraceMetadata { Name = "correlation", Value = "123" }
            ]
        };
        var test = new TraceDocument(config).AsTestDocument();

        test.RunInbound();

        var trace = test.SetupLoggerStore().Traces.Should().ContainSingle().Which;
        trace.Severity.Should().Be(severity);
        trace.Metadata.Should().BeEquivalentTo(new Dictionary<string, string>
        {
            ["operation"] = "create-order",
            ["correlation"] = "123"
        });
    }

    [TestMethod]
    public void Trace_AllowsEmptyMessageAndMetadataValue()
    {
        var config = CreateConfig() with
        {
            Message = "",
            Metadata = [new TraceMetadata { Name = "empty", Value = "" }]
        };
        var test = new TraceDocument(config).AsTestDocument();

        test.RunInbound();

        var trace = test.SetupLoggerStore().Traces.Should().ContainSingle().Which;
        trace.Message.Should().BeEmpty();
        trace.Metadata["empty"].Should().BeEmpty();
    }

    [TestMethod]
    public void Trace_Expressions_AreEvaluatedForEachInvocation()
    {
        var test = new ExpressionTrace().AsTestDocument();
        var store = test.SetupLoggerStore();
        test.Context.Variables["message"] = "first";

        test.RunInbound();
        test.Context.Variables["message"] = "second";
        test.RunInbound();

        store.Traces.Select(trace => trace.Message).Should().Equal("first", "second");
        store.Traces.Should().OnlyContain(trace =>
            trace.Metadata["request"] == test.Context.RequestId.ToString());
    }

    [TestMethod]
    public void Trace_RecordsAndStoreSnapshots_AreIndependentOfLaterChanges()
    {
        var metadata = new[] { new TraceMetadata { Name = "tag", Value = "before" } };
        var test = new TraceDocument(CreateConfig() with { Metadata = metadata }).AsTestDocument();
        var store = test.SetupLoggerStore();

        test.RunInbound();
        var snapshot = store.Traces;
        metadata[0] = new TraceMetadata { Name = "tag", Value = "after" };
        test.RunInbound();

        snapshot.Should().ContainSingle();
        snapshot[0].Metadata["tag"].Should().Be("before");
        store.Traces.Select(trace => trace.Metadata["tag"]).Should().Equal("before", "after");
    }

    [TestMethod]
    public void Trace_Stores_AreIsolatedBetweenContexts()
    {
        var first = new TraceDocument(CreateConfig()).AsTestDocument();
        var second = new TraceDocument(CreateConfig()).AsTestDocument();

        first.RunInbound();

        first.SetupLoggerStore().Traces.Should().ContainSingle();
        second.SetupLoggerStore().Traces.Should().BeEmpty();
    }

    [TestMethod]
    [DataRow("inbound")]
    [DataRow("outbound")]
    [DataRow("backend")]
    [DataRow("on-error")]
    public void Trace_Callback_OverridesRecordingInEverySection(string section)
    {
        var config = CreateConfig();
        var test = new TraceDocument(config).AsTestDocument();
        SetupCallback(test, section).WithCallback((context, observedConfig) =>
        {
            observedConfig.Should().BeSameAs(config);
            context.Variables["callback"] = section;
        });

        RunSection(test, section);

        test.Context.Variables["callback"].Should().Be(section);
        test.SetupLoggerStore().Traces.Should().BeEmpty();
    }

    [TestMethod]
    public void Trace_UnmatchedCallback_UsesDefaultRecording()
    {
        var test = new TraceDocument(CreateConfig()).AsTestDocument();
        var executedCallback = false;
        test.SetupInbound().Trace((_, config) => config.Source == "other-source")
            .WithCallback((_, _) => executedCallback = true);

        test.RunInbound();

        executedCallback.Should().BeFalse();
        test.SetupLoggerStore().Traces.Should().ContainSingle();
    }

    [TestMethod]
    public void Trace_Callback_CanOverrideInvalidConfiguration()
    {
        var test = new TraceDocument(CreateConfig() with { Severity = "mock-severity" }).AsTestDocument();
        test.SetupInbound().Trace().WithCallback((context, _) => context.Variables["callback"] = true);

        test.RunInbound();

        test.Context.Variables["callback"].Should().Be(true);
        test.SetupLoggerStore().Traces.Should().BeEmpty();
    }

    [TestMethod]
    public void Trace_CallbackFailure_IsSurfacedWithoutRecording()
    {
        var test = new TraceDocument(CreateConfig()).AsTestDocument();
        test.SetupInbound().Trace().WithCallback((_, _) =>
            throw new InvalidOperationException("Trace sink failed."));

        var act = () => test.RunInbound();

        var error = act.Should().Throw<PolicyException>().Which;
        error.Policy.Should().Be(nameof(IInboundContext.Trace));
        error.Section.Should().Be(nameof(IInboundContext));
        error.InnerException.Should().BeOfType<InvalidOperationException>()
            .Which.Message.Should().Be("Trace sink failed.");
        test.SetupLoggerStore().Traces.Should().BeEmpty();
    }

    [TestMethod]
    [DynamicData(nameof(InvalidConfigurations))]
    public void Trace_InvalidConfig_IsSurfacedWithoutRecording(TraceConfig config, string parameterName)
    {
        var test = new TraceDocument(config).AsTestDocument();

        var act = () => test.RunInbound();

        var error = act.Should().Throw<PolicyException>().Which;
        error.Policy.Should().Be(nameof(IInboundContext.Trace));
        error.Section.Should().Be(nameof(IInboundContext));
        error.InnerException.Should().BeAssignableTo<ArgumentException>()
            .Which.ParamName.Should().Be(parameterName);
        test.SetupLoggerStore().Traces.Should().BeEmpty();
    }

    public static IEnumerable<object[]> InvalidConfigurations()
    {
        var config = CreateConfig();
        yield return [null!, "args"];
        foreach (var source in new string?[] { null, "", " " })
        {
            yield return [config with { Source = source! }, nameof(TraceConfig.Source)];
        }

        yield return [config with { Message = null! }, nameof(TraceConfig.Message)];
        foreach (var severity in new[] { "", "warning", "Verbose" })
        {
            yield return [config with { Severity = severity }, nameof(TraceConfig.Severity)];
        }

        yield return [config with { Metadata = [null!] }, nameof(TraceConfig.Metadata)];
        foreach (var name in new string?[] { null, "", " " })
        {
            yield return
            [
                config with { Metadata = [new TraceMetadata { Name = name!, Value = "value" }] },
                nameof(TraceConfig.Metadata)
            ];
        }

        yield return
        [
            config with { Metadata = [new TraceMetadata { Name = "tag", Value = null! }] },
            nameof(TraceConfig.Metadata)
        ];
        yield return
        [
            config with
            {
                Metadata =
                [
                    new TraceMetadata { Name = "tag", Value = "first" },
                    new TraceMetadata { Name = "tag", Value = "second" }
                ]
            },
            nameof(TraceConfig.Metadata)
        ];
    }

    private static TraceConfig CreateConfig() => new() { Source = "test-source", Message = "test-message" };

    private static MockTraceProvider.Setup SetupCallback(TestDocument test, string section) => section switch
    {
        "inbound" => test.SetupInbound().Trace(),
        "outbound" => test.SetupOutbound().Trace(),
        "backend" => test.SetupBackend().Trace(),
        "on-error" => test.SetupOnError().Trace(),
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