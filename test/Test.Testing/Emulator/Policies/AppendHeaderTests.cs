// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Document;

namespace Test.Emulator.Emulator.Policies;

[TestClass]
public class AppendHeaderTests
{
    class SimpleAppendHeader : IDocument
    {
        public void Inbound(IInboundContext context)
        {
            context.AppendHeader("X-Inbound", "value-1", "value-2");
        }

        public void Outbound(IOutboundContext context)
        {
            context.AppendHeader("X-Outbound", "value-1");
        }

        public void OnError(IOnErrorContext context)
        {
            context.AppendHeader("X-OnError", "value-1", "value-2", "value-3");
        }
    }

    class MultiAppendHeader : IDocument
    {
        public void Inbound(IInboundContext context)
        {
            context.AppendHeader("A", "value-a");
            context.AppendHeader("B", "value-b");
        }
    }

    [TestMethod]
    public void AppendHeader_Inbound_HandleSimple()
    {
        // Arrange
        var test = new TestDocument(new SimpleAppendHeader())
        {
            Context = { Request = { Headers = { { "X-Inbound", ["value-0"] } } } }
        };

        // Act
        test.RunInbound();

        // Assert
        test.Context.Request.Headers.Should().NotContainKeys("X-Outbound", "X-OnError")
            .And.ContainKey("X-Inbound")
            .WhoseValue.Should().HaveCount(3).And.ContainInOrder("value-0", "value-1", "value-2");
        test.Context.Response.Headers.Should().NotContainKeys("X-Inbound", "X-Outbound", "X-OnError");
    }

    [TestMethod]
    public void AppendHeader_Inbound_HandleSimple_CreateIfNotExists()
    {
        // Arrange
        var test = new TestDocument(new MultiAppendHeader())
        {
            Context = { Request = { Headers = { { "A", ["value-0"] } } } }
        };

        // Act
        test.RunInbound();

        // Assert
        test.Context.Request.Headers.Should().ContainKeys("A", "B");
    }

    [TestMethod]
    public void AppendHeader_Inbound_HandleSimple_WithCallback()
    {
        // Arrange
        var test = new TestDocument(new SimpleAppendHeader())
        {
            Context = { Request = { Headers = { { "X-Inbound", ["value-0"] } } } }
        };
        bool callbackExecuted = false;
        test.SetupInbound().AppendHeader().WithCallback((context, name, values) =>
        {
            callbackExecuted = true;
            context.Request.Headers[name] = context.Request.Headers[name].Concat(values).Reverse().ToArray();
        });

        // Act
        test.RunInbound();

        // Assert
        callbackExecuted.Should().BeTrue();
        test.Context.Request.Headers.Should().NotContainKeys("X-Outbound", "X-OnError")
            .And.ContainKey("X-Inbound")
            .WhoseValue.Should().HaveCount(3).And.ContainInOrder("value-2", "value-1", "value-0");
        test.Context.Response.Headers.Should().NotContainKeys("X-Inbound", "X-Outbound", "X-OnError");
    }

    [TestMethod]
    public void AppendHeader_Inbound_HandleSimple_WithPredicateCallback()
    {
        // Arrange
        var test = new TestDocument(new MultiAppendHeader())
        {
            Context = { Request = { Headers = { { "A", ["value-0"] }, { "B", ["value-0"] } } } }
        };
        bool callbackExecuted = false;
        test.SetupInbound().AppendHeader((_, name, _) => name == "B").WithCallback((context, name, values) =>
        {
            callbackExecuted = true;
            context.Request.Headers[name] = context.Request.Headers[name].Concat(values).Reverse().ToArray();
        });

        // Act
        test.RunInbound();

        // Assert
        callbackExecuted.Should().BeTrue();
        test.Context.Request.Headers.Should().ContainKeys("A", "B")
            .And.ContainKey("B")
            .WhoseValue.Should().ContainInOrder("value-b", "value-0");
    }

    [TestMethod]
    public void AppendHeader_Outbound_HandleSimple()
    {
        // Arrange
        var test = new TestDocument(new SimpleAppendHeader())
        {
            Context = { Response = { Headers = { { "X-Outbound", ["value-0"] } } } }
        };

        // Act
        test.RunOutbound();

        // Assert
        test.Context.Response.Headers.Should().NotContainKeys("X-Inbound", "X-OnError")
            .And.ContainKey("X-Outbound")
            .WhoseValue.Should().HaveCount(2).And.ContainInOrder("value-0", "value-1");
        test.Context.Request.Headers.Should().NotContainKeys("X-Inbound", "X-Outbound", "X-OnError");
    }

    [TestMethod]
    public void AppendHeader_Outbound_HandleSimple_WithCallback()
    {
        // Arrange
        var test = new TestDocument(new SimpleAppendHeader())
        {
            Context = { Response = { Headers = { { "X-Outbound", ["value-0"] } } } }
        };
        bool callbackExecuted = false;
        test.SetupOutbound().AppendHeader().WithCallback((context, name, values) =>
        {
            callbackExecuted = true;
            context.Response.Headers[name] = context.Response.Headers[name].Concat(values).Reverse().ToArray();
        });

        // Act
        test.RunOutbound();

        // Assert
        callbackExecuted.Should().BeTrue();
        test.Context.Response.Headers.Should().NotContainKeys("X-Inbound", "X-OnError")
            .And.ContainKey("X-Outbound")
            .WhoseValue.Should().HaveCount(2).And.ContainInOrder("value-1", "value-0");
        test.Context.Request.Headers.Should().NotContainKeys("X-Inbound", "X-Outbound", "X-OnError");
    }

    [TestMethod]
    public void AppendHeader_OnError_HandleSimple()
    {
        // Arrange
        var test = new TestDocument(new SimpleAppendHeader())
        {
            Context = { Response = { Headers = { { "X-OnError", ["value-0"] } } } }
        };

        // Act
        test.RunOnError();

        // Assert
        test.Context.Response.Headers.Should().NotContainKeys("X-Inbound", "X-Outbound")
            .And.ContainKey("X-OnError")
            .WhoseValue.Should().HaveCount(4).And.ContainInOrder("value-0", "value-1", "value-2", "value-3");
        test.Context.Request.Headers.Should().NotContainKeys("X-Inbound", "X-Outbound", "X-OnError");
    }

    [TestMethod]
    public void AppendHeader_OnError_HandleSimple_WithCallback()
    {
        // Arrange
        var test = new TestDocument(new SimpleAppendHeader())
        {
            Context = { Response = { Headers = { { "X-OnError", ["value-0"] } } } }
        };
        bool callbackExecuted = false;
        test.SetupOnError().AppendHeader().WithCallback((context, name, values) =>
        {
            callbackExecuted = true;
            context.Response.Headers[name] = context.Response.Headers[name].Concat(values).Reverse().ToArray();
        });

        // Act
        test.RunOnError();

        // Assert
        callbackExecuted.Should().BeTrue();
        test.Context.Response.Headers.Should().NotContainKeys("X-Inbound", "X-Outbound")
            .And.ContainKey("X-OnError")
            .WhoseValue.Should().HaveCount(4).And.ContainInOrder("value-3", "value-2", "value-1", "value-0");
        test.Context.Request.Headers.Should().NotContainKeys("X-Inbound", "X-Outbound", "X-OnError");
    }
}