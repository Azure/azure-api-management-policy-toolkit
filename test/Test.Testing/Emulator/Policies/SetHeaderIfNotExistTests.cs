// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Document;

namespace Test.Emulator.Emulator.Policies;

[TestClass]
public class SetHeaderIfNotExistTests
{
    class ConfigurableSetHeaderIfNotExist(
        string name,
        string[] values,
        bool useExpressions = false,
        bool includeUnmatched = false) : IDocument
    {
        public void Inbound(IInboundContext context) =>
            HeaderQueryTestHelpers.Apply(context, context.SetHeaderIfNotExist, name, values, useExpressions, includeUnmatched);

        public void Backend(IBackendContext context) =>
            HeaderQueryTestHelpers.Apply(context, context.SetHeaderIfNotExist, name, values, useExpressions, includeUnmatched);

        public void Outbound(IOutboundContext context) =>
            HeaderQueryTestHelpers.Apply(context, context.SetHeaderIfNotExist, name, values, useExpressions, includeUnmatched);

        public void OnError(IOnErrorContext context) =>
            HeaderQueryTestHelpers.Apply(context, context.SetHeaderIfNotExist, name, values, useExpressions, includeUnmatched);
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
    public void SetHeaderIfNotExist_SkipsExistingHeadersIgnoringCaseEvenWhenEmpty(string section, string existing)
    {
        var test = new ConfigurableSetHeaderIfNotExist("x-test", ["", "new", "new"]).AsTestDocument();
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
        headers["X-TEST"].Should().Equal(previous ?? ["", "new", "new"]);
        if (previous is not null)
        {
            headers["x-test"].Should().BeSameAs(previous);
        }
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
    public void SetHeaderIfNotExist_HandlesEmptyIncomingValues(string section, bool exists)
    {
        var test = new ConfigurableSetHeaderIfNotExist("x-test", []).AsTestDocument();
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
    public void SetHeaderIfNotExist_CallbackOverridesDefaultInEverySection(string section, bool withPredicate)
    {
        var test = new ConfigurableSetHeaderIfNotExist("x-test", ["new"], includeUnmatched: withPredicate).AsTestDocument();
        HeaderQueryTestHelpers.SeedHeaders(test, section, ["old"]);
        var setup = withPredicate
            ? HeaderQueryTestHelpers.Setup<MockSetHeaderIfNotExistProvider.Setup>(
                test, section, typeof(MockSetHeaderIfNotExistProvider), nameof(MockSetHeaderIfNotExistProvider.SetHeaderIfNotExist),
                (_, name, _) => name == "x-test")
            : HeaderQueryTestHelpers.Setup<MockSetHeaderIfNotExistProvider.Setup>(
                test, section, typeof(MockSetHeaderIfNotExistProvider), nameof(MockSetHeaderIfNotExistProvider.SetHeaderIfNotExist));
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
    public void SetHeaderIfNotExist_UsesExpressionDrivenNameAndValues(string section)
    {
        var test = new ConfigurableSetHeaderIfNotExist("unused", [], useExpressions: true).AsTestDocument();
        HeaderQueryTestHelpers.SeedHeaders(test, section);
        test.Context.Request.Headers["X-Policy-Name"] = ["x-test"];
        test.Context.Request.Headers["X-Policy-Values"] = ["", "expression", "expression"];

        HeaderQueryTestHelpers.Run(test, section);

        HeaderQueryTestHelpers.Message(test.Context, section).Headers["X-TEST"]
            .Should().Equal("", "expression", "expression");
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
    public void SetHeaderIfNotExist_RejectsInvalidNameWithoutMutation(string section, string? name)
    {
        var test = new ConfigurableSetHeaderIfNotExist(name!, ["new"]).AsTestDocument();
        HeaderQueryTestHelpers.SeedHeaders(test, section, ["old"]);

        HeaderQueryTestHelpers.AssertInvalid(test, section, nameof(IInboundContext.SetHeaderIfNotExist));
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
    public void SetHeaderIfNotExist_RejectsNullValuesWithoutMutationEvenWhenSkipping(string section, bool exists)
    {
        var test = new ConfigurableSetHeaderIfNotExist("x-test", null!).AsTestDocument();
        HeaderQueryTestHelpers.SeedHeaders(test, section, exists ? ["old"] : null);

        HeaderQueryTestHelpers.AssertInvalid(test, section, nameof(IInboundContext.SetHeaderIfNotExist));
    }
}
