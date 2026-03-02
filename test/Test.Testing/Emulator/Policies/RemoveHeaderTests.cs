// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Document;

namespace Test.Emulator.Emulator.Policies;

[TestClass]
public class RemoveHeaderTests
{
    class SimpleRemoveHeader : IDocument
    {
        public void Inbound(IInboundContext context)
        {
            context.RemoveHeader("X-Remove-Me");
        }

        public void Backend(IBackendContext context)
        {
            context.RemoveHeader("X-Backend-Remove");
        }

        public void Outbound(IOutboundContext context)
        {
            context.RemoveHeader("X-Outbound-Remove");
        }

        public void OnError(IOnErrorContext context)
        {
            context.RemoveHeader("X-Error-Remove");
        }
    }

    [TestMethod]
    public void RemoveHeader_Inbound()
    {
        var test = new TestDocument(new SimpleRemoveHeader())
        {
            Context = { Request = { Headers = { { "X-Remove-Me", ["value1"] }, { "X-Keep", ["keep"] } } } }
        };

        test.RunInbound();

        test.Context.Request.Headers.Should().NotContainKey("X-Remove-Me")
            .And.ContainKey("X-Keep");
    }

    [TestMethod]
    public void RemoveHeader_Backend()
    {
        var test = new TestDocument(new SimpleRemoveHeader())
        {
            Context = { Request = { Headers = { { "X-Backend-Remove", ["value1"] }, { "X-Keep", ["keep"] } } } }
        };

        test.RunBackend();

        test.Context.Request.Headers.Should().NotContainKey("X-Backend-Remove")
            .And.ContainKey("X-Keep");
    }

    [TestMethod]
    public void RemoveHeader_Outbound()
    {
        var test = new TestDocument(new SimpleRemoveHeader())
        {
            Context = { Response = { Headers = { { "X-Outbound-Remove", ["value1"] }, { "X-Keep", ["keep"] } } } }
        };

        test.RunOutbound();

        test.Context.Response.Headers.Should().NotContainKey("X-Outbound-Remove")
            .And.ContainKey("X-Keep");
    }

    [TestMethod]
    public void RemoveHeader_OnError()
    {
        var test = new TestDocument(new SimpleRemoveHeader())
        {
            Context = { Response = { Headers = { { "X-Error-Remove", ["value1"] }, { "X-Keep", ["keep"] } } } }
        };

        test.RunOnError();

        test.Context.Response.Headers.Should().NotContainKey("X-Error-Remove")
            .And.ContainKey("X-Keep");
    }

    [TestMethod]
    public void RemoveHeader_Callback()
    {
        var test = new TestDocument(new SimpleRemoveHeader())
        {
            Context = { Request = { Headers = { { "X-Remove-Me", ["value1"] } } } }
        };
        var callbackExecuted = false;
        test.SetupInbound().RemoveHeader().WithCallback((_, _) =>
        {
            callbackExecuted = true;
        });

        test.RunInbound();

        callbackExecuted.Should().BeTrue();
        test.Context.Request.Headers.Should().ContainKey("X-Remove-Me");
    }

    [TestMethod]
    public void RemoveHeader_NonExistentHeader()
    {
        var test = new TestDocument(new SimpleRemoveHeader())
        {
            Context = { Request = { Headers = { { "X-Keep", ["keep"] } } } }
        };

        test.RunInbound();

        test.Context.Request.Headers.Should().ContainKey("X-Keep");
    }
}
