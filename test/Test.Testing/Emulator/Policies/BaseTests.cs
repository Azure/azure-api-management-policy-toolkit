// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Document;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator;

namespace Test.Emulator.Emulator.Policies;

[TestClass]
public class BaseTests
{
    class SimpleBase : IDocument
    {
        public void Inbound(IInboundContext context)
        {
            context.Base();
        }

        public void Backend(IBackendContext context)
        {
            context.Base();
        }

        public void Outbound(IOutboundContext context)
        {
            context.Base();
        }

        public void OnError(IOnErrorContext context)
        {
            context.Base();
        }
    }

    [TestMethod]
    public void Base_Inbound_HandleSimple()
    {
        // Arrange
        var test = new TestDocument(new SimpleBase());
        bool baseExecuted = false;
        test.SetupInbound().Base().WithCallback(_ => baseExecuted = !baseExecuted);

        // Act
        test.RunInbound();

        // Assert
        baseExecuted.Should().BeTrue();
    }

    [TestMethod]
    public void Base_Backend_HandleSimple()
    {
        // Arrange
        var test = new TestDocument(new SimpleBase());
        bool baseExecuted = false;

        test.SetupBackend().Base().WithCallback(_ => baseExecuted = !baseExecuted);

        // Act
        test.RunBackend();

        // Assert
        baseExecuted.Should().BeTrue();
    }

    [TestMethod]
    public void Base_Outbound_HandleSimple()
    {
        // Arrange
        var test = new TestDocument(new SimpleBase());
        bool baseExecuted = false;

        test.SetupOutbound().Base().WithCallback(_ => baseExecuted = !baseExecuted);

        // Act
        test.RunOutbound();

        // Assert
        baseExecuted.Should().BeTrue();
    }

    [TestMethod]
    public void BaseOnError_HandleSimple()
    {
        // Arrange
        var test = new TestDocument(new SimpleBase());
        bool baseExecuted = false;

        test.SetupOnError().Base().WithCallback(_ => baseExecuted = !baseExecuted);

        // Act
        test.RunOnError();

        // Assert
        baseExecuted.Should().BeTrue();
    }

    [TestMethod]
    public void Base_HandleSimple_WithPredicate()
    {
        // Arrange
        var test = new TestDocument(new SimpleBase()) { Context = { Variables = { { "a", true } } } };
        test.SetupInbound().Base(context => context.Variables.ContainsKey("b")).WithCallback(context =>
        {
            context.Variables.Remove("a");
            context.Variables.Remove("b");
        });

        // Act
        test.RunInbound();

        // Assert
        test.Context.Variables.Should().ContainKey("a");

        // Arrange
        test.Context.Variables.Add("b", true);

        // Act
        test.RunInbound();

        // Assert
        test.Context.Variables.Should().NotContainKey("a").And.NotContainKey("b");
    }

    [TestMethod]
    [DataRow("inbound")]
    [DataRow("backend")]
    [DataRow("outbound")]
    [DataRow("on-error")]
    public void Base_IsANoOpWithoutAMockOrNestedPipeline(string section)
    {
        var calls = new List<string>();
        var test = ExecutionTest.FlowDocument("standalone", calls, true).AsTestDocument();

        ExecutionTest.RunSection(test, section);

        calls.Should().Equal($"standalone:{section}:before", $"standalone:{section}:after");
        test.Context.ResponseTerminated.Should().BeFalse();
    }

