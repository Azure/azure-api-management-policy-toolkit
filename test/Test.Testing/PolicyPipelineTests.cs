// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Document;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator;

namespace Test.Emulator.Emulator.Policies;

[TestClass]
public class PolicyPipelineTests
{
    [TestMethod]
    [DataRow("inbound", "Global,Workspace,Product,Api,Operation")]
    [DataRow("backend", "Global,Workspace,Product,Api,Operation")]
    [DataRow("outbound", "Operation,Api,Product,Workspace,Global")]
    [DataRow("on-error", "Operation,Api,Product,Workspace,Global")]
    public void PolicyPipeline_ExecutesAllScopesInTheDocumentedSectionOrder(string section, string order)
    {
        var calls = new List<string>();
        var pipeline = CreatePipeline(calls);

        ExecutionTest.RunSection(pipeline, section);

        calls.Should().Equal(order.Split(',').SelectMany(scope => new[]
        {
            $"{scope}:{section}:before", $"{scope}:{section}:after"
        }));
    }

    [TestMethod]
    [DataRow("inbound", false, "Global,Workspace,Product")]
    [DataRow("backend", false, "Global,Workspace,Product")]
    [DataRow("outbound", false, "Operation,Api,Product")]
    [DataRow("on-error", false, "Operation,Api,Product")]
    [DataRow("inbound", true, "Global,Workspace,Product")]
    [DataRow("backend", true, "Global,Workspace,Product")]
    [DataRow("outbound", true, "Operation,Api,Product")]
    [DataRow("on-error", true, "Operation,Api,Product")]
    public void PolicyPipeline_ReturnResponseStopsRemainingScopesAndUnwindsNestedBase(
        string section, bool nested, string visited)
    {
        var calls = new List<string>();
        var pipeline = CreatePipeline(calls, nested, section);

        ExecutionTest.RunSection(pipeline, section, nested);

        var scopes = visited.Split(',');
        var expected = nested
            ? scopes.Select(scope => $"{scope}:{section}:before")
            : scopes.SelectMany(scope => scope == "Product"
                ? new[] { $"{scope}:{section}:before" }
                : new[] { $"{scope}:{section}:before", $"{scope}:{section}:after" });
        calls.Should().Equal(expected);
        pipeline.Context.Response.StatusCode.Should().Be(202);
        pipeline.Context.Response.Body.Content.Should().Be("returned");
        pipeline.Context.ResponseTerminated.Should().BeTrue();
    }

