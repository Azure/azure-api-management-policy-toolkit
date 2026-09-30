// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Document;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Expressions;

namespace Test.Emulator.Emulator.Policies;

[TestClass]
public class SetQueryParameterIfNotExistTests
{
    class ConfigurableSetQueryParameterIfNotExist(
        string name,
        string[] values,
        bool useExpressions = false,
        bool includeUnmatched = false) : IDocument
    {
        public void Inbound(IInboundContext context) =>
            HeaderQueryTestHelpers.Apply(context, context.SetQueryParameterIfNotExist, name, values, useExpressions, includeUnmatched);

        public void Backend(IBackendContext context) =>
            HeaderQueryTestHelpers.Apply(context, context.SetQueryParameterIfNotExist, name, values, useExpressions, includeUnmatched);

        public void Outbound(IOutboundContext context) =>
            HeaderQueryTestHelpers.Apply(context, context.SetQueryParameterIfNotExist, name, values, useExpressions, includeUnmatched);

        public void OnError(IOnErrorContext context) =>
            HeaderQueryTestHelpers.Apply(context, context.SetQueryParameterIfNotExist, name, values, useExpressions, includeUnmatched);
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
    public void SetQueryParameterIfNotExist_SkipsExistingParametersEvenWhenEmpty(string section, string existing)
    {
        var test = new ConfigurableSetQueryParameterIfNotExist("tag", ["", "new", "new"]).AsTestDocument();
        string[]? previous = existing switch
        {
            "missing" => null,
            "empty" => [],
            "repeated" => ["old", "old"],
            _ => throw new ArgumentOutOfRangeException(nameof(existing))
        };
        HeaderQueryTestHelpers.SeedQuery(test, previous);

        HeaderQueryTestHelpers.Run(test, section);

        test.Context.Request.Url.Query["tag"].Should().Equal(previous ?? ["", "new", "new"]);
        if (previous is not null)
        {
            test.Context.Request.Url.Query["tag"].Should().BeSameAs(previous);
        }
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
    public void SetQueryParameterIfNotExist_HandlesEmptyIncomingValues(string section, bool exists)
    {
        var test = new ConfigurableSetQueryParameterIfNotExist("tag", []).AsTestDocument();
        HeaderQueryTestHelpers.SeedQuery(test, exists ? ["old", "old"] : null);

        HeaderQueryTestHelpers.Run(test, section);

        test.Context.Request.Url.Query["tag"].Should().Equal(exists ? ["old", "old"] : Array.Empty<string>());
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
    public void SetQueryParameterIfNotExist_CallbackOverridesDefaultInEverySection(string section, bool withPredicate)
    {
        var test = new ConfigurableSetQueryParameterIfNotExist("tag", ["new"], includeUnmatched: withPredicate).AsTestDocument();
        HeaderQueryTestHelpers.SeedQuery(test, ["old"]);
        var setup = withPredicate
            ? HeaderQueryTestHelpers.Setup<MockSetQueryParameterIfNotExistProvider.Setup>(
                test, section, typeof(MockSetQueryParameterIfNotExistProvider), nameof(MockSetQueryParameterIfNotExistProvider.SetQueryParameterIfNotExist),
                (_, name, _) => name == "tag")
            : HeaderQueryTestHelpers.Setup<MockSetQueryParameterIfNotExistProvider.Setup>(
                test, section, typeof(MockSetQueryParameterIfNotExistProvider), nameof(MockSetQueryParameterIfNotExistProvider.SetQueryParameterIfNotExist));
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
    public void SetQueryParameterIfNotExist_UsesExpressionDrivenNameAndValues(string section)
    {
        var test = new ConfigurableSetQueryParameterIfNotExist("unused", [], useExpressions: true).AsTestDocument();
        HeaderQueryTestHelpers.SeedQuery(test);
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
    public void SetQueryParameterIfNotExist_DoesNotSkipDifferentlyCasedQueryName(string section)
    {
        var test = new ConfigurableSetQueryParameterIfNotExist("tag", ["new"]).AsTestDocument();
        HeaderQueryTestHelpers.SeedQuery(test);
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
    public void SetQueryParameterIfNotExist_PreservesParsedRepeatedValuesAndUnrelatedParameters(string section)
    {
        var test = new ConfigurableSetQueryParameterIfNotExist("tag", ["changed"]).AsTestDocument();
        var uri = new Uri("https://contoso.example/path?tag=old&tag=old&keep=a%26b");
        test.Context.Request = new MockRequest(uri);

        HeaderQueryTestHelpers.Run(test, section);

        test.Context.Request.Url.Query["tag"].Should().Equal("old", "old");
        test.Context.Request.Url.Query["keep"].Should().Equal("a&b");
        test.Context.Request.Url.QueryString.Should().Be("?tag=old&tag=old&keep=a%26b");
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
    public void SetQueryParameterIfNotExist_RejectsInvalidNameWithoutMutation(string section, string? name)
    {
        var test = new ConfigurableSetQueryParameterIfNotExist(name!, ["new"]).AsTestDocument();
        HeaderQueryTestHelpers.SeedQuery(test, ["old"]);

        HeaderQueryTestHelpers.AssertInvalid(test, section, nameof(IInboundContext.SetQueryParameterIfNotExist));
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
    public void SetQueryParameterIfNotExist_RejectsNullValuesWithoutMutationEvenWhenSkipping(string section, bool exists)
    {
        var test = new ConfigurableSetQueryParameterIfNotExist("tag", null!).AsTestDocument();
        HeaderQueryTestHelpers.SeedQuery(test, exists ? ["old"] : null);

        HeaderQueryTestHelpers.AssertInvalid(test, section, nameof(IInboundContext.SetQueryParameterIfNotExist));
    }
}
