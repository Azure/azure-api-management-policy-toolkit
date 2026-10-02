// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Document;

namespace Test.Emulator.Emulator.Policies;

[TestClass]
public class WaitTelemetryTests
{
    private const int EventCount = 2000;

    private sealed class TelemetryDocument : IDocument
    {
        public void Inbound(IInboundContext context)
        {
            context.LogToEventHub(new LogToEventHubConfig
            {
                LoggerId = "wait-telemetry",
                Value = "message"
            });
            context.Trace(new TraceConfig { Source = "wait", Message = "message" });
            context.EmitMetric(new EmitMetricConfig
            {
                Name = "wait",
                Dimensions = [new MetricDimensionConfig { Name = "API ID", Value = "test" }]
            });
        }
    }

    [TestMethod]
    public async Task SnapshotsRemainStableWhilePoliciesPublish()
    {
        var test = new TelemetryDocument().AsTestDocument();
        var store = test.SetupLoggerStore();
        var logger = store.Add("wait-telemetry");
        using var start = new ManualResetEventSlim();
        var writer = Task.Run(() =>
        {
            start.Wait();
            for (var i = 0; i < EventCount; i++)
            {
                test.RunInbound();
            }
        });

        start.Set();
        var reads = 0;
        while (!writer.IsCompleted)
        {
            logger.Events.Length.Should().BeLessThanOrEqualTo(EventCount);
            store.Traces.Length.Should().BeLessThanOrEqualTo(EventCount);
            store.Metrics.Length.Should().BeLessThanOrEqualTo(EventCount);
            reads++;
            Thread.Yield();
        }

        await writer.WaitAsync(TimeSpan.FromSeconds(30));
        reads.Should().BeGreaterThan(0);
        logger.Events.Should().HaveCount(EventCount);
        store.Traces.Should().HaveCount(EventCount);
        store.Metrics.Should().HaveCount(EventCount);
    }
}
