// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Document;

namespace Test.Emulator.Emulator.Policies;

[TestClass]
public class SetQueryParameterIfNotExistTests
{
    class SimpleSetQueryParameterIfNotExist : IDocument
    {
        public void Inbound(IInboundContext context)
        {
            context.SetQueryParameterIfNotExist("param1", "value-1");
        }

        public void Backend(IBackendContext context)
        {
            context.SetQueryParameterIfNotExist("backend-param", "b-value");
        }

        public void Outbound(IOutboundContext context)
        {
            context.SetQueryParameterIfNotExist("outbound-param", "o-value");
        }

        public void OnError(IOnErrorContext context)
        {
            context.SetQueryParameterIfNotExist("error-param", "e-value");
        }
    }

    [TestMethod]
    public void SetQueryParameterIfNotExist_Inbound_WhenNotExist()
    {
        var test = new SimpleSetQueryParameterIfNotExist().AsTestDocument();

        test.RunInbound();

        test.Context.Request.Url.Query.Should().ContainKey("param1")
            .WhoseValue.Should().ContainInOrder("value-1");
    }

    [TestMethod]
    public void SetQueryParameterIfNotExist_Inbound_WhenExists()
    {
        var test = new TestDocument(new SimpleSetQueryParameterIfNotExist())
        {
            Context = { Request = { Url = { Query = { { "param1", ["existing"] } } } } }
        };

        test.RunInbound();

        test.Context.Request.Url.Query.Should().ContainKey("param1")
            .WhoseValue.Should().ContainInOrder("existing");
    }

    [TestMethod]
    public void SetQueryParameterIfNotExist_Backend()
    {
        var test = new SimpleSetQueryParameterIfNotExist().AsTestDocument();

        test.RunBackend();

        test.Context.Request.Url.Query.Should().ContainKey("backend-param")
            .WhoseValue.Should().ContainInOrder("b-value");
    }

    [TestMethod]
    public void SetQueryParameterIfNotExist_Outbound()
    {
        var test = new SimpleSetQueryParameterIfNotExist().AsTestDocument();

        test.RunOutbound();

        test.Context.Request.Url.Query.Should().ContainKey("outbound-param")
            .WhoseValue.Should().ContainInOrder("o-value");
    }

    [TestMethod]
    public void SetQueryParameterIfNotExist_OnError()
    {
        var test = new SimpleSetQueryParameterIfNotExist().AsTestDocument();

        test.RunOnError();

        test.Context.Request.Url.Query.Should().ContainKey("error-param")
            .WhoseValue.Should().ContainInOrder("e-value");
    }

    [TestMethod]
    public void SetQueryParameterIfNotExist_Callback()
    {
        var test = new SimpleSetQueryParameterIfNotExist().AsTestDocument();
        var callbackExecuted = false;
        test.SetupInbound().SetQueryParameterIfNotExist().WithCallback((_, _, _) =>
        {
            callbackExecuted = true;
        });

        test.RunInbound();

        callbackExecuted.Should().BeTrue();
        test.Context.Request.Url.Query.Should().NotContainKey("param1");
    }
}
