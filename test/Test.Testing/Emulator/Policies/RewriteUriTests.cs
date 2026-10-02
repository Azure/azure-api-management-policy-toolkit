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
public class RewriteUriTests
{
    private class RewriteDocument(string template, bool copyUnmatchedParams = true) : IDocument
    {
        public void Inbound(IInboundContext context) => context.RewriteUri(template, copyUnmatchedParams);
    }

    private class ExpressionRewriteDocument : IDocument
    {
        public void Inbound(IInboundContext context) =>
            context.RewriteUri(Template(context.ExpressionContext), Copy(context.ExpressionContext));

        [Expression]
        private static string Template(IExpressionContext context) => (string)context.Variables["template"];

        [Expression]
        private static bool Copy(IExpressionContext context) => (bool)context.Variables["copy"];
    }

    private class RewriteFragment : IFragment
    {
        public void Fragment(IFragmentContext context) => context.RewriteUri("/not-allowed");
    }

    private class RewriteFragmentDocument : IDocument
    {
        public void Backend(IBackendContext context) => context.IncludeFragment("uri");
        public void OnError(IOnErrorContext context) => context.IncludeFragment("uri");
    }

    class SimpleRewriteUri : IDocument
    {
        public void Inbound(IInboundContext context)
        {
            context.RewriteUri("/api/v2/resource");
        }
    }

    class RewriteUriWithPlaceholders : IDocument
    {
        public void Inbound(IInboundContext context)
        {
            context.RewriteUri("/api/{version}/users/{id}");
        }
    }

    class RewriteUriWithQueryParams : IDocument
    {
        public void Inbound(IInboundContext context)
        {
            context.RewriteUri("/api/resource?filter=active&sort=name");
        }
    }

    class RewriteUriNoCopyParams : IDocument
    {
        public void Inbound(IInboundContext context)
        {
            context.RewriteUri("/api/resource?newparam=value", false);
        }
    }

    class RewriteUriCopyParamsExplicit : IDocument
    {
        public void Inbound(IInboundContext context)
        {
            context.RewriteUri("/api/resource?b=override", true);
        }
    }

    class RewriteUriWithPlaceholdersInQuery : IDocument
    {
        public void Inbound(IInboundContext context)
        {
            context.RewriteUri("/test?param={p1}&{p2}=t");
        }
    }

    [TestMethod]
    public void RewriteUri_SimplePath_ShouldUpdatePath()
    {
        var test = new TestDocument(new SimpleRewriteUri())
        {
            Context = { Request = { Url = { Path = "/original/path" } } }
        };

        test.RunInbound();

        test.Context.Request.Url.Path.Should().Be("/api/v2/resource");
    }

    [TestMethod]
    public void RewriteUri_WithMatchedParameters_ShouldResolvePlaceholders()
    {
        var test = new TestDocument(new RewriteUriWithPlaceholders())
        {
            Context =
            {
                Request =
                {
                    Url = { Path = "/original" },
                    MatchedParameters =
                        new Dictionary<string, string> { { "version", "v2" }, { "id", "123" } }
                }
            }
        };

        test.RunInbound();

        test.Context.Request.Url.Path.Should().Be("/api/v2/users/123");
    }

    [TestMethod]
    public void RewriteUri_MissingMatchedParameter_ShouldThrowArgumentException()
    {
        var test = new TestDocument(new RewriteUriWithPlaceholders())
        {
            Context =
            {
                Request =
                {
                    Url = { Path = "/original" },
                    MatchedParameters = new Dictionary<string, string> { { "version", "v2" } }
                }
            }
        };

        var act = () => test.RunInbound();

        var ex = act.Should().Throw<PolicyException>().Which;
        ex.Policy.Should().Be("RewriteUri");
        ex.InnerException.Should().BeOfType<ArgumentException>()
            .Which.Message.Should().Contain("Template placeholder 'id' not found in MatchedParameters");
    }

    [TestMethod]
    public void RewriteUri_CopyUnmatchedParamsTrue_ShouldMergeWithTemplateOverride()
    {
        var test = new TestDocument(new RewriteUriCopyParamsExplicit())
        {
            Context = { Request = { Url = { Path = "/original", Query = { { "a", ["1"] }, { "b", ["2"] } } } } }
        };

        test.RunInbound();

        test.Context.Request.Url.Path.Should().Be("/api/resource");
        test.Context.Request.Url.Query.Should().ContainKey("a").WhoseValue.Should().ContainInOrder("1");
        test.Context.Request.Url.Query.Should().ContainKey("b").WhoseValue.Should().ContainInOrder("override");
    }