    [TestMethod]
    [DataRow("inbound", "Global,Product,Operation")]
    [DataRow("backend", "Global,Product,Operation")]
    [DataRow("outbound", "Operation,Product,Global")]
    [DataRow("on-error", "Operation,Product,Global")]
    public void Base_NestedExecutionChainsThroughAvailableScopesAndReturnsInReverseOrder(
        string section, string scopeOrder)
    {
        var calls = new List<string>();
        var pipeline = PolicyPipelineBuilder.Create()
            .AddPolicy(PolicyScope.Product, ExecutionTest.FlowDocument("Product", calls, true))
            .AddPolicy(PolicyScope.Global, ExecutionTest.FlowDocument("Global", calls, true))
            .AddPolicy(PolicyScope.Operation, ExecutionTest.FlowDocument("Operation", calls, true))
            .Build();

        ExecutionTest.RunSection(pipeline, section, true);

        var scopes = scopeOrder.Split(',');
        calls.Should().Equal(scopes.Select(scope => $"{scope}:{section}:before")
            .Concat(scopes.Reverse().Select(scope => $"{scope}:{section}:after")));
        pipeline.Context.ResponseTerminated.Should().BeFalse();
    }

    [TestMethod]
    [DataRow("inbound", "Global")]
    [DataRow("backend", "Global")]
    [DataRow("outbound", "Operation")]
    [DataRow("on-error", "Operation")]
    public void Base_OmissionDoesNotImplicitlyExecuteTheNextNestedScope(string section, string first)
    {
        var calls = new List<string>();
        var pipeline = PolicyPipelineBuilder.Create()
            .AddPolicy(PolicyScope.Global, ExecutionTest.FlowDocument("Global", calls))
            .AddPolicy(PolicyScope.Operation, ExecutionTest.FlowDocument("Operation", calls))
            .Build();

        ExecutionTest.RunSection(pipeline, section, true);

        calls.Should().Equal($"{first}:{section}:before", $"{first}:{section}:after");
    }

    [TestMethod]
    [DataRow("inbound")]
    [DataRow("backend")]
    [DataRow("outbound")]
    [DataRow("on-error")]
    public void Base_EachCallExecutesTheNextNestedScope(string section)
    {
        var calls = new List<string>();
        var root = new ExecutionTestDocument
        {
            InboundAction = context => { context.Base(); context.Base(); },
            BackendAction = context => { context.Base(); context.Base(); },
            OutboundAction = context => { context.Base(); context.Base(); },
            OnErrorAction = context => { context.Base(); context.Base(); }
        };
        var first = section is "inbound" or "backend" ? PolicyScope.Global : PolicyScope.Operation;
        var last = section is "inbound" or "backend" ? PolicyScope.Operation : PolicyScope.Global;
        var pipeline = PolicyPipelineBuilder.Create()
            .AddPolicy(first, root)
            .AddPolicy(last, ExecutionTest.FlowDocument("inner", calls, true))
            .Build();

        ExecutionTest.RunSection(pipeline, section, true);

        calls.Should().Equal(
            $"inner:{section}:before", $"inner:{section}:after",
            $"inner:{section}:before", $"inner:{section}:after");
    }