    [TestMethod]
    [DataRow("inbound", false)]
    [DataRow("backend", false)]
    [DataRow("outbound", false)]
    [DataRow("on-error", false)]
    [DataRow("inbound", true)]
    [DataRow("backend", true)]
    [DataRow("outbound", true)]
    [DataRow("on-error", true)]
    public void PolicyPipeline_TerminationPreventsEveryLaterCoordinatedSection(string section, bool nested)
    {
        var calls = new List<string>();
        var pipeline = PolicyPipelineBuilder.Create()
            .AddPolicy(PolicyScope.Operation,
                ExecutionTest.FlowDocument("Operation", calls, nested, section))
            .Build();

        ExecutionTest.RunSection(pipeline, section, nested);
        pipeline.RunAll();
        pipeline.RunAllNested();
        pipeline.RunOnError();
        pipeline.RunOnErrorNested();

        calls.Should().Equal($"Operation:{section}:before");
        pipeline.Context.ResponseTerminated.Should().BeTrue();
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void PolicyPipeline_MockResponseStopsSubsequentScopesAndSections(bool nested)
    {
        var calls = new List<string>();
        var pipeline = PolicyPipelineBuilder.Create()
            .AddPolicy(PolicyScope.Global, new ExecutionTestDocument
            {
                InboundAction = context => context.MockResponse(new MockResponseConfig { StatusCode = 202 })
            })
            .AddPolicy(PolicyScope.Operation, ExecutionTest.FlowDocument("Operation", calls, nested))
            .Build();

        if (nested)
        {
            pipeline.RunAllNested();
        }
        else
        {
            pipeline.RunAll();
        }
        pipeline.RunOnError();

        calls.Should().BeEmpty();
        pipeline.Context.Response.StatusCode.Should().Be(202);
        pipeline.Context.ResponseTerminated.Should().BeTrue();
    }

    [TestMethod]
    [DataRow("inbound", false)]
    [DataRow("backend", false)]
    [DataRow("outbound", false)]
    [DataRow("on-error", false)]
    [DataRow("inbound", true)]
    [DataRow("backend", true)]
    [DataRow("outbound", true)]
    [DataRow("on-error", true)]
    public void PolicyPipeline_DirectTerminationSignalsAreNotSilentlySectionOnly(string section, bool nested)
    {
        var calls = new List<string>();
        var first = section is "inbound" or "backend" ? PolicyScope.Global : PolicyScope.Operation;
        var last = section is "inbound" or "backend" ? PolicyScope.Operation : PolicyScope.Global;
        var pipeline = PolicyPipelineBuilder.Create()
            .AddPolicy(first, new ExecutionTestDocument
            {
                InboundAction = _ => throw new FinishSectionProcessingException(),
                BackendAction = _ => throw new FinishSectionProcessingException(),
                OutboundAction = _ => throw new FinishSectionProcessingException(),
                OnErrorAction = _ => throw new FinishSectionProcessingException()
            })
            .AddPolicy(last, ExecutionTest.FlowDocument("later", calls, nested))
            .Build();

        ExecutionTest.RunSection(pipeline, section, nested);
        pipeline.RunAll();
        pipeline.RunAllNested();
        pipeline.RunOnError();

        calls.Should().BeEmpty();
        pipeline.Context.ResponseTerminated.Should().BeTrue();
    }

    [TestMethod]
    [DataRow("inbound", "Global,Workspace,Product,Api,Operation")]
    [DataRow("backend", "Global,Workspace,Product,Api,Operation")]
    [DataRow("outbound", "Operation,Api,Product,Workspace,Global")]
    [DataRow("on-error", "Operation,Api,Product,Workspace,Global")]
    public void PolicyPipeline_InvokeRequestStopsOnlyItsCurrentScopeSection(string section, string visited)
    {
        var calls = new List<string>();
        var pipeline = CreatePipeline(calls, false, section, true);
        SetupInvokeRequest(pipeline.Context);

        ExecutionTest.RunSection(pipeline, section);

        calls.Should().Equal(visited.Split(',').SelectMany(scope => scope == "Product"
            ? new[] { $"{scope}:{section}:before" }
            : new[] { $"{scope}:{section}:before", $"{scope}:{section}:after" }));
        pipeline.Context.Response.StatusCode.Should().Be(203);
        pipeline.Context.ResponseTerminated.Should().BeFalse();

        calls.Clear();
        pipeline.RunAll();
        pipeline.RunOnError();
        calls.Should().Contain(["Operation:backend:before", "Global:outbound:after", "Global:on-error:after"]);
        pipeline.Context.ResponseTerminated.Should().BeFalse();
    }

    [TestMethod]
    [DataRow("inbound")]
    [DataRow("backend")]
    [DataRow("outbound")]
    [DataRow("on-error")]
    public void PolicyPipeline_NestedInvokeRequestDoesNotTerminateCallerScopesOrLaterSections(string section)
    {
        var calls = new List<string>();
        var pipeline = CreatePipeline(calls, true, section, true);
        SetupInvokeRequest(pipeline.Context);

        ExecutionTest.RunSection(pipeline, section, true);

        var first = section is "inbound" or "backend" ? "Global" : "Operation";
        var second = section is "inbound" or "backend" ? "Workspace" : "Api";
        calls.Should().Equal(
            $"{first}:{section}:before", $"{second}:{section}:before", $"Product:{section}:before",
            $"{second}:{section}:after", $"{first}:{section}:after");
        pipeline.Context.ResponseTerminated.Should().BeFalse();

        calls.Clear();
        pipeline.RunAllNested();
        pipeline.RunOnErrorNested();
        calls.Should().Contain(["Global:backend:before", "Operation:outbound:before", "Operation:on-error:before"]);
        pipeline.Context.ResponseTerminated.Should().BeFalse();
    }

    [TestMethod]
    [DataRow("inbound")]
    [DataRow("backend")]
    [DataRow("outbound")]
    public void PolicyPipeline_IndependentExecutionIgnoresTerminationFromEarlierScopesAndSections(string section)
    {
        var calls = new List<string>();
        var pipeline = CreatePipeline(calls, false, section);

        ExecutionTest.RunIndependent(pipeline, section);

        calls.Should().Contain([$"Global:{section}:before", $"Operation:{section}:before"]);
        calls.Should().NotContain($"Product:{section}:after");
        pipeline.Context.ResponseTerminated.Should().BeTrue();

        calls.Clear();
        pipeline.RunInboundIndependent();
        pipeline.RunBackendIndependent();
        pipeline.RunOutboundIndependent();
        calls.Should().Contain(["Global:inbound:before", "Operation:backend:before", "Global:outbound:before"]);
    }

    [TestMethod]
    public void PolicyPipeline_IndependentInboundReportsIsolatedFailuresAndStillRunsLaterScopes()
    {
        var calls = new List<string>();
        var traces = new List<string>();
        var pipeline = PolicyPipelineBuilder.Create()
            .ConfigureContext(context => context.Trace = traces.Add)
            .AddPolicy(PolicyScope.Global, new ExecutionTestDocument
            {
                InboundAction = _ => throw new InvalidOperationException("isolated failure")
            })
            .AddPolicy(PolicyScope.Operation, ExecutionTest.FlowDocument("Operation", calls))
            .Build();

        pipeline.RunInboundIndependent();

        calls.Should().Equal("Operation:inbound:before", "Operation:inbound:after");
        traces.Should().ContainSingle().Which.Should().Contain("Global").And.Contain("isolated failure");
        pipeline.Context.ResponseTerminated.Should().BeFalse();
    }

    [TestMethod]
    [DataRow("inbound", false)]
    [DataRow("backend", false)]
    [DataRow("outbound", false)]
    [DataRow("on-error", false)]
    [DataRow("inbound", true)]
    [DataRow("backend", true)]
    [DataRow("outbound", true)]
    [DataRow("on-error", true)]
    public void PolicyPipeline_CoordinatedExecutionDoesNotSwallowDocumentFailures(string section, bool nested)
    {
        var pipeline = PolicyPipelineBuilder.Create()
            .AddPolicy(PolicyScope.Operation, new ExecutionTestDocument
            {
                InboundAction = _ => throw new InvalidOperationException("document failure"),
                BackendAction = _ => throw new InvalidOperationException("document failure"),
                OutboundAction = _ => throw new InvalidOperationException("document failure"),
                OnErrorAction = _ => throw new InvalidOperationException("document failure")
            })
            .Build();

        var error = Assert.ThrowsExactly<InvalidOperationException>(
            () => ExecutionTest.RunSection(pipeline, section, nested));

        error.Message.Should().Be("document failure");
        pipeline.Context.ResponseTerminated.Should().BeFalse();
    }

    private static PolicyPipeline CreatePipeline(
        List<string> calls, bool nested = false, string? stopSection = null, bool invokeRequest = false)
    {
        var builder = PolicyPipelineBuilder.Create();
        foreach (var scope in new[]
                 {
                     PolicyScope.Operation, PolicyScope.Api, PolicyScope.Product,
                     PolicyScope.Workspace, PolicyScope.Global
                 })
        {
            builder.AddPolicy(scope, ExecutionTest.FlowDocument(
                scope.ToString(), calls, nested, scope == PolicyScope.Product ? stopSection : null, invokeRequest));
        }

        return builder.Build();
    }

    internal static void SetupInvokeRequest(GatewayContext context)
    {
        var setup = new TestDocument(new ExecutionTestDocument()) { Context = context };
        void Callback(GatewayContext gateway, InvokeRequestConfig config)
        {
            gateway.Response.StatusCode = 203;
            gateway.Response.Body.Content = "invoked";
        }

        setup.SetupInbound().InvokeRequest().WithCallback(Callback);
        setup.SetupBackend().InvokeRequest().WithCallback(Callback);
        setup.SetupOutbound().InvokeRequest().WithCallback(Callback);
        setup.SetupOnError().InvokeRequest().WithCallback(Callback);
    }
}

internal sealed class ExecutionTestDocument : IDocument
{
    public Action<IInboundContext>? InboundAction { get; init; }
    public Action<IBackendContext>? BackendAction { get; init; }
    public Action<IOutboundContext>? OutboundAction { get; init; }
    public Action<IOnErrorContext>? OnErrorAction { get; init; }

