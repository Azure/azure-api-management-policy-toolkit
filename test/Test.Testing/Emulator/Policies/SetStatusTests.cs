// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Document;

namespace Test.Emulator.Emulator.Policies;

[TestClass]
public class SetStatusTests
{
    class SimpleSetStatus : IDocument
    {
        public void Inbound(IInboundContext context)
        {
            context.SetStatus(new StatusConfig() { Code = 200, Reason = "OK" });
        }

        public void Backend(IBackendContext context)
        {
            context.SetStatus(new StatusConfig() { Code = 201, Reason = "Created" });
        }

        public void Outbound(IOutboundContext context)
        {
            context.SetStatus(new StatusConfig() { Code = 202, Reason = "Accepted" });
        }

        public void OnError(IOnErrorContext context)
        {
            context.SetStatus(new StatusConfig() { Code = 500, Reason = "Internal Server Error" });
        }
    }

    [TestMethod]
    public void SetStatus_Callback()
    {
        var test = new SimpleSetStatus().AsTestDocument();
        var executedCallback = false;
        test.SetupInbound().SetStatus().WithCallback((_, _) =>
        {
            executedCallback = true;
        });

        test.RunInbound();

        executedCallback.Should().BeTrue();
    }

    [TestMethod]
    public void SetStatus_Inbound()
    {
        var test = new SimpleSetStatus().AsTestDocument();

        test.RunInbound();

        test.Context.Response.StatusCode.Should().Be(200);
        test.Context.Response.StatusReason.Should().Be("OK");
    }

    [TestMethod]
    public void SetStatus_Backend()
    {
        var test = new SimpleSetStatus().AsTestDocument();

        test.RunBackend();

        test.Context.Response.StatusCode.Should().Be(201);
        test.Context.Response.StatusReason.Should().Be("Created");
    }

    [TestMethod]
    public void SetStatus_Outbound()
    {
        var test = new SimpleSetStatus().AsTestDocument();

        test.RunOutbound();

        test.Context.Response.StatusCode.Should().Be(202);
        test.Context.Response.StatusReason.Should().Be("Accepted");
    }

    [TestMethod]
    public void SetStatus_OnError()
    {
        var test = new SimpleSetStatus().AsTestDocument();

        test.RunOnError();

        test.Context.Response.StatusCode.Should().Be(500);
        test.Context.Response.StatusReason.Should().Be("Internal Server Error");
    }
}