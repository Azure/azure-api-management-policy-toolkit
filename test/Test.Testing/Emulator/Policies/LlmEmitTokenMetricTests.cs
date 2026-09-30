// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Immutable;
using System.Text.Json;

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring.Expressions;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Document;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Data;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Services;

namespace Test.Emulator.Emulator.Policies;

[TestClass]
public class LlmEmitTokenMetricTests
{
    protected virtual bool UseAzureOpenAi => false;

    private string ExpectedPolicy => UseAzureOpenAi
        ? nameof(IInboundContext.AzureOpenAiEmitTokenMetric)
        : nameof(IInboundContext.LlmEmitTokenMetric);

    class MetricDocument(EmitTokenMetricConfig config, bool useAzureOpenAi) : IDocument
    {
        public void Inbound(IInboundContext context)
        {
            if (useAzureOpenAi)
            {
                context.AzureOpenAiEmitTokenMetric(config);
            }
            else
            {
                context.LlmEmitTokenMetric(config);
            }
        }
    }

    class ExpressionMetricDocument(bool useAzureOpenAi) : IDocument
    {
        public void Inbound(IInboundContext context)
        {
            var config = new EmitTokenMetricConfig
            {
                Namespace = GetNamespace(context.ExpressionContext),
                Dimensions = GetDimensions(context.ExpressionContext)
            };
            if (useAzureOpenAi)
            {
                context.AzureOpenAiEmitTokenMetric(config);
            }
            else
            {
                context.LlmEmitTokenMetric(config);
            }
        }

        [Expression]
        static string GetNamespace(IExpressionContext context) => (string)context.Variables["namespace"];

        [Expression]
        static MetricDimensionConfig[] GetDimensions(IExpressionContext context) =>
        [
            new MetricDimensionConfig
            {
                Name = (string)context.Variables["dimension-name"],
                Value = (string)context.Variables["dimension-value"]
            },
            new MetricDimensionConfig { Name = "API ID" }
        ];
    }

    class UsageProvider(Func<GatewayContext, LlmTokenUsage?> getUsage) : ILlmTokenUsageProvider
    {
        public int CallCount { get; private set; }
        public GatewayContext? LastContext { get; private set; }

        public LlmTokenUsage? GetUsage(GatewayContext context)
        {
            CallCount++;
            LastContext = context;
            return getUsage(context);
        }
    }

    [TestMethod]
    public void EmitTokenMetric_ResponseUsage_RecordsExactCountsAndPreservesBody()
    {
        var test = CreateTest(CreateConfig());
        test.Context.Api.Id = "test-api";
        var body = """{"usage":{"prompt_tokens":61,"completion_tokens":22,"total_tokens":83}}""";
        test.Context.Response.Body.Content = body;

        test.RunInbound();

        AssertMetrics(test, "API Management",
            [("Total Tokens", 83), ("Prompt Tokens", 61), ("Completion Tokens", 22)],
            new Dictionary<string, string?> { ["API ID"] = "test-api" });
        test.Context.Response.Body.Content.Should().Be(body);
    }

    [TestMethod]
    public void EmitTokenMetric_ProviderUsage_RecordsNamespaceDimensionsAndCategories()
    {
        var config = CreateConfig() with
        {
            Namespace = "contoso.llm",
            Dimensions =
            [
                new MetricDimensionConfig { Name = "API ID" },
                new MetricDimensionConfig { Name = "model", Value = "offline-model" }
            ]
        };
        var test = CreateTest(config);
        test.Context.Api.Id = "llm-api";
        var provider = new UsageProvider(_ => new LlmTokenUsage
        {
            TotalTokens = 451,
            PromptTokens = 310,
            CompletionTokens = 141,
            AdditionalTokens = new Dictionary<string, long>
            {
                ["Thinking Tokens"] = 29,
                ["Reasoning Tokens"] = 53,
                ["Cached Tokens"] = 47,
                ["Other Tokens"] = 17
            }
        });
        test.Context.Services.Register<ILlmTokenUsageProvider>(provider);

        test.RunInbound();

        AssertMetrics(test, "contoso.llm",
            [
                ("Total Tokens", 451), ("Prompt Tokens", 310), ("Completion Tokens", 141),
                ("Cached Tokens", 47), ("Other Tokens", 17), ("Reasoning Tokens", 53), ("Thinking Tokens", 29)
            ],
            new Dictionary<string, string?> { ["API ID"] = "llm-api", ["model"] = "offline-model" });
        provider.CallCount.Should().Be(1);
        provider.LastContext.Should().BeSameAs(test.Context);
    }

