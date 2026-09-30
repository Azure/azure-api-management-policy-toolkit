// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Document;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Expressions;

namespace Test.Emulator.Emulator.Policies;

[TestClass]
public class SetVariableTests
{
    class SimpleSetVariable : IDocument
    {
        public void Inbound(IInboundContext context)
        {
            context.SetVariable("inbound-var", new ConfigValue("inbound-value"));
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
            context.SetVariable("B", new ConfigValue("value-b"));
        }

        public void Outbound(IOutboundContext context) { }
        public void Backend(IBackendContext context) { }
        public void OnError(IOnErrorContext context) { }
    }

    [TestMethod]
    public void SetVariable_Inbound_AssignsExpressionValueAsString()
    {
        var test = new SimpleSetVariable().AsTestDocument();

        test.RunInbound();

        test.Context.Variables.Should().ContainKey("inbound-var")
            .WhoseValue.Should().BeOfType<string>().Which.Should().Be("inbound-value");
    }

    [TestMethod]
    public void SetVariable_Backend_AssignsTypedValue()
    {
        var test = new SimpleSetVariable().AsTestDocument();

        test.RunBackend();

        test.Context.Variables.Should().ContainKey("backend-var")
            .WhoseValue.Should().Be(42);
    }

    [TestMethod]
    public void SetVariable_Outbound_AssignsBooleanValue()
    {
        var test = new SimpleSetVariable().AsTestDocument();

        test.RunOutbound();

        test.Context.Variables.Should().ContainKey("outbound-var")
            .WhoseValue.Should().Be(true);
    }

    [TestMethod]
    public void SetVariable_OnError_AssignsStringValue()
    {
        var test = new SimpleSetVariable().AsTestDocument();

        test.RunOnError();

        test.Context.Variables.Should().ContainKey("onerror-var")
            .WhoseValue.Should().Be("error-value");
    }

    [TestMethod]
    public void SetVariable_ExistingValue_IsOverwritten()
    {
        var test = new SimpleSetVariable().AsTestDocument();
        test.Context.Variables["inbound-var"] = "old-value";

        test.RunInbound();

        test.Context.Variables["inbound-var"].Should().Be("inbound-value");
    }

    [TestMethod]
    public void SetVariable_Inbound_CallbackOverrideReplacesDefaultValue()
    {
        var test = new MultiSetVariable().AsTestDocument();

        test.SetupInbound().SetVariable((_, name, _) => name == "B").WithCallback((context, name, _) =>
        {
            context.Variables[name] = "callback-overridden";
        });

        test.RunInbound();

        test.Context.Variables.Should().ContainKey("A")
            .WhoseValue.Should().Be("value-a");
        test.Context.Variables.Should().ContainKey("B")
            .WhoseValue.Should().Be("callback-overridden");
    }
}
