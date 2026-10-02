// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring.Expressions;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Document;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Services;

namespace Test.Emulator.Emulator.Policies;

[TestClass]
public class SendServiceBusMessageTests
{
    [TestMethod]
    [DataRow(nameof(IInboundContext), false)]
    [DataRow(nameof(IInboundContext), true)]
    [DataRow(nameof(IOutboundContext), false)]
    [DataRow(nameof(IOutboundContext), true)]
    [DataRow(nameof(IOnErrorContext), false)]
    [DataRow(nameof(IOnErrorContext), true)]
    public void ShouldSendExactQueueOrTopicRequest(string section, bool topic)
    {
        var config = new SendServiceBusMessageConfig
        {
            QueueName = topic ? null : "orders",
            TopicName = topic ? "order-events" : null,
            Namespace = "contoso.servicebus.windows.net",
            ClientId = "9e265625-d4b5-4411-a2f7-dde2413b32dd",
            MessageProperties =
            [
                new ServiceBusMessageProperty { Name = "Customer", Value = "Contoso" },
                new ServiceBusMessageProperty { Name = "optional", Value = "" }
            ],
            Payload = """{"order":123}"""
        };
        var test = CreateTest(config);
        test.Context.Request.Body.Content = "original request";
        test.Context.Response.StatusCode = 202;
        test.Context.Response.Body.Content = "original response";
        var service = new MessageService();
        test.Context.Services.Register<IServiceBusMessageService>(service);

        RunSection(test, section);

        service.Requests.Should().ContainSingle().Which.Should().BeEquivalentTo(new ServiceBusMessageRequest(
            topic ? null : "orders", topic ? "order-events" : null,
            "contoso.servicebus.windows.net", "9e265625-d4b5-4411-a2f7-dde2413b32dd",
            new Dictionary<string, string> { ["Customer"] = "Contoso", ["optional"] = "" }, """{"order":123}"""));
        test.Context.Variables.Should().BeEmpty();
        test.Context.Request.Body.Content.Should().Be("original request");
        test.Context.Request.Body.Consumed.Should().BeFalse();
        test.Context.Response.StatusCode.Should().Be(202);
        test.Context.Response.Body.Content.Should().Be("original response");
    }

    [TestMethod]
    public void ShouldUseSystemIdentityAndPreserveOptionalNamespaceAndEmptyPayload()
    {
        var test = CreateTest(new SendServiceBusMessageConfig { QueueName = "orders", Payload = "" });
        test.Context.Request.Body.Content = "must not replace payload";
        var service = new MessageService();
        test.Context.Services.Register<IServiceBusMessageService>(service);

        test.RunInbound();

        service.Requests.Should().ContainSingle().Which.Should().BeEquivalentTo(new ServiceBusMessageRequest(
            "orders", null, null, null, new Dictionary<string, string>(), ""));
        test.Context.Request.Body.Consumed.Should().BeFalse();
    }

    [TestMethod]
    [DataRow(nameof(IInboundContext), false)]
    [DataRow(nameof(IInboundContext), true)]
    [DataRow(nameof(IOutboundContext), false)]
    [DataRow(nameof(IOutboundContext), true)]
    [DataRow(nameof(IOnErrorContext), false)]
    [DataRow(nameof(IOnErrorContext), true)]
    public void ShouldEvaluateDestinationIdentityPropertiesAndPayloadExpressions(string section, bool topic)
    {
        var test = new PolicyDocument(context => new SendServiceBusMessageConfig
        {
            QueueName = topic ? null : (string)context.Variables["destination"],
            TopicName = topic ? (string)context.Variables["destination"] : null,
            Namespace = (string)context.Variables["namespace"],
            ClientId = (string)context.Variables["client-id"],
            MessageProperties = [new ServiceBusMessageProperty { Name = "request-id", Value = context.RequestId.ToString() }],
            Payload = (context.Request.Body ?? throw new InvalidOperationException("Request body is required."))
                .As<string>(preserveContent: true)
        }).AsTestDocument();
        test.Context.Variables["destination"] = "expression-destination";
        test.Context.Variables["namespace"] = "expression.servicebus.windows.net";
        test.Context.Variables["client-id"] = "b9236a76-8525-4085-8bce-27a529639fcd";
        test.Context.RequestId = Guid.Parse("89f0f8e0-7638-4b87-9e71-f24e79a78d8a");
        test.Context.Request.Body.Content = """{"expression":true}""";
        var service = new MessageService();
        test.Context.Services.Register<IServiceBusMessageService>(service);

        RunSection(test, section);

        service.Requests.Should().ContainSingle().Which.Should().BeEquivalentTo(new ServiceBusMessageRequest(
            topic ? null : "expression-destination", topic ? "expression-destination" : null,
            "expression.servicebus.windows.net", "b9236a76-8525-4085-8bce-27a529639fcd",
            new Dictionary<string, string> { ["request-id"] = "89f0f8e0-7638-4b87-9e71-f24e79a78d8a" }, """{"expression":true}"""));
        test.Context.Request.Body.Consumed.Should().BeFalse();
        test.Context.Variables.Should().HaveCount(3);
    }

