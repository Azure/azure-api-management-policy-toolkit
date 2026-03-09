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
                        VariableName = "result"
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
                <cache-value key="my-key" variable-name="result">
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
                        VariableName = "result"
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
                <cache-value key="@(context.Product.Name)" variable-name="result">
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
                <cache-value key="my-key" variable-name="result" expires-after="28800">
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
                <cache-value key="my-key" variable-name="result" expires-after="@((int)context.Variables["ttl"])">
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
                <cache-value key="my-key" variable-name="result" refresh-after="14400">
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
                <cache-value key="my-key" variable-name="result" refresh-after="@((int)context.Variables["refresh"])">
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
                <cache-value key="my-key" variable-name="result" default-value="NotInCache">
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
                <cache-value key="my-key" variable-name="result" default-value="@((string)context.Variables["default"])">
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
                <cache-value key="my-key" variable-name="result" caching-type="prefer-external">
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
}
