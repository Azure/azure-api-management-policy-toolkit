// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Document;

namespace Test.Emulator.Emulator.Policies;

[TestClass]
public class SetVariableTests
{
    class SimpleSetVariable : IDocument
    {
        public void Inbound(IInboundContext context)
        {
            context.SetVariable("inbound-var", "inbound-value");
        }

        public void Backend(IBackendContext context)
        {
            context.SetVariable("backend-var", 42);
        }

        public void Outbound(IOutboundContext context)
        {
            context.SetVariable("outbound-var", true);
        }

        public void OnError(IOnErrorContext context)
        {
            context.SetVariable("onerror-var", "error-value");
        }
    }

    class MultiSetVariable : IDocument
    {
        public void Inbound(IInboundContext context)
        {
            context.SetVariable("A", "value-a");
            context.SetVariable("B", "value-b");
        }

        public void Outbound(IOutboundContext context) { }
        public void Backend(IBackendContext context) { }
        public void OnError(IOnErrorContext context) { }
    }

    [TestMethod]
    public void SetVariable_Inbound()
    {
        var test = new SimpleSetVariable().AsTestDocument();

        test.RunInbound();

        test.Context.Variables.Should().ContainKey("inbound-var")
            .WhoseValue.Should().Be("inbound-value");
    }

    [TestMethod]
    public void SetVariable_Backend()
    {
        var test = new SimpleSetVariable().AsTestDocument();

        test.RunBackend();

        test.Context.Variables.Should().ContainKey("backend-var")
            .WhoseValue.Should().Be(42);
    }

    [TestMethod]
    public void SetVariable_Outbound()
    {
        var test = new SimpleSetVariable().AsTestDocument();

        test.RunOutbound();

        test.Context.Variables.Should().ContainKey("outbound-var")
            .WhoseValue.Should().Be(true);
    }

    [TestMethod]
    public void SetVariable_OnError()
    {
        var test = new SimpleSetVariable().AsTestDocument();

        test.RunOnError();

        test.Context.Variables.Should().ContainKey("onerror-var")
            .WhoseValue.Should().Be("error-value");
    }

    [TestMethod]
    public void SetVariable_Callback()
    {
        var test = new SimpleSetVariable().AsTestDocument();
        var callbackExecuted = false;
        test.SetupInbound().SetVariable().WithCallback((_, _, _) =>
        {
            callbackExecuted = true;
        });

        test.RunInbound();

        callbackExecuted.Should().BeTrue();
        test.Context.Variables.Should().NotContainKey("inbound-var");
    }

    [TestMethod]
    public void SetVariable_PredicateCallback()
    {
        var test = new MultiSetVariable().AsTestDocument();
        var callbackExecuted = false;
        test.SetupInbound().SetVariable((_, name, _) => name == "B").WithCallback((context, name, value) =>
        {
            callbackExecuted = true;
            context.Variables[name] = "overridden";
        });

        test.RunInbound();

        callbackExecuted.Should().BeTrue();
        test.Context.Variables.Should().ContainKey("A")
            .WhoseValue.Should().Be("value-a");
        test.Context.Variables.Should().ContainKey("B")
            .WhoseValue.Should().Be("overridden");
    }

    [TestMethod]
    public void SetVariable_OverwritesExistingVariable()
    {
        var test = new SimpleSetVariable().AsTestDocument();
        test.Context.Variables["inbound-var"] = "old-value";

        test.RunInbound();

        test.Context.Variables.Should().ContainKey("inbound-var")
            .WhoseValue.Should().Be("inbound-value");
    }
}
