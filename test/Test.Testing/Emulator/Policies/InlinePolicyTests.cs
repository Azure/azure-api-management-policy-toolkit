// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Document;

namespace Test.Emulator.Emulator.Policies;

[TestClass]
public class InlinePolicyTests
{
    class SimpleInlinePolicy : IDocument
    {
        public void Inbound(IInboundContext context)
        {
            context.InlinePolicy("<set-header name=\"X-Test\" exists-action=\"override\"><value>test</value></set-header>");
        }

        public void Backend(IBackendContext context)
        {
            context.InlinePolicy("<set-backend-service base-url=\"https://backend.example.com\" />");
        }

        public void Outbound(IOutboundContext context)
        {
            context.InlinePolicy("<set-header name=\"X-Out\" exists-action=\"override\"><value>out</value></set-header>");
        }

        public void OnError(IOnErrorContext context)
        {
            context.InlinePolicy("<set-status code=\"500\" reason=\"Error\" />");
        }
    }

    [TestMethod]
    public void InlinePolicy_Inbound()
    {
        var test = new SimpleInlinePolicy().AsTestDocument();

        test.RunInbound();

        // InlinePolicy is a no-op in the emulator — raw XML is not processed
    }

    [TestMethod]
    public void InlinePolicy_Backend()
    {
        var test = new SimpleInlinePolicy().AsTestDocument();

        test.RunBackend();
    }

    [TestMethod]
    public void InlinePolicy_Outbound()
    {
        var test = new SimpleInlinePolicy().AsTestDocument();

        test.RunOutbound();
    }

    [TestMethod]
    public void InlinePolicy_OnError()
    {
        var test = new SimpleInlinePolicy().AsTestDocument();

        test.RunOnError();
    }

    [TestMethod]
    public void InlinePolicy_Callback()
    {
        var test = new SimpleInlinePolicy().AsTestDocument();
        var callbackExecuted = false;
        test.SetupInbound().Inline().WithCallback((context, policy) =>
        {
            callbackExecuted = true;
            context.Variables["inline-policy"] = policy;
        });

        test.RunInbound();

        callbackExecuted.Should().BeTrue();
        test.Context.Variables.Should().ContainKey("inline-policy")
            .WhoseValue.Should().Be("<set-header name=\"X-Test\" exists-action=\"override\"><value>test</value></set-header>");
    }

    [TestMethod]
    public void InlinePolicy_PredicateCallback()
    {
        var test = new SimpleInlinePolicy().AsTestDocument();
        var callbackExecuted = false;
        test.SetupInbound().Inline((_, policy) => policy.Contains("set-header")).WithCallback((context, _) =>
        {
            callbackExecuted = true;
        });

        test.RunInbound();

        callbackExecuted.Should().BeTrue();
    }
}
