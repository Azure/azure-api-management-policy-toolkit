// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring.Expressions;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Document;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator;

namespace Test.Emulator.Emulator.Policies;

[TestClass]
public class EmitMetricTests
{
    class MetricDocument(EmitMetricConfig config) : IDocument
    {
        public void Inbound(IInboundContext context) => context.EmitMetric(config);
        public void Outbound(IOutboundContext context) => context.EmitMetric(config);
        public void OnError(IOnErrorContext context) => context.EmitMetric(config);
    }

    class ExpressionMetric : IDocument
    {
        public void Inbound(IInboundContext context)
        {
            context.EmitMetric(new EmitMetricConfig
            {
                Name = "expression",
                Value = GetValue(context.ExpressionContext),
                Dimensions =
                [
                    new MetricDimensionConfig { Name = "request", Value = GetRequestId(context.ExpressionContext) },
                    new MetricDimensionConfig { Name = "tag", Value = GetTag(context.ExpressionContext) }
                ]
            });
        }

        [Expression]
        static double GetValue(IExpressionContext context) => (double)context.Variables["metric-value"];

        [Expression]
        static string GetRequestId(IExpressionContext context) => context.RequestId.ToString();

        [Expression]
        static string GetTag(IExpressionContext context) => (string)context.Variables["tag"];
    }

    [TestMethod]
    [DataRow("inbound")]
    [DataRow("outbound")]
    [DataRow("on-error")]
    public void EmitMetric_RequiredConfig_RecordsEveryExposedSection(string section)
    {
        var test = new MetricDocument(CreateConfig()).AsTestDocument();
        test.Context.Api.Id = "test-api";

        RunSection(test, section);

        var metric = test.SetupLoggerStore().Metrics.Should().ContainSingle().Which;
        metric.Name.Should().Be("test-metric");
        metric.Namespace.Should().Be("apim");
        metric.Value.Should().Be(1);
        metric.Dimensions.Should().BeEquivalentTo(new Dictionary<string, string?> { ["API ID"] = "test-api" });
    }

    [TestMethod]
    public void EmitMetric_RecordsNamespaceValueAndCustomDimensions()
    {
        var config = CreateConfig() with
        {
            Namespace = "contoso.metrics",
            Value = 12.5,
            Dimensions =
            [
                new MetricDimensionConfig { Name = "model", Value = "test-model" },
                new MetricDimensionConfig { Name = "region", Value = "west" }
            ]
        };
        var test = new MetricDocument(config).AsTestDocument();

        test.RunInbound();

        var metric = test.SetupLoggerStore().Metrics.Should().ContainSingle().Which;
        metric.Namespace.Should().Be("contoso.metrics");
        metric.Name.Should().Be("test-metric");
        metric.Value.Should().Be(12.5);
        metric.Dimensions.Should().BeEquivalentTo(new Dictionary<string, string?>
        {
            ["model"] = "test-model",
            ["region"] = "west"
        });
    }

    [TestMethod]
    [DataRow("API ID", "test-api")]
    [DataRow("Operation ID", "test-operation")]
    [DataRow("Product ID", "test-product")]
    [DataRow("User ID", "test-user")]
    [DataRow("Subscription ID", "test-subscription")]
    [DataRow("Location", "westeurope")]
    [DataRow("Gateway ID", "test-gateway")]
    public void EmitMetric_DefaultDimension_UsesExpressionContext(string name, string expectedValue)
    {
        var config = CreateConfig() with { Dimensions = [new MetricDimensionConfig { Name = name }] };
        var test = new MetricDocument(config).AsTestDocument();
        test.Context.Api.Id = "test-api";
        test.Context.Operation.Id = "test-operation";
        test.Context.Product.Id = "test-product";
        test.Context.User.Id = "test-user";
        test.Context.Subscription.Id = "test-subscription";
        test.Context.Deployment.Region = "westeurope";
        test.Context.Deployment.GatewayId = "test-gateway";

        test.RunInbound();

        var metric = test.SetupLoggerStore().Metrics.Should().ContainSingle().Which;
        metric.Dimensions.Should().ContainSingle();
        metric.Dimensions[name].Should().Be(expectedValue);
    }

    [TestMethod]
    public void EmitMetric_ExplicitDefaultDimensionValue_OverridesContext()
    {
        var config = CreateConfig() with
        {
            Dimensions = [new MetricDimensionConfig { Name = "API ID", Value = "explicit-api" }]
        };
        var test = new MetricDocument(config).AsTestDocument();

        test.RunInbound();

        test.SetupLoggerStore().Metrics.Single().Dimensions["API ID"].Should().Be("explicit-api");
    }