    [TestMethod]
    public void RewriteUri_CopyUnmatchedParamsFalse_ShouldOnlyUseTemplateParams()
    {
        var test = new TestDocument(new RewriteUriNoCopyParams())
        {
            Context = { Request = { Url = { Path = "/original", Query = { { "existing", ["value"] } } } } }
        };

        test.RunInbound();

        test.Context.Request.Url.Path.Should().Be("/api/resource");
        test.Context.Request.Url.Query.Should().NotContainKey("existing");
        test.Context.Request.Url.Query.Should().ContainKey("newparam").WhoseValue.Should().ContainInOrder("value");
    }

    [TestMethod]
    public void RewriteUri_DefaultCopyParams_ShouldPreserveOriginalParams()
    {
        var test = new TestDocument(new SimpleRewriteUri())
        {
            Context = { Request = { Url = { Path = "/original", Query = { { "keep", ["this"] } } } } }
        };

        test.RunInbound();

        test.Context.Request.Url.Path.Should().Be("/api/v2/resource");
        test.Context.Request.Url.Query.Should().ContainKey("keep").WhoseValue.Should().ContainInOrder("this");
    }

    [TestMethod]
    public void RewriteUri_WithQueryParamsInTemplate_ShouldParseTemplateParams()
    {
        var test = new TestDocument(new RewriteUriWithQueryParams())
        {
            Context = { Request = { Url = { Path = "/original" } } }
        };

        test.RunInbound();

        test.Context.Request.Url.Path.Should().Be("/api/resource");
        test.Context.Request.Url.Query.Should().ContainKey("filter").WhoseValue.Should().ContainInOrder("active");
        test.Context.Request.Url.Query.Should().ContainKey("sort").WhoseValue.Should().ContainInOrder("name");
    }

    [TestMethod]
    public void RewriteUri_WithCallback_ShouldExecuteCallback()
    {
        var test = new TestDocument(new SimpleRewriteUri())
        {
            Context = { Request = { Url = { Path = "/original" } } }
        };
        var callbackExecuted = false;
        test.SetupInbound().RewriteUri().WithCallback((context, template, copyUnmatchedParams) =>
        {
            callbackExecuted = true;
            context.Request.Url.Path = "/callback/override";
        });

        test.RunInbound();

        callbackExecuted.Should().BeTrue();
        test.Context.Request.Url.Path.Should().Be("/callback/override");
    }

    [TestMethod]
    public void RewriteUri_WithPredicateCallback_ShouldExecuteOnlyWhenMatched()
    {
        var test = new TestDocument(new SimpleRewriteUri())
        {
            Context = { Request = { Url = { Path = "/original" } } }
        };
        var callbackExecuted = false;
        test.SetupInbound()
            .RewriteUri((_, template, _) => template.Contains("nonexistent"))
            .WithCallback((context, template, copyUnmatchedParams) =>
            {
                callbackExecuted = true;
            });

        test.RunInbound();

        callbackExecuted.Should().BeFalse();
        test.Context.Request.Url.Path.Should().Be("/api/v2/resource");
    }

    [TestMethod]
    public void RewriteUri_WithPlaceholdersInQueryString_ShouldResolveBothKeyAndValue()
    {
        var test = new TestDocument(new RewriteUriWithPlaceholdersInQuery())
        {
            Context =
            {
                Request =
                {
                    Url = { Path = "/original" },
                    MatchedParameters =
                        new Dictionary<string, string> { { "p1", "resolvedValue" }, { "p2", "dynamicKey" } }
                }
            }
        };

        test.RunInbound();

        test.Context.Request.Url.Path.Should().Be("/test");
        test.Context.Request.Url.Query.Should().ContainKey("param").WhoseValue.Should().ContainInOrder("resolvedValue");
        test.Context.Request.Url.Query.Should().ContainKey("dynamicKey").WhoseValue.Should().ContainInOrder("t");
    }

