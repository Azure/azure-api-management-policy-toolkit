// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Document;

namespace Test.Emulator.Emulator.Policies;

[TestClass]
public class BaseTests
{
    class SimpleBase : IDocument
    {
        public void Inbound(IInboundContext context)
        {
            context.Base();
        }

        public void Backend(IBackendContext context)
        {
            context.Base();
        }

        public void Outbound(IOutboundContext context)
        {
            context.Base();
        }

        public void OnError(IOnErrorContext context)
        {
            context.Base();
        }
    }

    [TestMethod]
    public void Base_Inbound_HandleSimple()
    {
        // Arrange
        var test = new TestDocument(new SimpleBase());
        bool baseExecuted = false;
        test.SetupInbound().Base().WithCallback(_ => baseExecuted = !baseExecuted);

        // Act
        test.RunInbound();

        // Assert
        baseExecuted.Should().BeTrue();
    }

    [TestMethod]
    public void Base_Backend_HandleSimple()
    {
        // Arrange
        var test = new TestDocument(new SimpleBase());
        bool baseExecuted = false;

        test.SetupBackend().Base().WithCallback(_ => baseExecuted = !baseExecuted);

        // Act
        test.RunBackend();

        // Assert
        baseExecuted.Should().BeTrue();
    }

    [TestMethod]
    public void Base_Outbound_HandleSimple()
    {
        // Arrange
        var test = new TestDocument(new SimpleBase());
        bool baseExecuted = false;

        test.SetupOutbound().Base().WithCallback(_ => baseExecuted = !baseExecuted);

        // Act
        test.RunOutbound();

        // Assert
        baseExecuted.Should().BeTrue();
    }

    [TestMethod]
    public void BaseOnError_HandleSimple()
    {
        // Arrange
        var test = new TestDocument(new SimpleBase());
        bool baseExecuted = false;

        test.SetupOnError().Base().WithCallback(_ => baseExecuted = !baseExecuted);

        // Act
        test.RunOnError();

        // Assert
        baseExecuted.Should().BeTrue();
    }

    [TestMethod]
    public void Base_HandleSimple_WithPredicate()
    {
        // Arrange
        var test = new TestDocument(new SimpleBase()) { Context = { Variables = { { "a", true } } } };
        test.SetupInbound().Base(context => context.Variables.ContainsKey("b")).WithCallback(context =>
        {
            context.Variables.Remove("a");
            context.Variables.Remove("b");
        });

        // Act
        test.RunInbound();

        // Assert
        test.Context.Variables.Should().ContainKey("a");

        // Arrange
        test.Context.Variables.Add("b", true);

        // Act
        test.RunInbound();

        // Assert
        test.Context.Variables.Should().NotContainKey("a").And.NotContainKey("b");
    }
}