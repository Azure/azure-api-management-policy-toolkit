// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Document;

namespace Test.Emulator.Emulator.Policies;

[TestClass]
public class SetBodyTests
{
    class SimpleSetBody : IDocument
    {
        public void Inbound(IInboundContext context)
        {
            context.SetBody("inbound-body");
        }

        public void Backend(IBackendContext context)
        {
            context.SetBody("backend-body");
        }

        public void Outbound(IOutboundContext context)
        {
            context.SetBody("outbound-body");
        }

        public void OnError(IOnErrorContext context)
        {
            context.SetBody("error-body");
        }
    }

    [TestMethod]
    public void SetBody_Inbound()
    {
        var test = new SimpleSetBody().AsTestDocument();

        test.RunInbound();

        test.Context.Request.Body.Content.Should().Be("inbound-body");
    }

    [TestMethod]
    public void SetBody_Backend()
    {
        var test = new SimpleSetBody().AsTestDocument();

        test.RunBackend();

        test.Context.Request.Body.Content.Should().Be("backend-body");
    }

    [TestMethod]
    public void SetBody_Outbound()
    {
        var test = new SimpleSetBody().AsTestDocument();

        test.RunOutbound();

        test.Context.Response.Body.Content.Should().Be("outbound-body");
    }

    [TestMethod]
    public void SetBody_OnError()
    {
        var test = new SimpleSetBody().AsTestDocument();

        test.RunOnError();

        test.Context.Response.Body.Content.Should().Be("error-body");
    }

    [TestMethod]
    public void SetBody_Callback()
    {
        var test = new SimpleSetBody().AsTestDocument();
        var callbackExecuted = false;
        test.SetupInbound().SetBody().WithCallback((context, body, _) =>
        {
            callbackExecuted = true;
            context.Request.Body.Content = body.ToUpper();
        });

        test.RunInbound();

        callbackExecuted.Should().BeTrue();
        test.Context.Request.Body.Content.Should().Be("INBOUND-BODY");
    }

    [TestMethod]
    public void SetBody_OverwritesExistingBody()
    {
        var test = new SimpleSetBody().AsTestDocument();
        test.Context.Request.Body.Content = "old-body";

        test.RunInbound();

        test.Context.Request.Body.Content.Should().Be("inbound-body");
    }
}