    [TestMethod]
    public void ShouldPreserveEscapedPathsAndDecodeAndReencodeRepeatedQueryValuesExactlyOnce()
    {
        var test = CreateTest("/api/%E9%9B%AA%20file?tag=first&tag=second&plus=%2B&space=a+b&symbol=%26%3D%23&empty=", false);

        AssertOnlyUrlMutation(test, test.RunInbound);

        test.Context.Request.Url.Path.Should().Be("/api/%E9%9B%AA%20file");
        test.Context.Request.Url.Query.Should().HaveCount(5);
        test.Context.Request.Url.Query["tag"].Should().Equal("first", "second");
        test.Context.Request.Url.Query["plus"].Should().Equal("+");
        test.Context.Request.Url.Query["space"].Should().Equal("a b");
        test.Context.Request.Url.Query["symbol"].Should().Equal("&=#");
        test.Context.Request.Url.Query["empty"].Should().Equal("");
        test.Context.Request.Url.QueryString.Should().Be(
            "?tag=first&tag=second&plus=%2B&space=a%20b&symbol=%26%3D%23&empty=");
        test.Context.Request.Url.ToUri().AbsoluteUri.Should().Be(
            "https://gateway.example:9443/api/%E9%9B%AA%20file?tag=first&tag=second&plus=%2B&space=a%20b&symbol=%26%3D%23&empty=");
    }

    [TestMethod]
    public void ShouldEscapePlaceholderValuesWithoutInjectingPathOrQueryComponents()
    {
        var test = CreateTest("/api/{id}?{key}={value}&dup={value}&dup={id}", false);
        var id = "caf\u00E9 /?&#%";
        var key = "x&admin=true";
        var value = "+ \u96EA&extra=yes/#?%";
        test.Context.Request.MatchedParameters = new Dictionary<string, string>
        {
            ["id"] = id,
            ["key"] = key,
            ["value"] = value,
        };
        var parameters = test.Context.Request.MatchedParameters.ToDictionary(parameter => parameter.Key, parameter => parameter.Value);

        AssertOnlyUrlMutation(test, test.RunInbound);

        test.Context.Request.Url.Path.Should().Be($"/api/{Uri.EscapeDataString(id)}");
        test.Context.Request.Url.Query.Should().HaveCount(2);
        test.Context.Request.Url.Query[key].Should().Equal(value);
        test.Context.Request.Url.Query["dup"].Should().Equal(value, id);
        test.Context.Request.Url.QueryString.Should().Be(
            $"?{Uri.EscapeDataString(key)}={Uri.EscapeDataString(value)}&dup={Uri.EscapeDataString(value)}&dup={Uri.EscapeDataString(id)}");
        test.Context.Request.MatchedParameters.Should().BeEquivalentTo(parameters);
    }