    [TestMethod]
    [DataRow(nameof(IInboundContext))]
    [DataRow(nameof(IOutboundContext))]
    [DataRow(nameof(IOnErrorContext))]
    public void ShouldFailExplicitlyWithoutRegisteredService(string section)
    {
        var test = CreateTest(new SendServiceBusMessageConfig { QueueName = "orders", Payload = "payload" });

        var action = () => RunSection(test, section);

        var error = action.Should().Throw<PolicyException>().Which;
        error.Policy.Should().Be(nameof(IInboundContext.SendServiceBusMessage));
        error.Section.Should().Be(section);
        error.InnerException.Should().BeOfType<InvalidOperationException>()
            .Which.Message.Should().Contain("IServiceBusMessageService").And.Contain("Register");
        test.Context.Variables.Should().BeEmpty();
    }

    [TestMethod]
    [DataRow(nameof(IInboundContext))]
    [DataRow(nameof(IOutboundContext))]
    [DataRow(nameof(IOnErrorContext))]
    public void ShouldRejectPresentButEmptyMessagePropertiesBeforeDispatch(string section)
    {
        var test = CreateTest(new SendServiceBusMessageConfig
        {
            QueueName = "orders", Payload = "payload", MessageProperties = []
        });
        var service = new MessageService();
        test.Context.Services.Register<IServiceBusMessageService>(service);

        var action = () => RunSection(test, section);

        var error = action.Should().Throw<PolicyException>().Which;
        error.Policy.Should().Be(nameof(IInboundContext.SendServiceBusMessage));
        error.Section.Should().Be(section);
        error.InnerException.Should().BeOfType<ArgumentException>()
            .Which.ParamName.Should().Be(nameof(SendServiceBusMessageConfig.MessageProperties));
        service.Requests.Should().BeEmpty();
        test.Context.Variables.Should().BeEmpty();
    }

    [TestMethod]
    [DataRow("missing-destination")]
    [DataRow("both-destinations")]
    [DataRow("queue-empty")]
    [DataRow("queue-whitespace")]
    [DataRow("topic-empty")]
    [DataRow("topic-whitespace")]
    [DataRow("namespace-empty")]
    [DataRow("namespace-whitespace")]
    [DataRow("client-id-empty")]
    [DataRow("client-id-whitespace")]
    [DataRow("payload-null")]
    [DataRow("property-null-item")]
    [DataRow("property-null-name")]
    [DataRow("property-empty-name")]
    [DataRow("property-whitespace-name")]
    [DataRow("property-null-value")]
    [DataRow("property-duplicate")]
    public void ShouldRejectInvalidConfigurationBeforeCallingService(string invalid)
    {
        var config = new SendServiceBusMessageConfig { QueueName = "orders", Payload = "payload" };
        config = invalid switch
        {
            "missing-destination" => config with { QueueName = null },
            "both-destinations" => config with { TopicName = "events" },
            "queue-empty" => config with { QueueName = "" },
            "queue-whitespace" => config with { QueueName = " " },
            "topic-empty" => config with { QueueName = null, TopicName = "" },
            "topic-whitespace" => config with { QueueName = null, TopicName = " " },
            "namespace-empty" => config with { Namespace = "" },
            "namespace-whitespace" => config with { Namespace = " " },
            "client-id-empty" => config with { ClientId = "" },
            "client-id-whitespace" => config with { ClientId = " " },
            "payload-null" => config with { Payload = null! },
            "property-null-item" => config with { MessageProperties = [null!] },
            "property-null-name" => config with { MessageProperties = [new ServiceBusMessageProperty { Name = null!, Value = "value" }] },
            "property-empty-name" => config with { MessageProperties = [new ServiceBusMessageProperty { Name = "", Value = "value" }] },
            "property-whitespace-name" => config with { MessageProperties = [new ServiceBusMessageProperty { Name = " ", Value = "value" }] },
            "property-null-value" => config with { MessageProperties = [new ServiceBusMessageProperty { Name = "key", Value = null! }] },
            "property-duplicate" => config with
            {
                MessageProperties =
                [
                    new ServiceBusMessageProperty { Name = "key", Value = "first" },
                    new ServiceBusMessageProperty { Name = "key", Value = "second" }
                ]
            },
            _ => throw new ArgumentOutOfRangeException(nameof(invalid))
        };
        var test = CreateTest(config);
        var service = new MessageService();
        test.Context.Services.Register<IServiceBusMessageService>(service);

        var action = () => test.RunInbound();

        action.Should().Throw<PolicyException>().Which.InnerException.Should().BeAssignableTo<ArgumentException>();
        service.Requests.Should().BeEmpty();
        test.Context.Variables.Should().BeEmpty();
    }

