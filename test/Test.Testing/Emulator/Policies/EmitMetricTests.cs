// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Document;

namespace Test.Emulator.Emulator.Policies;

[TestClass]
public class EmitMetricTests
{
    class SimpleEmitMetric : IDocument
    {
        public void Inbound(IInboundContext context)
        {
            context.EmitMetric(new EmitMetricConfig
            {
                Name = "my-metric",
                Value = 1,
                Dimensions = [new MetricDimensionConfig { Name = "region", Value = "west" }]
            });
        }

        public void Outbound(IOutboundContext context)
        {
            context.EmitMetric(new EmitMetricConfig
            {
                Name = "outbound-metric",
                Value = 2.5,
                Dimensions = [new MetricDimensionConfig { Name = "status" }]
            });
        }

        public void OnError(IOnErrorContext context)
        {
            context.EmitMetric(new EmitMetricConfig
            {
                Name = "error-metric",
                Value = 1,
                Dimensions = [new MetricDimensionConfig { Name = "error-type" }]
            });
        }
    }

    class MultiEmitMetric : IDocument
    {
        public void Inbound(IInboundContext context)
        {
            context.EmitMetric(new EmitMetricConfig
            {
                Name = "metric-a",
                Dimensions = [new MetricDimensionConfig { Name = "dim" }]
            });
            context.EmitMetric(new EmitMetricConfig
            {
                Name = "metric-b",
                Value = 5,
                Dimensions = [new MetricDimensionConfig { Name = "dim" }]
            });
        }
    }

    [TestMethod]
    public void EmitMetric_Inbound()
    {
        var test = new SimpleEmitMetric().AsTestDocument();

        test.RunInbound();

        var metrics = test.SetupMetricStore().Metrics;
        metrics.Should().HaveCount(1);
        metrics[0].Name.Should().Be("my-metric");
        metrics[0].Value.Should().Be(1);
        metrics[0].Dimensions.Should().HaveCount(1);
        metrics[0].Dimensions[0].Name.Should().Be("region");
    }

    [TestMethod]
    public void EmitMetric_Outbound()
    {
        var test = new SimpleEmitMetric().AsTestDocument();

        test.RunOutbound();

        var metrics = test.SetupMetricStore().Metrics;
        metrics.Should().HaveCount(1);
        metrics[0].Name.Should().Be("outbound-metric");
        metrics[0].Value.Should().Be(2.5);
    }

    [TestMethod]
    public void EmitMetric_OnError()
    {
        var test = new SimpleEmitMetric().AsTestDocument();

        test.RunOnError();

        var metrics = test.SetupMetricStore().Metrics;
        metrics.Should().HaveCount(1);
        metrics[0].Name.Should().Be("error-metric");
    }

    [TestMethod]
    public void EmitMetric_DefaultValueIsOne()
    {
        var test = new MultiEmitMetric().AsTestDocument();

        test.RunInbound();

        var metrics = test.SetupMetricStore().Metrics;
        metrics[0].Value.Should().Be(1);
    }

    [TestMethod]
    public void EmitMetric_MultipleMetricsCollected()
    {
        var test = new MultiEmitMetric().AsTestDocument();

        test.RunInbound();

        var metrics = test.SetupMetricStore().Metrics;
        metrics.Should().HaveCount(2);
        metrics[0].Name.Should().Be("metric-a");
        metrics[1].Name.Should().Be("metric-b");
        metrics[1].Value.Should().Be(5);
    }

    [TestMethod]
    public void EmitMetric_Callback()
    {
        var test = new SimpleEmitMetric().AsTestDocument();
        var callbackExecuted = false;
        test.SetupInbound().EmitMetric().WithCallback((_, config) =>
        {
            callbackExecuted = true;
            config.Name.Should().Be("my-metric");
        });

        test.RunInbound();

        callbackExecuted.Should().BeTrue();
        test.SetupMetricStore().Metrics.Should().BeEmpty();
    }
}
