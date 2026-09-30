// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring.Expressions;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Document;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Expressions;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Services;

namespace Test.Emulator.Emulator.Policies;

[TestClass]
public class PublishToDarpTests
{
    [TestMethod]
    [DataRow(nameof(IInboundContext))]
    [DataRow(nameof(IOutboundContext))]
    [DataRow(nameof(IOnErrorContext))]
    public void ShouldPublishExactRequestAndStoreExactResponse(string section)
    {
        var config = new PublishToDarpConfig
        {
            PubSubName = "orders",
            Topic = "created",
            Content = """{"order":"{{body.order}}"}""",
            Template = "Liquid",
            ContentType = "application/json",
            Timeout = 5,
            ResponseVariableName = "publish-response"
        };
        var test = CreateTest(config);
        test.Context.Request.Body.Content = "original request";
        test.Context.Response.StatusCode = 202;
        test.Context.Response.Body.Content = "original response";
        var response = new MockResponse { StatusCode = 204, StatusReason = "No Content" };
        response.Headers["X-Publish"] = ["accepted"];
        response.Body.Content = "publish result";
        var service = new PubSubService { Handler = (_, _) => Task.FromResult<IResponse>(response) };
        test.Context.Services.Register<IDaprPubSubService>(service);

        RunSection(test, section);

        service.Requests.Should().ContainSingle().Which.Should().BeEquivalentTo(new DaprPublishRequest(
            "orders", "created", """{"order":"{{body.order}}"}""", "Liquid", "application/json",
            TimeSpan.FromSeconds(5)));
        service.CancellationToken.CanBeCanceled.Should().BeTrue();
        test.Context.Variables.Should().ContainSingle()
            .Which.Should().Be(new KeyValuePair<string, object>("publish-response", response));
        test.Context.Variables["publish-response"].Should().BeSameAs(response);
        test.Context.Request.Body.Content.Should().Be("original request");
        test.Context.Request.Body.Consumed.Should().BeFalse();
        test.Context.Response.StatusCode.Should().Be(202);
        test.Context.Response.Body.Content.Should().Be("original response");
    }

    [TestMethod]
    [DataRow("orders/created", "orders", "created")]
    [DataRow("orders/customer/created", "orders", "customer/created")]
    public void ShouldExtractComponentFromTopicAndUseDefaults(string topic, string component, string expectedTopic)
    {
        var test = CreateTest(new PublishToDarpConfig { Topic = topic, Content = "payload" });
        var service = new PubSubService();
        test.Context.Services.Register<IDaprPubSubService>(service);

        test.RunInbound();

        service.Requests.Should().ContainSingle().Which.Should().BeEquivalentTo(new DaprPublishRequest(
            component, expectedTopic, "payload", null, null, TimeSpan.FromSeconds(5)));
        test.Context.Variables.Should().BeEmpty();
    }

    [TestMethod]
    public void ShouldPreserveExplicitComponentAndEmptyContent()
    {
        var test = CreateTest(new PublishToDarpConfig { PubSubName = "orders", Topic = "customer/created", Content = "" });
        test.Context.Request.Body.Content = "must not replace content";
        var service = new PubSubService();
        test.Context.Services.Register<IDaprPubSubService>(service);

        test.RunInbound();

        service.Requests.Should().ContainSingle().Which.Should().BeEquivalentTo(new DaprPublishRequest(
            "orders", "customer/created", "", null, null, TimeSpan.FromSeconds(5)));
        test.Context.Request.Body.Consumed.Should().BeFalse();
    }