    [TestMethod]
    public void EmitMetric_BackendIdWithoutValue_ReportsUnsupportedContext()
    {
        var config = CreateConfig() with { Dimensions = [new MetricDimensionConfig { Name = "Backend ID" }] };
        var test = new MetricDocument(config).AsTestDocument();

        var act = () => test.RunOutbound();

        var error = act.Should().Throw<PolicyException>().Which;
        error.Policy.Should().Be(nameof(IOutboundContext.EmitMetric));
        error.InnerException.Should().BeOfType<NotSupportedException>()
            .Which.Message.Should().Contain("Backend ID").And.Contain("Value");
        test.SetupLoggerStore().Metrics.Should().BeEmpty();
    }

    [TestMethod]
    public void EmitMetric_BackendIdWithExplicitValue_IsRecorded()
    {
        var config = CreateConfig() with
        {
            Dimensions = [new MetricDimensionConfig { Name = "Backend ID", Value = "orders-backend" }]
        };
        var test = new MetricDocument(config).AsTestDocument();

        test.RunOutbound();

        test.SetupLoggerStore().Metrics.Single().Dimensions["Backend ID"].Should().Be("orders-backend");
    }

    [TestMethod]
    [DataRow(0.0)]
    [DataRow(-42.5)]
    [DataRow(double.MinValue)]
    [DataRow(double.MaxValue)]
    public void EmitMetric_AllowsFiniteValues(double value)
    {
        var test = new MetricDocument(CreateConfig() with { Value = value }).AsTestDocument();

        test.RunInbound();

        test.SetupLoggerStore().Metrics.Single().Value.Should().Be(value);
    }

    [TestMethod]
    public void EmitMetric_AllowsFiveDimensionsAndEmptyExplicitValues()
    {
        var dimensions = Enumerable.Range(0, 5)
            .Select(index => new MetricDimensionConfig { Name = $"dimension-{index}", Value = "" }).ToArray();
        var test = new MetricDocument(CreateConfig() with { Dimensions = dimensions }).AsTestDocument();

        test.RunInbound();

        var metric = test.SetupLoggerStore().Metrics.Should().ContainSingle().Which;
        metric.Dimensions.Should().HaveCount(5);
        metric.Dimensions.Values.Should().OnlyContain(value => value == "");
    }

    [TestMethod]
    public void EmitMetric_Expressions_AreEvaluatedForEachInvocation()
    {
        var test = new ExpressionMetric().AsTestDocument();
        test.Context.Variables["metric-value"] = 5.25;
        test.Context.Variables["tag"] = "first";

        test.RunInbound();
        test.Context.Variables["metric-value"] = 12.5;
        test.Context.Variables["tag"] = "second";
        test.RunInbound();

        var metrics = test.SetupLoggerStore().Metrics;
        metrics.Select(metric => metric.Value).Should().Equal(5.25, 12.5);
        metrics.Select(metric => metric.Dimensions["tag"]).Should().Equal("first", "second");
        metrics.Should().OnlyContain(metric => metric.Dimensions["request"] == test.Context.RequestId.ToString());
    }

    [TestMethod]
    public void EmitMetric_RecordsAndStoreSnapshots_AreIndependentOfLaterChanges()
    {
        var dimensions = new[] { new MetricDimensionConfig { Name = "tag", Value = "before" } };
        var test = new MetricDocument(CreateConfig() with { Dimensions = dimensions }).AsTestDocument();
        var store = test.SetupLoggerStore();

        test.RunInbound();
        var snapshot = store.Metrics;
        dimensions[0] = new MetricDimensionConfig { Name = "tag", Value = "after" };
        test.RunInbound();

        snapshot.Should().ContainSingle();
        snapshot[0].Dimensions["tag"].Should().Be("before");
        store.Metrics.Select(metric => metric.Dimensions["tag"]).Should().Equal("before", "after");
    }

    [TestMethod]
    public void EmitMetric_Stores_AreIsolatedBetweenContexts()
    {
        var first = new MetricDocument(CreateConfig()).AsTestDocument();
        var second = new MetricDocument(CreateConfig()).AsTestDocument();

        first.RunInbound();

        first.SetupLoggerStore().Metrics.Should().ContainSingle();
        second.SetupLoggerStore().Metrics.Should().BeEmpty();
    }

    [TestMethod]
    [DataRow("inbound")]
    [DataRow("outbound")]
    [DataRow("on-error")]
    public void EmitMetric_Callback_OverridesRecordingInEveryExposedSection(string section)
    {
        var config = CreateConfig();
        var test = new MetricDocument(config).AsTestDocument();
        SetupCallback(test, section).WithCallback((context, observedConfig) =>
        {
            observedConfig.Should().BeSameAs(config);
            context.Variables["callback"] = section;
        });

        RunSection(test, section);

        test.Context.Variables["callback"].Should().Be(section);
        test.SetupLoggerStore().Metrics.Should().BeEmpty();
    }

    [TestMethod]
    public void EmitMetric_UnmatchedCallback_UsesDefaultRecording()
    {
        var test = new MetricDocument(CreateConfig()).AsTestDocument();
        var executedCallback = false;
        test.SetupInbound().EmitMetric((_, config) => config.Name == "other-metric")
            .WithCallback((_, _) => executedCallback = true);

        test.RunInbound();

        executedCallback.Should().BeFalse();
        test.SetupLoggerStore().Metrics.Should().ContainSingle();
    }