    [TestMethod]
    public void EmitTokenMetric_ResponseUsage_TakesPrecedenceOverProvider()
    {
        var test = CreateTest(CreateConfig());
        test.Context.Response.Body.Content = """{"usage":{"total_tokens":97,"prompt_tokens":67,"completion_tokens":30}}""";
        var provider = new UsageProvider(_ => throw new InvalidOperationException("The model must not be called."));
        test.Context.Services.Register<ILlmTokenUsageProvider>(provider);

        test.RunInbound();

        AssertMetrics(test, "API Management",
            [("Total Tokens", 97), ("Prompt Tokens", 67), ("Completion Tokens", 30)], DefaultDimensions(test));
        provider.CallCount.Should().Be(0);
    }

    [TestMethod]
    public void EmitTokenMetric_ResponsesApi_RecordsInputOutputAndAvailableCategories()
    {
        var test = CreateTest(CreateConfig());
        test.Context.Response.Body.Content = """
            {
              "usage": {
                "input_tokens": 101,
                "output_tokens": 50,
                "total_tokens": 151,
                "input_tokens_details": {"cached_tokens": 23},
                "output_tokens_details": {"reasoning_tokens": 19},
                "thinking_tokens": 7,
                "provider_tokens": 13,
                "service_tier": "default"
              }
            }
            """;

        test.RunInbound();

        AssertMetrics(test, "API Management",
            [
                ("Total Tokens", 151), ("Prompt Tokens", 101), ("Completion Tokens", 50),
                ("Cached Tokens", 23), ("Reasoning Tokens", 19), ("Thinking Tokens", 7), ("provider_tokens", 13)
            ],
            DefaultDimensions(test));
    }

    [TestMethod]
    public void EmitTokenMetric_ChatCompletions_RecordsReportedDetailCategories()
    {
        var test = CreateTest(CreateConfig());
        test.Context.Response.Body.Content = """
            {
              "usage": {
                "prompt_tokens": 89,
                "completion_tokens": 48,
                "total_tokens": 137,
                "prompt_tokens_details": {"cached_tokens": 17, "audio_tokens": 29, "image_tokens": 31},
                "completion_tokens_details": {
                  "reasoning_tokens": 11,
                  "audio_tokens": 13,
                  "accepted_prediction_tokens": 7,
                  "rejected_prediction_tokens": 5
                }
              }
            }
            """;

        test.RunInbound();

        AssertMetrics(test, "API Management",
            [
                ("Total Tokens", 137), ("Prompt Tokens", 89), ("Completion Tokens", 48),
                ("Accepted Prediction Tokens", 7), ("Cached Tokens", 17), ("Completion Audio Tokens", 13),
                ("Prompt Audio Tokens", 29), ("Reasoning Tokens", 11), ("Rejected Prediction Tokens", 5),
                ("prompt_tokens_details.image_tokens", 31)
            ],
            DefaultDimensions(test));
    }

    [TestMethod]
    public void EmitTokenMetric_AnthropicUsage_DoesNotInventTotalOrMergeCaches()
    {
        var test = CreateTest(CreateConfig());
        test.Context.Response.Body.Content = """
            {"usage":{"input_tokens":43,"output_tokens":17,"cache_creation_input_tokens":23,"cache_read_input_tokens":31}}
            """;

        test.RunInbound();

        AssertMetrics(test, "API Management",
            [
                ("Prompt Tokens", 43), ("Completion Tokens", 17),
                ("Cache Creation Prompt Tokens", 23), ("Cache Read Prompt Tokens", 31)
            ],
            DefaultDimensions(test));
    }

