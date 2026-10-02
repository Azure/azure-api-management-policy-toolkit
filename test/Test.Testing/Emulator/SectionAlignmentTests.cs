// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Document;

namespace Test.Emulator.Emulator;

// Policies run in the sections API Management accepts them in.
[TestClass]
public class SectionAlignmentTests
{
    class InboundRedirect : IDocument
    {
        public void Inbound(IInboundContext context) => context.RedirectContentUrls();
    }

    class OutboundContentSafety : IDocument
    {
        public void Outbound(IOutboundContext context) =>
            context.LlmContentSafety(new LlmContentSafetyConfig { BackendId = "safety" });
    }

    // Calling it in inbound threw NotImplementedException: the handler was registered for outbound only.
    [TestMethod]
    public void RedirectContentUrls_RunsInInbound()
    {
        var test = new InboundRedirect().AsTestDocument();
        var called = false;
        test.SetupInbound().RedirectContentUrls().WithCallback(_ => called = true);

        test.RunInbound();

        called.Should().BeTrue();
    }

    // Without a handler registered for the section, the emulator throws NotImplementedException.
    [TestMethod]
    public void LlmContentSafety_RunsInOutbound()
    {
        var test = new OutboundContentSafety().AsTestDocument();

        var run = () => test.RunOutbound();

        run.Should().NotThrow();
    }
}
