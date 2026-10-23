// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling;

[TestClass]
public class CacheValueTests
{
    [TestMethod]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context)
            {
                context.CacheValue(new CacheValueConfig()
                    {
                        Key = "my-key",
                        VariableName = "result",
                        ExpiresAfter = 28800,
                        RefreshAfter = 14400,
                        DefaultValue = "fallback",
                        CachingType = "prefer-external"
                    },
                    () =>
                    {
                        context.SetVariable("result", "computed-value");
                    });
            }

            public void Backend(IBackendContext context)
            {
                context.CacheValue(new CacheValueConfig()
                    {
                        Key = "my-key",
                        VariableName = "result",
                        ExpiresAfter = 28800,
                        RefreshAfter = 14400,
                        DefaultValue = "fallback",
                        CachingType = "prefer-external"
                    },
                    () =>
                    {
                        context.SetVariable("result", "computed-value");
                    });
            }

            public void Outbound(IOutboundContext context)
            {
                context.CacheValue(new CacheValueConfig()
                    {
                        Key = "my-key",
                        VariableName = "result",
                        ExpiresAfter = 28800,
                        RefreshAfter = 14400,
                        DefaultValue = "fallback",
                        CachingType = "prefer-external"
                    },
                    () =>
                    {
                        context.SetVariable("result", "computed-value");
                    });
            }

            public void OnError(IOnErrorContext context)
            {
                context.CacheValue(new CacheValueConfig()
                    {
                        Key = "my-key",
                        VariableName = "result",
                        ExpiresAfter = 28800,
                        RefreshAfter = 14400,
                        DefaultValue = "fallback",
                        CachingType = "prefer-external"
                    },
                    () =>
                    {
                        context.SetVariable("result", "computed-value");
                    });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <cache-value key="my-key" variable-name="result" expires-after="28800" refresh-after="14400" default-value="fallback" caching-type="prefer-external">
                    <value>
                        <set-variable name="result" value="computed-value" />
                    </value>
                </cache-value>
            </inbound>
            <backend>
                <cache-value key="my-key" variable-name="result" expires-after="28800" refresh-after="14400" default-value="fallback" caching-type="prefer-external">
                    <value>
                        <set-variable name="result" value="computed-value" />
                    </value>
                </cache-value>
            </backend>
            <outbound>
                <cache-value key="my-key" variable-name="result" expires-after="28800" refresh-after="14400" default-value="fallback" caching-type="prefer-external">
                    <value>
                        <set-variable name="result" value="computed-value" />
                    </value>
                </cache-value>
            </outbound>
            <on-error>
                <cache-value key="my-key" variable-name="result" expires-after="28800" refresh-after="14400" default-value="fallback" caching-type="prefer-external">
                    <value>
                        <set-variable name="result" value="computed-value" />
                    </value>
                </cache-value>
            </on-error>
        </policies>
        """,
        DisplayName = "Should compile cache-value policy in sections"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context)
            {
                context.CacheValue(new CacheValueConfig()
                    {
                        Key = "my-key",
                        VariableName = "result",
                        ExpiresAfter = 28800,
                        RefreshAfter = 14400,
                    },
                    () =>
                    {
                        context.SetVariable("result", "computed-value");
                    });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <cache-value key="my-key" variable-name="result" expires-after="28800" refresh-after="14400">
                    <value>
                        <set-variable name="result" value="computed-value" />
                    </value>
                </cache-value>
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile cache-value policy with required attributes only"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context)
            {
                context.CacheValue(new CacheValueConfig()
                    {
                        Key = KeyExp(context.ExpressionContext),
                        VariableName = "result",
                        ExpiresAfter = 28800,
                        RefreshAfter = 14400,
                    },
                    () =>
                    {
                        context.SetVariable("result", "computed-value");
                    });
            }

            string KeyExp(IExpressionContext context) => context.Product.Name;
        }
        """,
        """
        <policies>
            <inbound>
                <cache-value key="@(context.Product.Name)" variable-name="result" expires-after="28800" refresh-after="14400">
                    <value>
                        <set-variable name="result" value="computed-value" />
                    </value>
                </cache-value>
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile cache-value policy with expression in key"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context)
            {
                context.CacheValue(new CacheValueConfig()
                    {
                        Key = "my-key",
                        VariableName = "result",
                        RefreshAfter = 14400,
                        ExpiresAfter = 28800
                    },
                    () =>
                    {
                        context.SetVariable("result", "computed-value");
                    });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <cache-value key="my-key" variable-name="result" expires-after="28800" refresh-after="14400">
                    <value>
                        <set-variable name="result" value="computed-value" />
                    </value>
                </cache-value>
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile cache-value policy with expires-after"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context)
            {
                context.CacheValue(new CacheValueConfig()
                    {
                        Key = "my-key",
                        VariableName = "result",
                        RefreshAfter = 14400,
                        ExpiresAfter = ExpiresAfterExp(context.ExpressionContext)
                    },
                    () =>
                    {
                        context.SetVariable("result", "computed-value");
                    });
            }

            int ExpiresAfterExp(IExpressionContext context) => (int)context.Variables["ttl"];
        }
        """,
        """
        <policies>
            <inbound>
                <cache-value key="my-key" variable-name="result" expires-after="@((int)context.Variables["ttl"])" refresh-after="14400">
                    <value>
                        <set-variable name="result" value="computed-value" />
                    </value>
                </cache-value>
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile cache-value policy with expression in expires-after"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context)
            {
                context.CacheValue(new CacheValueConfig()
                    {
                        Key = "my-key",
                        VariableName = "result",
                        ExpiresAfter = 28800,
                        RefreshAfter = 14400
                    },
                    () =>
                    {
                        context.SetVariable("result", "computed-value");
                    });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <cache-value key="my-key" variable-name="result" expires-after="28800" refresh-after="14400">
                    <value>
                        <set-variable name="result" value="computed-value" />
                    </value>
                </cache-value>
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile cache-value policy with refresh-after"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context)
            {
                context.CacheValue(new CacheValueConfig()
                    {
                        Key = "my-key",
                        VariableName = "result",
                        ExpiresAfter = 28800,
                        RefreshAfter = RefreshAfterExp(context.ExpressionContext)
                    },
                    () =>
                    {
                        context.SetVariable("result", "computed-value");
                    });
            }

            int RefreshAfterExp(IExpressionContext context) => (int)context.Variables["refresh"];
        }
        """,
        """
        <policies>
            <inbound>
                <cache-value key="my-key" variable-name="result" expires-after="28800" refresh-after="@((int)context.Variables["refresh"])">
                    <value>
                        <set-variable name="result" value="computed-value" />
                    </value>
                </cache-value>
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile cache-value policy with expression in refresh-after"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context)
            {
                context.CacheValue(new CacheValueConfig()
                    {
                        Key = "my-key",
                        VariableName = "result",
                        ExpiresAfter = 28800,
                        RefreshAfter = 14400,
                        DefaultValue = "NotInCache"
                    },
                    () =>
                    {
                        context.SetVariable("result", "computed-value");
                    });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <cache-value key="my-key" variable-name="result" expires-after="28800" refresh-after="14400" default-value="NotInCache">
                    <value>
                        <set-variable name="result" value="computed-value" />
                    </value>
                </cache-value>
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile cache-value policy with default-value"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context)
            {
                context.CacheValue(new CacheValueConfig()
                    {
                        Key = "my-key",
                        VariableName = "result",
                        ExpiresAfter = 28800,
                        RefreshAfter = 14400,
                        DefaultValue = DefaultValueExp(context.ExpressionContext)
                    },
                    () =>
                    {
                        context.SetVariable("result", "computed-value");
                    });
            }

            string DefaultValueExp(IExpressionContext context) => (string)context.Variables["default"];
        }
        """,
        """
        <policies>
            <inbound>
                <cache-value key="my-key" variable-name="result" expires-after="28800" refresh-after="14400" default-value="@((string)context.Variables["default"])">
                    <value>
                        <set-variable name="result" value="computed-value" />
                    </value>
                </cache-value>
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile cache-value policy with expression in default-value"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context)
            {
                context.CacheValue(new CacheValueConfig()
                    {
                        Key = "my-key",
                        VariableName = "result",
                        ExpiresAfter = 28800,
                        RefreshAfter = 14400,
                        CachingType = "prefer-external"
                    },
                    () =>
                    {
                        context.SetVariable("result", "computed-value");
                    });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <cache-value key="my-key" variable-name="result" expires-after="28800" refresh-after="14400" caching-type="prefer-external">
                    <value>
                        <set-variable name="result" value="computed-value" />
                    </value>
                </cache-value>
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile cache-value policy with caching-type"
    )]
    public void ShouldCompileCacheValuePolicy(string code, string expectedXml)
    {
        code.CompileDocument().Should().BeSuccessful().And.DocumentEquivalentTo(expectedXml);
    }

    // API Management rejects cache-value without either duration when the policy is saved (Consumption gateway,
    // eastus, 2026-09-22).
    [TestMethod]
    [DataRow("ExpiresAfter = 60", "RefreshAfter")]
    [DataRow("RefreshAfter = 30", "ExpiresAfter")]
    public void ShouldReportMissingDuration(string duration, string missing)
    {
        var result = $$"""
                       [Document]
                       public class PolicyDocument : IDocument
                       {
                           public void Inbound(IInboundContext context)
                           {
                               context.CacheValue(new CacheValueConfig { Key = "k", VariableName = "v", {{duration}} },
                                   () =>
                                   {
                                       context.SetVariable("v", "computed");
                                   });
                           }
                       }
                       """.CompileDocument();

        result.Errors.Should().ContainSingle(error => error.Id == "APIM2006" && error.GetMessage(null).Contains(missing));
    }
}
