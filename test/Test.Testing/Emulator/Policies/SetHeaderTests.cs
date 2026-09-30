// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring.Expressions;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Document;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Expressions;

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

    class ConfigurableSetHeader(
        string name,
        string[] values,
        bool useExpressions = false,
        bool includeUnmatched = false) : IDocument
    {
        public void Inbound(IInboundContext context) =>
            HeaderQueryTestHelpers.Apply(context, context.SetHeader, name, values, useExpressions, includeUnmatched);

        public void Backend(IBackendContext context) =>
            HeaderQueryTestHelpers.Apply(context, context.SetHeader, name, values, useExpressions, includeUnmatched);

        public void Outbound(IOutboundContext context) =>
            HeaderQueryTestHelpers.Apply(context, context.SetHeader, name, values, useExpressions, includeUnmatched);

        public void OnError(IOnErrorContext context) =>
            HeaderQueryTestHelpers.Apply(context, context.SetHeader, name, values, useExpressions, includeUnmatched);
    }

    [TestMethod]
    [DataRow("Inbound")]
    [DataRow("Backend")]
    [DataRow("Outbound")]
    [DataRow("OnError")]
    public void SetHeader_ReplacesExistingHeaderIgnoringCase(string section)
    {
        var test = new ConfigurableSetHeader("x-test", ["", "new", "new"]).AsTestDocument();
        HeaderQueryTestHelpers.SeedHeaders(test, section, ["old", "old"]);

        HeaderQueryTestHelpers.Run(test, section);

        var headers = HeaderQueryTestHelpers.Message(test.Context, section).Headers;
        headers["X-TEST"].Should().Equal("", "new", "new");
        headers.Keys.Should().Contain("X-Test");
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
    public void SetHeader_SetsEmptyValuesForMissingOrExistingHeader(string section, bool exists)
    {
        var test = new ConfigurableSetHeader("x-test", []).AsTestDocument();
        HeaderQueryTestHelpers.SeedHeaders(test, section, exists ? ["old", "old"] : null);

        HeaderQueryTestHelpers.Run(test, section);

        var headers = HeaderQueryTestHelpers.Message(test.Context, section).Headers;
        headers.Should().HaveCount(2);
        headers["X-Test"].Should().BeEmpty();
        HeaderQueryTestHelpers.AssertUnrelatedHeaders(test, section);
    }

    [TestMethod]
    [DataRow("Inbound")]
    [DataRow("Backend")]
    [DataRow("Outbound")]
    [DataRow("OnError")]
    public void SetHeader_CreatesMissingHeaderWithRepeatedAndEmptyValues(string section)
    {
        var test = new ConfigurableSetHeader("x-test", ["", "new", "new"]).AsTestDocument();
        HeaderQueryTestHelpers.SeedHeaders(test, section);

        HeaderQueryTestHelpers.Run(test, section);

        var headers = HeaderQueryTestHelpers.Message(test.Context, section).Headers;
        headers.Should().HaveCount(2);
        headers["X-Test"].Should().Equal("", "new", "new");
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
    public void SetHeader_CallbackOverridesDefaultInEverySection(string section, bool withPredicate)
    {
        var test = new ConfigurableSetHeader("x-test", ["new"], includeUnmatched: withPredicate).AsTestDocument();
        HeaderQueryTestHelpers.SeedHeaders(test, section, ["old"]);
        var setup = withPredicate
            ? HeaderQueryTestHelpers.Setup<MockSetHeaderProvider.Setup>(
                test, section, typeof(MockSetHeaderProvider), nameof(MockSetHeaderProvider.SetHeader),
                (_, name, _) => name == "x-test")
            : HeaderQueryTestHelpers.Setup<MockSetHeaderProvider.Setup>(
                test, section, typeof(MockSetHeaderProvider), nameof(MockSetHeaderProvider.SetHeader));
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
    public void SetHeader_UsesExpressionDrivenNameAndValues(string section)
    {
        var test = new ConfigurableSetHeader("unused", [], useExpressions: true).AsTestDocument();
        HeaderQueryTestHelpers.SeedHeaders(test, section, ["old"]);
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
    public void SetHeader_RejectsInvalidNameWithoutMutation(string section, string? name)
    {
        var test = new ConfigurableSetHeader(name!, ["new"]).AsTestDocument();
        HeaderQueryTestHelpers.SeedHeaders(test, section, ["old"]);

        HeaderQueryTestHelpers.AssertInvalid(test, section, nameof(IInboundContext.SetHeader));
    }

    [TestMethod]
    [DataRow("Inbound")]
    [DataRow("Backend")]
    [DataRow("Outbound")]
    [DataRow("OnError")]
    public void SetHeader_RejectsNullValuesWithoutMutation(string section)
    {
        var test = new ConfigurableSetHeader("x-test", null!).AsTestDocument();
        HeaderQueryTestHelpers.SeedHeaders(test, section, ["old"]);

        HeaderQueryTestHelpers.AssertInvalid(test, section, nameof(IInboundContext.SetHeader));
    }

    [TestMethod]
    [DataRow("Inbound")]
    [DataRow("Backend")]
    [DataRow("Outbound")]
    [DataRow("OnError")]
    public void SetHeader_CallbackCanShortCircuitSection(string section)
    {
        var test = new ConfigurableSetHeader("x-test", ["new"], includeUnmatched: true).AsTestDocument();
        HeaderQueryTestHelpers.SeedHeaders(test, section, ["old"]);
        HeaderQueryTestHelpers.Setup<MockSetHeaderProvider.Setup>(
            test, section, typeof(MockSetHeaderProvider), nameof(MockSetHeaderProvider.SetHeader))
            .WithCallback((context, _, _) =>
            {
                context.Response.StatusCode = 409;
                throw new FinishSectionProcessingException();
            });

        HeaderQueryTestHelpers.Run(test, section);

        test.Context.Response.StatusCode.Should().Be(409);
        var headers = HeaderQueryTestHelpers.Message(test.Context, section).Headers;
        headers["X-Test"].Should().Equal("old");
        headers.Should().NotContainKey("x-other");
        HeaderQueryTestHelpers.AssertUnrelatedHeaders(test, section);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void Headers_DefaultStorageExposesCaseInsensitiveMutableAndReadOnlyDictionaries(bool response)
    {
        MockMessage message = response ? new MockResponse() : new MockRequest();
        var headers = message.Headers;
        IDictionary<string, string[]> mutable = headers;
        IReadOnlyDictionary<string, string[]> readOnly = message switch
        {
            MockRequest request => ((IRequest)request).Headers,
            MockResponse result => ((IResponse)result).Headers,
            _ => throw new InvalidOperationException()
        };
        mutable.Add("X-Test", ["old"]);

        headers.Comparer.Should().BeSameAs(StringComparer.OrdinalIgnoreCase);
        headers.ContainsKey("x-test").Should().BeTrue();
        readOnly.TryGetValue("X-TEST", out var existing).Should().BeTrue();
        existing.Should().Equal("old");
        headers.TryAdd("x-test", ["duplicate"]).Should().BeFalse();
        var addDuplicate = () => mutable.Add("x-test", ["duplicate"]);
        addDuplicate.Should().Throw<ArgumentException>();

        mutable["x-test"] = ["new", "new", ""];

        headers.Should().BeSameAs(message.Headers);
        readOnly.Should().BeSameAs(headers);
        headers.Keys.Should().Equal("X-Test");
        readOnly["X-TEST"].Should().Equal("new", "new", "");
        mutable.Remove("x-TEST").Should().BeTrue();
        headers.Should().BeEmpty();
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void Headers_AssignmentPreservesIdentityComparerAndCaseVariantEntries(bool response)
    {
        MockMessage message = response ? new MockResponse() : new MockRequest();
        var keep = new[] { "keep" };
        var source = new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["X-Test"] = ["first", "first"],
            ["x-test"] = ["", "last"],
            ["X-Keep"] = keep
        };

        message.Headers = source;

        message.Headers.Should().BeSameAs(source);
        message.Headers.Comparer.Should().BeSameAs(source.Comparer);
        message.Headers.Should().HaveCount(3);
        message.Headers.Keys.Should().Equal("X-Test", "x-test", "X-Keep");
        message.Headers["X-Test"].Should().Equal("first", "first");
        message.Headers["x-test"].Should().Equal("", "last");
        message.Headers["X-Keep"].Should().BeSameAs(keep);
        message.Headers.ContainsKey("X-TEST").Should().BeFalse();
    }

    [TestMethod]
    [DataRow("Default")]
    [DataRow("Ordinal")]
    [DataRow("OrdinalIgnoreCase")]
    [DataRow("InvariantCultureIgnoreCase")]
    public void Headers_SharedAssignmentsReflectMutationsInBothDirections(string comparerName)
    {
        IEqualityComparer<string>? comparer = comparerName switch
        {
            "Default" => null,
            "Ordinal" => StringComparer.Ordinal,
            "OrdinalIgnoreCase" => StringComparer.OrdinalIgnoreCase,
            "InvariantCultureIgnoreCase" => StringComparer.InvariantCultureIgnoreCase,
            _ => throw new ArgumentOutOfRangeException(nameof(comparerName))
        };
        var source = new Dictionary<string, string[]>(comparer) { ["X-Test"] = ["original"] };
        var originalComparer = source.Comparer;
        var request = new MockRequest { Headers = source };
        var response = new MockResponse { Headers = source };

        request.Headers.Should().BeSameAs(source);
        response.Headers.Should().BeSameAs(source);
        ((IRequest)request).Headers.Should().BeSameAs(source);
        ((IResponse)response).Headers.Should().BeSameAs(source);
        request.Headers.Comparer.Should().BeSameAs(originalComparer);
        response.Headers.Comparer.Should().BeSameAs(originalComparer);
        request.Headers.ContainsKey("x-test").Should().Be(originalComparer.Equals("X-Test", "x-test"));

        var external = new[] { "external" };
        source["X-External"] = external;

        request.Headers["X-External"].Should().BeSameAs(external);
        response.Headers["X-External"].Should().BeSameAs(external);

        external[0] = "updated";
        request.Headers["X-Request"] = ["request"];

        source["X-Request"].Should().Equal("request");
        response.Headers["X-Request"].Should().Equal("request");
        request.Headers["X-External"].Should().Equal("updated");
        response.Headers["X-External"].Should().Equal("updated");

        response.Headers["X-Response"] = ["response"];

        source["X-Response"].Should().Equal("response");
        request.Headers["X-Response"].Should().Equal("response");

        source.Remove("X-External").Should().BeTrue();
        request.Headers.Remove("X-Request").Should().BeTrue();

        request.Headers.Should().NotContainKey("X-External");
        response.Headers.Should().NotContainKeys("X-External", "X-Request");
        source.Should().NotContainKey("X-Request");

        response.Headers.Clear();

        source.Should().BeEmpty();
        request.Headers.Should().BeEmpty();
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void Headers_RetainAssignedCaseInsensitiveDictionaryReference(bool response)
    {
        MockMessage message = response ? new MockResponse() : new MockRequest();
        var source = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["X-Test"] = ["old"]
        };

        message.Headers = source;
        source["x-test"] = ["new"];

        message.Headers.Should().BeSameAs(source);
        message.Headers["X-TEST"].Should().Equal("new");
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void Headers_RejectNullAssignmentWithoutReplacingDictionary(bool response)
    {
        MockMessage message = response ? new MockResponse() : new MockRequest();
        message.Headers["X-Test"] = ["old"];
        var headers = message.Headers;

        var assign = () => message.Headers = null!;

        assign.Should().Throw<ArgumentNullException>();
        message.Headers.Should().BeSameAs(headers);
        message.Headers["X-Test"].Should().Equal("old");
    }

    [TestMethod]
    public void Headers_DoNotShareStorageBetweenMessages()
    {
        var request = new MockRequest();
        var response = new MockResponse();

        request.Headers["X-Test"] = ["request"];

        response.Headers.Should().BeEmpty();
        response.Headers.Should().NotBeSameAs(request.Headers);
    }

    class CheckStoredHeader : IDocument
    {
        public void Inbound(IInboundContext context)
        {
            context.CheckHeader(new CheckHeaderConfig
            {
                Name = "Test",
                Values = [],
                IgnoreCase = false,
                FailCheckHttpCode = 400,
                FailCheckErrorMessage = "Header check failed"
            });
            context.SetHeader("X-After-Check", "continued");
        }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void Headers_CheckHeaderPreservesDefaultLookupAndInjectedCaseVariantResult(bool injectedCaseVariants)
    {
        var test = new CheckStoredHeader().AsTestDocument();
        var source = test.Context.Request.Headers;
        if (injectedCaseVariants)
        {
            source = new Dictionary<string, string[]>
            {
                ["Test"] = ["upper"],
                ["test"] = ["lower"]
            };
            test.Context.Request.Headers = source;
        }
        else
        {
            source["test"] = ["lower"];
        }
        var comparer = source.Comparer;
        test.Context.Response.Headers["X-Preserved"] = ["response"];

        test.RunInbound();

        test.Context.Response.StatusCode.Should().Be(200);
        test.Context.Response.Headers.Should().HaveCount(1);
        test.Context.Response.Headers["X-Preserved"].Should().Equal("response");
        test.Context.Request.Headers.Should().BeSameAs(source);
        test.Context.Request.Headers.Comparer.Should().BeSameAs(comparer);
        source["X-After-Check"].Should().Equal("continued");
        source["test"].Should().Equal("lower");
        if (injectedCaseVariants)
        {
            source["Test"].Should().Equal("upper");
            source.Keys.Should().Equal("Test", "test", "X-After-Check");
        }
        else
        {
            source["Test"].Should().Equal("lower");
            source.Keys.Should().Equal("test", "X-After-Check");
        }
    }
}

internal static class HeaderQueryTestHelpers
{
    public static void Run(TestDocument test, string section)
    {
        switch (section)
        {
            case "Inbound":
                test.RunInbound();
                break;
            case "Backend":
                test.RunBackend();
                break;
            case "Outbound":
                test.RunOutbound();
                break;
            case "OnError":
                test.RunOnError();
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(section));
        }
    }

    public static MockMessage Message(GatewayContext context, string section) => section switch
    {
        "Inbound" or "Backend" => context.Request,
        "Outbound" or "OnError" => context.Response,
        _ => throw new ArgumentOutOfRangeException(nameof(section))
    };

    public static MockMessage OtherMessage(GatewayContext context, string section) => section switch
    {
        "Inbound" or "Backend" => context.Response,
        "Outbound" or "OnError" => context.Request,
        _ => throw new ArgumentOutOfRangeException(nameof(section))
    };

    public static void SeedHeaders(TestDocument test, string section, string[]? existing = null)
    {
        var headers = Message(test.Context, section).Headers;
        headers["X-Keep"] = ["keep"];
        if (existing is not null)
        {
            headers["X-Test"] = existing;
        }
        OtherMessage(test.Context, section).Headers["X-Test"] = ["untouched"];
    }

    public static void AssertUnrelatedHeaders(TestDocument test, string section)
    {
        Message(test.Context, section).Headers["X-Keep"].Should().Equal("keep");
        OtherMessage(test.Context, section).Headers["X-Test"].Should().Equal("untouched");
    }

    public static void SeedQuery(TestDocument test, string[]? existing = null)
    {
        test.Context.Request = new MockRequest(new Uri("https://contoso.example/path?keep=a%26b"));
        if (existing is not null)
        {
            test.Context.Request.Url.Query["tag"] = existing;
        }
        test.Context.Request.Headers["X-Keep"] = ["request"];
        test.Context.Response.Headers["X-Keep"] = ["response"];
    }

    public static void AssertUnrelatedQueryState(TestDocument test)
    {
        test.Context.Request.Url.Query["keep"].Should().Equal("a&b");
        test.Context.Request.OriginalUrl.Query.Keys.Should().Equal("keep");
        test.Context.Request.OriginalUrl.Query["keep"].Should().Equal("a&b");
        test.Context.Request.Headers["X-Keep"].Should().Equal("request");
        test.Context.Response.Headers["X-Keep"].Should().Equal("response");
    }

    public static void Apply(
        IHaveExpressionContext context,
        Action<string, string[]> policy,
        string name,
        string[] values,
        bool useExpressions,
        bool includeUnmatched)
    {
        if (useExpressions)
        {
            name = context.ExpressionContext.Request.Headers["x-policy-name"][0];
            values = context.ExpressionContext.Request.Headers["x-policy-values"];
        }
        policy(name, values);
        if (includeUnmatched)
        {
            policy("x-other", ["default"]);
        }
    }

    public static void Apply(
        IHaveExpressionContext context,
        Action<string> policy,
        string name,
        bool useExpressions,
        bool includeUnmatched)
    {
        if (useExpressions)
        {
            name = context.ExpressionContext.Request.Headers["x-policy-name"][0];
        }
        policy(name);
        if (includeUnmatched)
        {
            policy("x-other");
        }
    }

    public static TSetup Setup<TSetup>(
        TestDocument test, string section, Type provider, string policy) =>
        InvokeSetup<TSetup>(test, section, provider, policy, null);

    public static TSetup Setup<TSetup>(
        TestDocument test, string section, Type provider, string policy,
        Func<GatewayContext, string, string[], bool> predicate) =>
        InvokeSetup<TSetup>(test, section, provider, policy, predicate);

    public static TSetup Setup<TSetup>(
        TestDocument test, string section, Type provider, string policy,
        Func<GatewayContext, string, bool> predicate) =>
        InvokeSetup<TSetup>(test, section, provider, policy, predicate);

    private static TSetup InvokeSetup<TSetup>(
        TestDocument test, string section, Type provider, string policy, Delegate? predicate)
    {
        object mock = section switch
        {
            "Inbound" => test.SetupInbound(),
            "Backend" => test.SetupBackend(),
            "Outbound" => test.SetupOutbound(),
            "OnError" => test.SetupOnError(),
            _ => throw new ArgumentOutOfRangeException(nameof(section))
        };
        var types = predicate is null
            ? new[] { mock.GetType() }
            : new[] { mock.GetType(), predicate.GetType() };
        // Missing public overloads must fail at runtime in the test-first phase.
        var method = provider.GetMethod(policy, types);
        method.Should().NotBeNull($"{policy} must expose public setup for {section}");
        var result = method!.Invoke(null, predicate is null ? [mock] : [mock, predicate]);
        return result is TSetup setup ? setup : throw new InvalidOperationException("Unexpected mock setup type.");
    }

    public static void AssertInvalid(TestDocument test, string section, string policy)
    {
        var requestHeaders = Snapshot(test.Context.Request.Headers);
        var responseHeaders = Snapshot(test.Context.Response.Headers);
        var query = Snapshot(test.Context.Request.Url.Query);

        var run = () => Run(test, section);

        var exception = run.Should().Throw<PolicyException>().Which;
        exception.Policy.Should().Be(policy);
        exception.Section.Should().Be($"I{section}Context");
        exception.InnerException.Should().BeAssignableTo<ArgumentException>();
        test.Context.Request.Headers.Should().BeEquivalentTo(requestHeaders, options => options.WithStrictOrdering());
        test.Context.Response.Headers.Should().BeEquivalentTo(responseHeaders, options => options.WithStrictOrdering());
        test.Context.Request.Url.Query.Should().BeEquivalentTo(query, options => options.WithStrictOrdering());
    }

    private static Dictionary<string, string[]> Snapshot(Dictionary<string, string[]> values) =>
        values.ToDictionary(pair => pair.Key, pair => pair.Value.ToArray());
}