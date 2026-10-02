// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Document;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Expressions;

namespace Test.Emulator.Emulator.Policies;

[TestClass]
public class AppendQueryParameterTests
{
    class SimpleAppendQueryParameter : IDocument
    {
        public void Inbound(IInboundContext context)
        {
            context.AppendQueryParameter("param1", "value-1", "value-2");
        }
    }

    class MultipleAppendQueryParameter : IDocument
    {
        public void Inbound(IInboundContext context)
        {
            context.AppendQueryParameter("paramA", "A");
            context.AppendQueryParameter("paramB", "B");
        }
    }

    [TestMethod]
    public void AppendQueryParameter_ShouldCreateParam_WhenNotExist()
    {
        // Arrange
        var test = new SimpleAppendQueryParameter().AsTestDocument();

        // Act
        test.RunInbound();

        // Assert
        test.Context.Request.Url
            .Query.Should().ContainKey("param1")
            .WhoseValue.Should().ContainInOrder("value-1", "value-2");
    }

    [TestMethod]
    public void AppendQueryParameter_ShouldAppendParams_WhenExist()
    {
        // Arrange
        var test = new TestDocument(new SimpleAppendQueryParameter())
        {
            Context = { Request = { Url = { Query = { { "param1", ["value-0"] } } } } }
        };

        // Act
        test.RunInbound();

        // Assert
        test.Context.Request.Url
            .Query.Should().ContainKey("param1")
            .WhoseValue.Should().ContainInOrder("value-0", "value-1", "value-2");
    }

    [TestMethod]
    public void AppendQueryParameter_WithCallback()
    {
        // Arrange
        var test = new SimpleAppendQueryParameter().AsTestDocument();
        var callbackExecuted = false;
        test.SetupInbound().AppendQueryParameter().WithCallback(((context, name, values) =>
        {
            callbackExecuted = true;
            context.Request.Url.Query.Add(name, Enumerable.Reverse(values).ToArray());
        }));

        // Act
        test.RunInbound();

        // Assert
        callbackExecuted.Should().BeTrue();
        test.Context.Request.Url
            .Query.Should().ContainKey("param1")
            .WhoseValue.Should().ContainInOrder("value-2", "value-1");
    }

    [TestMethod]
    public void AppendQueryParameter_WithPredicateCallback()
    {
        // Arrange
        var test = new TestDocument(new MultipleAppendQueryParameter())
        {
            Context = { Request = { Url = { Query = { { "paramA", ["AA"] } } } } }
        };
        var callbackExecuted = false;
        test.SetupInbound().AppendQueryParameter((_, name, _) => name == "paramB").WithCallback(((_, _, _) =>
        {
            callbackExecuted = true;
        }));

        // Act
        test.RunInbound();

        // Assert
        callbackExecuted.Should().BeTrue();
        var query = test.Context.Request.Url.Query;
        query.Should().ContainKey("paramA")
            .WhoseValue.Should().ContainInOrder("AA", "A");
        query.Should().NotContainKey("paramB");
    }

