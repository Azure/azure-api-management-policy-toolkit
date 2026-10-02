// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Document;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Expressions;

namespace Test.Emulator.Emulator.Policies;

[TestClass]
public class SetQueryParameterTests
{
    class ConfigurableSetQueryParameter(
        string name,
        string[] values,
        bool useExpressions = false,
        bool includeUnmatched = false) : IDocument
    {
        public void Inbound(IInboundContext context) =>
            HeaderQueryTestHelpers.Apply(context, context.SetQueryParameter, name, values, useExpressions, includeUnmatched);

        public void Backend(IBackendContext context) =>
            HeaderQueryTestHelpers.Apply(context, context.SetQueryParameter, name, values, useExpressions, includeUnmatched);

        public void Outbound(IOutboundContext context) =>
            HeaderQueryTestHelpers.Apply(context, context.SetQueryParameter, name, values, useExpressions, includeUnmatched);

        public void OnError(IOnErrorContext context) =>
            HeaderQueryTestHelpers.Apply(context, context.SetQueryParameter, name, values, useExpressions, includeUnmatched);
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
    public void SetQueryParameter_ReplacesOrCreatesOrderedRepeatedAndEmptyValues(string section, bool exists)
    {
        var test = new ConfigurableSetQueryParameter("tag", ["", "new", "new"]).AsTestDocument();
        HeaderQueryTestHelpers.SeedQuery(test, exists ? ["old", "old"] : null);

        HeaderQueryTestHelpers.Run(test, section);

        test.Context.Request.Url.Query["tag"].Should().Equal("", "new", "new");
        test.Context.Request.Url.Query.Should().HaveCount(2);
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
    public void SetQueryParameter_SetsEmptyValuesForMissingOrExistingParameter(string section, bool exists)
    {
        var test = new ConfigurableSetQueryParameter("tag", []).AsTestDocument();
        HeaderQueryTestHelpers.SeedQuery(test, exists ? ["old", "old"] : null);

        HeaderQueryTestHelpers.Run(test, section);

        test.Context.Request.Url.Query["tag"].Should().BeEmpty();
        test.Context.Request.Url.Query.Should().HaveCount(2);
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
    public void SetQueryParameter_CallbackOverridesDefaultInEverySection(string section, bool withPredicate)
    {
        var test = new ConfigurableSetQueryParameter("tag", ["new"], includeUnmatched: withPredicate).AsTestDocument();
        HeaderQueryTestHelpers.SeedQuery(test, ["old"]);
        var setup = withPredicate
            ? HeaderQueryTestHelpers.Setup<MockSetQueryParameterProvider.Setup>(
                test, section, typeof(MockSetQueryParameterProvider), nameof(MockSetQueryParameterProvider.SetQueryParameter),
                (_, name, _) => name == "tag")
            : HeaderQueryTestHelpers.Setup<MockSetQueryParameterProvider.Setup>(
                test, section, typeof(MockSetQueryParameterProvider), nameof(MockSetQueryParameterProvider.SetQueryParameter));
        setup.WithCallback((context, name, values) =>
            context.Request.Url.Query[name] = ["callback", .. values]);

        HeaderQueryTestHelpers.Run(test, section);

        var query = test.Context.Request.Url.Query;
        query["tag"].Should().Equal("callback", "new");
        query.Should().HaveCount(withPredicate ? 3 : 2);
        if (withPredicate)
        {
            query["x-other"].Should().Equal("default");
        }
        HeaderQueryTestHelpers.AssertUnrelatedQueryState(test);
    }

    [TestMethod]
    [DataRow("Inbound")]
    [DataRow("Backend")]
    [DataRow("Outbound")]
    [DataRow("OnError")]
    public void SetQueryParameter_UsesExpressionDrivenNameAndValues(string section)
    {
        var test = new ConfigurableSetQueryParameter("unused", [], useExpressions: true).AsTestDocument();
        HeaderQueryTestHelpers.SeedQuery(test, ["old"]);
        test.Context.Request.Headers["X-Policy-Name"] = ["tag"];
        test.Context.Request.Headers["X-Policy-Values"] = ["", "expression", "expression"];

        HeaderQueryTestHelpers.Run(test, section);

        test.Context.Request.Url.Query["tag"].Should().Equal("", "expression", "expression");
        HeaderQueryTestHelpers.AssertUnrelatedQueryState(test);
    }

    [TestMethod]
    [DataRow("Inbound")]
    [DataRow("Backend")]
    [DataRow("Outbound")]
    [DataRow("OnError")]
    public void SetQueryParameter_DoesNotReplaceDifferentlyCasedQueryName(string section)
    {
        var test = new ConfigurableSetQueryParameter("tag", ["new"]).AsTestDocument();
        HeaderQueryTestHelpers.SeedQuery(test, ["old"]);
        test.Context.Request.Url.Query["Tag"] = ["separate"];

        HeaderQueryTestHelpers.Run(test, section);

        test.Context.Request.Url.Query["tag"].Should().Equal("new");
        test.Context.Request.Url.Query["Tag"].Should().Equal("separate");
        test.Context.Request.Url.Query.Should().HaveCount(3);
        HeaderQueryTestHelpers.AssertUnrelatedQueryState(test);
    }

    [TestMethod]
    [DataRow("Inbound")]
    [DataRow("Backend")]
    [DataRow("Outbound")]
    [DataRow("OnError")]
    public void SetQueryParameter_ReplacesParsedRepeatedValuesAndEncodesEachValue(string section)
    {
        var test = new ConfigurableSetQueryParameter("tag", ["", "a b", "a&b", "a&b"]).AsTestDocument();
        var uri = new Uri("https://contoso.example/path?tag=old&tag=old&keep=a%26b");
        test.Context.Request = new MockRequest(uri);

        HeaderQueryTestHelpers.Run(test, section);

        test.Context.Request.Url.Query["tag"].Should().Equal("", "a b", "a&b", "a&b");
        test.Context.Request.Url.Query["keep"].Should().Equal("a&b");
        test.Context.Request.Url.QueryString.Should().Be("?tag=&tag=a%20b&tag=a%26b&tag=a%26b&keep=a%26b");
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
    public void SetQueryParameter_RejectsInvalidNameWithoutMutation(string section, string? name)
    {
        var test = new ConfigurableSetQueryParameter(name!, ["new"]).AsTestDocument();
        HeaderQueryTestHelpers.SeedQuery(test, ["old"]);

        HeaderQueryTestHelpers.AssertInvalid(test, section, nameof(IInboundContext.SetQueryParameter));
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
    public void SetQueryParameter_RejectsNullValuesWithoutMutation(string section, bool exists)
    {
        var test = new ConfigurableSetQueryParameter("tag", null!).AsTestDocument();
        HeaderQueryTestHelpers.SeedQuery(test, exists ? ["old"] : null);

        HeaderQueryTestHelpers.AssertInvalid(test, section, nameof(IInboundContext.SetQueryParameter));
    }

    [TestMethod]
    [DataRow("Inbound")]
    [DataRow("Backend")]
    [DataRow("Outbound")]
    [DataRow("OnError")]
    public void SetQueryParameter_CallbackCanShortCircuitSection(string section)
    {
        var test = new ConfigurableSetQueryParameter("tag", ["new"], includeUnmatched: true).AsTestDocument();
        HeaderQueryTestHelpers.SeedQuery(test, ["old"]);
        HeaderQueryTestHelpers.Setup<MockSetQueryParameterProvider.Setup>(
            test, section, typeof(MockSetQueryParameterProvider), nameof(MockSetQueryParameterProvider.SetQueryParameter))
            .WithCallback((context, _, _) =>
            {
                context.Response.StatusCode = 409;
                throw new FinishSectionProcessingException();
            });

        HeaderQueryTestHelpers.Run(test, section);

        test.Context.Response.StatusCode.Should().Be(409);
        test.Context.Request.Url.Query["tag"].Should().Equal("old");
        test.Context.Request.Url.Query.Should().NotContainKey("x-other");
        HeaderQueryTestHelpers.AssertUnrelatedQueryState(test);
    }
}
