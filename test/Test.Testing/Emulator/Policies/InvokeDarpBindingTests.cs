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
public class InvokeDarpBindingTests
{
    [TestMethod]
    [DataRow(nameof(IInboundContext))]
    [DataRow(nameof(IOutboundContext))]
    [DataRow(nameof(IOnErrorContext))]
    public void ShouldInvokeBindingAndStoreExactResponse(string section)
    {
        var config = new InvokeDarpBindingConfig
        {
            Name = "external-system",
            Operation = "create",
            MetaData =
            [
                new DarpMetaData { Key = "source", Value = "api-management" },
                new DarpMetaData { Key = "optional", Value = "" }
            ],
            Data = """{"order":"{{body.order}}"}""",
            Template = "Liquid",
            ContentType = "application/json",
            Timeout = 8,
            ResponseVariableName = "binding-response"
        };
        var test = CreateTest(config);
        test.Context.Request.Body.Content = "original request";
        test.Context.Response.StatusCode = 202;
        test.Context.Response.Body.Content = "original response";
        var response = new MockResponse { StatusCode = 201, StatusReason = "Created" };
        response.Headers["X-Binding"] = ["first", "second"];
        response.Body.Content = """{"id":"123"}""";
        var service = new BindingService { Handler = (_, _) => Task.FromResult<IResponse>(response) };
        test.Context.Services.Register<IDaprBindingService>(service);

        RunSection(test, section);

        service.Requests.Should().ContainSingle().Which.Should().BeEquivalentTo(new DaprBindingRequest(
            "external-system", "create",
            new Dictionary<string, string> { ["source"] = "api-management", ["optional"] = "" },
            """{"order":"{{body.order}}"}""", "Liquid", "application/json", TimeSpan.FromSeconds(8)));
        service.CancellationToken.CanBeCanceled.Should().BeTrue();
        test.Context.Variables.Should().ContainSingle()
            .Which.Should().Be(new KeyValuePair<string, object>("binding-response", response));
        test.Context.Variables["binding-response"].Should().BeSameAs(response);
        test.Context.Request.Body.Content.Should().Be("original request");
        test.Context.Request.Body.Consumed.Should().BeFalse();
        test.Context.Response.StatusCode.Should().Be(202);
        test.Context.Response.Body.Content.Should().Be("original response");
    }

    [TestMethod]
    [DataRow(nameof(IInboundContext))]
    [DataRow(nameof(IOutboundContext))]
    [DataRow(nameof(IOnErrorContext))]
    public void ShouldUseDefaultsAndPreserveRequestBodyWhenDataIsOmitted(string section)
    {
        var test = CreateTest(new InvokeDarpBindingConfig { Name = "binding" });
        test.Context.Request.Body.Content = "request payload";
        test.Context.Response.Body.Content = "response payload";
        var service = new BindingService();
        test.Context.Services.Register<IDaprBindingService>(service);

        RunSection(test, section);

        service.Requests.Should().ContainSingle().Which.Should().BeEquivalentTo(new DaprBindingRequest(
            "binding", null, new Dictionary<string, string>(), "request payload", null, null,
            TimeSpan.FromSeconds(5)));
        test.Context.Request.Body.Consumed.Should().BeFalse();
        test.Context.Variables.Should().BeEmpty();
    }

    [TestMethod]
    public void ShouldNotReplaceExplicitlyEmptyDataWithRequestBody()
    {
        var test = CreateTest(new InvokeDarpBindingConfig { Name = "binding", Data = "" });
        test.Context.Request.Body.Content = "request payload";
        var service = new BindingService();
        test.Context.Services.Register<IDaprBindingService>(service);

        test.RunInbound();

        service.Requests.Should().ContainSingle().Which.Data.Should().BeEmpty();
        test.Context.Request.Body.Consumed.Should().BeFalse();
    }