    public void Inbound(IInboundContext context) => InboundAction?.Invoke(context);
    public void Backend(IBackendContext context) => BackendAction?.Invoke(context);
    public void Outbound(IOutboundContext context) => OutboundAction?.Invoke(context);
    public void OnError(IOnErrorContext context) => OnErrorAction?.Invoke(context);
}

internal static class ExecutionTest
{
    public static string SectionName(string section) => section switch
    {
        "inbound" => nameof(IInboundContext),
        "backend" => nameof(IBackendContext),
        "outbound" => nameof(IOutboundContext),
        "on-error" => nameof(IOnErrorContext),
        _ => throw new ArgumentOutOfRangeException(nameof(section), section, null)
    };

    public static void RunSection(TestDocument test, string section)
    {
        switch (section)
        {
            case "inbound": test.RunInbound(); break;
            case "backend": test.RunBackend(); break;
            case "outbound": test.RunOutbound(); break;
            case "on-error": test.RunOnError(); break;
            default: throw new ArgumentOutOfRangeException(nameof(section), section, null);
        }
    }

    public static void RunSection(PolicyPipeline pipeline, string section, bool nested = false)
    {
        switch (section, nested)
        {
            case ("inbound", false): pipeline.RunInbound(); break;
            case ("backend", false): pipeline.RunBackend(); break;
            case ("outbound", false): pipeline.RunOutbound(); break;
            case ("on-error", false): pipeline.RunOnError(); break;
            case ("inbound", true): pipeline.RunInboundNested(); break;
            case ("backend", true): pipeline.RunBackendNested(); break;
            case ("outbound", true): pipeline.RunOutboundNested(); break;
            case ("on-error", true): pipeline.RunOnErrorNested(); break;
            default: throw new ArgumentOutOfRangeException(nameof(section), section, null);
        }
    }

