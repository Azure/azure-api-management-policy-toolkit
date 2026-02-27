// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Document;

namespace Test.Emulator.Emulator.Policies;

[TestClass]
public class RemoveQueryParameterTests
{
    class SimpleRemoveQueryParameter : IDocument
    {
        public void Inbound(IInboundContext context)
        {
            context.RemoveQueryParameter("remove-me");
        }

        public void Backend(IBackendContext context)
        {
            context.RemoveQueryParameter("backend-remove");
        }

        public void Outbound(IOutboundContext context)
        {
            context.RemoveQueryParameter("outbound-remove");
        }

        public void OnError(IOnErrorContext context)
        {
            context.RemoveQueryParameter("error-remove");
        }
    }

    [TestMethod]
    public void RemoveQueryParameter_Inbound()
    {
        var test = new TestDocument(new SimpleRemoveQueryParameter())
        {
            Context = { Request = { Url = { Query = { { "remove-me", ["value1"] }, { "keep", ["value2"] } } } } }
        };

        test.RunInbound();

        test.Context.Request.Url.Query.Should().NotContainKey("remove-me")
            .And.ContainKey("keep");
    }

    [TestMethod]
    public void RemoveQueryParameter_Backend()
    {
        var test = new TestDocument(new SimpleRemoveQueryParameter())
        {
            Context = { Request = { Url = { Query = { { "backend-remove", ["value1"] }, { "keep", ["value2"] } } } } }
        };

        test.RunBackend();

        test.Context.Request.Url.Query.Should().NotContainKey("backend-remove")
            .And.ContainKey("keep");
    }

    [TestMethod]
    public void RemoveQueryParameter_Outbound()
    {
        var test = new TestDocument(new SimpleRemoveQueryParameter())
        {
            Context = { Request = { Url = { Query = { { "outbound-remove", ["value1"] }, { "keep", ["value2"] } } } } }
        };

        test.RunOutbound();

        test.Context.Request.Url.Query.Should().NotContainKey("outbound-remove")
            .And.ContainKey("keep");
    }

    [TestMethod]
    public void RemoveQueryParameter_OnError()
    {
        var test = new TestDocument(new SimpleRemoveQueryParameter())
        {
            Context = { Request = { Url = { Query = { { "error-remove", ["value1"] }, { "keep", ["value2"] } } } } }
        };

        test.RunOnError();

        test.Context.Request.Url.Query.Should().NotContainKey("error-remove")
            .And.ContainKey("keep");
    }

    [TestMethod]
    public void RemoveQueryParameter_Callback()
    {
        var test = new TestDocument(new SimpleRemoveQueryParameter())
        {
            Context = { Request = { Url = { Query = { { "remove-me", ["value1"] } } } } }
        };
        var callbackExecuted = false;
        test.SetupInbound().RemoveQueryParameter().WithCallback((_, _) =>
        {
            callbackExecuted = true;
        });

        test.RunInbound();

        callbackExecuted.Should().BeTrue();
        test.Context.Request.Url.Query.Should().ContainKey("remove-me");
    }

    [TestMethod]
    public void RemoveQueryParameter_NonExistent()
    {
        var test = new TestDocument(new SimpleRemoveQueryParameter())
        {
            Context = { Request = { Url = { Query = { { "keep", ["value"] } } } } }
        };

        test.RunInbound();

        test.Context.Request.Url.Query.Should().ContainKey("keep");
    }
}