    [TestMethod]
    public void EmitTokenMetric_VertexUsageMetadata_RecordsReportedCountsAndCategories()
    {
        var test = CreateTest(CreateConfig());
        test.Context.Response.Body.Content = """
            {
              "usageMetadata": {
                "totalTokenCount": 113,
                "promptTokenCount": 71,
                "candidatesTokenCount": 31,
                "cachedContentTokenCount": 23,
                "thoughtsTokenCount": 11,
                "toolUsePromptTokenCount": 17
              }
            }
            """;

        test.RunInbound();

        AssertMetrics(test, "API Management",
            [
                ("Total Tokens", 113), ("Prompt Tokens", 71), ("Completion Tokens", 31),
                ("Cached Tokens", 23), ("Thinking Tokens", 11), ("Tool Use Prompt Tokens", 17)
            ],
            DefaultDimensions(test));
    }

    [TestMethod]
    [DataRow("""{"usage":{"prompt_tokens":37}}""", "Prompt Tokens", 37L)]
    [DataRow("""{"usage":{"total_tokens":71}}""", "Total Tokens", 71L)]
    [DataRow("""{"usage":{"completion_tokens":0}}""", "Completion Tokens", 0L)]
    [DataRow("""{"usage":{"reasoning_tokens":29}}""", "Reasoning Tokens", 29L)]
    public void EmitTokenMetric_PartialUsage_RecordsOnlyObservedCounts(string body, string name, long value)
    {
        var test = CreateTest(CreateConfig());
        test.Context.Response.Body.Content = body;
        var provider = new UsageProvider(_ => throw new InvalidOperationException("Missing counts must not be filled."));
        test.Context.Services.Register<ILlmTokenUsageProvider>(provider);

        test.RunInbound();

        AssertMetrics(test, "API Management", [(name, value)], DefaultDimensions(test));
        provider.CallCount.Should().Be(0);
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow(" ")]
    [DataRow("{}")]
    [DataRow("""{"usage":null}""")]
    [DataRow("""{"usage":{}}""")]
    [DataRow("""{"usageMetadata":null}""")]
    public void EmitTokenMetric_MissingResponseUsage_FallsBackToProvider(string? body)
    {
        var test = CreateTest(CreateConfig());
        test.Context.Response.Body.Content = body!;
        var provider = new UsageProvider(_ => new LlmTokenUsage
        {
            TotalTokens = 317, PromptTokens = 211, CompletionTokens = 106
        });
        test.Context.Services.Register<ILlmTokenUsageProvider>(provider);

        test.RunInbound();

        AssertMetrics(test, "API Management",
            [("Total Tokens", 317), ("Prompt Tokens", 211), ("Completion Tokens", 106)], DefaultDimensions(test));
        provider.CallCount.Should().Be(1);
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow(" ")]
    [DataRow("{}")]
    [DataRow("""{"usage":null}""")]
    [DataRow("""{"usage":{}}""")]
    [DataRow("""{"usage":{"service_tier":"default"}}""")]
    public void EmitTokenMetric_MissingUsage_IsExplicitAndRecordsNothing(string? body)
    {
        var test = CreateTest(CreateConfig());
        test.Context.Response.Body.Content = body!;

        var act = () => test.RunInbound();

        var error = AssertPolicyError<InvalidOperationException>(test, act);
        error.Message.Should().Contain(nameof(ILlmTokenUsageProvider)).And.Contain("response");
    }

    [TestMethod]
    public void EmitTokenMetric_ProviderReturningNoUsage_IsExplicit()
    {
        var test = CreateTest(CreateConfig());
        var provider = new UsageProvider(_ => null);
        test.Context.Services.Register<ILlmTokenUsageProvider>(provider);

        var act = () => test.RunInbound();

        var error = AssertPolicyError<InvalidOperationException>(test, act);
        error.Message.Should().Contain(nameof(ILlmTokenUsageProvider)).And.Contain("response");
        provider.CallCount.Should().Be(1);
    }

