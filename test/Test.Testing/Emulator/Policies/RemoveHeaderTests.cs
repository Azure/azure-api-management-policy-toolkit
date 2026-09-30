// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Document;

namespace Test.Emulator.Emulator.Policies;

[TestClass]
public class RemoveHeaderTests
{
    class SimpleRemoveHeader : IDocument
    {
        public void Inbound(IInboundContext context)
        {
            context.RemoveHeader("X-Remove-Me");
        }

        public void Backend(IBackendContext context)
        {
            context.RemoveHeader("X-Backend-Remove");
        }

        public void Outbound(IOutboundContext context)
        {
            context.RemoveHeader("X-Outbound-Remove");
        }

        public void OnError(IOnErrorContext context)
        {
            context.RemoveHeader("X-Error-Remove");
        }
    }

    [TestMethod]
    public void RemoveHeader_Inbound()
    {
        var test = new TestDocument(new SimpleRemoveHeader())
        {
            Context = { Request = { Headers = { { "X-Remove-Me", ["value1"] }, { "X-Keep", ["keep"] } } } }
        };

        test.RunInbound();

        test.Context.Request.Headers.Should().NotContainKey("X-Remove-Me")
            .And.ContainKey("X-Keep");
    }

    [TestMethod]
    public void RemoveHeader_Backend()
    {
        var test = new TestDocument(new SimpleRemoveHeader())
        {
            Context = { Request = { Headers = { { "X-Backend-Remove", ["value1"] }, { "X-Keep", ["keep"] } } } }
        };

        test.RunBackend();

        test.Context.Request.Headers.Should().NotContainKey("X-Backend-Remove")
            .And.ContainKey("X-Keep");
    }

    [TestMethod]
    public void RemoveHeader_Outbound()
    {
        var test = new TestDocument(new SimpleRemoveHeader())
        {
            Context = { Response = { Headers = { { "X-Outbound-Remove", ["value1"] }, { "X-Keep", ["keep"] } } } }
        };

        test.RunOutbound();

        test.Context.Response.Headers.Should().NotContainKey("X-Outbound-Remove")
            .And.ContainKey("X-Keep");
    }

    [TestMethod]
    public void RemoveHeader_OnError()
    {
        var test = new TestDocument(new SimpleRemoveHeader())
        {
            Context = { Response = { Headers = { { "X-Error-Remove", ["value1"] }, { "X-Keep", ["keep"] } } } }
        };

        test.RunOnError();

        test.Context.Response.Headers.Should().NotContainKey("X-Error-Remove")
            .And.ContainKey("X-Keep");
    }

    [TestMethod]
    public void RemoveHeader_Callback()
    {
        var test = new TestDocument(new SimpleRemoveHeader())
        {
            Context = { Request = { Headers = { { "X-Remove-Me", ["value1"] } } } }
        };
        var callbackExecuted = false;
        test.SetupInbound().RemoveHeader().WithCallback((_, _) =>
        {
            callbackExecuted = true;
        });

        test.RunInbound();

        callbackExecuted.Should().BeTrue();
        test.Context.Request.Headers.Should().ContainKey("X-Remove-Me");
    }

    [TestMethod]
    public void RemoveHeader_NonExistentHeader()
    {
        var test = new TestDocument(new SimpleRemoveHeader())
        {
            Context = { Request = { Headers = { { "X-Keep", ["keep"] } } } }
        };

        test.RunInbound();

        test.Context.Request.Headers.Should().ContainKey("X-Keep");
    }

    class ConfigurableRemoveHeader(
        string name,
        bool useExpressions = false,
        bool includeUnmatched = false) : IDocument
    {
        public void Inbound(IInboundContext context) =>
            HeaderQueryTestHelpers.Apply(context, context.RemoveHeader, name, useExpressions, includeUnmatched);

        public void Backend(IBackendContext context) =>
            HeaderQueryTestHelpers.Apply(context, context.RemoveHeader, name, useExpressions, includeUnmatched);

        public void Outbound(IOutboundContext context) =>
            HeaderQueryTestHelpers.Apply(context, context.RemoveHeader, name, useExpressions, includeUnmatched);

        public void OnError(IOnErrorContext context) =>
            HeaderQueryTestHelpers.Apply(context, context.RemoveHeader, name, useExpressions, includeUnmatched);
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
    public void RemoveHeader_RemovesAllValuesIgnoringCase(string section, bool empty)
    {
        var test = new ConfigurableRemoveHeader("x-test").AsTestDocument();
        HeaderQueryTestHelpers.SeedHeaders(test, section, empty ? [] : ["", "old", "old"]);

        HeaderQueryTestHelpers.Run(test, section);

        var headers = HeaderQueryTestHelpers.Message(test.Context, section).Headers;
        headers.Should().HaveCount(1).And.NotContainKey("X-TEST");
        HeaderQueryTestHelpers.AssertUnrelatedHeaders(test, section);
    }

    [TestMethod]
    [DataRow("Inbound")]
    [DataRow("Backend")]
    [DataRow("Outbound")]
    [DataRow("OnError")]
    public void RemoveHeader_PreservesInjectedStorageAndCaseInsensitiveRemoval(string section)
    {
        var test = new ConfigurableRemoveHeader("x-test").AsTestDocument();
        HeaderQueryTestHelpers.SeedHeaders(test, section);
        var source = new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["X-Test"] = ["old"],
            ["X-Keep"] = ["keep"]
        };
        var comparer = source.Comparer;
        HeaderQueryTestHelpers.Message(test.Context, section).Headers = source;

        HeaderQueryTestHelpers.Run(test, section);

        var headers = HeaderQueryTestHelpers.Message(test.Context, section).Headers;
        headers.Should().BeSameAs(source);
        headers.Comparer.Should().BeSameAs(comparer);
        source.Should().HaveCount(1).And.NotContainKey("X-Test");
        HeaderQueryTestHelpers.AssertUnrelatedHeaders(test, section);
    }

