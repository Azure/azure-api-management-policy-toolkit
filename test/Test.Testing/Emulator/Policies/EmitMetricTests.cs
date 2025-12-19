// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Document;

namespace Test.Emulator.Emulator.Policies;

[TestClass]
public class EmitMetricTests
{
    public class SimpleEmitMetric : IDocument
    {
        public void Inbound(IInboundContext context)
        {
            context.EmitMetric(new EmitMetricConfig
            {
                Name = "TestMetric",
                Dimensions =
                [
                    new MetricDimensionConfig { Name = "TestDimension", Value = "TestValue" }
                ],
                Namespace = "TestNamespace",
                Value = 1.5
            });
        }

        public void Outbound(IOutboundContext context)
        {
            context.EmitMetric(new EmitMetricConfig
            {
                Name = "TestMetric",
                Dimensions =
                [
                    new MetricDimensionConfig { Name = "TestDimension", Value = "TestValue" }
                ],
                Namespace = "TestNamespace",
                Value = 1.5
            });
        }

        public void OnError(IOnErrorContext context)
        {
            context.EmitMetric(new EmitMetricConfig
            {
                Name = "TestMetric",
                Dimensions =
                [
                    new MetricDimensionConfig { Name = "TestDimension", Value = "TestValue" }
                ],
                Namespace = "TestNamespace",
                Value = 1.5
            });
        }
    }

    public class MinimalEmitMetric : IDocument
    {
        public void Inbound(IInboundContext context)
        {
            context.EmitMetric(new EmitMetricConfig
            {
                Name = "TestMetric",
                Dimensions =
                [
                    new MetricDimensionConfig { Name = "TestDimension" }
                ]
            });
        }
    }

    [TestMethod]
    public void EmitMetric_Inbound()
    {
        var test = new SimpleEmitMetric().AsTestDocument();
        var diagnosticStore = test.SetupDiagnosticStore();
        diagnosticStore.Enabled = true;

        test.RunInbound();

        var metric = diagnosticStore.Metrics.Should().ContainSingle().Subject;
        metric.Should().NotBeNull();
        metric.Name.Should().Be("TestMetric");
        metric.Namespace.Should().Be("TestNamespace");
        metric.Value.Should().Be(1.5);
        var dimension = metric.Dimensions.Should().ContainSingle().Subject;
        dimension.Name.Should().Be("TestDimension");
        dimension.Value.Should().Be("TestValue");
    }

    [TestMethod]
    public void EmitMetric_Outbound()
    {
        var test = new SimpleEmitMetric().AsTestDocument();
        var diagnosticStore = test.SetupDiagnosticStore();
        diagnosticStore.Enabled = true;

        test.RunOutbound();

        var metric = diagnosticStore.Metrics.Should().ContainSingle().Subject;
        metric.Should().NotBeNull();
        metric.Name.Should().Be("TestMetric");
        metric.Namespace.Should().Be("TestNamespace");
        metric.Value.Should().Be(1.5);
        var dimension = metric.Dimensions.Should().ContainSingle().Subject;
        dimension.Name.Should().Be("TestDimension");
        dimension.Value.Should().Be("TestValue");
    }

    [TestMethod]
    public void EmitMetric_OnError()
    {
        var test = new SimpleEmitMetric().AsTestDocument();
        var diagnosticStore = test.SetupDiagnosticStore();
        diagnosticStore.Enabled = true;

        test.RunOnError();

        var metric = diagnosticStore.Metrics.Should().ContainSingle().Subject;
        metric.Should().NotBeNull();
        metric.Name.Should().Be("TestMetric");
        metric.Namespace.Should().Be("TestNamespace");
        metric.Value.Should().Be(1.5);
        var dimension = metric.Dimensions.Should().ContainSingle().Subject;
        dimension.Name.Should().Be("TestDimension");
        dimension.Value.Should().Be("TestValue");
    }

    [TestMethod]
    public void EmitMetric_NotCollectWhenDisabled()
    {
        var test = new SimpleEmitMetric().AsTestDocument();
        var diagnosticStore = test.SetupDiagnosticStore();

        test.RunInbound();

        diagnosticStore.Metrics.Should().BeEmpty();
    }

    [TestMethod]
    public void EmitMetric_FillDefaults()
    {
        var test = new MinimalEmitMetric().AsTestDocument();
        var diagnosticStore = test.SetupDiagnosticStore();
        diagnosticStore.Enabled = true;

        test.RunInbound();

        var metric = diagnosticStore.Metrics.Should().ContainSingle().Subject;
        metric.Should().NotBeNull();
        metric.Name.Should().Be("TestMetric");
        metric.Namespace.Should().Be("API Management");
        metric.Value.Should().Be(1.0);
        var dimension = metric.Dimensions.Should().ContainSingle().Subject;
        dimension.Name.Should().Be("TestDimension");
        dimension.Value.Should().Be("N/A");
    }
}