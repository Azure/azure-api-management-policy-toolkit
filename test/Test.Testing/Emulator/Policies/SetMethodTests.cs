// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Document;

namespace Test.Emulator.Emulator.Policies;

[TestClass]
public class SetMethodTests
{
    class SimpleSetMethod : IDocument
    {
        public void Inbound(IInboundContext context)
        {
            context.SetMethod("POST");
        }

        public void Backend(IBackendContext context)
        {
            context.SetMethod("PUT");
        }

        public void Outbound(IOutboundContext context)
        {
            context.SetMethod("DELETE");
        }

        public void OnError(IOnErrorContext context)
        {
            context.SetMethod("PATCH");
        }
    }

    [TestMethod]
    public void SetMethod_Inbound()
    {
        var test = new SimpleSetMethod().AsTestDocument();
        test.Context.Request.Method = "GET";

        test.RunInbound();

        test.Context.Request.Method.Should().Be("POST");
    }

    [TestMethod]
    public void SetMethod_Backend()
    {
        var test = new SimpleSetMethod().AsTestDocument();
        test.Context.Request.Method = "GET";

        test.RunBackend();

        test.Context.Request.Method.Should().Be("PUT");
    }

    [TestMethod]
    public void SetMethod_Outbound()
    {
        var test = new SimpleSetMethod().AsTestDocument();
        test.Context.Request.Method = "GET";

        test.RunOutbound();

        test.Context.Request.Method.Should().Be("DELETE");
    }

    [TestMethod]
    public void SetMethod_OnError()
    {
        var test = new SimpleSetMethod().AsTestDocument();
        test.Context.Request.Method = "GET";

        test.RunOnError();

        test.Context.Request.Method.Should().Be("PATCH");
    }

    [TestMethod]
    public void SetMethod_Callback()
    {
        var test = new SimpleSetMethod().AsTestDocument();
        test.Context.Request.Method = "GET";
        var callbackExecuted = false;
        test.SetupInbound().SetMethod().WithCallback((_, _) =>
        {
            callbackExecuted = true;
        });

        test.RunInbound();

        callbackExecuted.Should().BeTrue();
        test.Context.Request.Method.Should().Be("GET");
    }
}