    [TestMethod]
    [DataRow(nameof(IInboundContext))]
    [DataRow(nameof(IOutboundContext))]
    [DataRow(nameof(IOnErrorContext))]
    public void ShouldEvaluateComponentTopicAndContentExpressions(string section)
    {
        var test = new PolicyDocument(context => new PublishToDarpConfig
        {
            PubSubName = (string)context.Variables["component"],
            Topic = (string)context.Variables["topic"],
            Content = (context.Request.Body ?? throw new InvalidOperationException("Request body is required."))
                .As<string>(preserveContent: true)
        }).AsTestDocument();
        test.Context.Variables["component"] = "expression-component";
        test.Context.Variables["topic"] = "expression-topic";
        test.Context.Request.Body.Content = """{"expression":true}""";
        var service = new PubSubService();
        test.Context.Services.Register<IDaprPubSubService>(service);

        RunSection(test, section);

        service.Requests.Should().ContainSingle().Which.Should().BeEquivalentTo(new DaprPublishRequest(
            "expression-component", "expression-topic", """{"expression":true}""", null, null, TimeSpan.FromSeconds(5)));
        test.Context.Request.Body.Consumed.Should().BeFalse();
        test.Context.Variables.Should().HaveCount(2);
    }

    [TestMethod]
    [DataRow(1)]
    [DataRow(240)]
    public void ShouldAcceptTimeoutBoundariesInSeconds(int timeout)
    {
        var test = CreateTest(new PublishToDarpConfig { Topic = "orders/created", Content = "payload", Timeout = timeout });
        var service = new PubSubService();
        test.Context.Services.Register<IDaprPubSubService>(service);

        test.RunInbound();

        service.Requests.Should().ContainSingle().Which.Timeout.Should().Be(TimeSpan.FromSeconds(timeout));
    }

    [TestMethod]
    [DataRow(nameof(IInboundContext), 5)]
    [DataRow(nameof(IInboundContext), null)]
    [DataRow(nameof(IOutboundContext), 5)]
    [DataRow(nameof(IOutboundContext), null)]
    [DataRow(nameof(IOnErrorContext), 5)]
    [DataRow(nameof(IOnErrorContext), null)]
    public void ShouldAllow100MillisecondPublishWithFiveSecondTimeout(string section, int? timeout)
    {
        var test = CreateTest(new PublishToDarpConfig
        {
            Topic = "orders/created", Content = "payload", Timeout = timeout, ResponseVariableName = "result"
        });
        var response = new MockResponse { StatusCode = 204, StatusReason = "No Content" };
        var service = new PubSubService
        {
            Handler = async (_, cancellationToken) =>
            {
                await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken);
                return response;
            }
        };
        test.Context.Services.Register<IDaprPubSubService>(service);

        RunSection(test, section);

