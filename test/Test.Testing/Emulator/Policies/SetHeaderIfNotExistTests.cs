// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Document;

namespace Test.Emulator.Emulator.Policies;

[TestClass]
public class SetHeaderIfNotExistTests
{
    class SimpleSetHeaderIfNotExist : IDocument
    {
        public void Inbound(IInboundContext context)
        {
            context.SetHeaderIfNotExist("X-Inbound", "value-1");
        }

        public void Backend(IBackendContext context)
        {
            context.SetHeaderIfNotExist("X-Backend", "value-1");
        }

        public void Outbound(IOutboundContext context)
        {
            context.SetHeaderIfNotExist("X-Outbound", "value-1");
        }

        public void OnError(IOnErrorContext context)
        {
            context.SetHeaderIfNotExist("X-OnError", "value-1");
        }
    }

    [TestMethod]
    public void SetHeaderIfNotExist_Inbound_WhenNotExist()
    {
        var test = new SimpleSetHeaderIfNotExist().AsTestDocument();

        test.RunInbound();

        test.Context.Request.Headers.Should().ContainKey("X-Inbound")
            .WhoseValue.Should().ContainInOrder("value-1");
    }

    [TestMethod]
    public void SetHeaderIfNotExist_Inbound_WhenExists()
    {
        var test = new TestDocument(new SimpleSetHeaderIfNotExist())
        {
            Context = { Request = { Headers = { { "X-Inbound", ["existing"] } } } }
        };

        test.RunInbound();

        test.Context.Request.Headers.Should().ContainKey("X-Inbound")
            .WhoseValue.Should().ContainInOrder("existing");
    }

    [TestMethod]
    public void SetHeaderIfNotExist_Backend()
    {
        var test = new SimpleSetHeaderIfNotExist().AsTestDocument();

        test.RunBackend();

        test.Context.Request.Headers.Should().ContainKey("X-Backend")
            .WhoseValue.Should().ContainInOrder("value-1");
    }

    [TestMethod]
    public void SetHeaderIfNotExist_Outbound()
    {
        var test = new SimpleSetHeaderIfNotExist().AsTestDocument();

        test.RunOutbound();

        test.Context.Response.Headers.Should().ContainKey("X-Outbound")
            .WhoseValue.Should().ContainInOrder("value-1");
    }

    [TestMethod]
    public void SetHeaderIfNotExist_OnError()
    {
        var test = new SimpleSetHeaderIfNotExist().AsTestDocument();

        test.RunOnError();

        test.Context.Response.Headers.Should().ContainKey("X-OnError")
            .WhoseValue.Should().ContainInOrder("value-1");
    }

    [TestMethod]
    public void SetHeaderIfNotExist_Callback()
    {
        var test = new SimpleSetHeaderIfNotExist().AsTestDocument();
        var callbackExecuted = false;
        test.SetupInbound().SetHeaderIfNotExist().WithCallback((_, _, _) =>
        {
            callbackExecuted = true;
        });

        test.RunInbound();

        callbackExecuted.Should().BeTrue();
        test.Context.Request.Headers.Should().NotContainKey("X-Inbound");
    }
}