    [TestMethod]
    public void EmitMetric_Callback_CanOverrideInvalidConfiguration()
    {
        var test = new MetricDocument(CreateConfig() with { Value = double.NaN }).AsTestDocument();
        test.SetupInbound().EmitMetric().WithCallback((context, _) => context.Variables["callback"] = true);

        test.RunInbound();

        test.Context.Variables["callback"].Should().Be(true);
        test.SetupLoggerStore().Metrics.Should().BeEmpty();
    }

    [TestMethod]
    public void EmitMetric_CallbackFailure_IsSurfacedWithoutRecording()
    {
        var test = new MetricDocument(CreateConfig()).AsTestDocument();
        test.SetupInbound().EmitMetric().WithCallback((_, _) =>
            throw new InvalidOperationException("Metric sink failed."));

        var act = () => test.RunInbound();

        var error = act.Should().Throw<PolicyException>().Which;
        error.Policy.Should().Be(nameof(IInboundContext.EmitMetric));
        error.Section.Should().Be(nameof(IInboundContext));
        error.InnerException.Should().BeOfType<InvalidOperationException>()
            .Which.Message.Should().Be("Metric sink failed.");
        test.SetupLoggerStore().Metrics.Should().BeEmpty();
    }

    [TestMethod]
    [DynamicData(nameof(InvalidConfigurations))]
    public void EmitMetric_InvalidConfig_IsSurfacedWithoutRecording(EmitMetricConfig config, string parameterName)
    {
        var test = new MetricDocument(config).AsTestDocument();

        var act = () => test.RunInbound();

        var error = act.Should().Throw<PolicyException>().Which;
        error.Policy.Should().Be(nameof(IInboundContext.EmitMetric));
        error.Section.Should().Be(nameof(IInboundContext));
        error.InnerException.Should().BeAssignableTo<ArgumentException>()
            .Which.ParamName.Should().Be(parameterName);
        test.SetupLoggerStore().Metrics.Should().BeEmpty();
    }

    public static IEnumerable<object[]> InvalidConfigurations()
    {
        var config = CreateConfig();
        yield return [null!, "args"];
        foreach (var name in new string?[] { null, "", " " })
        {
            yield return [config with { Name = name! }, nameof(EmitMetricConfig.Name)];
        }

        foreach (var metricNamespace in new[] { "", " " })
        {
            yield return [config with { Namespace = metricNamespace }, nameof(EmitMetricConfig.Namespace)];
        }

        foreach (var value in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        {
            yield return [config with { Value = value }, nameof(EmitMetricConfig.Value)];
        }

        yield return [config with { Dimensions = null! }, nameof(EmitMetricConfig.Dimensions)];
        yield return [config with { Dimensions = [] }, nameof(EmitMetricConfig.Dimensions)];
        yield return [config with { Dimensions = [null!] }, nameof(EmitMetricConfig.Dimensions)];
        foreach (var name in new string?[] { null, "", " " })
        {
            yield return
            [
                config with { Dimensions = [new MetricDimensionConfig { Name = name!, Value = "value" }] },
                nameof(EmitMetricConfig.Dimensions)
            ];
        }

        yield return
        [
            config with { Dimensions = [new MetricDimensionConfig { Name = "custom" }] },
            nameof(EmitMetricConfig.Dimensions)
        ];
        yield return
        [
            config with
            {
                Dimensions =
                [
                    new MetricDimensionConfig { Name = "tag", Value = "first" },
                    new MetricDimensionConfig { Name = "tag", Value = "second" }
                ]
            },
            nameof(EmitMetricConfig.Dimensions)
        ];
        yield return
        [
            config with
            {
                Dimensions = Enumerable.Range(0, 6)
                    .Select(index => new MetricDimensionConfig { Name = $"dimension-{index}", Value = "value" })
                    .ToArray()
            },
            nameof(EmitMetricConfig.Dimensions)
        ];
    }

    private static EmitMetricConfig CreateConfig() => new()
    {
        Name = "test-metric",
        Dimensions = [new MetricDimensionConfig { Name = "API ID" }]
    };

    private static MockEmitMetricProvider.Setup SetupCallback(TestDocument test, string section) => section switch
    {
        "inbound" => test.SetupInbound().EmitMetric(),
        "outbound" => test.SetupOutbound().EmitMetric(),
        "on-error" => test.SetupOnError().EmitMetric(),
        _ => throw new ArgumentException("Unknown test section.", nameof(section))
    };

    private static void RunSection(TestDocument test, string section)
    {
        switch (section)
        {
            case "inbound": test.RunInbound(); break;
            case "outbound": test.RunOutbound(); break;
            case "on-error": test.RunOnError(); break;
            default: throw new ArgumentException("Unknown test section.", nameof(section));
        }
    }
}