    [TestMethod]
    [DataRow("API ID", "test-api")]
    [DataRow("Operation ID", "test-operation")]
    [DataRow("Product ID", "test-product")]
    [DataRow("User ID", "test-user")]
    [DataRow("Subscription ID", "test-subscription")]
    [DataRow("Location", "westeurope")]
    [DataRow("Gateway ID", "test-gateway")]
    public void EmitTokenMetric_DefaultDimensions_ResolveExpressionContext(string name, string expectedValue)
    {
        var config = CreateConfig() with { Dimensions = [new MetricDimensionConfig { Name = name }] };
        var test = CreateTest(config);
        test.Context.Api.Id = "test-api";
        test.Context.Operation.Id = "test-operation";
        test.Context.Product.Id = "test-product";
        test.Context.User.Id = "test-user";
        test.Context.Subscription.Id = "test-subscription";
        test.Context.Deployment.Region = "westeurope";
        test.Context.Deployment.GatewayId = "test-gateway";
        SetResponseUsage(test);

        test.RunInbound();

        AssertMetrics(test, "API Management", StandardCounts(),
            new Dictionary<string, string?> { [name] = expectedValue });
    }

    [TestMethod]
    public void EmitTokenMetric_ExplicitValues_OverrideDefaultsAndSupportBackendId()
    {
        var config = CreateConfig() with
        {
            Dimensions =
            [
                new MetricDimensionConfig { Name = "API ID", Value = "explicit-api" },
                new MetricDimensionConfig { Name = "Backend ID", Value = "llm-backend" }
            ]
        };
        var test = CreateTest(config);
        test.Context.Api.Id = "context-api";
        SetResponseUsage(test);

        test.RunInbound();

        AssertMetrics(test, "API Management", StandardCounts(),
            new Dictionary<string, string?> { ["API ID"] = "explicit-api", ["Backend ID"] = "llm-backend" });
    }

    [TestMethod]
    public void EmitTokenMetric_BackendDefaultWithoutValue_ReportsUnsupportedContext()
    {
        var config = CreateConfig() with { Dimensions = [new MetricDimensionConfig { Name = "Backend ID" }] };
        var test = CreateTest(config);
        SetResponseUsage(test);

        var act = () => test.RunInbound();

        AssertPolicyError<NotSupportedException>(test, act).Message.Should().Contain("Backend ID").And.Contain("Value");
    }

    [TestMethod]
    public void EmitTokenMetric_FiveDimensionsAndEmptyExplicitValues_AreAllowed()
    {
        var dimensions = Enumerable.Range(0, 5)
            .Select(index => new MetricDimensionConfig { Name = $"dimension-{index}", Value = "" }).ToArray();
        var test = CreateTest(CreateConfig() with { Dimensions = dimensions });
        SetResponseUsage(test);

        test.RunInbound();

        AssertMetrics(test, "API Management", StandardCounts(),
            dimensions.ToDictionary(dimension => dimension.Name, dimension => dimension.Value));
    }