    [TestMethod]
    [DataRow("inbound", "complete")]
    [DataRow("backend", "complete")]
    [DataRow("outbound", "complete")]
    [DataRow("on-error", "complete")]
    [DataRow("inbound", "terminate")]
    [DataRow("backend", "terminate")]
    [DataRow("outbound", "terminate")]
    [DataRow("on-error", "terminate")]
    [DataRow("inbound", "error")]
    [DataRow("backend", "error")]
    [DataRow("outbound", "error")]
    [DataRow("on-error", "error")]
    public void Base_RestoresExistingPredicateHooksAfterEveryNestedExit(string section, string exit)
    {
        var calls = new List<string>();
        var first = section is "inbound" or "backend" ? PolicyScope.Global : PolicyScope.Operation;
        var last = section is "inbound" or "backend" ? PolicyScope.Operation : PolicyScope.Global;
        var inner = exit == "error"
            ? new ExecutionTestDocument
            {
                InboundAction = _ => throw new InvalidOperationException("inner failure"),
                BackendAction = _ => throw new InvalidOperationException("inner failure"),
                OutboundAction = _ => throw new InvalidOperationException("inner failure"),
                OnErrorAction = _ => throw new InvalidOperationException("inner failure")
            }
            : ExecutionTest.FlowDocument("inner", calls, true, exit == "terminate" ? section : null);
        var pipeline = PolicyPipelineBuilder.Create()
            .AddPolicy(first, ExecutionTest.FlowDocument("outer", calls, true))
            .AddPolicy(last, inner)
            .Build();
        var standalone = new TestDocument(new SimpleBase()) { Context = pipeline.Context };
        var callbacks = 0;
        void Unmatched(GatewayContext context) => Assert.Fail("The false predicate must remain false.");
        void Callback(GatewayContext context) => callbacks++;

        standalone.SetupInbound().Base(_ => false).WithCallback(Unmatched);
        standalone.SetupInbound().Base().WithCallback(Callback);
        standalone.SetupBackend().Base(_ => false).WithCallback(Unmatched);
        standalone.SetupBackend().Base().WithCallback(Callback);
        standalone.SetupOutbound().Base(_ => false).WithCallback(Unmatched);
        standalone.SetupOutbound().Base().WithCallback(Callback);
        standalone.SetupOnError().Base(_ => false).WithCallback(Unmatched);
        standalone.SetupOnError().Base().WithCallback(Callback);

        if (exit == "error")
        {
            var error = Assert.ThrowsExactly<PolicyException>(
                () => ExecutionTest.RunSection(pipeline, section, true));
            error.Policy.Should().Be(nameof(IInboundContext.Base));
            error.Section.Should().Be(ExecutionTest.SectionName(section));
            error.InnerException.Should().BeOfType<InvalidOperationException>()
                .Which.Message.Should().Be("inner failure");
        }
        else
        {
            ExecutionTest.RunSection(pipeline, section, true);
        }

        callbacks.Should().Be(0);
        if (exit == "terminate")
        {
            calls.Should().Equal($"outer:{section}:before", $"inner:{section}:before");
            pipeline.Context.ResponseTerminated.Should().BeTrue();
        }

        ExecutionTest.RunSection(standalone, section);

        callbacks.Should().Be(1);
    }

    [TestMethod]
    [DataRow("inbound")]
    [DataRow("backend")]
    [DataRow("outbound")]
    [DataRow("on-error")]
    public void Base_CallbackErrorsKeepThePolicyAndSectionDiagnostics(string section)
    {
        var test = new SimpleBase().AsTestDocument();
        void Callback(GatewayContext context) => throw new InvalidOperationException("base callback failure");
        test.SetupInbound().Base().WithCallback(Callback);
        test.SetupBackend().Base().WithCallback(Callback);
        test.SetupOutbound().Base().WithCallback(Callback);
        test.SetupOnError().Base().WithCallback(Callback);

        var error = Assert.ThrowsExactly<PolicyException>(() => ExecutionTest.RunSection(test, section));

        error.Policy.Should().Be(nameof(IInboundContext.Base));
        error.Section.Should().Be(ExecutionTest.SectionName(section));
        error.PolicyArgs.Should().BeEmpty();
        error.InnerException.Should().BeOfType<InvalidOperationException>()
            .Which.Message.Should().Be("base callback failure");
        test.Context.ResponseTerminated.Should().BeFalse();
    }

    [TestMethod]
    public void Base_CallbackTerminationStopsTheCurrentStandaloneInvocationButNotLaterManualSections()
    {
        var test = new ExecutionTestDocument
        {
            InboundAction = context =>
            {
                context.Base();
                context.SetVariable("after", true);
            },
            OutboundAction = context => context.SetVariable("outbound", true)
        }.AsTestDocument();
        test.SetupInbound().Base().WithCallback(context =>
        {
            context.Response.StatusCode = 204;
            throw new FinishSectionProcessingException();
        });

        test.RunInbound();
        test.RunOutbound();

        test.Context.Response.StatusCode.Should().Be(204);
        test.Context.ResponseTerminated.Should().BeTrue();
        test.Context.Variables.Should().NotContainKey("after").And.ContainKey("outbound");
    }
}