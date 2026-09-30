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

    class ConfigurableAppendHeader(
        string name,
        string[] values,
        bool useExpressions = false,
        bool includeUnmatched = false) : IDocument
    {
        public void Inbound(IInboundContext context) =>
            HeaderQueryTestHelpers.Apply(context, context.AppendHeader, name, values, useExpressions, includeUnmatched);

        public void Backend(IBackendContext context) =>
            HeaderQueryTestHelpers.Apply(context, context.AppendHeader, name, values, useExpressions, includeUnmatched);

        public void Outbound(IOutboundContext context) =>
            HeaderQueryTestHelpers.Apply(context, context.AppendHeader, name, values, useExpressions, includeUnmatched);

        public void OnError(IOnErrorContext context) =>
            HeaderQueryTestHelpers.Apply(context, context.AppendHeader, name, values, useExpressions, includeUnmatched);
    }

    [TestMethod]
    [DataRow("Inbound", "missing")]
    [DataRow("Inbound", "empty")]
    [DataRow("Inbound", "repeated")]
    [DataRow("Backend", "missing")]
    [DataRow("Backend", "empty")]
    [DataRow("Backend", "repeated")]
    [DataRow("Outbound", "missing")]
    [DataRow("Outbound", "empty")]
    [DataRow("Outbound", "repeated")]
    [DataRow("OnError", "missing")]
    [DataRow("OnError", "empty")]
    [DataRow("OnError", "repeated")]
    public void AppendHeader_PreservesOrderedRepeatedAndEmptyValuesIgnoringCase(string section, string existing)
    {
        var test = new ConfigurableAppendHeader("x-test", ["", "new", "new"]).AsTestDocument();
        string[]? previous = existing switch
        {
            "missing" => null,
            "empty" => [],
            "repeated" => ["old", "old"],
            _ => throw new ArgumentOutOfRangeException(nameof(existing))
        };
        HeaderQueryTestHelpers.SeedHeaders(test, section, previous);

        HeaderQueryTestHelpers.Run(test, section);

        var headers = HeaderQueryTestHelpers.Message(test.Context, section).Headers;
        var expected = (previous ?? []).Concat(["", "new", "new"]).ToArray();
        headers["X-TEST"].Should().Equal(expected);
        headers.Should().HaveCount(2);
        HeaderQueryTestHelpers.AssertUnrelatedHeaders(test, section);
    }

    [TestMethod]
    [DataRow("Inbound", false)]
    [DataRow("Inbound", true)]
    [DataRow("Backend", false)]
    [DataRow("Backend", true)]
    [DataRow("Outbound", false)]
    [DataRow("Outbound", true)]
    [DataRow("OnError", false)]
    [DataRow("OnError", true)]
    public void AppendHeader_HandlesEmptyIncomingValues(string section, bool exists)
    {
        var test = new ConfigurableAppendHeader("x-test", []).AsTestDocument();
        HeaderQueryTestHelpers.SeedHeaders(test, section, exists ? ["old", "old"] : null);

        HeaderQueryTestHelpers.Run(test, section);

        var headers = HeaderQueryTestHelpers.Message(test.Context, section).Headers;
        headers["X-Test"].Should().Equal(exists ? ["old", "old"] : Array.Empty<string>());
        headers.Should().HaveCount(2);
        HeaderQueryTestHelpers.AssertUnrelatedHeaders(test, section);
    }

    [TestMethod]
    [DataRow("Inbound", false)]
    [DataRow("Inbound", true)]
    [DataRow("Backend", false)]
    [DataRow("Backend", true)]
    [DataRow("Outbound", false)]
    [DataRow("Outbound", true)]
    [DataRow("OnError", false)]
    [DataRow("OnError", true)]
    public void AppendHeader_CallbackOverridesDefaultInEverySection(string section, bool withPredicate)
    {
        var test = new ConfigurableAppendHeader("x-test", ["new"], includeUnmatched: withPredicate).AsTestDocument();
        HeaderQueryTestHelpers.SeedHeaders(test, section, ["old"]);
        var setup = withPredicate
            ? HeaderQueryTestHelpers.Setup<MockAppendHeaderProvider.Setup>(
                test, section, typeof(MockAppendHeaderProvider), nameof(MockAppendHeaderProvider.AppendHeader),
                (_, name, _) => name == "x-test")
            : HeaderQueryTestHelpers.Setup<MockAppendHeaderProvider.Setup>(
                test, section, typeof(MockAppendHeaderProvider), nameof(MockAppendHeaderProvider.AppendHeader));
        setup.WithCallback((context, name, values) =>
            HeaderQueryTestHelpers.Message(context, section).Headers[name] = ["callback", .. values]);

        HeaderQueryTestHelpers.Run(test, section);

        var headers = HeaderQueryTestHelpers.Message(test.Context, section).Headers;
        headers["X-Test"].Should().Equal("callback", "new");
        headers.Should().HaveCount(withPredicate ? 3 : 2);
        if (withPredicate)
        {
            headers["x-other"].Should().Equal("default");
        }
        HeaderQueryTestHelpers.AssertUnrelatedHeaders(test, section);
    }

    [TestMethod]
    [DataRow("Inbound")]
    [DataRow("Backend")]
    [DataRow("Outbound")]
    [DataRow("OnError")]
    public void AppendHeader_UsesExpressionDrivenNameAndValues(string section)
    {
        var test = new ConfigurableAppendHeader("unused", [], useExpressions: true).AsTestDocument();
        HeaderQueryTestHelpers.SeedHeaders(test, section, ["old"]);
        test.Context.Request.Headers["X-Policy-Name"] = ["x-test"];
        test.Context.Request.Headers["X-Policy-Values"] = ["", "expression", "expression"];

        HeaderQueryTestHelpers.Run(test, section);

        HeaderQueryTestHelpers.Message(test.Context, section).Headers["X-TEST"]
            .Should().Equal("old", "", "expression", "expression");
        HeaderQueryTestHelpers.AssertUnrelatedHeaders(test, section);
    }

    [TestMethod]
    [DataRow("Inbound", null)]
    [DataRow("Inbound", "")]
    [DataRow("Inbound", " ")]
    [DataRow("Backend", null)]
    [DataRow("Backend", "")]
    [DataRow("Backend", " ")]
    [DataRow("Outbound", null)]
    [DataRow("Outbound", "")]
    [DataRow("Outbound", " ")]
    [DataRow("OnError", null)]
    [DataRow("OnError", "")]
    [DataRow("OnError", " ")]
    public void AppendHeader_RejectsInvalidNameWithoutMutation(string section, string? name)
    {
        var test = new ConfigurableAppendHeader(name!, ["new"]).AsTestDocument();
        HeaderQueryTestHelpers.SeedHeaders(test, section, ["old"]);

        HeaderQueryTestHelpers.AssertInvalid(test, section, nameof(IInboundContext.AppendHeader));
    }

    [TestMethod]
    [DataRow("Inbound", false)]
    [DataRow("Inbound", true)]
    [DataRow("Backend", false)]
    [DataRow("Backend", true)]
    [DataRow("Outbound", false)]
    [DataRow("Outbound", true)]
    [DataRow("OnError", false)]
    [DataRow("OnError", true)]
    public void AppendHeader_RejectsNullValuesWithoutMutation(string section, bool exists)
    {
        var test = new ConfigurableAppendHeader("x-test", null!).AsTestDocument();
        HeaderQueryTestHelpers.SeedHeaders(test, section, exists ? ["old"] : null);

        HeaderQueryTestHelpers.AssertInvalid(test, section, nameof(IInboundContext.AppendHeader));
    }
}