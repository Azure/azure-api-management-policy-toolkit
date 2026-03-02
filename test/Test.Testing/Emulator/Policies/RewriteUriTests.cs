// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Document;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator;

namespace Test.Emulator.Emulator.Policies;

[TestClass]
public class RewriteUriTests
{
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
}