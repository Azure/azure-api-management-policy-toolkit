// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Reflection;

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.IoC;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling;

[TestClass]
public class WaitTests
{
    [TestMethod]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context)
            {
                context.Wait(() =>
                    {
                        context.SendRequest(new SendRequestConfig {
                            ResponseVariableName = "variable"
                        });
                    });
            }
            public void Backend(IBackendContext context)
            {
                context.Wait(() =>
                    {
                        context.SendRequest(new SendRequestConfig {
                            ResponseVariableName = "variable"
                        });
                    });
            }
            public void Outbound(IOutboundContext context)
            {
                context.Wait(() =>
                    {
                        context.SendRequest(new SendRequestConfig {
                            ResponseVariableName = "variable"
                        });
                    });
            }
            public void OnError(OnErrorContext context)
            {
                context.Wait(() =>
                    {
                        context.SendRequest(new SendRequestConfig {
                            ResponseVariableName = "variable"
                        });
                    });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <wait>
                    <send-request response-variable-name="variable" />
                </wait>
            </inbound>
            <backend>
                <wait>
                    <send-request response-variable-name="variable" />
                </wait>
            </backend>
            <outbound>
                <wait>
                    <send-request response-variable-name="variable" />
                </wait>
            </outbound>
            <on-error>
                <wait>
                    <send-request response-variable-name="variable" />
                </wait>
            </on-error>
        </policies>
        """,
        DisplayName = "Should compile wait policy in sections"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context)
            {
                context.Wait(() =>
                    {
                        context.SendRequest(new SendRequestConfig {
                            ResponseVariableName = "variable"
                        });
                    },
                    "any");
            }
        }
        """,
        """
        <policies>
            <inbound>
                <wait for="any">
                    <send-request response-variable-name="variable" />
                </wait>
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile wait policy with for attribute"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context)
            {
                context.Wait(() =>
                    {
                        context.SendRequest(new SendRequestConfig {
                            ResponseVariableName = "variable"
                        });
                    },
                    GetForAtt(context.ExpressionContext));
            }
            string GetForAtt(IExpressionContext context) => context.Variables.ContainsKey("any") ? "any" : "all";
        }
        """,
        """
        <policies>
            <inbound>
                <wait for="@(context.Variables.ContainsKey("any") ? "any" : "all")">
                    <send-request response-variable-name="variable" />
                </wait>
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile wait policy with expression in for attribute"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context)
            {
                context.Wait(() =>
                    {
                        if(CacheCondition(context.ExpressionContext))
                        {
                            context.CacheLookupValue(new CacheLookupValueConfig {
                                Key = "key",
                                VariableName = "cache"
                            });
                        }
                        if(RequestCondition(context.ExpressionContext))
                        {
                            context.SendRequest(new SendRequestConfig {
                                ResponseVariableName = "request"
                            });
                        }
                    });
            }
            bool CacheCondition(IExpressionContext context) => !context.Variables.ContainsKey("cache");
            bool RequestCondition(IExpressionContext context) => !context.Variables.ContainsKey("request");
        }
        """,
        """
        <policies>
            <inbound>
                <wait>
                    <choose>
                        <when condition="@(!context.Variables.ContainsKey("cache"))">
                            <cache-lookup-value key="key" variable-name="cache" />
                        </when>
                    </choose>
                    <choose>
                        <when condition="@(!context.Variables.ContainsKey("request"))">
                            <send-request response-variable-name="request" />
                        </when>
                    </choose>
                </wait>
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile wait policy with send-request, cache-lookup-value and choose policies"
    )]
    public void ShouldCompileWaitPolicy(string code, string expectedXml)
    {
        code.CompileDocument().Should().BeSuccessful().And.DocumentEquivalentTo(expectedXml);
    }

    [TestMethod]
    [DataRow(typeof(IInboundContext), DisplayName = "Inbound Wait API")]
    [DataRow(typeof(IOutboundContext), DisplayName = "Outbound Wait API")]
    [DataRow(typeof(IBackendContext), DisplayName = "Backend Wait API")]
    [DataRow(typeof(IOnErrorContext), DisplayName = "On-error Wait API")]
    [DataRow(typeof(IFragmentContext), DisplayName = "Fragment Wait API")]
    public void ShouldExposeBranchScopedWaitApi(Type contextType)
    {
        var methods = contextType.GetMethods().Where(method => method.Name == "Wait").ToArray();
        methods.Should().HaveCount(2);

        var branchMethod = methods.Single(method => method.GetParameters()[1].IsDefined(typeof(ParamArrayAttribute)));
        branchMethod.ReturnType.Should().Be(typeof(void));
        branchMethod.IsDefined(typeof(ObsoleteAttribute)).Should().BeFalse();
        var parameters = branchMethod.GetParameters();
        parameters[0].Name.Should().Be("waitFor");
        parameters[0].ParameterType.Should().Be(typeof(string));
        parameters[0].HasDefaultValue.Should().BeFalse();
        parameters[0].IsDefined(typeof(ExpressionAllowedAttribute)).Should().BeTrue();
        parameters[1].Name.Should().Be("branches");
        parameters[1].ParameterType.Should().Be(typeof(Action<>).MakeGenericType(contextType).MakeArrayType());

        var legacyMethod = methods.Single(method => method.GetParameters()[0].ParameterType == typeof(Action));
        var legacyParameters = legacyMethod.GetParameters();
        legacyParameters[0].Name.Should().Be("section");
        legacyParameters[1].HasDefaultValue.Should().BeTrue();
        legacyParameters[1].DefaultValue.Should().BeNull();
        legacyParameters[1].IsDefined(typeof(ExpressionAllowedAttribute)).Should().BeTrue();
        var obsolete = legacyMethod.GetCustomAttribute<ObsoleteAttribute>();
        obsolete.Should().NotBeNull();
        obsolete!.IsError.Should().BeFalse();
        obsolete.Message.Should().Contain("Wait(waitFor, branches)");
    }

    [TestMethod]
    [DataRow("Inbound", "IInboundContext", "inbound", DisplayName = "Inbound branch-scoped Wait")]
    [DataRow("Outbound", "IOutboundContext", "outbound", DisplayName = "Outbound branch-scoped Wait")]
    [DataRow("Backend", "IBackendContext", "backend", DisplayName = "Backend branch-scoped Wait")]
    [DataRow("OnError", "IOnErrorContext", "on-error", DisplayName = "On-error branch-scoped Wait")]
    public void ShouldCompileBranchScopedWaitInSections(string method, string contextType, string section)
    {
        var code = $$"""
            [Document]
            public class PolicyDocument : IDocument
            {
                public void {{method}}({{contextType}} context)
                {
                    context.Wait(null, branch => branch.SendRequest(new SendRequestConfig
                    {
                        ResponseVariableName = "request"
                    }));
                }
            }
            """;
        var expectedXml = $$"""
            <policies>
                <{{section}}>
                    <wait>
                        <send-request response-variable-name="request" />
                    </wait>
                </{{section}}>
            </policies>
            """;

        CompileBranchDocument(code).Should().BeSuccessful().And.DocumentEquivalentTo(expectedXml);
    }

    [TestMethod]
    public void ShouldCompileBranchScopedWaitInFragment()
    {
        var code = """
            [Document(Type = DocumentType.Fragment)]
            public class PolicyDocument : IFragment
            {
                public void Fragment(IFragmentContext context)
                {
                    context.Wait(null, branch =>
                    {
                        branch.CacheLookupValue(new CacheLookupValueConfig
                        {
                            Key = "key",
                            VariableName = "cache"
                        });
                    });
                }
            }
            """;
        var expectedXml = """
            <fragment>
                <wait>
                    <cache-lookup-value key="key" variable-name="cache" />
                </wait>
            </fragment>
            """;

        CompileBranchDocument(code).Should().BeSuccessful().And.DocumentEquivalentTo(expectedXml);
    }

    [TestMethod]
    [DataRow("any", DisplayName = "Wait for any")]
    [DataRow("all", DisplayName = "Wait for all")]
    public void ShouldCompileMultipleWaitBranches(string waitFor)
    {
        var code = $$"""
            [Document]
            public class PolicyDocument : IDocument
            {
                public void Inbound(IInboundContext context)
                {
                    context.Wait("{{waitFor}}",
                        branch => branch.SendRequest(new SendRequestConfig
                        {
                            ResponseVariableName = "first"
                        }),
                        branch =>
                        {
                            branch.CacheLookupValue(new CacheLookupValueConfig
                            {
                                Key = "key",
                                VariableName = "cache"
                            });
                        },
                        branch => branch.SendRequest(new SendRequestConfig
                        {
                            ResponseVariableName = "last"
                        }));
                }
            }
            """;
        var expectedXml = $$"""
            <policies>
                <inbound>
                    <wait for="{{waitFor}}">
                        <send-request response-variable-name="first" />
                        <cache-lookup-value key="key" variable-name="cache" />
                        <send-request response-variable-name="last" />
                    </wait>
                </inbound>
            </policies>
            """;

        CompileBranchDocument(code).Should().BeSuccessful().And.DocumentEquivalentTo(expectedXml);
    }

    [TestMethod]
    [DataRow("null", DisplayName = "Wait omits null for")]
    [DataRow("default", DisplayName = "Wait omits default for")]
    [DataRow("default(string)", DisplayName = "Wait omits typed default for")]
    [DataRow("NullWaitFor", DisplayName = "Wait omits constant null for")]
    public void ShouldOmitNullWaitFor(string waitFor)
    {
        var code = $$"""
            [Document]
            public class PolicyDocument : IDocument
            {
                private const string? NullWaitFor = null;

                public void Inbound(IInboundContext context)
                {
                    context.Wait({{waitFor}}, branch =>
                    {
                        branch.SendRequest(new SendRequestConfig
                        {
                            ResponseVariableName = "request"
                        });
                    });
                }
            }
            """;

        CompileBranchDocument(code).Should().BeSuccessful().And.DocumentEquivalentTo(
            """
            <policies>
                <inbound>
                    <wait>
                        <send-request response-variable-name="request" />
                    </wait>
                </inbound>
            </policies>
            """);
    }

    [TestMethod]
    [DataRow(
        """
        context.Wait(null, branch =>
        {
            branch.SendRequest(new SendRequestConfig { ResponseVariableName = "request" });
        })
        """,
        """
        <policies>
            <inbound>
                <wait>
                    <send-request response-variable-name="request" />
                </wait>
            </inbound>
        </policies>
        """,
        DisplayName = "Wait block branch")]
    [DataRow(
        """
        context.Wait(null, branch => branch.CacheLookupValue(new CacheLookupValueConfig
        {
            Key = "key",
            VariableName = "cache"
        }))
        """,
        """
        <policies>
            <inbound>
                <wait>
                    <cache-lookup-value key="key" variable-name="cache" />
                </wait>
            </inbound>
        </policies>
        """,
        DisplayName = "Wait cache expression branch")]
    [DataRow(
        """
        context.Wait(null, (IInboundContext branch) =>
            branch.SendRequest(new SendRequestConfig { ResponseVariableName = "request" }))
        """,
        """
        <policies>
            <inbound>
                <wait>
                    <send-request response-variable-name="request" />
                </wait>
            </inbound>
        </policies>
        """,
        DisplayName = "Wait typed branch")]
    [DataRow(
        """
        context.Wait(null, static (branch) =>
            branch.SendRequest(new SendRequestConfig { ResponseVariableName = "request" }))
        """,
        """
        <policies>
            <inbound>
                <wait>
                    <send-request response-variable-name="request" />
                </wait>
            </inbound>
        </policies>
        """,
        DisplayName = "Wait static branch")]
    [DataRow(
        """
        context.Wait(null, branch => branch.SendRequest(new SendRequestConfig
        {
            ResponseVariableName = ResponseVariableNameExp(branch.ExpressionContext),
            Url = UrlExp(branch.ExpressionContext)
        }))
        """,
        """
        <policies>
            <inbound>
                <wait>
                    <send-request response-variable-name="@(context.Request.Method)">
                        <set-url>@(context.Request.Url.ToString())</set-url>
                    </send-request>
                </wait>
            </inbound>
        </policies>
        """,
        DisplayName = "Wait expression-bodied child policy expressions")]
    [DataRow(
        """
        context.Wait(WaitForExp(context.ExpressionContext), branch =>
        {
            branch.SendRequest(new SendRequestConfig { ResponseVariableName = "request" });
        })
        """,
        """
        <policies>
            <inbound>
                <wait for="@(context.Variables.ContainsKey("any") ? "any" : "all")">
                    <send-request response-variable-name="request" />
                </wait>
            </inbound>
        </policies>
        """,
        DisplayName = "Wait expression waitFor")]
    [DataRow(
        """
        context.Wait(waitFor: null, branch =>
            branch.SendRequest(new SendRequestConfig { ResponseVariableName = "request" }))
        """,
        """
        <policies>
            <inbound>
                <wait>
                    <send-request response-variable-name="request" />
                </wait>
            </inbound>
        </policies>
        """,
        DisplayName = "Wait named waitFor")]
    public void ShouldCompileBranchScopedWaitPolicy(string invocation, string expectedXml)
    {
        CompileBranchDocument(CreateInboundDocument(invocation)).Should().BeSuccessful().And.DocumentEquivalentTo(expectedXml);
    }

    [TestMethod]
    public void ShouldCompileWaitConditionalChainWithSequentialNestedPolicies()
    {
        var code = """
            [Document]
            public class PolicyDocument : IDocument
            {
                public void Inbound(IInboundContext context)
                {
                    context.Wait(null, branch =>
                    {
                        if (FirstExp(branch.ExpressionContext))
                        {
                            branch.SendRequest(new SendRequestConfig { ResponseVariableName = "request" });
                            branch.SetHeader("X-Result", "first");
                            if (NestedExp(branch.ExpressionContext))
                            {
                                branch.SetHeader("X-Nested", "yes");
                                branch.SetVariable("nested", "value");
                            }
                        }
                        else if (SecondExp(branch.ExpressionContext))
                        {
                            branch.CacheLookupValue(new CacheLookupValueConfig
                            {
                                Key = "key",
                                VariableName = "cache"
                            });
                            branch.SetVariable("result", "second");
                        }
                        else
                        {
                            branch.SetHeader("X-Result", "none");
                            branch.SendRequest(new SendRequestConfig { ResponseVariableName = "fallback" });
                        }
                    });
                }

                bool FirstExp(IExpressionContext context) => context.Variables.ContainsKey("first");
                bool SecondExp(IExpressionContext context) => context.Variables.ContainsKey("second");
                bool NestedExp(IExpressionContext context) => context.Variables.ContainsKey("nested");
            }
            """;
        var expectedXml = """
            <policies>
                <inbound>
                    <wait>
                        <choose>
                            <when condition="@(context.Variables.ContainsKey("first"))">
                                <send-request response-variable-name="request" />
                                <set-header name="X-Result">
                                    <value>first</value>
                                </set-header>
                                <choose>
                                    <when condition="@(context.Variables.ContainsKey("nested"))">
                                        <set-header name="X-Nested">
                                            <value>yes</value>
                                        </set-header>
                                        <set-variable name="nested" value="value" />
                                    </when>
                                </choose>
                            </when>
                            <when condition="@(context.Variables.ContainsKey("second"))">
                                <cache-lookup-value key="key" variable-name="cache" />
                                <set-variable name="result" value="second" />
                            </when>
                            <otherwise>
                                <set-header name="X-Result">
                                    <value>none</value>
                                </set-header>
                                <send-request response-variable-name="fallback" />
                            </otherwise>
                        </choose>
                    </wait>
                </inbound>
            </policies>
            """;

        CompileBranchDocument(code).Should().BeSuccessful().And.DocumentEquivalentTo(expectedXml);
    }

    [TestMethod]
    public void ShouldCompileSeparateConditionalWaitBranches()
    {
        var code = """
            [Document]
            public class PolicyDocument : IDocument
            {
                public void Inbound(IInboundContext context)
                {
                    context.Wait(null,
                        branch =>
                        {
                            if (ConditionExp(branch.ExpressionContext))
                            {
                                branch.SendRequest(new SendRequestConfig { ResponseVariableName = "request" });
                            }
                        },
                        branch =>
                        {
                            if (ConditionExp(branch.ExpressionContext))
                            {
                                branch.CacheLookupValue(new CacheLookupValueConfig
                                {
                                    Key = "key",
                                    VariableName = "cache"
                                });
                            }
                        });
                }

                bool ConditionExp(IExpressionContext context) => context.Variables.ContainsKey("condition");
            }
            """;
        var expectedXml = """
            <policies>
                <inbound>
                    <wait>
                        <choose>
                            <when condition="@(context.Variables.ContainsKey("condition"))">
                                <send-request response-variable-name="request" />
                            </when>
                        </choose>
                        <choose>
                            <when condition="@(context.Variables.ContainsKey("condition"))">
                                <cache-lookup-value key="key" variable-name="cache" />
                            </when>
                        </choose>
                    </wait>
                </inbound>
            </policies>
            """;

        CompileBranchDocument(code).Should().BeSuccessful().And.DocumentEquivalentTo(expectedXml);
    }

    [TestMethod]
    [DataRow("", "", DisplayName = "Legacy Wait permits sequential child policies")]
    [DataRow(", null", " for=\"null\"", DisplayName = "Legacy Wait preserves explicit null for")]
    public void ShouldPreserveLegacyWaitCompilation(string waitFor, string attribute)
    {
        var code = CreateInboundDocument($$"""
            context.Wait(() =>
            {
                context.Base();
                context.SetHeader("X-Legacy", "kept");
            }{{waitFor}})
            """);
        var expectedXml = $$"""
            <policies>
                <inbound>
                    <wait{{attribute}}>
                        <base />
                        <set-header name="X-Legacy">
                            <value>kept</value>
                        </set-header>
                    </wait>
                </inbound>
            </policies>
            """;

        CompileBranchDocument(code).Should().BeSuccessful().And.DocumentEquivalentTo(expectedXml);
    }

    [TestMethod]
    [DataRow("\"all\"", "APIM2007", "branches", DisplayName = "Reject Wait without branches")]
    [DataRow("(string?)null, branches: null", "APIM2012", "individual lambdas", DisplayName = "Reject null branch array")]
    [DataRow("null, SendBranch", "APIM2012", "individual lambdas", DisplayName = "Reject branch method group")]
    [DataRow("null, Branches", "APIM2012", "individual lambdas", DisplayName = "Reject branch array reference")]
    [DataRow(
        """
        null, new Action<IInboundContext>[]
        {
            branch => branch.SendRequest(new SendRequestConfig { ResponseVariableName = "request" })
        }
        """,
        "APIM2012", "individual lambdas", DisplayName = "Reject explicit branch array")]
    [DataRow(
        """
        null, new[]
        {
            (Action<IInboundContext>)(branch =>
                branch.SendRequest(new SendRequestConfig { ResponseVariableName = "request" }))
        }
        """,
        "APIM2012", "individual lambdas", DisplayName = "Reject implicit branch array")]
    [DataRow(
        """
        null, [branch => branch.SendRequest(new SendRequestConfig { ResponseVariableName = "request" })]
        """,
        "APIM2012", "individual lambdas", DisplayName = "Reject branch collection expression")]
    [DataRow(
        "null, Array.Empty<Action<IInboundContext>>()",
        "APIM2012", "individual lambdas", DisplayName = "Reject branch array factory")]
    [DataRow(
        "null, new Action<IInboundContext>(SendBranch)",
        "APIM2012", "individual lambdas", DisplayName = "Reject branch delegate construction")]
    [DataRow(
        """
        null, (Action<IInboundContext>)(branch =>
            branch.SendRequest(new SendRequestConfig { ResponseVariableName = "request" }))
        """,
        "APIM2012", "individual lambdas", DisplayName = "Reject cast branch argument")]
    [DataRow(
        """
        branches: branch => branch.SendRequest(new SendRequestConfig { ResponseVariableName = "request" })
        """,
        "APIM2006", "waitFor", DisplayName = "Reject missing waitFor argument")]
    public void ShouldRejectUnsupportedWaitArguments(string arguments, string diagnosticId, string reason)
    {
        var result = CompileBranchDocument(CreateInboundDocument($"context.Wait({arguments})"), verifyCSharp: false);

        AssertRejectedWait(result, diagnosticId, reason);
    }

    [TestMethod]
    [DataRow("branch => { }", "exactly one top-level statement", DisplayName = "Reject empty Wait branch")]
    [DataRow(
        """
        branch =>
        {
            branch.SendRequest(new SendRequestConfig { ResponseVariableName = "request" });
            branch.CacheLookupValue(new CacheLookupValueConfig { Key = "key", VariableName = "cache" });
        }
        """,
        "exactly one top-level statement", DisplayName = "Reject sequential immediate Wait policies")]
    [DataRow(
        """
        branch =>
        {
            if (ConditionExp(branch.ExpressionContext))
            {
                branch.SendRequest(new SendRequestConfig { ResponseVariableName = "first" });
            }
            if (ConditionExp(branch.ExpressionContext))
            {
                branch.SendRequest(new SendRequestConfig { ResponseVariableName = "second" });
            }
        }
        """,
        "exactly one top-level statement", DisplayName = "Reject separate immediate if statements")]
    [DataRow(
        "branch => branch.Base()",
        "send-request, cache-lookup-value, or choose", DisplayName = "Reject immediate base policy")]
    [DataRow(
        """branch => { branch.SetHeader("X-Test", "value"); }""",
        "send-request, cache-lookup-value, or choose", DisplayName = "Reject immediate set-header policy")]
    [DataRow(
        """
        branch => branch.SendOneWayRequest(new SendOneWayRequestConfig { Url = "https://example.test" })
        """,
        "send-request, cache-lookup-value, or choose", DisplayName = "Reject immediate send-one-way-request policy")]
    [DataRow(
        "branch => branch.Wait(() => { })",
        "send-request, cache-lookup-value, or choose", DisplayName = "Reject immediate nested Wait policy")]
    [DataRow(
        """
        branch => { { branch.SendRequest(new SendRequestConfig { ResponseVariableName = "request" }); } }
        """,
        "send-request, cache-lookup-value, or choose", DisplayName = "Reject nested top-level block")]
    [DataRow(
        "branch => { branch = context; }",
        "send-request, cache-lookup-value, or choose", DisplayName = "Reject branch assignment statement")]
    [DataRow(
        "branch => { return; }",
        "send-request, cache-lookup-value, or choose", DisplayName = "Reject branch return statement")]
    [DataRow(
        """
        async branch => { branch.SendRequest(new SendRequestConfig { ResponseVariableName = "request" }); }
        """,
        "synchronous", DisplayName = "Reject async Wait branch")]
    [DataRow(
        """
        () => { context.SendRequest(new SendRequestConfig { ResponseVariableName = "request" }); }
        """,
        "exactly one context parameter", DisplayName = "Reject parameterless new Wait branch")]
    [DataRow(
        """
        (branch, otherBranch) => { branch.SendRequest(new SendRequestConfig { ResponseVariableName = "request" }); }
        """,
        "exactly one context parameter", DisplayName = "Reject multi-parameter Wait branch")]
    [DataRow(
        """
        branch => context.SendRequest(new SendRequestConfig { ResponseVariableName = "request" })
        """,
        "branch context parameter", DisplayName = "Reject outer-context immediate policy")]
    [DataRow(
        """
        branch => other.SendRequest(new SendRequestConfig { ResponseVariableName = "request" })
        """,
        "branch context parameter", DisplayName = "Reject different-context immediate policy")]
    [DataRow(
        """
        branch =>
        {
            if (ConditionExp(branch.ExpressionContext))
            {
                context.SendRequest(new SendRequestConfig { ResponseVariableName = "request" });
            }
        }
        """,
        "branch context parameter", DisplayName = "Reject outer-context policy inside choose")]
    [DataRow(
        """
        branch =>
        {
            if (ConditionExp(branch.ExpressionContext))
            {
                branch.SendRequest(new SendRequestConfig { ResponseVariableName = "request" });
            }
            else
            {
                context.CacheLookupValue(new CacheLookupValueConfig { Key = "key", VariableName = "cache" });
            }
        }
        """,
        "branch context parameter", DisplayName = "Reject outer-context policy inside otherwise")]
    [DataRow(
        """
        branch =>
        {
            if (ConditionExp(branch.ExpressionContext))
            {
                if (ConditionExp(branch.ExpressionContext))
                {
                    context.SetHeader("X-Test", "value");
                }
            }
        }
        """,
        "branch context parameter", DisplayName = "Reject outer-context policy inside nested choose")]
    [DataRow(
        """
        (IOutboundContext branch) => branch.SendRequest(new SendRequestConfig { ResponseVariableName = "request" })
        """,
        "same context type", DisplayName = "Reject mismatched branch context type")]
    [DataRow(
        """branch => throw new InvalidOperationException("not a policy")""",
        "send-request, cache-lookup-value, or choose", DisplayName = "Reject non-policy branch expression")]
    public void ShouldRejectInvalidWaitBranch(string branch, string reason)
    {
        var result = CompileBranchDocument(CreateInboundDocument($"context.Wait(null, {branch})"), verifyCSharp: false);

        AssertRejectedWait(result, "APIM2012", reason);
    }

    [TestMethod]
    [DataRow(
        "branch => branch.SendRequest(new SendRequestConfig { })",
        "APIM2006", "ResponseVariableName", DisplayName = "Propagate send-request branch compiler error")]
    [DataRow(
        """branch => branch.CacheLookupValue(new CacheLookupValueConfig { Key = "key" })""",
        "APIM2006", "VariableName", DisplayName = "Propagate cache-lookup-value branch compiler error")]
    [DataRow(
        """
        branch => { if (true) { branch.SendRequest(new SendRequestConfig { ResponseVariableName = "request" }); } }
        """,
        "APIM9992", "InvocationExpressionSyntax", DisplayName = "Propagate choose condition compiler error")]
    [DataRow(
        """
        branch => { if (ConditionExp(branch.ExpressionContext)) branch.SendRequest(new SendRequestConfig
        {
            ResponseVariableName = "request"
        }); }
        """,
        "APIM9993", "ExpressionStatementSyntax", DisplayName = "Propagate choose block compiler error")]
    public void ShouldPropagateWaitChildCompilerErrors(string branch, string diagnosticId, string reason)
    {
        var result = CompileBranchDocument(CreateInboundDocument($"context.Wait(null, {branch})"), verifyCSharp: false);

        AssertRejectedWait(result, diagnosticId, reason);
    }

    [TestMethod]
    public void ShouldPreserveLegacyWaitDiagnostics()
    {
        var code = CreateInboundDocument(
            """
            context.Wait(() => context.SendRequest(new SendRequestConfig { ResponseVariableName = "request" }))
            """);

        AssertRejectedWait(CompileBranchDocument(code), "APIM9993", "ParenthesizedLambdaExpressionSyntax");
    }

    private static readonly MetadataReference[] s_references = CreateReferences();

    private static MetadataReference[] CreateReferences()
    {
        if (AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") is not string assemblies)
        {
            throw new InvalidOperationException("Runtime assembly references are unavailable for Wait compiler tests.");
        }

        return assemblies.Split(Path.PathSeparator)
            .Append(typeof(IDocument).Assembly.Location)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(path => MetadataReference.CreateFromFile(path))
            .ToArray();
    }

    private static IDocumentCompilationResult CompileBranchDocument(string code, bool verifyCSharp = true)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(
            $$"""
              using System;
              using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
              using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring.Expressions;

              {{code}}
              """);
        var compilation = CSharpCompilation.Create(
            "WaitPolicy",
            [syntaxTree],
            s_references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        if (verifyCSharp)
        {
            compilation.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
                .Should().BeEmpty("branch-scoped Wait authoring examples must be valid C#");
        }

        using var services = new ServiceCollection().SetupCompiler().BuildServiceProvider();
        var document = syntaxTree.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>().First();
        return services.GetRequiredService<DocumentCompiler>().Compile(compilation, document);
    }

    private static string CreateInboundDocument(string invocation) => $$"""
        [Document]
        public class PolicyDocument : IDocument
        {
            private IInboundContext other = null!;
            private Action<IInboundContext>[] Branches => [SendBranch];

            public void Inbound(IInboundContext context)
            {
                {{invocation}};
            }

            void SendBranch(IInboundContext branch)
            {
                branch.SendRequest(new SendRequestConfig { ResponseVariableName = "request" });
            }

            bool ConditionExp(IExpressionContext context) => context.Variables.ContainsKey("condition");
            string WaitForExp(IExpressionContext context) => context.Variables.ContainsKey("any") ? "any" : "all";
            string ResponseVariableNameExp(IExpressionContext context) => context.Request.Method;
            string UrlExp(IExpressionContext context) => context.Request.Url.ToString();
        }
        """;

    private static void AssertRejectedWait(IDocumentCompilationResult result, string diagnosticId, string reason)
    {
        result.Errors.Should().ContainSingle();
        var diagnostic = result.Errors.Single();
        diagnostic.Id.Should().Be(diagnosticId);
        diagnostic.Severity.Should().Be(DiagnosticSeverity.Error);
        diagnostic.GetMessage().Should().Contain(reason);
        diagnostic.Location.IsInSource.Should().BeTrue();
        result.Document.Should().NotBeNull();
        result.Document!.Descendants("wait").Should().BeEmpty();
    }

    [TestMethod]
    [DataRow(
        """
        context.Wait(null, branch => branch.WithId("id").SendRequest(new SendRequestConfig
        {
            ResponseVariableName = "request"
        }))
        """,
        """
        <policies>
            <inbound>
                <wait>
                    <send-request id="id" response-variable-name="request" />
                </wait>
            </inbound>
        </policies>
        """,
        DisplayName = "Wait expression branch preserves WithId")]
    [DataRow(
        """
        context.Wait(null, branch =>
        {
            branch.WithId("id").SendRequest(new SendRequestConfig { ResponseVariableName = "request" });
        })
        """,
        """
        <policies>
            <inbound>
                <wait>
                    <send-request id="id" response-variable-name="request" />
                </wait>
            </inbound>
        </policies>
        """,
        DisplayName = "Wait block branch preserves WithId")]
    [DataRow(
        """
        context.Wait(null, branch => branch.WithId("id").CacheLookupValue(new CacheLookupValueConfig
        {
            Key = "key",
            VariableName = "cache"
        }))
        """,
        """
        <policies>
            <inbound>
                <wait>
                    <cache-lookup-value id="id" key="key" variable-name="cache" />
                </wait>
            </inbound>
        </policies>
        """,
        DisplayName = "Wait cache expression branch preserves WithId")]
    [DataRow(
        """
        context.Wait(null, branch =>
        {
            branch.WithId("id").CacheLookupValue(new CacheLookupValueConfig { Key = "key", VariableName = "cache" });
        })
        """,
        """
        <policies>
            <inbound>
                <wait>
                    <cache-lookup-value id="id" key="key" variable-name="cache" />
                </wait>
            </inbound>
        </policies>
        """,
        DisplayName = "Wait cache block branch preserves WithId")]
    [DataRow(
        """
        context.Wait(null, branch => branch.WithId("first").WithId("second").WithId("last")
            .SendRequest(new SendRequestConfig { ResponseVariableName = "request" }))
        """,
        """
        <policies>
            <inbound>
                <wait>
                    <send-request id="last" response-variable-name="request" />
                </wait>
            </inbound>
        </policies>
        """,
        DisplayName = "Wait WithId chain uses last ID")]
    [DataRow(
        """
        context.Wait(null, branch => branch.WithId(id: "id")
            .SendRequest(new SendRequestConfig { ResponseVariableName = "request" }))
        """,
        """
        <policies>
            <inbound>
                <wait>
                    <send-request id="id" response-variable-name="request" />
                </wait>
            </inbound>
        </policies>
        """,
        DisplayName = "Wait WithId accepts named ID")]
    [DataRow(
        """
        context.Wait(null, branch => branch.WithId(nameof(context))
            .SendRequest(new SendRequestConfig { ResponseVariableName = "request" }))
        """,
        """
        <policies>
            <inbound>
                <wait>
                    <send-request id="context" response-variable-name="request" />
                </wait>
            </inbound>
        </policies>
        """,
        DisplayName = "Wait WithId nameof does not capture outer state")]
    [DataRow(
        """
        context.Wait(null, branch => branch.WithId("id").SendRequest(new SendRequestConfig
        {
            ResponseVariableName = ResponseVariableNameExp(branch.ExpressionContext),
            Url = UrlExp(branch.ExpressionContext)
        }))
        """,
        """
        <policies>
            <inbound>
                <wait>
                    <send-request id="id" response-variable-name="@(context.Request.Method)">
                        <set-url>@(context.Request.Url.ToString())</set-url>
                    </send-request>
                </wait>
            </inbound>
        </policies>
        """,
        DisplayName = "Wait WithId preserves child policy expressions")]
    public void ShouldCompileWaitBranchMetadata(string invocation, string expectedXml)
    {
        CompileBranchDocument(CreateInboundDocument(invocation)).Should().BeSuccessful().And.DocumentEquivalentTo(expectedXml);
    }

    [TestMethod]
    [DataRow("Inbound", "IInboundContext", "inbound", DisplayName = "Inbound Wait branch metadata")]
    [DataRow("Outbound", "IOutboundContext", "outbound", DisplayName = "Outbound Wait branch metadata")]
    [DataRow("Backend", "IBackendContext", "backend", DisplayName = "Backend Wait branch metadata")]
    [DataRow("OnError", "IOnErrorContext", "on-error", DisplayName = "On-error Wait branch metadata")]
    public void ShouldCompileWaitBranchMetadataInSections(string method, string contextType, string section)
    {
        var code = $$"""
            [Document]
            public class PolicyDocument : IDocument
            {
                public void {{method}}({{contextType}} context)
                {
                    context.Wait(null, branch => branch.WithId("id").SendRequest(new SendRequestConfig
                    {
                        ResponseVariableName = "request"
                    }));
                }
            }
            """;
        var expectedXml = $$"""
            <policies>
                <{{section}}>
                    <wait>
                        <send-request id="id" response-variable-name="request" />
                    </wait>
                </{{section}}>
            </policies>
            """;

        CompileBranchDocument(code).Should().BeSuccessful().And.DocumentEquivalentTo(expectedXml);
    }

    [TestMethod]
    public void ShouldCompileWaitBranchMetadataInFragment()
    {
        var code = """
            [Document(Type = DocumentType.Fragment)]
            public class PolicyDocument : IFragment
            {
                public void Fragment(IFragmentContext context)
                {
                    context.Wait(null, branch => branch.WithId("id").SendRequest(new SendRequestConfig
                    {
                        ResponseVariableName = "request"
                    }));
                }
            }
            """;
        var expectedXml = """
            <fragment>
                <wait>
                    <send-request id="id" response-variable-name="request" />
                </wait>
            </fragment>
            """;

        CompileBranchDocument(code).Should().BeSuccessful().And.DocumentEquivalentTo(expectedXml);
    }

    [TestMethod]
    public void ShouldCompileWaitBranchMetadataFromConstants()
    {
        var code = """
            [Document]
            public class PolicyDocument : IDocument
            {
                private const string PolicyId = "constant-id";
                private const string VariableName = "request";

                public void Inbound(IInboundContext context)
                {
                    context.Wait(null, branch => branch.WithId(PolicyId).SendRequest(new SendRequestConfig
                    {
                        ResponseVariableName = PolicyDocument.VariableName
                    }));
                }
            }
            """;
        var expectedXml = """
            <policies>
                <inbound>
                    <wait>
                        <send-request id="constant-id" response-variable-name="request" />
                    </wait>
                </inbound>
            </policies>
            """;

        CompileBranchDocument(code).Should().BeSuccessful().And.DocumentEquivalentTo(expectedXml);
    }

    [TestMethod]
    public void ShouldIsolateWaitAndBranchPolicyIds()
    {
        var code = CreateInboundDocument(
            """
            context.WithId("wait").Wait("any",
                branch => branch.WithId("first").SendRequest(new SendRequestConfig { ResponseVariableName = "first" }),
                branch => branch.SendRequest(new SendRequestConfig { ResponseVariableName = "second" }),
                branch => branch.WithId("last").CacheLookupValue(new CacheLookupValueConfig
                {
                    Key = "key",
                    VariableName = "cache"
                }))
            """);
        var expectedXml = """
            <policies>
                <inbound>
                    <wait id="wait" for="any">
                        <send-request id="first" response-variable-name="first" />
                        <send-request response-variable-name="second" />
                        <cache-lookup-value id="last" key="key" variable-name="cache" />
                    </wait>
                </inbound>
            </policies>
            """;

        CompileBranchDocument(code).Should().BeSuccessful().And.DocumentEquivalentTo(expectedXml);
    }

    [TestMethod]
    public void ShouldCompileWaitMetadataInsideChoose()
    {
        var code = CreateInboundDocument(
            """
            context.Wait(null, branch =>
            {
                if (ConditionExp(branch.ExpressionContext))
                {
                    branch.WithId("request").SendRequest(new SendRequestConfig { ResponseVariableName = "request" });
                    branch.WithId("header").SetHeader("X-Metadata", "value");
                    branch.SendRequest(new SendRequestConfig { ResponseVariableName = "second" });
                }
            })
            """);
        var expectedXml = """
            <policies>
                <inbound>
                    <wait>
                        <choose>
                            <when condition="@(context.Variables.ContainsKey("condition"))">
                                <send-request id="request" response-variable-name="request" />
                                <set-header id="header" name="X-Metadata">
                                    <value>value</value>
                                </set-header>
                                <send-request response-variable-name="second" />
                            </when>
                        </choose>
                    </wait>
                </inbound>
            </policies>
            """;

        CompileBranchDocument(code).Should().BeSuccessful().And.DocumentEquivalentTo(expectedXml);
    }

    [TestMethod]
    public void ShouldPreserveLegacyWaitBranchMetadata()
    {
        var code = CreateInboundDocument(
            """
            context.Wait(() =>
            {
                context.WithId("legacy").SendRequest(new SendRequestConfig { ResponseVariableName = "request" });
            })
            """);
        var expectedXml = """
            <policies>
                <inbound>
                    <wait>
                        <send-request id="legacy" response-variable-name="request" />
                    </wait>
                </inbound>
            </policies>
            """;

        CompileBranchDocument(code).Should().BeSuccessful().And.DocumentEquivalentTo(expectedXml);
    }

    [TestMethod]
    [DataRow(
        """
        branch =>
        {
            if (ConditionExp(context.ExpressionContext))
            {
                branch.SendRequest(new SendRequestConfig { ResponseVariableName = "request" });
            }
        }
        """,
        DisplayName = "Reject outer context in Wait condition")]
    [DataRow(
        """
        branch => branch.SendRequest(new SendRequestConfig
        {
            ResponseVariableName = ResponseVariableNameExp(context.ExpressionContext)
        })
        """,
        DisplayName = "Reject outer context in Wait config expression")]
    [DataRow(
        """
        branch => branch.SendRequest(new SendRequestConfig
        {
            ResponseVariableName = context.ExpressionContext.Request.Method
        })
        """,
        DisplayName = "Reject outer context in direct Wait config")]
    [DataRow(
        """
        branch =>
        {
            if (ConditionExp(branch.ExpressionContext))
            {
                branch.SendRequest(new SendRequestConfig { ResponseVariableName = "first" });
            }
            else if (ConditionExp(context.ExpressionContext))
            {
                branch.SendRequest(new SendRequestConfig { ResponseVariableName = "second" });
            }
        }
        """,
        DisplayName = "Reject outer context in else-if condition")]
    [DataRow(
        """
        branch =>
        {
            if (ConditionExp(branch.ExpressionContext))
            {
                branch.SendRequest(new SendRequestConfig { ResponseVariableName = "first" });
            }
            else
            {
                branch.SendRequest(new SendRequestConfig
                {
                    ResponseVariableName = ResponseVariableNameExp(context.ExpressionContext)
                });
            }
        }
        """,
        DisplayName = "Reject outer context in otherwise config")]
    [DataRow(
        """
        branch =>
        {
            if (ConditionExp(branch.ExpressionContext))
            {
                if (ConditionExp(context.ExpressionContext))
                {
                    branch.SendRequest(new SendRequestConfig { ResponseVariableName = "request" });
                }
            }
        }
        """,
        DisplayName = "Reject outer context in nested choose condition")]
    [DataRow(
        """
        branch =>
        {
            if (ConditionExp(other.ExpressionContext))
            {
                branch.SendRequest(new SendRequestConfig { ResponseVariableName = "request" });
            }
        }
        """,
        DisplayName = "Reject other context field in Wait condition")]
    [DataRow(
        """
        branch =>
        {
            if (ConditionExp(branch.ExpressionContext))
            {
                branch.Wait(null, inner => inner.SendRequest(new SendRequestConfig
                {
                    ResponseVariableName = ResponseVariableNameExp(context.ExpressionContext)
                }));
            }
        }
        """,
        DisplayName = "Reject outer context inside nested typed Wait branch")]
    [DataRow(
        """
        branch =>
        {
            if (ConditionExp(branch.ExpressionContext))
            {
                branch.Wait(null, inner => inner.SendRequest(new SendRequestConfig
                {
                    ResponseVariableName = ResponseVariableNameExp(branch.ExpressionContext)
                }));
            }
        }
        """,
        DisplayName = "Reject enclosing branch context inside nested Wait")]
    public void ShouldRejectOuterContextReferencesInWaitBranch(string branch)
    {
        var code = CreateInboundDocument($"context.Wait(null, {branch})");

        AssertRejectedWait(CompileBranchDocument(code), "APIM2012", "outer section context");
    }

    [TestMethod]
    [DataRow("Inbound", "IInboundContext", DisplayName = "Reject inbound Wait expression capture")]
    [DataRow("Outbound", "IOutboundContext", DisplayName = "Reject outbound Wait expression capture")]
    [DataRow("Backend", "IBackendContext", DisplayName = "Reject backend Wait expression capture")]
    [DataRow("OnError", "IOnErrorContext", DisplayName = "Reject on-error Wait expression capture")]
    public void ShouldRejectOuterContextReferencesInWaitSections(string method, string contextType)
    {
        var code = $$"""
            [Document]
            public class PolicyDocument : IDocument
            {
                public void {{method}}({{contextType}} context)
                {
                    context.Wait(null, branch =>
                    {
                        if (ConditionExp(context.ExpressionContext))
                        {
                            branch.SendRequest(new SendRequestConfig { ResponseVariableName = "request" });
                        }
                    });
                }

                bool ConditionExp(IExpressionContext context) => context.Variables.ContainsKey("condition");
            }
            """;

        AssertRejectedWait(CompileBranchDocument(code), "APIM2012", "outer section context");
    }

    [TestMethod]
    public void ShouldRejectOuterContextReferenceInWaitFragment()
    {
        var code = """
            [Document(Type = DocumentType.Fragment)]
            public class PolicyDocument : IFragment
            {
                public void Fragment(IFragmentContext context)
                {
                    context.Wait(null, branch =>
                    {
                        if (ConditionExp(context.ExpressionContext))
                        {
                            branch.SendRequest(new SendRequestConfig { ResponseVariableName = "request" });
                        }
                    });
                }

                bool ConditionExp(IExpressionContext context) => context.Variables.ContainsKey("condition");
            }
            """;

        AssertRejectedWait(CompileBranchDocument(code), "APIM2012", "outer section context");
    }

    [TestMethod]
    public void ShouldRejectCommonContextInterfaceCapture()
    {
        var code = """
            [Document]
            public class PolicyDocument : IDocument
            {
                private IHaveExpressionContext other = null!;

                public void Inbound(IInboundContext context)
                {
                    context.Wait(null, branch =>
                    {
                        if (ConditionExp(other.ExpressionContext))
                        {
                            branch.SendRequest(new SendRequestConfig { ResponseVariableName = "request" });
                        }
                    });
                }

                bool ConditionExp(IExpressionContext context) => context.Variables.ContainsKey("condition");
            }
            """;

        AssertRejectedWait(CompileBranchDocument(code), "APIM2012", "outer section context");
    }

    [TestMethod]
    public void ShouldRejectOuterContextPassedToWaitExpressionHelper()
    {
        var code = """
            [Document]
            public class PolicyDocument : IDocument
            {
                public void Inbound(IInboundContext context)
                {
                    context.Wait(null, branch =>
                    {
                        if (ConditionExp(context))
                        {
                            branch.SendRequest(new SendRequestConfig { ResponseVariableName = "request" });
                        }
                    });
                }

                bool ConditionExp(IInboundContext context) => context.ExpressionContext.Variables.ContainsKey("condition");
            }
            """;

        AssertRejectedWait(CompileBranchDocument(code), "APIM2012", "outer section context");
    }

    [TestMethod]
    public void ShouldRejectUnrelatedReceiverWithId()
    {
        var code = """
            [Document]
            public class PolicyDocument : IDocument
            {
                private IInboundContext other = null!;

                public void Inbound(IInboundContext context)
                {
                    context.Wait(null, branch => GetOtherContext(branch).WithId("id")
                        .SendRequest(new SendRequestConfig { ResponseVariableName = "request" }));
                }

                IInboundContext GetOtherContext(IInboundContext branch) => other;
            }
            """;

        AssertRejectedWait(CompileBranchDocument(code), "APIM2012", "branch context parameter");
    }

    [TestMethod]
    public void ShouldCompileNestedWaitWithItsOwnBranchContexts()
    {
        var code = CreateInboundDocument(
            """
            context.Wait(null, branch =>
            {
                if (ConditionExp(branch.ExpressionContext))
                {
                    branch.Wait(null, inner => inner.WithId("inner").SendRequest(new SendRequestConfig
                    {
                        ResponseVariableName = ResponseVariableNameExp(inner.ExpressionContext)
                    }));
                }
            })
            """);
        var expectedXml = """
            <policies>
                <inbound>
                    <wait>
                        <choose>
                            <when condition="@(context.Variables.ContainsKey("condition"))">
                                <wait>
                                    <send-request id="inner" response-variable-name="@(context.Request.Method)" />
                                </wait>
                            </when>
                        </choose>
                    </wait>
                </inbound>
            </policies>
            """;

        CompileBranchDocument(code).Should().BeSuccessful().And.DocumentEquivalentTo(expectedXml);
    }

    [TestMethod]
    [DataRow(
        """
        branch => context.WithId("id").SendRequest(new SendRequestConfig { ResponseVariableName = "request" })
        """,
        "branch context parameter", DisplayName = "Reject captured outer WithId receiver")]
    [DataRow(
        """
        branch => other.WithId("id").SendRequest(new SendRequestConfig { ResponseVariableName = "request" })
        """,
        "branch context parameter", DisplayName = "Reject unrelated WithId receiver")]
    [DataRow(
        """
        branch => branch.WithId(branch.ExpressionContext.Request.Method)
            .SendRequest(new SendRequestConfig { ResponseVariableName = "request" })
        """,
        "constant string", DisplayName = "Reject nonconstant Wait branch ID")]
    public void ShouldRejectInvalidWaitBranchMetadata(string branch, string reason)
    {
        var code = CreateInboundDocument($"context.Wait(null, {branch})");

        AssertRejectedWait(CompileBranchDocument(code), "APIM2012", reason);
    }

    [TestMethod]
    [DataRow(
        """
        branch =>
        {
            if (Always(branch.ExpressionContext))
            {
                branch.CacheLookupValue(new CacheLookupValueConfig { Key = "key", VariableName = "cached" });
                if (Missing(_outer))
                {
                    branch.SendRequest(new SendRequestConfig { ResponseVariableName = "fallback" });
                }
            }
        }
        """,
        DisplayName = "Reject captured expression context after branch cache lookup")]
    [DataRow(
        """
        branch =>
        {
            if (Missing(_outer))
            {
                branch.SendRequest(new SendRequestConfig { ResponseVariableName = "request" });
            }
        }
        """,
        DisplayName = "Reject expression-context field in Wait condition")]
    [DataRow(
        """
        branch => branch.SendRequest(new SendRequestConfig { ResponseVariableName = NameExp(_outer) })
        """,
        DisplayName = "Reject expression-context field in Wait config expression")]
    [DataRow(
        """
        branch => branch.SendRequest(new SendRequestConfig { ResponseVariableName = _outer.Request.Method })
        """,
        DisplayName = "Reject expression-context field in direct Wait config")]
    [DataRow(
        """
        branch =>
        {
            if (Always(branch.ExpressionContext))
            {
                branch.SendRequest(new SendRequestConfig { ResponseVariableName = "first" });
            }
            else if (Missing(_outer))
            {
                branch.SendRequest(new SendRequestConfig { ResponseVariableName = "second" });
            }
        }
        """,
        DisplayName = "Reject expression-context field in else-if condition")]
    [DataRow(
        """
        branch =>
        {
            if (Always(branch.ExpressionContext))
            {
                branch.SendRequest(new SendRequestConfig { ResponseVariableName = "first" });
            }
            else
            {
                branch.SendRequest(new SendRequestConfig { ResponseVariableName = NameExp(_outer) });
            }
        }
        """,
        DisplayName = "Reject expression-context field in otherwise config")]
    [DataRow(
        """
        branch =>
        {
            if (Always(branch.ExpressionContext))
            {
                branch.Wait(null, inner => inner.SendRequest(new SendRequestConfig
                {
                    ResponseVariableName = NameExp(_outer)
                }));
            }
        }
        """,
        DisplayName = "Reject expression-context field in nested typed Wait")]
    [DataRow(
        """
        branch =>
        {
            if (Always(branch.ExpressionContext))
            {
                if (Missing(_outer))
                {
                    branch.SendRequest(new SendRequestConfig { ResponseVariableName = "request" });
                }
            }
        }
        """,
        DisplayName = "Reject expression-context field in nested choose")]
    public void ShouldRejectCapturedExpressionContextField(string branch)
    {
        AssertRejectedWait(CompileBranchDocument(CreateExpressionContextDocument(branch)),
            "APIM2012", "outer expression context");
    }

    [TestMethod]
    public void ShouldRejectCapturedPrimaryConstructorExpressionContext()
    {
        var code = """
            [Document]
            public class PolicyDocument(IExpressionContext outer) : IDocument
            {
                public void Inbound(IInboundContext context)
                {
                    context.Wait(null, branch =>
                    {
                        if (Missing(outer))
                        {
                            branch.SendRequest(new SendRequestConfig { ResponseVariableName = "request" });
                        }
                    });
                }

                bool Missing(IExpressionContext context) => !context.Variables.ContainsKey("cached");
            }
            """;

        AssertRejectedWait(CompileBranchDocument(code), "APIM2012", "outer expression context");
    }

    [TestMethod]
    public void ShouldRejectCapturedExpressionContextProperty()
    {
        var code = """
            [Document]
            public class PolicyDocument : IDocument
            {
                private IExpressionContext Outer { get; }

                public PolicyDocument(IExpressionContext outer)
                {
                    Outer = outer;
                }

                public void Inbound(IInboundContext context)
                {
                    context.Wait(null, branch => branch.SendRequest(new SendRequestConfig
                    {
                        ResponseVariableName = NameExp(Outer)
                    }));
                }

                string NameExp(IExpressionContext context) => context.Request.Method;
            }
            """;

        AssertRejectedWait(CompileBranchDocument(code), "APIM2012", "outer expression context");
    }

    [TestMethod]
    public void ShouldRejectOuterExpressionContextThroughBranchLocalAlias()
    {
        var code = CreateExpressionContextDocument(
            """
            branch =>
            {
                if (Always(branch.ExpressionContext))
                {
                    IExpressionContext alias = _outer;
                    if (Missing(alias))
                    {
                        branch.SendRequest(new SendRequestConfig { ResponseVariableName = "request" });
                    }
                }
            }
            """);

        AssertRejectedWait(CompileBranchDocument(code), "APIM2012", "outer expression context");
    }

    [TestMethod]
    public void ShouldRejectCapturedDerivedExpressionContext()
    {
        var code = """
            public interface ICapturedExpressionContext : IExpressionContext { }

            [Document]
            public class PolicyDocument : IDocument
            {
                private readonly ICapturedExpressionContext _outer;

                public PolicyDocument(ICapturedExpressionContext outer)
                {
                    _outer = outer;
                }

                public void Inbound(IInboundContext context)
                {
                    context.Wait(null, branch =>
                    {
                        if (Missing(_outer))
                        {
                            branch.SendRequest(new SendRequestConfig { ResponseVariableName = "request" });
                        }
                    });
                }

                bool Missing(IExpressionContext context) => !context.Variables.ContainsKey("cached");
            }
            """;

        AssertRejectedWait(CompileBranchDocument(code), "APIM2012", "outer expression context");
    }

    [TestMethod]
    [DataRow(
        """
        branch =>
        {
            if (Missing(branch.ExpressionContext))
            {
                branch.SendRequest(new SendRequestConfig { ResponseVariableName = "request" });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <wait>
                    <choose>
                        <when condition="@(!context.Variables.ContainsKey("cached"))">
                            <send-request response-variable-name="request" />
                        </when>
                    </choose>
                </wait>
            </inbound>
        </policies>
        """,
        DisplayName = "Allow branch expression context in Wait condition")]
    [DataRow(
        """
        branch =>
        {
            if (Missing(branch.WithId("projection").ExpressionContext))
            {
                branch.SendRequest(new SendRequestConfig { ResponseVariableName = "request" });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <wait>
                    <choose>
                        <when condition="@(!context.Variables.ContainsKey("cached"))">
                            <send-request response-variable-name="request" />
                        </when>
                    </choose>
                </wait>
            </inbound>
        </policies>
        """,
        DisplayName = "Allow WithId branch expression context in Wait condition")]
    [DataRow(
        """
        branch => branch.SendRequest(new SendRequestConfig { ResponseVariableName = NameExp(branch.ExpressionContext) })
        """,
        """
        <policies>
            <inbound>
                <wait>
                    <send-request response-variable-name="@(context.Request.Method)" />
                </wait>
            </inbound>
        </policies>
        """,
        DisplayName = "Allow branch expression context in Wait config")]
    [DataRow(
        """
        branch => branch.SendRequest(new SendRequestConfig
        {
            ResponseVariableName = NameExp(branch.WithId("projection").ExpressionContext)
        })
        """,
        """
        <policies>
            <inbound>
                <wait>
                    <send-request response-variable-name="@(context.Request.Method)" />
                </wait>
            </inbound>
        </policies>
        """,
        DisplayName = "Allow WithId branch expression context in Wait config")]
    public void ShouldAllowBranchExpressionContextProjection(string branch, string expectedXml)
    {
        CompileBranchDocument(CreateExpressionContextDocument(branch)).Should().BeSuccessful()
            .And.DocumentEquivalentTo(expectedXml);
    }

    [TestMethod]
    public void ShouldAllowNameofCapturedExpressionContext()
    {
        var code = CreateExpressionContextDocument(
            """
            branch => branch.WithId(nameof(_outer)).SendRequest(new SendRequestConfig
            {
                ResponseVariableName = "request"
            })
            """);
        var expectedXml = """
            <policies>
                <inbound>
                    <wait>
                        <send-request id="_outer" response-variable-name="request" />
                    </wait>
                </inbound>
            </policies>
            """;

        CompileBranchDocument(code).Should().BeSuccessful().And.DocumentEquivalentTo(expectedXml);
    }

    [TestMethod]
    public void ShouldAllowExpressionContextProjectionInNestedTypedWait()
    {
        var code = CreateExpressionContextDocument(
            """
            branch =>
            {
                if (Always(branch.ExpressionContext))
                {
                    branch.Wait(null, inner => inner.SendRequest(new SendRequestConfig
                    {
                        ResponseVariableName = NameExp(inner.WithId("projection").ExpressionContext)
                    }));
                }
            }
            """);
        var expectedXml = """
            <policies>
                <inbound>
                    <wait>
                        <choose>
                            <when condition="@(true)">
                                <wait>
                                    <send-request response-variable-name="@(context.Request.Method)" />
                                </wait>
                            </when>
                        </choose>
                    </wait>
                </inbound>
            </policies>
            """;

        CompileBranchDocument(code).Should().BeSuccessful().And.DocumentEquivalentTo(expectedXml);
    }

    private static string CreateExpressionContextDocument(string branch) => $$"""
        [Document]
        public class PolicyDocument : IDocument
        {
            private readonly IExpressionContext _outer;

            public PolicyDocument(IExpressionContext outer)
            {
                _outer = outer;
            }

            public void Inbound(IInboundContext context)
            {
                context.Wait(null, {{branch}});
            }

            bool Always(IExpressionContext context) => true;
            bool Missing(IExpressionContext context) => !context.Variables.ContainsKey("cached");
            string NameExp(IExpressionContext context) => context.Request.Method;
        }
        """;
}