    [TestMethod]
    [DataRow(nameof(IInboundContext))]
    [DataRow(nameof(IOutboundContext))]
    [DataRow(nameof(IOnErrorContext))]
    public void ShouldEvaluateNameTimeoutDataAndMetadataExpressions(string section)
    {
        var test = new PolicyDocument(context => new InvokeDarpBindingConfig
        {
            Name = (string)context.Variables["binding"],
            Timeout = (int)context.Variables["timeout"],
            Data = (context.Request.Body ?? throw new InvalidOperationException("Request body is required."))
                .As<string>(preserveContent: true),
            MetaData = [new DarpMetaData { Key = "client-ip", Value = context.Request.IpAddress }]
        }).AsTestDocument();
        test.Context.Variables["binding"] = "expression-binding";
        test.Context.Variables["timeout"] = 12;
        test.Context.Request.IpAddress = "192.0.2.1";
        test.Context.Request.Body.Content = """{"expression":true}""";
        var service = new BindingService();
        test.Context.Services.Register<IDaprBindingService>(service);

        RunSection(test, section);

        service.Requests.Should().ContainSingle().Which.Should().BeEquivalentTo(new DaprBindingRequest(
            "expression-binding", null, new Dictionary<string, string> { ["client-ip"] = "192.0.2.1" },
            """{"expression":true}""", null, null, TimeSpan.FromSeconds(12)));
        test.Context.Request.Body.Consumed.Should().BeFalse();
        test.Context.Variables.Should().HaveCount(2);
    }

    [TestMethod]
    public void ShouldSnapshotCaseSensitiveMetadata()
    {
        var metadata = new[]
        {
            new DarpMetaData { Key = "key", Value = "first" },
            new DarpMetaData { Key = "Key", Value = "second" }
        };
        var test = CreateTest(new InvokeDarpBindingConfig { Name = "binding", MetaData = metadata });
        var service = new BindingService();
        test.Context.Services.Register<IDaprBindingService>(service);

        test.RunInbound();
        metadata[0] = metadata[0] with { Value = "changed" };

        service.Requests.Should().ContainSingle().Which.Metadata.Should()
            .BeEquivalentTo(new Dictionary<string, string> { ["key"] = "first", ["Key"] = "second" });
    }

    [TestMethod]
    [DataRow(1)]
    [DataRow(240)]
    public void ShouldAcceptTimeoutBoundariesInSeconds(int timeout)
    {
        var test = CreateTest(new InvokeDarpBindingConfig { Name = "binding", Timeout = timeout });
        var service = new BindingService();
        test.Context.Services.Register<IDaprBindingService>(service);

        test.RunInbound();

        service.Requests.Should().ContainSingle().Which.Timeout.Should().Be(TimeSpan.FromSeconds(timeout));
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
        var test = CreateTest(new InvokeDarpBindingConfig
        {
            Name = "binding", IgnoreError = ignoreError, ResponseVariableName = "result"
        });

        var action = () => RunSection(test, section);

        var error = action.Should().Throw<PolicyException>().Which;
        error.Policy.Should().Be(nameof(IInboundContext.InvokeDarpBinding));
        error.Section.Should().Be(section);
        error.InnerException.Should().BeOfType<InvalidOperationException>()
            .Which.Message.Should().Contain("IDaprBindingService").And.Contain("Register");
        test.Context.Variables.Should().BeEmpty();
    }