    [TestMethod]
    public void EmitTokenMetric_Expressions_AreEvaluatedForEachInvocation()
    {
        var test = new ExpressionMetricDocument(UseAzureOpenAi).AsTestDocument();
        test.Context.Api.Id = "expression-api";
        test.Context.Variables["namespace"] = "first.namespace";
        test.Context.Variables["dimension-name"] = "first-tag";
        test.Context.Variables["dimension-value"] = "first";
        test.Context.Response.Body.Content = """{"usage":{"total_tokens":139,"prompt_tokens":101,"completion_tokens":38}}""";

        test.RunInbound();
        test.Context.Variables["namespace"] = "second.namespace";
        test.Context.Variables["dimension-name"] = "second-tag";
        test.Context.Variables["dimension-value"] = "second";
        test.Context.Response.Body.Content = """{"usage":{"total_tokens":271,"prompt_tokens":181,"completion_tokens":90}}""";
        test.RunInbound();

        var expected = ExpectedMetrics("first.namespace",
                [("Total Tokens", 139), ("Prompt Tokens", 101), ("Completion Tokens", 38)],
                new Dictionary<string, string?> { ["API ID"] = "expression-api", ["first-tag"] = "first" })
            .Concat(ExpectedMetrics("second.namespace",
                [("Total Tokens", 271), ("Prompt Tokens", 181), ("Completion Tokens", 90)],
                new Dictionary<string, string?> { ["API ID"] = "expression-api", ["second-tag"] = "second" }));
        test.SetupLoggerStore().Metrics.Should().BeEquivalentTo(expected, options => options.WithStrictOrdering());
    }

    [TestMethod]
    public void EmitTokenMetric_Provider_IsEvaluatedForEachInvocation()
    {
        var test = CreateTest(CreateConfig());
        var usage = new LlmTokenUsage { TotalTokens = 73, PromptTokens = 49, CompletionTokens = 24 };
        var provider = new UsageProvider(_ => usage);
        test.Context.Services.Register<ILlmTokenUsageProvider>(provider);

        test.RunInbound();
        usage = new LlmTokenUsage { TotalTokens = 127, PromptTokens = 83, CompletionTokens = 44 };
        test.RunInbound();

        var expected = ExpectedMetrics("API Management",
                [("Total Tokens", 73), ("Prompt Tokens", 49), ("Completion Tokens", 24)], DefaultDimensions(test))
            .Concat(ExpectedMetrics("API Management",
                [("Total Tokens", 127), ("Prompt Tokens", 83), ("Completion Tokens", 44)], DefaultDimensions(test)));
        test.SetupLoggerStore().Metrics.Should().BeEquivalentTo(expected, options => options.WithStrictOrdering());
        provider.CallCount.Should().Be(2);
    }

    [TestMethod]
    public void EmitTokenMetric_RepeatedInvocations_RecordIndependentSnapshots()
    {
        var dimensions = new[] { new MetricDimensionConfig { Name = "tag", Value = "first" } };
        var test = CreateTest(CreateConfig() with { Dimensions = dimensions });
        SetResponseUsage(test);
        var store = test.SetupLoggerStore();

        test.RunInbound();
        var snapshot = store.Metrics;
        dimensions[0] = new MetricDimensionConfig { Name = "tag", Value = "second" };
        test.RunInbound();

        var first = ExpectedMetrics("API Management", StandardCounts(),
            new Dictionary<string, string?> { ["tag"] = "first" });
        var second = ExpectedMetrics("API Management", StandardCounts(),
            new Dictionary<string, string?> { ["tag"] = "second" });
        snapshot.Should().BeEquivalentTo(first, options => options.WithStrictOrdering());
        store.Metrics.Should().BeEquivalentTo(first.Concat(second), options => options.WithStrictOrdering());
    }

    [TestMethod]
    public void EmitTokenMetric_StoresAndUsageProviders_AreIsolatedBetweenContexts()
    {
        var first = CreateTest(CreateConfig());
        var second = CreateTest(CreateConfig());
        first.Context.Services.Register<ILlmTokenUsageProvider>(new UsageProvider(_ =>
            new LlmTokenUsage { TotalTokens = 173, PromptTokens = 131, CompletionTokens = 42 }));
        SetResponseUsage(second);

        first.RunInbound();

        AssertMetrics(first, "API Management",
            [("Total Tokens", 173), ("Prompt Tokens", 131), ("Completion Tokens", 42)], DefaultDimensions(first));
        second.SetupLoggerStore().Metrics.Should().BeEmpty();

        second.RunInbound();

        AssertMetrics(second, "API Management", StandardCounts(), DefaultDimensions(second));
    }