    [TestMethod]
    [DataRow("/", "%2F")]
    [DataRow("%2F", "%252F")]
    [DataRow("", "")]
    [DataRow("{literal}", "%7Bliteral%7D")]
    public void ShouldTreatMatchedParametersAsValuesRatherThanTemplateSyntax(string value, string escaped)
    {
        var test = CreateTest("/api/{id}", false);
        test.Context.Request.MatchedParameters["id"] = value;

        test.RunInbound();

        test.Context.Request.Url.Path.Should().Be($"/api/{escaped}");
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void ShouldExcludeQueryParametersMatchedByTheOriginalOperationTemplate(bool copy)
    {
        var test = CreateTest("/put?new=value", copy);
        test.Context.Operation.UrlTemplate = "/get?a={matchedValue}&matched={other}";
        test.Context.Request.Url.Query = new Dictionary<string, string[]>
        {
            ["a"] = ["one", "two"],
            ["matched"] = ["consumed"],
            ["keep"] = ["first", "second"],
        };

        AssertOnlyUrlMutation(test, test.RunInbound);

        test.Context.Request.Url.Path.Should().Be("/put");
        test.Context.Request.Url.Query.Should().HaveCount(copy ? 2 : 1);
        test.Context.Request.Url.Query["new"].Should().Equal("value");
        test.Context.Request.Url.Query.Should().NotContainKey("a").And.NotContainKey("matched");
        if (copy)
        {
            test.Context.Request.Url.Query["keep"].Should().Equal("first", "second");
            test.Context.Request.Url.QueryString.Should().Be("?new=value&keep=first&keep=second");
        }
        else
        {
            test.Context.Request.Url.QueryString.Should().Be("?new=value");
        }
    }

    [TestMethod]
    public void ShouldKeepTemplateOverridesAndCopyEveryUnmatchedRepeatedValue()
    {
        var test = CreateTest("/resource?a=new&a=again&added=%E9%9B%AA");
        test.Context.Request.Url.Query["keep"] = ["one & two", "three+four"];

        test.RunInbound();

        test.Context.Request.Url.Query.Should().HaveCount(3);
        test.Context.Request.Url.Query["a"].Should().Equal("new", "again");
        test.Context.Request.Url.Query["added"].Should().Equal("\u96EA");
        test.Context.Request.Url.Query["keep"].Should().Equal("one & two", "three+four");
        test.Context.Request.Url.QueryString.Should().Be(
            "?a=new&a=again&added=%E9%9B%AA&keep=one%20%26%20two&keep=three%2Bfour");
    }

    [TestMethod]
    public void ShouldPreserveNamedFlagsEmptyValuesAndQueryKeyCasing()
    {
        var test = CreateTest("/resource?flag&&empty=&Name=one&name=two&tag=1&tag=2&", false);

        test.RunInbound();

        test.Context.Request.Url.Query.Should().HaveCount(5);
        test.Context.Request.Url.Query["flag"].Should().Equal("");
        test.Context.Request.Url.Query["empty"].Should().Equal("");
        test.Context.Request.Url.Query["Name"].Should().Equal("one");
        test.Context.Request.Url.Query["name"].Should().Equal("two");
        test.Context.Request.Url.Query["tag"].Should().Equal("1", "2");
        test.Context.Request.Url.QueryString.Should().Be("?flag=&empty=&Name=one&name=two&tag=1&tag=2");
    }

    [TestMethod]
    public void ShouldClearTheQueryWhenTheTemplateHasAnEmptyQueryAndCopyIsDisabled()
    {
        var test = CreateTest("/resource?", false);

        test.RunInbound();

        test.Context.Request.Url.Path.Should().Be("/resource");
        test.Context.Request.Url.Query.Should().BeEmpty();
        test.Context.Request.Url.QueryString.Should().BeEmpty();
    }

    [TestMethod]
    public void ShouldUseTheEvaluatedTemplateAndCopyFlag()
    {
        var test = CreateTest(new ExpressionRewriteDocument());
        test.Context.Variables["template"] = "/expression/%2F?value=%2B&value=two";
        test.Context.Variables["copy"] = false;

        AssertOnlyUrlMutation(test, test.RunInbound);

        test.Context.Request.Url.Path.Should().Be("/expression/%2F");
        test.Context.Request.Url.Query.Should().ContainSingle();
        test.Context.Request.Url.Query["value"].Should().Equal("+", "two");
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow(" ")]
    [DataRow("resource/path")]
    [DataRow("//elsewhere.example/path")]
    [DataRow("https://elsewhere.example/path")]
    [DataRow("/resource#fragment")]
    [DataRow("/bad\\path")]
    [DataRow("/bad\npath")]
    [DataRow("/bad%")]
    [DataRow("/bad%2")]
    [DataRow("/bad%GG")]
    [DataRow("/path?name=%GG")]
    [DataRow("/path?=nameless")]
    [DataRow("/path/{}")]
    [DataRow("/path/{id")]
    [DataRow("/path/id}")]
    public void ShouldRejectInvalidTemplatesBeforeMutatingTheUrlOrEitherMessage(string? template)
    {
        AssertFailure(CreateTest(template!)).InnerException.Should().BeAssignableTo<ArgumentException>();
    }

    [TestMethod]
    public void ShouldRejectAMissingPlaceholderWithoutPartiallyUpdatingTheUrl()
    {
        var test = CreateTest("/changed/{known}/{missing}?x=value");
        test.Context.Request.MatchedParameters["known"] = "resolved";

        AssertFailure(test).InnerException.Should().BeOfType<ArgumentException>()
            .Which.Message.Should().Contain("missing");
    }

    [TestMethod]
    public void ShouldRejectANullMatchedValueWithoutPartiallyUpdatingTheUrl()
    {
        var test = CreateTest("/changed/{id}");
        test.Context.Request.MatchedParameters["id"] = null!;

        AssertFailure(test).InnerException.Should().BeAssignableTo<ArgumentException>();
    }

    [TestMethod]
    public void ShouldAllowCallbacksToOverrideUnsupportedTemplateInputs()
    {
        var test = CreateTest("https://not-used.example/path", false);
        var originalQuery = test.Context.Request.Url.Query.ToDictionary(parameter => parameter.Key, parameter => parameter.Value.ToArray());
        var calls = 0;
        test.SetupInbound().RewriteUri((_, template, copy) => template == "https://not-used.example/path" && !copy)
            .WithCallback((context, _, _) =>
            {
                calls++;
                context.Request.Url.Path = "/callback";
            });
        test.SetupInbound().RewriteUri().WithCallback((_, _, _) => calls += 100);

        AssertOnlyUrlMutation(test, test.RunInbound);

        calls.Should().Be(1);
        test.Context.Request.Url.Path.Should().Be("/callback");
        test.Context.Request.Url.Query.Should().BeEquivalentTo(originalQuery, options => options.WithStrictOrdering());
    }

    [TestMethod]
    public void ShouldReportCallbackFailuresWithoutExecutingDefaultRewriting()
    {
        var test = CreateTest("/not-used");
        test.SetupInbound().RewriteUri().WithCallback((_, _, _) => throw new InvalidOperationException("callback failure"));

        AssertFailure(test).InnerException.Should().BeOfType<InvalidOperationException>()
            .Which.Message.Should().Be("callback failure");
    }

    [TestMethod]
    [DataRow(nameof(IBackendContext))]
    [DataRow(nameof(IOnErrorContext))]
    public void ShouldKeepInboundOnlyRewritingUnavailableInBackendAndOnErrorFragments(string section)
    {
        var test = CreateTest(new RewriteFragmentDocument()).RegisterFragment("uri", new RewriteFragment());
        Action run = section == nameof(IBackendContext) ? test.RunBackend : test.RunOnError;

        AssertFailure(test, run, section).InnerException.Should().BeOfType<NotImplementedException>();
    }

    private static TestDocument CreateTest(string template, bool copy = true) =>
        CreateTest(new RewriteDocument(template, copy));

    private static TestDocument CreateTest(IDocument document)
    {
        var test = document.AsTestDocument();
        var original = new Uri("https://gateway.example:9443/public/get?a=one&a=two&keep=value");
        test.Context.Request.Url = new MockUrl(original);
        test.Context.Request.OriginalUrl = new MockUrl(original);
        test.Context.Operation.UrlTemplate = "/get";
        test.Context.Request.Body.Content = "request body";
        test.Context.Response.Body.Content = "response body";
        test.Context.Request.Headers["content-type"] = ["text/plain"];
        test.Context.Request.Headers["content-length"] = ["12"];
        test.Context.Response.Headers["content-type"] = ["text/html"];
        test.Context.Response.Headers["content-length"] = ["13"];
        test.Context.Response.StatusCode = 202;
        return test;
    }

    private static void AssertOnlyUrlMutation(TestDocument test, Action run)
    {
        var requestBody = test.Context.Request.Body.Content;
        var responseBody = test.Context.Response.Body.Content;
        var requestHeaders = test.Context.Request.Headers.ToDictionary(header => header.Key, header => header.Value.ToArray());
        var responseHeaders = test.Context.Response.Headers.ToDictionary(header => header.Key, header => header.Value.ToArray());
        var originalUrl = test.Context.Request.OriginalUrl.ToString();
        var scheme = test.Context.Request.Url.Scheme;
        var host = test.Context.Request.Url.Host;
        var port = test.Context.Request.Url.Port;

        run();

        test.Context.Request.Body.Content.Should().Be(requestBody);
        test.Context.Response.Body.Content.Should().Be(responseBody);
        test.Context.Request.Headers.Should().BeEquivalentTo(requestHeaders, options => options.WithStrictOrdering());
        test.Context.Response.Headers.Should().BeEquivalentTo(responseHeaders, options => options.WithStrictOrdering());
        test.Context.Request.OriginalUrl.ToString().Should().Be(originalUrl);
        test.Context.Request.Url.Scheme.Should().Be(scheme);
        test.Context.Request.Url.Host.Should().Be(host);
        test.Context.Request.Url.Port.Should().Be(port);
        test.Context.Response.StatusCode.Should().Be(202);
    }

    private static PolicyException AssertFailure(
        TestDocument test, Action? run = null, string section = nameof(IInboundContext))
    {
        var path = test.Context.Request.Url.Path;
        var query = test.Context.Request.Url.Query.ToDictionary(parameter => parameter.Key, parameter => parameter.Value.ToArray());
        PolicyException? exception = null;

        AssertOnlyUrlMutation(test, () =>
        {
            var action = run ?? test.RunInbound;
            exception = action.Should().Throw<PolicyException>().Which;
        });

        exception.Should().NotBeNull();
        exception!.Policy.Should().Be("RewriteUri");
        exception.Section.Should().Be(section);
        test.Context.Request.Url.Path.Should().Be(path);
        test.Context.Request.Url.Query.Should().BeEquivalentTo(query, options => options.WithStrictOrdering());
        return exception;
    }
}