    public static void RunIndependent(PolicyPipeline pipeline, string section)
    {
        switch (section)
        {
            case "inbound": pipeline.RunInboundIndependent(); break;
            case "backend": pipeline.RunBackendIndependent(); break;
            case "outbound": pipeline.RunOutboundIndependent(); break;
            default: throw new ArgumentOutOfRangeException(nameof(section), section, null);
        }
    }

    public static ExecutionTestDocument FlowDocument(
        string name, List<string> calls, bool chain = false, string? stopSection = null, bool invokeRequest = false)
    {
        void Finish(Action<ReturnResponseConfig> returnResponse, Action<InvokeRequestConfig> invoke)
        {
            if (invokeRequest)
            {
                invoke(new InvokeRequestConfig { Url = "https://example.com/invoke" });
            }
            else
            {
                returnResponse(new ReturnResponseConfig
                {
                    Status = new StatusConfig { Code = 202, Reason = "Accepted" },
                    Body = new BodyConfig { Content = "returned" }
                });
            }
        }

        void Record(string section, Action @base, Action finish)
        {
            calls.Add($"{name}:{section}:before");
            if (section == stopSection)
            {
                finish();
            }
            if (chain)
            {
                @base();
            }
            calls.Add($"{name}:{section}:after");
        }

        return new ExecutionTestDocument
        {
            InboundAction = context => Record("inbound", () => context.WithId("base").Base(),
                () => Finish(context.ReturnResponse, context.InvokeRequest)),
            BackendAction = context => Record("backend", () => context.WithId("base").Base(),
                () => Finish(context.ReturnResponse, context.InvokeRequest)),
            OutboundAction = context => Record("outbound", () => context.WithId("base").Base(),
                () => Finish(context.ReturnResponse, context.InvokeRequest)),
            OnErrorAction = context => Record("on-error", () => context.WithId("base").Base(),
                () => Finish(context.ReturnResponse, context.InvokeRequest))
        };
    }
}