    [TestMethod]
    public void ShouldSnapshotCaseSensitiveMessageProperties()
    {
        var properties = new[]
        {
            new ServiceBusMessageProperty { Name = "key", Value = "first" },
            new ServiceBusMessageProperty { Name = "Key", Value = "second" }
        };
        var test = CreateTest(new SendServiceBusMessageConfig { QueueName = "orders", Payload = "payload", MessageProperties = properties });
        var service = new MessageService();
        test.Context.Services.Register<IServiceBusMessageService>(service);

        test.RunInbound();
        properties[0] = properties[0] with { Value = "changed" };

        service.Requests.Should().ContainSingle().Which.MessageProperties.Should()
            .BeEquivalentTo(new Dictionary<string, string> { ["key"] = "first", ["Key"] = "second" });
    }

    [TestMethod]
    [DataRow(nameof(IInboundContext), "http")]
    [DataRow(nameof(IOutboundContext), "http")]
    [DataRow(nameof(IOnErrorContext), "http")]
    [DataRow(nameof(IInboundContext), "cancellation")]
    [DataRow(nameof(IOutboundContext), "cancellation")]
    [DataRow(nameof(IOnErrorContext), "cancellation")]
    [DataRow(nameof(IInboundContext), "unexpected")]
    [DataRow(nameof(IOutboundContext), "unexpected")]
    [DataRow(nameof(IOnErrorContext), "unexpected")]
    public void ShouldPropagateServiceFailuresWithPolicyAndSection(string section, string failure)
    {
        Exception expected = failure switch
        {
            "http" => new HttpRequestException("send failed"),
            "cancellation" => new OperationCanceledException("send canceled"),
            "unexpected" => new InvalidOperationException("mock implementation error"),
            _ => throw new ArgumentOutOfRangeException(nameof(failure))
        };
        var test = CreateTest(new SendServiceBusMessageConfig { QueueName = "orders", Payload = "payload" });
        var service = new MessageService { Handler = (_, _) => Task.FromException(expected) };
        test.Context.Services.Register<IServiceBusMessageService>(service);

        var action = () => RunSection(test, section);

        var error = action.Should().Throw<PolicyException>().Which;
        error.Policy.Should().Be(nameof(IInboundContext.SendServiceBusMessage));
        error.Section.Should().Be(section);
        error.InnerException.Should().BeSameAs(expected);
        service.Requests.Should().ContainSingle();
        test.Context.Variables.Should().BeEmpty();
    }

