// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Document;

namespace Test.Emulator.Emulator.Policies;

[TestClass]
public class FindAndReplaceTests
{
    class InboundFindAndReplace : IDocument
    {
        public void Inbound(IInboundContext context)
        {
            context.FindAndReplace("foo", "bar");
        }

        public void Backend(IBackendContext context) { }
        public void Outbound(IOutboundContext context) { }
        public void OnError(IOnErrorContext context) { }
    }

    class OutboundFindAndReplace : IDocument
    {
        public void Inbound(IInboundContext context) { }
        public void Backend(IBackendContext context) { }

        public void Outbound(IOutboundContext context)
        {
            context.FindAndReplace("old-url", "new-url");
        }

        public void OnError(IOnErrorContext context) { }
    }

    [TestMethod]
    public void FindAndReplace_Inbound_ShouldReplaceInRequestBody()
    {
        // Arrange
        var test = new TestDocument(new InboundFindAndReplace());
        test.Context.Request.Body.Content = "hello foo world foo";

        // Act
        test.RunInbound();

        // Assert
        test.Context.Request.Body.Content.Should().Be("hello bar world bar");
    }

    [TestMethod]
    public void FindAndReplace_Inbound_NullBody_ShouldNotThrow()
    {
        // Arrange
        var test = new TestDocument(new InboundFindAndReplace());
        test.Context.Request.Body.Content = null;

        // Act & Assert - should not throw
        test.RunInbound();
    }

    [TestMethod]
    public void FindAndReplace_Outbound_ShouldReplaceInResponseBody()
    {
        // Arrange
        var test = new TestDocument(new OutboundFindAndReplace());
        test.Context.Response.Body.Content = "visit old-url for details";

        // Act
        test.RunOutbound();

        // Assert
        test.Context.Response.Body.Content.Should().Be("visit new-url for details");
    }

    [TestMethod]
    public void FindAndReplace_Inbound_Callback()
    {
        // Arrange
        var test = new TestDocument(new InboundFindAndReplace());
        test.Context.Request.Body.Content = "hello foo world";
        var executedCallback = false;

        test.SetupInbound().FindAndReplace().WithCallback((context, from, to) =>
        {
            executedCallback = true;
            // Custom replacement logic
            context.Request.Body.Content = context.Request.Body.Content?.Replace(from, "custom");
        });

        // Act
        test.RunInbound();

        // Assert
        executedCallback.Should().BeTrue();
        test.Context.Request.Body.Content.Should().Be("hello custom world");
    }

    [TestMethod]
    public void FindAndReplace_Outbound_Callback()
    {
        // Arrange
        var test = new TestDocument(new OutboundFindAndReplace());
        test.Context.Response.Body.Content = "visit old-url";
        var executedCallback = false;

        test.SetupOutbound().FindAndReplace().WithCallback((context, from, to) =>
        {
            executedCallback = true;
        });

        // Act
        test.RunOutbound();

        // Assert
        executedCallback.Should().BeTrue();
    }

    [TestMethod]
    public void FindAndReplace_Inbound_NoMatch_ShouldKeepBodyUnchanged()
    {
        // Arrange
        var test = new TestDocument(new InboundFindAndReplace());
        test.Context.Request.Body.Content = "hello world";

        // Act
        test.RunInbound();

        // Assert - "foo" not in body, so no change
        test.Context.Request.Body.Content.Should().Be("hello world");
    }

    [TestMethod]
    public void FindAndReplace_Inbound_CallbackWithPredicate()
    {
        // Arrange
        var test = new TestDocument(new InboundFindAndReplace());
        test.Context.Request.Body.Content = "hello foo world";
        var executedCallback = false;

        test.SetupInbound()
            .FindAndReplace((_, from, _) => from == "foo")
            .WithCallback((_, _, _) =>
            {
                executedCallback = true;
            });

        // Act
        test.RunInbound();

        // Assert
        executedCallback.Should().BeTrue();
    }
}
