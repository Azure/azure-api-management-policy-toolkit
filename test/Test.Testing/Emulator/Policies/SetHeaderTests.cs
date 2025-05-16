// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Document;

namespace Test.Emulator.Emulator.Policies;

[TestClass]
public class SetHeaderTests
{
    class SimpleSetHeader : IDocument
    {
        public void Inbound(IInboundContext context)
        {
            context.SetHeader("X-Inbound", "value-1", "value-2");
        }

        public void Outbound(IOutboundContext context)
        {
            context.SetHeader("X-Outbound", "value-1");
        }

        public void OnError(IOnErrorContext context)
        {
            context.SetHeader("X-OnError", "value-1", "value-2", "value-3");
        }
    }

    class MultiSetHeader : IDocument
    {
        public void Inbound(IInboundContext context)
        {
            context.SetHeader("A", "value-a");
            context.SetHeader("B", "value-b");
        }
    }

    [TestMethod]
    public void SetHeader_Inbound_HandleSimple()
    {
        // Arrange
        var test = new TestDocument(new SimpleSetHeader())
        {
            Context = { Request = { Headers = { { "X-Inbound", ["overriden"] } } } }
        };

        // Act
        test.RunInbound();

        // Assert
        test.Context.Request.Headers.Should().NotContainKeys("X-Outbound", "X-OnError")
            .And.ContainKey("X-Inbound")
            .WhoseValue.Should().HaveCount(2).And.ContainInOrder("value-1", "value-2");
        test.Context.Response.Headers.Should().NotContainKeys("X-Inbound", "X-Outbound", "X-OnError");
    }

    [TestMethod]
    public void SetHeader_Inbound_HandleSimple_WithCallback()
    {
        // Arrange
        var test = new TestDocument(new SimpleSetHeader())
        {
            Context = { Request = { Headers = { { "X-Inbound", ["overriden"] } } } }
        };
        bool callbackExecuted = false;
        test.SetupInbound().SetHeader().WithCallback((context, name, values) =>
        {
            callbackExecuted = true;
            context.Request.Headers[name] = values;
        });

        // Act
        test.RunInbound();

        // Assert
        callbackExecuted.Should().BeTrue();
        test.Context.Request.Headers.Should().NotContainKeys("X-Outbound", "X-OnError")
            .And.ContainKey("X-Inbound")
            .WhoseValue.Should().HaveCount(2).And.ContainInOrder("value-1", "value-2");
        test.Context.Response.Headers.Should().NotContainKeys("X-Inbound", "X-Outbound", "X-OnError");
    }

    [TestMethod]
    public void SetHeader_Inbound_HandleSimple_WithPredicateCallback()
    {
        // Arrange

        var test = new TestDocument(new MultiSetHeader())
        {
            Context = { Request = { Headers = { { "A", ["overriden"] }, { "B", ["overriden"] } } } }
        };
        bool callbackExecuted = false;
        test.SetupInbound().SetHeader((_, name, _) => name == "B").WithCallback((context, name, values) =>
        {
            callbackExecuted = true;
            context.Request.Headers[name] = values;
        });

        // Act
        test.RunInbound();

        // Assert
        callbackExecuted.Should().BeTrue();
        test.Context.Request.Headers.Should().ContainKeys("A", "B")
            .And.ContainKey("B")
            .WhoseValue.Should().ContainInOrder("value-b");
    }

    [TestMethod]
    public void SetHeader_Outbound_HandleSimple()
    {
        // Arrange
        var test = new TestDocument(new SimpleSetHeader())
        {
            Context = { Response = { Headers = { { "X-Outbound", ["overriden-1", "overriden-2"] } } } }
        };

        // Act
        test.RunOutbound();

        // Assert
        test.Context.Response.Headers.Should().NotContainKeys("X-Inbound", "X-OnError")
            .And.ContainKey("X-Outbound")
            .WhoseValue.Should().HaveCount(1).And.ContainInOrder("value-1");
        test.Context.Request.Headers.Should().NotContainKeys("X-Inbound", "X-Outbound", "X-OnError");
    }

    [TestMethod]
    public void SetHeader_Outbound_HandleSimple_WithCallback()
    {
        // Arrange
        var test = new TestDocument(new SimpleSetHeader())
        {
            Context = { Response = { Headers = { { "X-Outbound", ["overriden-1", "overriden-2"] } } } }
        };
        bool callbackExecuted = false;
        test.SetupOutbound().SetHeader().WithCallback((context, name, values) =>
        {
            callbackExecuted = true;
            context.Response.Headers[name] = values;
        });

        // Act
        test.RunOutbound();

        // Assert
        callbackExecuted.Should().BeTrue();
        test.Context.Response.Headers.Should().NotContainKeys("X-Inbound", "X-OnError")
            .And.ContainKey("X-Outbound")
            .WhoseValue.Should().HaveCount(1).And.ContainInOrder("value-1");
        test.Context.Request.Headers.Should().NotContainKeys("X-Inbound", "X-Outbound", "X-OnError");
    }

    [TestMethod]
    public void SetHeader_OnError_HandleSimple()
    {
        // Arrange
        var test = new TestDocument(new SimpleSetHeader())
        {
            Context = { Response = { Headers = { { "X-OnError", ["overriden-1", "overriden-2"] } } } }
        };

        // Act
        test.RunOnError();

        // Assert
        test.Context.Response.Headers.Should().NotContainKeys("X-Inbound", "X-Outbound")
            .And.ContainKey("X-OnError")
            .WhoseValue.Should().HaveCount(3).And.ContainInOrder("value-1", "value-2", "value-3");
        test.Context.Request.Headers.Should().NotContainKeys("X-Inbound", "X-Outbound", "X-OnError");
    }

    [TestMethod]
    public void SetHeader_OnError_HandleSimple_WithCallback()
    {
        // Arrange
        var test = new TestDocument(new SimpleSetHeader())
        {
            Context = { Response = { Headers = { { "X-OnError", ["overriden-1", "overriden-2"] } } } }
        };
        bool callbackExecuted = false;
        test.SetupOnError().SetHeader().WithCallback((context, name, values) =>
        {
            callbackExecuted = true;
            context.Response.Headers[name] = values;
        });

        // Act
        test.RunOnError();

        // Assert
        callbackExecuted.Should().BeTrue();
        test.Context.Response.Headers.Should().NotContainKeys("X-Inbound", "X-Outbound")
            .And.ContainKey("X-OnError")
            .WhoseValue.Should().HaveCount(3).And.ContainInOrder("value-1", "value-2", "value-3");
        test.Context.Request.Headers.Should().NotContainKeys("X-Inbound", "X-Outbound", "X-OnError");
    }
}