    [TestMethod]
    public void EmitTokenMetric_Callback_OverridesInvalidConfigAndUsageProvider()
    {
        var config = CreateConfig() with { Namespace = "", Dimensions = [] };
        var test = CreateTest(config);
        var provider = new UsageProvider(_ => throw new InvalidOperationException("The model must not be called."));
        test.Context.Services.Register<ILlmTokenUsageProvider>(provider);
        SetupCallback(test, (_, _) => true, (context, observedConfig) =>
        {
            observedConfig.Should().BeSameAs(config);
            context.Variables["callback"] = true;
        });

        test.RunInbound();

        test.Context.Variables["callback"].Should().Be(true);
        test.SetupLoggerStore().Metrics.Should().BeEmpty();
        provider.CallCount.Should().Be(0);
    }

    [TestMethod]
    public void EmitTokenMetric_UnmatchedCallback_UsesDefaultRecording()
    {
        var test = CreateTest(CreateConfig());
        SetResponseUsage(test);
        var executedCallback = false;
        SetupCallback(test, (_, config) => config.Namespace == "other", (_, _) => executedCallback = true);

        test.RunInbound();

        executedCallback.Should().BeFalse();
        AssertMetrics(test, "API Management", StandardCounts(), DefaultDimensions(test));
    }

    [TestMethod]
    public void EmitTokenMetric_CallbackFailure_IsSurfacedWithoutRecording()
    {
        var test = CreateTest(CreateConfig());
        SetupCallback(test, (_, _) => true, (_, _) => throw new InvalidOperationException("Token metric callback failed."));

        var act = () => test.RunInbound();

        AssertPolicyError<InvalidOperationException>(test, act).Message.Should().Be("Token metric callback failed.");
    }

    [TestMethod]
    public void EmitTokenMetric_ProviderFailure_IsSurfacedWithoutRecording()
    {
        var test = CreateTest(CreateConfig());
        test.Context.Services.Register<ILlmTokenUsageProvider>(new UsageProvider(_ =>
            throw new InvalidOperationException("Usage model failed.")));

        var act = () => test.RunInbound();

        AssertPolicyError<InvalidOperationException>(test, act).Message.Should().Be("Usage model failed.");
    }

    [TestMethod]
    [DynamicData(nameof(InvalidConfigurations))]
    public void EmitTokenMetric_InvalidConfig_IsSurfacedWithoutRecording(EmitTokenMetricConfig config, string parameterName)
    {
        var test = CreateTest(config);
        SetResponseUsage(test);

        var act = () => test.RunInbound();

        AssertPolicyError<ArgumentException>(test, act).ParamName.Should().Be(parameterName);
    }

    [TestMethod]
    [DataRow("""{"usage":37}""")]
    [DataRow("""{"usage":[]}""")]
    [DataRow("""{"usage":"invalid"}""")]
    [DataRow("""{"usage":{"total_tokens":71,"prompt_tokens":-17}}""")]
    [DataRow("""{"usage":{"total_tokens":71,"completion_tokens":2.5}}""")]
    [DataRow("""{"usage":{"total_tokens":71,"prompt_tokens":"17"}}""")]
    [DataRow("""{"usage":{"total_tokens":71,"prompt_tokens":null}}""")]
    [DataRow("""{"usage":{"total_tokens":71,"prompt_tokens":9007199254740993}}""")]
    [DataRow("""{"usage":{"total_tokens":71,"prompt_tokens":9223372036854775808}}""")]
    [DataRow("""{"usage":{"total_tokens":71,"prompt_tokens_details":"invalid"}}""")]
    [DataRow("""{"usage":{"total_tokens":71,"completion_tokens_details":{"reasoning_tokens":-3}}}""")]
    [DataRow("""{"usage":{"total_tokens":71,"provider_tokens":[17]}}""")]
    [DataRow("""{"usage":{"total_tokens":71,"prompt_tokens":37,"input_tokens":37}}""")]
    [DataRow("""{"usage":{"total_tokens":71,"total_tokens":73}}""")]
    [DataRow("""{"usageMetadata":{"totalTokenCount":71,"thoughtsTokenCount":null}}""")]
    public void EmitTokenMetric_InvalidResponseUsage_IsAtomicAndDoesNotFallBack(string body)
    {
        var test = CreateTest(CreateConfig());
        test.Context.Response.Body.Content = body;
        var provider = new UsageProvider(_ => new LlmTokenUsage { TotalTokens = 97 });
        test.Context.Services.Register<ILlmTokenUsageProvider>(provider);

        var act = () => test.RunInbound();

        AssertPolicyError<ArgumentException>(test, act).ParamName.Should().Be("usage");
        provider.CallCount.Should().Be(0);
    }