    [TestMethod]
    [DataRow("Inbound")]
    [DataRow("Backend")]
    [DataRow("Outbound")]
    [DataRow("OnError")]
    public void RemoveHeader_RemovesAllCaseVariantsFromInjectedDictionary(string section)
    {
        var test = new ConfigurableRemoveHeader("x-test").AsTestDocument();
        HeaderQueryTestHelpers.SeedHeaders(test, section);
        var keep = new[] { "keep" };
        var prefix = new[] { "unrelated" };
        var source = new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["X-Test"] = ["first"],
            ["x-test"] = [],
            ["X-TEST"] = ["repeated", "repeated"],
            ["X-Keep"] = keep,
            ["X-Test-Other"] = prefix
        };
        var comparer = source.Comparer;
        HeaderQueryTestHelpers.Message(test.Context, section).Headers = source;

        HeaderQueryTestHelpers.Run(test, section);

        var headers = HeaderQueryTestHelpers.Message(test.Context, section).Headers;
        headers.Should().BeSameAs(source);
        headers.Comparer.Should().BeSameAs(comparer);
        source.Keys.Should().NotContain(key => string.Equals(key, "x-test", StringComparison.OrdinalIgnoreCase));
        source.Keys.Should().Equal("X-Keep", "X-Test-Other");
        source["X-Keep"].Should().BeSameAs(keep).And.Equal("keep");
        source["X-Test-Other"].Should().BeSameAs(prefix).And.Equal("unrelated");
        HeaderQueryTestHelpers.AssertUnrelatedHeaders(test, section);
    }

    [TestMethod]
    [DataRow("Inbound")]
    [DataRow("Backend")]
    [DataRow("Outbound")]
    [DataRow("OnError")]
    public void RemoveHeader_LeavesMissingHeaderAndOppositeMessageUntouched(string section)
    {
        var test = new ConfigurableRemoveHeader("x-test").AsTestDocument();
        HeaderQueryTestHelpers.SeedHeaders(test, section);

        HeaderQueryTestHelpers.Run(test, section);

        HeaderQueryTestHelpers.Message(test.Context, section).Headers.Should().HaveCount(1);
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
    public void RemoveHeader_CallbackOverridesDefaultInEverySection(string section, bool withPredicate)
    {
        var test = new ConfigurableRemoveHeader("x-test", includeUnmatched: withPredicate).AsTestDocument();
        HeaderQueryTestHelpers.SeedHeaders(test, section, ["old"]);
        HeaderQueryTestHelpers.Message(test.Context, section).Headers["x-other"] = ["default"];
        var setup = withPredicate
            ? HeaderQueryTestHelpers.Setup<MockRemoveHeaderProvider.Setup>(
                test, section, typeof(MockRemoveHeaderProvider), nameof(MockRemoveHeaderProvider.RemoveHeader),
                (_, name) => name == "x-test")
            : HeaderQueryTestHelpers.Setup<MockRemoveHeaderProvider.Setup>(
                test, section, typeof(MockRemoveHeaderProvider), nameof(MockRemoveHeaderProvider.RemoveHeader));
        setup.WithCallback((context, name) =>
            HeaderQueryTestHelpers.Message(context, section).Headers[name] = ["callback"]);

        HeaderQueryTestHelpers.Run(test, section);

        var headers = HeaderQueryTestHelpers.Message(test.Context, section).Headers;
        headers["X-Test"].Should().Equal("callback");
        headers.Should().HaveCount(withPredicate ? 2 : 3);
        headers.ContainsKey("x-other").Should().Be(!withPredicate);
        HeaderQueryTestHelpers.AssertUnrelatedHeaders(test, section);
    }

    [TestMethod]
    [DataRow("Inbound")]
    [DataRow("Backend")]
    [DataRow("Outbound")]
    [DataRow("OnError")]
    public void RemoveHeader_UsesExpressionDrivenName(string section)
    {
        var test = new ConfigurableRemoveHeader("unused", useExpressions: true).AsTestDocument();
        HeaderQueryTestHelpers.SeedHeaders(test, section, ["old", "old"]);
        test.Context.Request.Headers["X-Policy-Name"] = ["x-test"];

        HeaderQueryTestHelpers.Run(test, section);

        HeaderQueryTestHelpers.Message(test.Context, section).Headers.Should().NotContainKey("X-Test");
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
    public void RemoveHeader_RejectsInvalidNameWithoutMutation(string section, string? name)
    {
        var test = new ConfigurableRemoveHeader(name!).AsTestDocument();
        HeaderQueryTestHelpers.SeedHeaders(test, section, ["old"]);

        HeaderQueryTestHelpers.AssertInvalid(test, section, nameof(IInboundContext.RemoveHeader));
    }
}