    [TestMethod]
    [DataRow(nameof(IInboundContext))]
    [DataRow(nameof(IOutboundContext))]
    [DataRow(nameof(IOnErrorContext))]
    public void ShouldAllowCallbackToOverrideServiceAndValidation(string section)
    {
        var config = new SendServiceBusMessageConfig { Payload = null! };
        var test = CreateTest(config);
        var service = new MessageService();
        test.Context.Services.Register<IServiceBusMessageService>(service);
        SendServiceBusMessageConfig? captured = null;
        var setup = section switch
        {
            nameof(IInboundContext) => test.SetupInbound().SendServiceBusMessage((_, candidate) => ReferenceEquals(candidate, config)),
            nameof(IOutboundContext) => test.SetupOutbound().SendServiceBusMessage((_, candidate) => ReferenceEquals(candidate, config)),
            nameof(IOnErrorContext) => test.SetupOnError().SendServiceBusMessage((_, candidate) => ReferenceEquals(candidate, config)),
            _ => throw new ArgumentOutOfRangeException(nameof(section))
        };
        setup.WithCallback((context, candidate) =>
        {
            captured = candidate;
            context.Variables["callback"] = "message override";
        });

        RunSection(test, section);

        captured.Should().BeSameAs(config);
        service.Requests.Should().BeEmpty();
        test.Context.Variables.Should().ContainSingle()
            .Which.Should().Be(new KeyValuePair<string, object>("callback", "message override"));
    }

    [TestMethod]
    public void ShouldAllowCallbackWithoutRegisteredService()
    {
        var test = CreateTest(new SendServiceBusMessageConfig { QueueName = "orders", Payload = "payload" });
        test.SetupInbound().SendServiceBusMessage().WithCallback((context, _) => context.Variables["callback"] = true);

        test.RunInbound();

        test.Context.Variables["callback"].Should().Be(true);
    }

    [TestMethod]
    public void ShouldUseServiceWhenCallbackPredicateDoesNotMatch()
    {
        var test = CreateTest(new SendServiceBusMessageConfig { QueueName = "orders", Payload = "payload" });
        var service = new MessageService();
        test.Context.Services.Register<IServiceBusMessageService>(service);
        test.SetupInbound().SendServiceBusMessage((_, config) => config.QueueName == "other")
            .WithCallback((_, _) => Assert.Fail("Nonmatching callback was invoked."));

        test.RunInbound();

        service.Requests.Should().ContainSingle().Which.QueueName.Should().Be("orders");
    }

    [TestMethod]
    [DataRow(nameof(IInboundContext))]
    [DataRow(nameof(IOutboundContext))]
    [DataRow(nameof(IOnErrorContext))]
    public void ShouldPreservePolicyAndSectionForCallbackErrors(string section)
    {
        var test = CreateTest(new SendServiceBusMessageConfig { QueueName = "orders", Payload = "payload" });
        var expected = new HttpRequestException("callback failure");
        var setup = section switch
        {
            nameof(IInboundContext) => test.SetupInbound().SendServiceBusMessage(),
            nameof(IOutboundContext) => test.SetupOutbound().SendServiceBusMessage(),
            nameof(IOnErrorContext) => test.SetupOnError().SendServiceBusMessage(),
            _ => throw new ArgumentOutOfRangeException(nameof(section))
        };
        setup.WithCallback((_, _) => throw expected);

        var action = () => RunSection(test, section);

        var error = action.Should().Throw<PolicyException>().Which;
        error.Policy.Should().Be(nameof(IInboundContext.SendServiceBusMessage));
        error.Section.Should().Be(section);
        error.InnerException.Should().BeSameAs(expected);
    }

    private static TestDocument CreateTest(SendServiceBusMessageConfig config) =>
        new PolicyDocument(_ => config).AsTestDocument();

    private static void RunSection(TestDocument test, string section)
    {
        switch (section)
        {
            case nameof(IInboundContext): test.RunInbound(); break;
            case nameof(IOutboundContext): test.RunOutbound(); break;
            case nameof(IOnErrorContext): test.RunOnError(); break;
            default: throw new ArgumentOutOfRangeException(nameof(section));
        }
    }

    private sealed class PolicyDocument(Func<IExpressionContext, SendServiceBusMessageConfig> config) : IDocument
    {
        public void Inbound(IInboundContext context) => context.SendServiceBusMessage(config(context.ExpressionContext));
        public void Outbound(IOutboundContext context) => context.SendServiceBusMessage(config(context.ExpressionContext));
        public void OnError(IOnErrorContext context) => context.SendServiceBusMessage(config(context.ExpressionContext));
        public void Backend(IBackendContext context) { }
    }

    private sealed class MessageService : IServiceBusMessageService
    {
        public List<ServiceBusMessageRequest> Requests { get; } = [];
        public Func<ServiceBusMessageRequest, CancellationToken, Task> Handler { get; init; } = (_, _) => Task.CompletedTask;

        public Task SendAsync(ServiceBusMessageRequest request, CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            return Handler(request, cancellationToken);
        }
    }
}