    [TestMethod]
    [DataRow("{")]
    [DataRow("not-json")]
    public void EmitTokenMetric_InvalidResponseJson_IsSurfacedWithoutRecording(string body)
    {
        var test = CreateTest(CreateConfig());
        test.Context.Response.Body.Content = body;

        var act = () => test.RunInbound();

        AssertPolicyError<JsonException>(test, act);
    }

    [TestMethod]
    [DataRow("[]")]
    [DataRow("37")]
    [DataRow("null")]
    public void EmitTokenMetric_InvalidResponseRoot_IsSurfacedWithoutRecording(string body)
    {
        var test = CreateTest(CreateConfig());
        test.Context.Response.Body.Content = body;

        var act = () => test.RunInbound();

        AssertPolicyError<ArgumentException>(test, act).ParamName.Should().Be("body");
    }

    [TestMethod]
    [DynamicData(nameof(InvalidUsages))]
    public void EmitTokenMetric_InvalidProviderUsage_IsAtomic(LlmTokenUsage usage)
    {
        var test = CreateTest(CreateConfig());
        test.Context.Services.Register<ILlmTokenUsageProvider>(new UsageProvider(_ => usage));

        var act = () => test.RunInbound();

        AssertPolicyError<ArgumentException>(test, act).ParamName.Should().Be("usage");
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void EmitTokenMetric_ExactlyRepresentableBoundaryCount_IsRecorded(bool useProvider)
    {
        var test = CreateTest(CreateConfig());
        if (useProvider)
        {
            test.Context.Services.Register<ILlmTokenUsageProvider>(new UsageProvider(_ =>
                new LlmTokenUsage { TotalTokens = 9_007_199_254_740_992 }));
        }
        else
        {
            test.Context.Response.Body.Content = """{"usage":{"total_tokens":9007199254740992}}""";
        }

        test.RunInbound();

        AssertMetrics(test, "API Management", [("Total Tokens", 9_007_199_254_740_992)], DefaultDimensions(test));
    }

    public static IEnumerable<object[]> InvalidConfigurations()
    {
        var config = CreateConfig();
        yield return [null!, "args"];
        foreach (var metricNamespace in new[] { "", " " })
        {
            yield return [config with { Namespace = metricNamespace }, nameof(EmitTokenMetricConfig.Namespace)];
        }

        yield return [config with { Dimensions = null! }, nameof(EmitTokenMetricConfig.Dimensions)];
        yield return [config with { Dimensions = [] }, nameof(EmitTokenMetricConfig.Dimensions)];
        yield return [config with { Dimensions = [null!] }, nameof(EmitTokenMetricConfig.Dimensions)];
        foreach (var name in new string?[] { null, "", " " })
        {
            yield return
            [
                config with { Dimensions = [new MetricDimensionConfig { Name = name!, Value = "value" }] },
                nameof(EmitTokenMetricConfig.Dimensions)
            ];
        }

        yield return
        [
            config with { Dimensions = [new MetricDimensionConfig { Name = "custom" }] },
            nameof(EmitTokenMetricConfig.Dimensions)
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
            nameof(EmitTokenMetricConfig.Dimensions)
        ];
        yield return
        [
            config with
            {
                Dimensions = Enumerable.Range(0, 6)
                    .Select(index => new MetricDimensionConfig { Name = $"dimension-{index}", Value = "value" }).ToArray()
            },
            nameof(EmitTokenMetricConfig.Dimensions)
        ];
    }

    public static IEnumerable<object[]> InvalidUsages()
    {
        yield return [new LlmTokenUsage()];
        yield return [new LlmTokenUsage { TotalTokens = -1 }];
        yield return [new LlmTokenUsage { TotalTokens = 71, PromptTokens = -17 }];
        yield return [new LlmTokenUsage { TotalTokens = 71, CompletionTokens = -31 }];
        yield return [new LlmTokenUsage { TotalTokens = 9_007_199_254_740_993 }];
        yield return [new LlmTokenUsage { TotalTokens = 71, AdditionalTokens = null! }];
        foreach (var name in new[] { "", " ", "Total Tokens", "Prompt Tokens", "Completion Tokens" })
        {
            yield return
            [
                new LlmTokenUsage { TotalTokens = 71, AdditionalTokens = new Dictionary<string, long> { [name] = 29 } }
            ];
        }

        yield return
        [
            new LlmTokenUsage
            {
                TotalTokens = 71,
                AdditionalTokens = new Dictionary<string, long> { ["Thinking Tokens"] = -7 }
            }
        ];
        yield return
        [
            new LlmTokenUsage
            {
                TotalTokens = 71,
                AdditionalTokens = new Dictionary<string, long> { ["Other Tokens"] = 9_007_199_254_740_993 }
            }
        ];
    }

    private TestDocument CreateTest(EmitTokenMetricConfig config) =>
        new MetricDocument(config, UseAzureOpenAi).AsTestDocument();

    private static EmitTokenMetricConfig CreateConfig() => new()
    {
        Dimensions = [new MetricDimensionConfig { Name = "API ID" }]
    };

    private static void SetResponseUsage(TestDocument test) =>
        test.Context.Response.Body.Content = """{"usage":{"total_tokens":97,"prompt_tokens":67,"completion_tokens":30}}""";

    private static (string Name, long Value)[] StandardCounts() =>
        [("Total Tokens", 97), ("Prompt Tokens", 67), ("Completion Tokens", 30)];

    private static Dictionary<string, string?> DefaultDimensions(TestDocument test) =>
        new() { ["API ID"] = test.Context.Api.Id };

    private static MetricEvent[] ExpectedMetrics(
        string metricNamespace,
        (string Name, long Value)[] counts,
        IReadOnlyDictionary<string, string?> dimensions)
    {
        var snapshot = dimensions.ToImmutableDictionary(StringComparer.Ordinal);
        return counts.Select(count => new MetricEvent(metricNamespace, count.Name, count.Value, snapshot)).ToArray();
    }

    private static void AssertMetrics(
        TestDocument test,
        string metricNamespace,
        (string Name, long Value)[] counts,
        IReadOnlyDictionary<string, string?> dimensions) =>
        test.SetupLoggerStore().Metrics.Should().BeEquivalentTo(
            ExpectedMetrics(metricNamespace, counts, dimensions), options => options.WithStrictOrdering());

    private TException AssertPolicyError<TException>(TestDocument test, Action act) where TException : Exception
    {
        var error = act.Should().Throw<PolicyException>().Which;
        error.Policy.Should().Be(ExpectedPolicy);
        error.Section.Should().Be(nameof(IInboundContext));
        test.SetupLoggerStore().Metrics.Should().BeEmpty();
        return error.InnerException.Should().BeAssignableTo<TException>().Which;
    }

    private void SetupCallback(
        TestDocument test,
        Func<GatewayContext, EmitTokenMetricConfig, bool> predicate,
        Action<GatewayContext, EmitTokenMetricConfig> callback)
    {
        if (UseAzureOpenAi)
        {
            test.SetupInbound().AzureOpenAiEmitTokenMetric(predicate).WithCallback(callback);
        }
        else
        {
            test.SetupInbound().LlmEmitTokenMetric(predicate).WithCallback(callback);
        }
    }
}