        service.Requests.Should().ContainSingle().Which.Timeout.Should().Be(TimeSpan.FromSeconds(5));
        service.CancellationToken.IsCancellationRequested.Should().BeFalse();
        test.Context.Variables["result"].Should().BeSameAs(response);
    }

    [TestMethod]
    [DataRow(nameof(IInboundContext), false)]
    [DataRow(nameof(IInboundContext), true)]
    [DataRow(nameof(IOutboundContext), false)]
    [DataRow(nameof(IOutboundContext), true)]
    [DataRow(nameof(IOnErrorContext), false)]
    [DataRow(nameof(IOnErrorContext), true)]
    public void ShouldFailExplicitlyWithoutRegisteredServiceEvenWhenErrorsAreIgnored(string section, bool ignoreError)
    {
        var test = CreateTest(new PublishToDarpConfig
        {
            Topic = "orders/created", Content = "payload", IgnoreError = ignoreError, ResponseVariableName = "result"
        });

        var action = () => RunSection(test, section);

        var error = action.Should().Throw<PolicyException>().Which;
        error.Policy.Should().Be(nameof(IInboundContext.PublishToDarp));
        error.Section.Should().Be(section);
        error.InnerException.Should().BeOfType<InvalidOperationException>()
            .Which.Message.Should().Contain("IDaprPubSubService").And.Contain("Register");
        test.Context.Variables.Should().BeEmpty();
    }

    [TestMethod]
    [DataRow("topic-null")]
    [DataRow("topic-empty")]
    [DataRow("topic-whitespace")]
    [DataRow("content-null")]
    [DataRow("component-empty")]
    [DataRow("component-whitespace")]
    [DataRow("missing-component")]
    [DataRow("missing-component-prefix")]
    [DataRow("missing-topic-suffix")]
    [DataRow("whitespace-component-prefix")]
    [DataRow("whitespace-topic-suffix")]
    [DataRow("response-empty")]
    [DataRow("response-whitespace")]
    [DataRow("timeout-zero")]
    [DataRow("timeout-negative")]
    [DataRow("timeout-over-limit")]
    [DataRow("template")]
    [DataRow("content-type")]
    public void ShouldRejectInvalidConfigurationBeforeCallingService(string invalid)
    {
        var config = new PublishToDarpConfig { PubSubName = "orders", Topic = "created", Content = "payload", IgnoreError = true };
        config = invalid switch
        {
            "topic-null" => config with { Topic = null! },
            "topic-empty" => config with { Topic = "" },
            "topic-whitespace" => config with { Topic = " " },
            "content-null" => config with { Content = null! },
            "component-empty" => config with { PubSubName = "" },
            "component-whitespace" => config with { PubSubName = " " },
            "missing-component" => config with { PubSubName = null },
            "missing-component-prefix" => config with { PubSubName = null, Topic = "/created" },
            "missing-topic-suffix" => config with { PubSubName = null, Topic = "orders/" },
            "whitespace-component-prefix" => config with { PubSubName = null, Topic = " /created" },
            "whitespace-topic-suffix" => config with { PubSubName = null, Topic = "orders/ " },
            "response-empty" => config with { ResponseVariableName = "" },
            "response-whitespace" => config with { ResponseVariableName = " " },
            "timeout-zero" => config with { Timeout = 0 },
            "timeout-negative" => config with { Timeout = -1 },
            "timeout-over-limit" => config with { Timeout = 241 },
            "template" => config with { Template = "unsupported" },
            "content-type" => config with { ContentType = "text/plain" },
            _ => throw new ArgumentOutOfRangeException(nameof(invalid))
        };
        var test = CreateTest(config);
        var service = new PubSubService();
        test.Context.Services.Register<IDaprPubSubService>(service);

        var action = () => test.RunInbound();

        action.Should().Throw<PolicyException>().Which.InnerException.Should().BeAssignableTo<ArgumentException>();
        service.Requests.Should().BeEmpty();
        test.Context.Variables.Should().BeEmpty();
    }

    [TestMethod]
    [DataRow(302, false)]
    [DataRow(302, true)]
    [DataRow(400, false)]
    [DataRow(400, true)]
    [DataRow(503, false)]
    [DataRow(503, true)]
    public void ShouldStoreErrorResponseAndHonorIgnoreError(int status, bool ignoreError)
    {
        var test = CreateTest(new PublishToDarpConfig
        {
            Topic = "orders/created", Content = "payload", IgnoreError = ignoreError, ResponseVariableName = "result"
        });
        var response = new MockResponse { StatusCode = status, StatusReason = "Publishing failed" };
        response.Body.Content = "remote error";
        var service = new PubSubService { Handler = (_, _) => Task.FromResult<IResponse>(response) };
        test.Context.Services.Register<IDaprPubSubService>(service);
        var traces = new List<string>();
        test.Context.Trace = traces.Add;

        if (ignoreError)
        {
            test.RunInbound();
        }
        else
        {
            var action = () => test.RunInbound();
            action.Should().Throw<PolicyException>().Which.InnerException.Should().BeOfType<HttpRequestException>()
                .Which.StatusCode.Should().Be((System.Net.HttpStatusCode)status);
        }

        service.Requests.Should().ContainSingle();
        test.Context.Variables["result"].Should().BeSameAs(response);
        test.Context.Response.StatusCode.Should().Be(200);
        traces.Should().HaveCount(ignoreError ? 1 : 0);
    }

    [TestMethod]
    [DataRow("http", false)]
    [DataRow("http", true)]
    [DataRow("timeout", false)]
    [DataRow("timeout", true)]
    [DataRow("cancellation", false)]
    [DataRow("cancellation", true)]
    [DataRow("unexpected", true)]
    public void ShouldHandleTransportFailuresWithoutHidingUnexpectedErrors(string failure, bool ignoreError)
    {
        Exception expected = failure switch
        {
            "http" => new HttpRequestException("transport failed"),
            "timeout" => new TimeoutException("remote timeout"),
            "cancellation" => new OperationCanceledException("remote cancellation"),
            "unexpected" => new InvalidOperationException("mock implementation error"),
            _ => throw new ArgumentOutOfRangeException(nameof(failure))
        };
        var test = CreateTest(new PublishToDarpConfig
        {
            Topic = "orders/created", Content = "payload", IgnoreError = ignoreError, ResponseVariableName = "result"
        });
        test.Context.Variables["result"] = "stale response";
        var service = new PubSubService { Handler = (_, _) => Task.FromException<IResponse>(expected) };
        test.Context.Services.Register<IDaprPubSubService>(service);
        var traces = new List<string>();
        test.Context.Trace = traces.Add;

        if (ignoreError && failure != "unexpected")
        {
            test.RunInbound();
        }
        else
        {
            var action = () => test.RunInbound();
            action.Should().Throw<PolicyException>().Which.InnerException.Should().BeSameAs(expected);
        }

        service.Requests.Should().ContainSingle();
        test.Context.Variables.Should().ContainKey("result").WhoseValue.Should().BeNull();
        traces.Should().HaveCount(ignoreError && failure != "unexpected" ? 1 : 0);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void ShouldEnforceTimeoutEvenIfServiceDoesNotObserveCancellation(bool ignoreError)
    {
        var pending = new TaskCompletionSource<IResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
        var test = CreateTest(new PublishToDarpConfig
        {
            Topic = "orders/created", Content = "payload", Timeout = 1, IgnoreError = ignoreError, ResponseVariableName = "result"
        });
        var service = new PubSubService { Handler = (_, _) => pending.Task };
        test.Context.Services.Register<IDaprPubSubService>(service);
        var stopwatch = Stopwatch.StartNew();

        try
        {
            if (ignoreError)
            {
                test.RunInbound();
            }
            else
            {
                var action = () => test.RunInbound();
                action.Should().Throw<PolicyException>().Which.InnerException.Should().BeOfType<TimeoutException>()
                    .Which.Message.Should().Contain("1 second");
            }

            stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(5));
            service.Requests.Should().ContainSingle().Which.Timeout.Should().Be(TimeSpan.FromSeconds(1));
            service.CancellationToken.IsCancellationRequested.Should().BeTrue();
            test.Context.Variables.Should().ContainKey("result").WhoseValue.Should().BeNull();
        }
        finally
        {
            pending.TrySetResult(new MockResponse());
        }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void ShouldRejectNullServiceResponse(bool ignoreError)
    {
        var test = CreateTest(new PublishToDarpConfig { Topic = "orders/created", Content = "payload", IgnoreError = ignoreError });
        var service = new PubSubService { Handler = (_, _) => Task.FromResult<IResponse>(null!) };
        test.Context.Services.Register<IDaprPubSubService>(service);

        var action = () => test.RunInbound();

        action.Should().Throw<PolicyException>().Which.InnerException.Should().BeOfType<InvalidOperationException>();
        service.Requests.Should().ContainSingle();
    }

    [TestMethod]
    [DataRow(nameof(IInboundContext))]
    [DataRow(nameof(IOutboundContext))]
    [DataRow(nameof(IOnErrorContext))]
    public void ShouldAllowCallbackToOverrideServiceAndValidation(string section)
    {
        var config = new PublishToDarpConfig { Topic = "", Content = null!, Timeout = -1 };
        var test = CreateTest(config);
        var service = new PubSubService();
        test.Context.Services.Register<IDaprPubSubService>(service);
        PublishToDarpConfig? captured = null;
        var setup = section switch
        {
            nameof(IInboundContext) => test.SetupInbound().PublishToDarp((_, candidate) => ReferenceEquals(candidate, config)),
            nameof(IOutboundContext) => test.SetupOutbound().PublishToDarp((_, candidate) => ReferenceEquals(candidate, config)),
            nameof(IOnErrorContext) => test.SetupOnError().PublishToDarp((_, candidate) => ReferenceEquals(candidate, config)),
            _ => throw new ArgumentOutOfRangeException(nameof(section))
        };
        setup.WithCallback((context, candidate) =>
        {
            captured = candidate;
            context.Variables["callback"] = "publish override";
        });

        RunSection(test, section);

        captured.Should().BeSameAs(config);
        service.Requests.Should().BeEmpty();
        test.Context.Variables.Should().ContainSingle()
            .Which.Should().Be(new KeyValuePair<string, object>("callback", "publish override"));
    }

    [TestMethod]
    public void ShouldAllowCallbackWithoutRegisteredService()
    {
        var test = CreateTest(new PublishToDarpConfig { Topic = "orders/created", Content = "payload" });
        test.SetupInbound().PublishToDarp().WithCallback((context, _) => context.Variables["callback"] = true);

        test.RunInbound();

        test.Context.Variables["callback"].Should().Be(true);
    }

    [TestMethod]
    public void ShouldUseServiceWhenCallbackPredicateDoesNotMatch()
    {
        var test = CreateTest(new PublishToDarpConfig { Topic = "orders/created", Content = "payload" });
        var service = new PubSubService();
        test.Context.Services.Register<IDaprPubSubService>(service);
        test.SetupInbound().PublishToDarp((_, config) => config.Topic == "other")
            .WithCallback((_, _) => Assert.Fail("Nonmatching callback was invoked."));

        test.RunInbound();

        service.Requests.Should().ContainSingle().Which.Topic.Should().Be("created");
    }

    [TestMethod]
    [DataRow(nameof(IInboundContext))]
    [DataRow(nameof(IOutboundContext))]
    [DataRow(nameof(IOnErrorContext))]
    public void ShouldPreservePolicyAndSectionForCallbackErrors(string section)
    {
        var test = CreateTest(new PublishToDarpConfig { Topic = "orders/created", Content = "payload" });
        var expected = new HttpRequestException("callback failure");
        var setup = section switch
        {
            nameof(IInboundContext) => test.SetupInbound().PublishToDarp(),
            nameof(IOutboundContext) => test.SetupOutbound().PublishToDarp(),
            nameof(IOnErrorContext) => test.SetupOnError().PublishToDarp(),
            _ => throw new ArgumentOutOfRangeException(nameof(section))
        };
        setup.WithCallback((_, _) => throw expected);

        var action = () => RunSection(test, section);

        var error = action.Should().Throw<PolicyException>().Which;
        error.Policy.Should().Be(nameof(IInboundContext.PublishToDarp));
        error.Section.Should().Be(section);
        error.InnerException.Should().BeSameAs(expected);
    }

    private static TestDocument CreateTest(PublishToDarpConfig config) =>
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

    private sealed class PolicyDocument(Func<IExpressionContext, PublishToDarpConfig> config) : IDocument
    {
        public void Inbound(IInboundContext context) => context.PublishToDarp(config(context.ExpressionContext));
        public void Outbound(IOutboundContext context) => context.PublishToDarp(config(context.ExpressionContext));
        public void OnError(IOnErrorContext context) => context.PublishToDarp(config(context.ExpressionContext));
        public void Backend(IBackendContext context) { }
    }

    private sealed class PubSubService : IDaprPubSubService
    {
        public List<DaprPublishRequest> Requests { get; } = [];
        public CancellationToken CancellationToken { get; private set; }
        public Func<DaprPublishRequest, CancellationToken, Task<IResponse>> Handler { get; init; } =
            (_, _) => Task.FromResult<IResponse>(new MockResponse());

        public Task<IResponse> PublishAsync(DaprPublishRequest request, CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            CancellationToken = cancellationToken;
            return Handler(request, cancellationToken);
        }
    }
}