    [TestMethod]
    [DataRow("name-null")]
    [DataRow("name-empty")]
    [DataRow("name-whitespace")]
    [DataRow("operation-empty")]
    [DataRow("operation-whitespace")]
    [DataRow("response-empty")]
    [DataRow("response-whitespace")]
    [DataRow("timeout-zero")]
    [DataRow("timeout-negative")]
    [DataRow("timeout-over-limit")]
    [DataRow("template")]
    [DataRow("content-type")]
    [DataRow("metadata-null-item")]
    [DataRow("metadata-null-key")]
    [DataRow("metadata-empty-key")]
    [DataRow("metadata-whitespace-key")]
    [DataRow("metadata-null-value")]
    [DataRow("metadata-duplicate")]
    public void ShouldRejectInvalidConfigurationBeforeCallingService(string invalid)
    {
        var config = new InvokeDarpBindingConfig { Name = "binding", IgnoreError = true };
        config = invalid switch
        {
            "name-null" => config with { Name = null! },
            "name-empty" => config with { Name = "" },
            "name-whitespace" => config with { Name = " " },
            "operation-empty" => config with { Operation = "" },
            "operation-whitespace" => config with { Operation = " " },
            "response-empty" => config with { ResponseVariableName = "" },
            "response-whitespace" => config with { ResponseVariableName = " " },
            "timeout-zero" => config with { Timeout = 0 },
            "timeout-negative" => config with { Timeout = -1 },
            "timeout-over-limit" => config with { Timeout = 241 },
            "template" => config with { Template = "unsupported" },
            "content-type" => config with { ContentType = "text/plain" },
            "metadata-null-item" => config with { MetaData = [null!] },
            "metadata-null-key" => config with { MetaData = [new DarpMetaData { Key = null!, Value = "value" }] },
            "metadata-empty-key" => config with { MetaData = [new DarpMetaData { Key = "", Value = "value" }] },
            "metadata-whitespace-key" => config with { MetaData = [new DarpMetaData { Key = " ", Value = "value" }] },
            "metadata-null-value" => config with { MetaData = [new DarpMetaData { Key = "key", Value = null! }] },
            "metadata-duplicate" => config with
            {
                MetaData = [new DarpMetaData { Key = "key", Value = "first" }, new DarpMetaData { Key = "key", Value = "second" }]
            },
            _ => throw new ArgumentOutOfRangeException(nameof(invalid))
        };
        var test = CreateTest(config);
        var service = new BindingService();
        test.Context.Services.Register<IDaprBindingService>(service);

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
        var test = CreateTest(new InvokeDarpBindingConfig
        {
            Name = "binding", IgnoreError = ignoreError, ResponseVariableName = "result"
        });
        var response = new MockResponse { StatusCode = status, StatusReason = "Binding failed" };
        response.Body.Content = "remote error";
        var service = new BindingService { Handler = (_, _) => Task.FromResult<IResponse>(response) };
        test.Context.Services.Register<IDaprBindingService>(service);
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
        var test = CreateTest(new InvokeDarpBindingConfig
        {
            Name = "binding", IgnoreError = ignoreError, ResponseVariableName = "result"
        });
        test.Context.Variables["result"] = "stale response";
        var service = new BindingService { Handler = (_, _) => Task.FromException<IResponse>(expected) };
        test.Context.Services.Register<IDaprBindingService>(service);
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
        var test = CreateTest(new InvokeDarpBindingConfig
        {
            Name = "binding", Timeout = 1, IgnoreError = ignoreError, ResponseVariableName = "result"
        });
        var service = new BindingService { Handler = (_, _) => pending.Task };
        test.Context.Services.Register<IDaprBindingService>(service);
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
                action.Should().Throw<PolicyException>().Which.InnerException.Should().BeOfType<TimeoutException>();
            }

            stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(5));
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
        var test = CreateTest(new InvokeDarpBindingConfig { Name = "binding", IgnoreError = ignoreError });
        var service = new BindingService { Handler = (_, _) => Task.FromResult<IResponse>(null!) };
        test.Context.Services.Register<IDaprBindingService>(service);

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
        var config = new InvokeDarpBindingConfig { Name = "", Timeout = -1 };
        var test = CreateTest(config);
        var service = new BindingService();
        test.Context.Services.Register<IDaprBindingService>(service);
        InvokeDarpBindingConfig? captured = null;
        var setup = section switch
        {
            nameof(IInboundContext) => test.SetupInbound().InvokeDarpBinding((_, candidate) => ReferenceEquals(candidate, config)),
            nameof(IOutboundContext) => test.SetupOutbound().InvokeDarpBinding((_, candidate) => ReferenceEquals(candidate, config)),
            nameof(IOnErrorContext) => test.SetupOnError().InvokeDarpBinding((_, candidate) => ReferenceEquals(candidate, config)),
            _ => throw new ArgumentOutOfRangeException(nameof(section))
        };
        setup.WithCallback((context, candidate) =>
        {
            captured = candidate;
            context.Variables["callback"] = "binding override";
        });

        RunSection(test, section);

        captured.Should().BeSameAs(config);
        service.Requests.Should().BeEmpty();
        test.Context.Variables.Should().ContainSingle()
            .Which.Should().Be(new KeyValuePair<string, object>("callback", "binding override"));
    }

    [TestMethod]
    public void ShouldAllowCallbackWithoutRegisteredService()
    {
        var test = CreateTest(new InvokeDarpBindingConfig { Name = "binding" });
        test.SetupInbound().InvokeDarpBinding().WithCallback((context, _) => context.Variables["callback"] = true);

        test.RunInbound();

        test.Context.Variables["callback"].Should().Be(true);
    }

    [TestMethod]
    public void ShouldUseServiceWhenCallbackPredicateDoesNotMatch()
    {
        var test = CreateTest(new InvokeDarpBindingConfig { Name = "binding" });
        var service = new BindingService();
        test.Context.Services.Register<IDaprBindingService>(service);
        test.SetupInbound().InvokeDarpBinding((_, config) => config.Name == "other")
            .WithCallback((_, _) => Assert.Fail("Nonmatching callback was invoked."));

        test.RunInbound();

        service.Requests.Should().ContainSingle().Which.Name.Should().Be("binding");
    }

    [TestMethod]
    [DataRow(nameof(IInboundContext))]
    [DataRow(nameof(IOutboundContext))]
    [DataRow(nameof(IOnErrorContext))]
    public void ShouldPreservePolicyAndSectionForCallbackErrors(string section)
    {
        var test = CreateTest(new InvokeDarpBindingConfig { Name = "binding" });
        var expected = new HttpRequestException("callback failure");
        var setup = section switch
        {
            nameof(IInboundContext) => test.SetupInbound().InvokeDarpBinding(),
            nameof(IOutboundContext) => test.SetupOutbound().InvokeDarpBinding(),
            nameof(IOnErrorContext) => test.SetupOnError().InvokeDarpBinding(),
            _ => throw new ArgumentOutOfRangeException(nameof(section))
        };
        setup.WithCallback((_, _) => throw expected);

        var action = () => RunSection(test, section);

        var error = action.Should().Throw<PolicyException>().Which;
        error.Policy.Should().Be(nameof(IInboundContext.InvokeDarpBinding));
        error.Section.Should().Be(section);
        error.InnerException.Should().BeSameAs(expected);
    }

    private static TestDocument CreateTest(InvokeDarpBindingConfig config) =>
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

    private sealed class PolicyDocument(Func<IExpressionContext, InvokeDarpBindingConfig> config) : IDocument
    {
        public void Inbound(IInboundContext context) => context.InvokeDarpBinding(config(context.ExpressionContext));
        public void Outbound(IOutboundContext context) => context.InvokeDarpBinding(config(context.ExpressionContext));
        public void OnError(IOnErrorContext context) => context.InvokeDarpBinding(config(context.ExpressionContext));
        public void Backend(IBackendContext context) { }
    }

    private sealed class BindingService : IDaprBindingService
    {
        public List<DaprBindingRequest> Requests { get; } = [];
        public CancellationToken CancellationToken { get; private set; }
        public Func<DaprBindingRequest, CancellationToken, Task<IResponse>> Handler { get; init; } =
            (_, _) => Task.FromResult<IResponse>(new MockResponse());

        public Task<IResponse> InvokeAsync(DaprBindingRequest request, CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            CancellationToken = cancellationToken;
            return Handler(request, cancellationToken);
        }
    }
}
