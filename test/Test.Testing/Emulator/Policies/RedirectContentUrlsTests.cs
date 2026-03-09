// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Document;

namespace Test.Emulator.Emulator.Policies;

[TestClass]
public class RedirectContentUrlsTests
{
    class SimpleRedirectContentUrls : IDocument
    {
        public void Inbound(IInboundContext context) { }
        public void Backend(IBackendContext context) { }

        public void Outbound(IOutboundContext context)
        {
            context.RedirectContentUrls();
        }

        public void OnError(IOnErrorContext context) { }
    }

    [TestMethod]
    public void RedirectContentUrls_Outbound_ShouldExecuteWithoutError()
    {
        // Arrange
        var test = new TestDocument(new SimpleRedirectContentUrls());

        // Act & Assert - no-op should not throw
        test.RunOutbound();
    }

    [TestMethod]
    public void RedirectContentUrls_Outbound_Callback()
    {
        // Arrange
        var test = new TestDocument(new SimpleRedirectContentUrls());
        var executedCallback = false;

        test.SetupOutbound().RedirectContentUrls().WithCallback(_ =>
        {
            executedCallback = true;
        });

        // Act
        test.RunOutbound();

        // Assert
        executedCallback.Should().BeTrue();
    }

    [TestMethod]
    public void RedirectContentUrls_Outbound_CallbackWithPredicate()
    {
        // Arrange
        var test = new TestDocument(new SimpleRedirectContentUrls())
        {
            Context = { Variables = { { "redirect", true } } }
        };

        test.SetupOutbound()
            .RedirectContentUrls(context => context.Variables.ContainsKey("redirect"))
            .WithCallback(context =>
            {
                context.Variables["redirected"] = true;
            });

        // Act
        test.RunOutbound();

        // Assert
        test.Context.Variables.Should().ContainKey("redirected")
            .WhoseValue.Should().Be(true);
    }

    [TestMethod]
    public void RedirectContentUrls_Outbound_PredicateNotMatching()
    {
        // Arrange
        var test = new TestDocument(new SimpleRedirectContentUrls());
        var executedCallback = false;

        test.SetupOutbound()
            .RedirectContentUrls(context => context.Variables.ContainsKey("nonexistent"))
            .WithCallback(_ =>
            {
                executedCallback = true;
            });

        // Act
        test.RunOutbound();

        // Assert
        executedCallback.Should().BeFalse();
    }
}
