// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Document;

namespace Test.Emulator.Emulator.Policies;

[TestClass]
public class SetQueryParameterTests
{
    class SimpleSetQueryParameter : IDocument
    {
        public void Inbound(IInboundContext context)
        {
            context.SetQueryParameter("param1", "value-1", "value-2");
        }

        public void Backend(IBackendContext context)
        {
            context.SetQueryParameter("backend-param", "b-value");
        }

        public void Outbound(IOutboundContext context)
        {
            context.SetQueryParameter("outbound-param", "o-value");
        }

        public void OnError(IOnErrorContext context)
        {
            context.SetQueryParameter("error-param", "e-value");
        }
    }

    [TestMethod]
    public void SetQueryParameter_Inbound()
    {
        var test = new SimpleSetQueryParameter().AsTestDocument();

        test.RunInbound();

        test.Context.Request.Url.Query.Should().ContainKey("param1")
            .WhoseValue.Should().ContainInOrder("value-1", "value-2");
    }

    [TestMethod]
    public void SetQueryParameter_Inbound_Overwrites()
    {
        var test = new TestDocument(new SimpleSetQueryParameter())
        {
            Context = { Request = { Url = { Query = { { "param1", ["old-value"] } } } } }
        };

        test.RunInbound();

        test.Context.Request.Url.Query.Should().ContainKey("param1")
            .WhoseValue.Should().ContainInOrder("value-1", "value-2");
    }

    [TestMethod]
    public void SetQueryParameter_Backend()
    {
        var test = new SimpleSetQueryParameter().AsTestDocument();

        test.RunBackend();

        test.Context.Request.Url.Query.Should().ContainKey("backend-param")
            .WhoseValue.Should().ContainInOrder("b-value");
    }

    [TestMethod]
    public void SetQueryParameter_Outbound()
    {
        var test = new SimpleSetQueryParameter().AsTestDocument();

        test.RunOutbound();

        test.Context.Request.Url.Query.Should().ContainKey("outbound-param")
            .WhoseValue.Should().ContainInOrder("o-value");
    }

    [TestMethod]
    public void SetQueryParameter_OnError()
    {
        var test = new SimpleSetQueryParameter().AsTestDocument();

        test.RunOnError();

        test.Context.Request.Url.Query.Should().ContainKey("error-param")
            .WhoseValue.Should().ContainInOrder("e-value");
    }

    [TestMethod]
    public void SetQueryParameter_Callback()
    {
        var test = new SimpleSetQueryParameter().AsTestDocument();
        var callbackExecuted = false;
        test.SetupInbound().SetQueryParameter().WithCallback((_, _, _) =>
        {
            callbackExecuted = true;
        });

        test.RunInbound();

        callbackExecuted.Should().BeTrue();
        test.Context.Request.Url.Query.Should().NotContainKey("param1");
    }
}
