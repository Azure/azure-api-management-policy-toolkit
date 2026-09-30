// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Document;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Expressions;

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

    class ConfigurableRemoveQueryParameter(
        string name,
        bool useExpressions = false,
        bool includeUnmatched = false) : IDocument
    {
        public void Inbound(IInboundContext context) =>
            HeaderQueryTestHelpers.Apply(context, context.RemoveQueryParameter, name, useExpressions, includeUnmatched);

        public void Backend(IBackendContext context) =>
            HeaderQueryTestHelpers.Apply(context, context.RemoveQueryParameter, name, useExpressions, includeUnmatched);

        public void Outbound(IOutboundContext context) =>
            HeaderQueryTestHelpers.Apply(context, context.RemoveQueryParameter, name, useExpressions, includeUnmatched);

        public void OnError(IOnErrorContext context) =>
            HeaderQueryTestHelpers.Apply(context, context.RemoveQueryParameter, name, useExpressions, includeUnmatched);
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
    public void RemoveQueryParameter_RemovesAllRepeatedOrEmptyValues(string section, bool empty)
    {
        var test = new ConfigurableRemoveQueryParameter("tag").AsTestDocument();
        HeaderQueryTestHelpers.SeedQuery(test, empty ? [] : ["", "old", "old"]);

        HeaderQueryTestHelpers.Run(test, section);

        test.Context.Request.Url.Query.Should().HaveCount(1).And.NotContainKey("tag");
        HeaderQueryTestHelpers.AssertUnrelatedQueryState(test);
    }

    [TestMethod]
    [DataRow("Inbound")]
    [DataRow("Backend")]
    [DataRow("Outbound")]
    [DataRow("OnError")]
    public void RemoveQueryParameter_LeavesMissingParameterAndResponseUntouched(string section)
    {
        var test = new ConfigurableRemoveQueryParameter("tag").AsTestDocument();
        HeaderQueryTestHelpers.SeedQuery(test);

        HeaderQueryTestHelpers.Run(test, section);

        test.Context.Request.Url.Query.Should().HaveCount(1);
        HeaderQueryTestHelpers.AssertUnrelatedQueryState(test);
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
    public void RemoveQueryParameter_CallbackOverridesDefaultInEverySection(string section, bool withPredicate)
    {
        var test = new ConfigurableRemoveQueryParameter("tag", includeUnmatched: withPredicate).AsTestDocument();
        HeaderQueryTestHelpers.SeedQuery(test, ["old"]);
        test.Context.Request.Url.Query["x-other"] = ["default"];
        var setup = withPredicate
            ? HeaderQueryTestHelpers.Setup<MockRemoveQueryParameterProvider.Setup>(
                test, section, typeof(MockRemoveQueryParameterProvider), nameof(MockRemoveQueryParameterProvider.RemoveQueryParameter),
                (_, name) => name == "tag")
            : HeaderQueryTestHelpers.Setup<MockRemoveQueryParameterProvider.Setup>(
                test, section, typeof(MockRemoveQueryParameterProvider), nameof(MockRemoveQueryParameterProvider.RemoveQueryParameter));
        setup.WithCallback((context, name) => context.Request.Url.Query[name] = ["callback"]);

        HeaderQueryTestHelpers.Run(test, section);

        var query = test.Context.Request.Url.Query;
        query["tag"].Should().Equal("callback");
        query.Should().HaveCount(withPredicate ? 2 : 3);
        query.ContainsKey("x-other").Should().Be(!withPredicate);
        HeaderQueryTestHelpers.AssertUnrelatedQueryState(test);
    }

    [TestMethod]
    [DataRow("Inbound")]
    [DataRow("Backend")]
    [DataRow("Outbound")]
    [DataRow("OnError")]
    public void RemoveQueryParameter_UsesExpressionDrivenName(string section)
    {
        var test = new ConfigurableRemoveQueryParameter("unused", useExpressions: true).AsTestDocument();
        HeaderQueryTestHelpers.SeedQuery(test, ["old", "old"]);
        test.Context.Request.Headers["X-Policy-Name"] = ["tag"];

        HeaderQueryTestHelpers.Run(test, section);

        test.Context.Request.Url.Query.Should().NotContainKey("tag");
        HeaderQueryTestHelpers.AssertUnrelatedQueryState(test);
    }

    [TestMethod]
    [DataRow("Inbound")]
    [DataRow("Backend")]
    [DataRow("Outbound")]
    [DataRow("OnError")]
    public void RemoveQueryParameter_DoesNotRemoveDifferentlyCasedQueryName(string section)
    {
        var test = new ConfigurableRemoveQueryParameter("tag").AsTestDocument();
        HeaderQueryTestHelpers.SeedQuery(test, ["old"]);
        test.Context.Request.Url.Query["Tag"] = ["separate"];

        HeaderQueryTestHelpers.Run(test, section);

        test.Context.Request.Url.Query.Should().NotContainKey("tag").And.HaveCount(2);
        test.Context.Request.Url.Query["Tag"].Should().Equal("separate");
        HeaderQueryTestHelpers.AssertUnrelatedQueryState(test);
    }

    [TestMethod]
    [DataRow("Inbound")]
    [DataRow("Backend")]
    [DataRow("Outbound")]
    [DataRow("OnError")]
    public void RemoveQueryParameter_RemovesParsedRepeatedValuesAndPreservesUnrelatedParameters(string section)
    {
        var test = new ConfigurableRemoveQueryParameter("tag").AsTestDocument();
        var uri = new Uri("https://contoso.example/path?tag=old&tag=old&keep=a%26b");
        test.Context.Request = new MockRequest(uri);

        HeaderQueryTestHelpers.Run(test, section);

        test.Context.Request.Url.Query.Should().HaveCount(1).And.NotContainKey("tag");
        test.Context.Request.Url.Query["keep"].Should().Equal("a&b");
        test.Context.Request.Url.QueryString.Should().Be("?keep=a%26b");
        test.Context.Request.Url.Path.Should().Be("/path");
        test.Context.Request.OriginalUrl.ToUri().Should().Be(uri);
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
    public void RemoveQueryParameter_RejectsInvalidNameWithoutMutation(string section, string? name)
    {
        var test = new ConfigurableRemoveQueryParameter(name!).AsTestDocument();
        HeaderQueryTestHelpers.SeedQuery(test, ["old"]);

        HeaderQueryTestHelpers.AssertInvalid(test, section, nameof(IInboundContext.RemoveQueryParameter));
    }
}