    class ConfigurableAppendQueryParameter(
        string name,
        string[] values,
        bool useExpressions = false,
        bool includeUnmatched = false) : IDocument
    {
        public void Inbound(IInboundContext context) =>
            HeaderQueryTestHelpers.Apply(context, context.AppendQueryParameter, name, values, useExpressions, includeUnmatched);

        public void Backend(IBackendContext context) =>
            HeaderQueryTestHelpers.Apply(context, context.AppendQueryParameter, name, values, useExpressions, includeUnmatched);

        public void Outbound(IOutboundContext context) =>
            HeaderQueryTestHelpers.Apply(context, context.AppendQueryParameter, name, values, useExpressions, includeUnmatched);

        public void OnError(IOnErrorContext context) =>
            HeaderQueryTestHelpers.Apply(context, context.AppendQueryParameter, name, values, useExpressions, includeUnmatched);
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
    public void AppendQueryParameter_PreservesOrderedRepeatedAndEmptyValues(string section, string existing)
    {
        var test = new ConfigurableAppendQueryParameter("tag", ["", "new", "new"]).AsTestDocument();
        string[]? previous = existing switch
        {
            "missing" => null,
            "empty" => [],
            "repeated" => ["old", "old"],
            _ => throw new ArgumentOutOfRangeException(nameof(existing))
        };
        HeaderQueryTestHelpers.SeedQuery(test, previous);

        HeaderQueryTestHelpers.Run(test, section);

        test.Context.Request.Url.Query["tag"].Should().Equal((previous ?? []).Concat(["", "new", "new"]));
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
    public void AppendQueryParameter_HandlesEmptyIncomingValues(string section, bool exists)
    {
        var test = new ConfigurableAppendQueryParameter("tag", []).AsTestDocument();
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
    public void AppendQueryParameter_CallbackOverridesDefaultInEverySection(string section, bool withPredicate)
    {
        var test = new ConfigurableAppendQueryParameter("tag", ["new"], includeUnmatched: withPredicate).AsTestDocument();
        HeaderQueryTestHelpers.SeedQuery(test, ["old"]);
        var setup = withPredicate
            ? HeaderQueryTestHelpers.Setup<MockAppendQueryParameterProvider.Setup>(
                test, section, typeof(MockAppendQueryParameterProvider), nameof(MockAppendQueryParameterProvider.AppendQueryParameter),
                (_, name, _) => name == "tag")
            : HeaderQueryTestHelpers.Setup<MockAppendQueryParameterProvider.Setup>(
                test, section, typeof(MockAppendQueryParameterProvider), nameof(MockAppendQueryParameterProvider.AppendQueryParameter));
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
    public void AppendQueryParameter_UsesExpressionDrivenNameAndValues(string section)
    {
        var test = new ConfigurableAppendQueryParameter("unused", [], useExpressions: true).AsTestDocument();
        HeaderQueryTestHelpers.SeedQuery(test, ["old"]);
        test.Context.Request.Headers["X-Policy-Name"] = ["tag"];
        test.Context.Request.Headers["X-Policy-Values"] = ["", "expression", "expression"];

        HeaderQueryTestHelpers.Run(test, section);

        test.Context.Request.Url.Query["tag"].Should().Equal("old", "", "expression", "expression");
        HeaderQueryTestHelpers.AssertUnrelatedQueryState(test);
    }

    [TestMethod]
    [DataRow("Inbound")]
    [DataRow("Backend")]
    [DataRow("Outbound")]
    [DataRow("OnError")]
    public void AppendQueryParameter_DoesNotCombineDifferentlyCasedQueryNames(string section)
    {
        var test = new ConfigurableAppendQueryParameter("tag", ["new"]).AsTestDocument();
        HeaderQueryTestHelpers.SeedQuery(test, ["old"]);
        test.Context.Request.Url.Query["Tag"] = ["separate"];

        HeaderQueryTestHelpers.Run(test, section);

        test.Context.Request.Url.Query["tag"].Should().Equal("old", "new");
        test.Context.Request.Url.Query["Tag"].Should().Equal("separate");
        test.Context.Request.Url.Query.Should().HaveCount(3);
        HeaderQueryTestHelpers.AssertUnrelatedQueryState(test);
    }

    [TestMethod]
    [DataRow("Inbound")]
    [DataRow("Backend")]
    [DataRow("Outbound")]
    [DataRow("OnError")]
    public void AppendQueryParameter_PreservesParsedRepeatedValuesAndUnrelatedParameters(string section)
    {
        var test = new ConfigurableAppendQueryParameter("tag", ["", "new", "new"]).AsTestDocument();
        var uri = new Uri("https://contoso.example/path?tag=old&tag=old&keep=a%26b");
        test.Context.Request = new MockRequest(uri);

        HeaderQueryTestHelpers.Run(test, section);

        test.Context.Request.Url.Query["tag"].Should().Equal("old", "old", "", "new", "new");
        test.Context.Request.Url.Query["keep"].Should().Equal("a&b");
        test.Context.Request.Url.QueryString.Should().Be("?tag=old&tag=old&tag=&tag=new&tag=new&keep=a%26b");
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
    public void AppendQueryParameter_RejectsInvalidNameWithoutMutation(string section, string? name)
    {
        var test = new ConfigurableAppendQueryParameter(name!, ["new"]).AsTestDocument();
        HeaderQueryTestHelpers.SeedQuery(test, ["old"]);

        HeaderQueryTestHelpers.AssertInvalid(test, section, nameof(IInboundContext.AppendQueryParameter));
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
    public void AppendQueryParameter_RejectsNullValuesWithoutMutation(string section, bool exists)
    {
        var test = new ConfigurableAppendQueryParameter("tag", null!).AsTestDocument();
        HeaderQueryTestHelpers.SeedQuery(test, exists ? ["old"] : null);

        HeaderQueryTestHelpers.AssertInvalid(test, section, nameof(IInboundContext.AppendQueryParameter));
    }
}