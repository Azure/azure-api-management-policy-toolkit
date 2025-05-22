// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling;

[TestClass]
public class RetryTests
{
    [TestMethod]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context)
            {
                context.Retry(new RetryConfig()
                    {
                        Condition = Condition(context.ExpressionContext),
                        Count = 10,
                        Interval = 1
                    },
                    () =>
                    {
                        context.AuthenticationManagedIdentity(new ManagedIdentityAuthenticationConfig { Resource = "resource" });
                    });
            }

            public void Backend(IBackendContext context)
            {
                context.Retry(new RetryConfig()
                    {
                        Condition = Condition(context.ExpressionContext),
                        Count = 10,
                        Interval = 1
                    },
                    () =>
                    {
                        context.ForwardRequest();
                    });
            }
            
            public void Outbound(IOutboundContext context)
            {
                context.Retry(new RetryConfig()
                    {
                        Condition = Condition(context.ExpressionContext),
                        Count = 10,
                        Interval = 1
                    },
                    () =>
                    {
                        context.SendRequest(new SendRequestConfig {
                            ResponseVariableName = "variable"
                        });
                    });
            }

            public void OnError(IOnErrorContext context)
            {
                context.Retry(new RetryConfig()
                    {
                        Condition = Condition(context.ExpressionContext),
                        Count = 10,
                        Interval = 1
                    },
                    () =>
                    {
                        context.SendRequest(new SendRequestConfig {
                            ResponseVariableName = "variable"
                        });
                    });
            }

            bool Condition(IExpressionContext context) => context.Variables.ContainsKey("retry");
        }
        """,
        """
        <policies>
            <inbound>
                <retry condition="@(context.Variables.ContainsKey("retry"))" count="10" interval="1">
                    <authentication-managed-identity resource="resource" />
                </retry>
            </inbound>
            <backend>
                <retry condition="@(context.Variables.ContainsKey("retry"))" count="10" interval="1">
                    <forward-request />
                </retry>
            </backend>
            <outbound>
                <retry condition="@(context.Variables.ContainsKey("retry"))" count="10" interval="1">
                    <send-request response-variable-name="variable" />
                </retry>
            </outbound>
            <on-error>
                <retry condition="@(context.Variables.ContainsKey("retry"))" count="10" interval="1">
                    <send-request response-variable-name="variable" />
                </retry>
            </on-error>
        </policies>
        """,
        DisplayName = "Should compile retry policy in sections"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context)
            {
                context.Retry(new RetryConfig()
                    {
                        Condition = Condition(context.ExpressionContext),
                        Count = GetRetryCount(context.ExpressionContext),
                        Interval = 1
                    },
                    () =>
                    {
                        context.AuthenticationManagedIdentity(new ManagedIdentityAuthenticationConfig { Resource = "resource" });
                    });
            }

            bool Condition(IExpressionContext context) => context.Variables.ContainsKey("retry");
            int GetRetryCount(IExpressionContext context) => (int)context.Variables["retry-count"];
        }
        """,
        """
        <policies>
            <inbound>
                <retry condition="@(context.Variables.ContainsKey("retry"))" count="@((int)context.Variables["retry-count"])" interval="1">
                    <authentication-managed-identity resource="resource" />
                </retry>
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile retry policy with expression in count"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context)
            {
                context.Retry(new RetryConfig()
                    {
                        Condition = Condition(context.ExpressionContext),
                        Count = 10,
                        Interval = GetInterval(context.ExpressionContext)
                    },
                    () =>
                    {
                        context.AuthenticationManagedIdentity(new ManagedIdentityAuthenticationConfig { Resource = "resource" });
                    });
            }

            bool Condition(IExpressionContext context) => context.Variables.ContainsKey("retry");
            int GetInterval(IExpressionContext context) => (int)context.Variables["retry-interval"];
        }
        """,
        """
        <policies>
            <inbound>
                <retry condition="@(context.Variables.ContainsKey("retry"))" count="10" interval="@((int)context.Variables["retry-interval"])">
                    <authentication-managed-identity resource="resource" />
                </retry>
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile retry policy with expression in interval"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context)
            {
                context.Retry(new RetryConfig()
                    {
                        Condition = Condition(context.ExpressionContext),
                        Count = 10,
                        Interval = 1,
                        MaxInterval = 5
                    },
                    () =>
                    {
                        context.AuthenticationManagedIdentity(new ManagedIdentityAuthenticationConfig { Resource = "resource" });
                    });
            }

            bool Condition(IExpressionContext context) => context.Variables.ContainsKey("retry");
        }
        """,
        """
        <policies>
            <inbound>
                <retry condition="@(context.Variables.ContainsKey("retry"))" count="10" interval="1" max-interval="5">
                    <authentication-managed-identity resource="resource" />
                </retry>
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile retry policy with max-interval"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context)
            {
                context.Retry(new RetryConfig()
                    {
                        Condition = Condition(context.ExpressionContext),
                        Count = 10,
                        Interval = 1,
                        MaxInterval = GetMaxInterval(context.ExpressionContext),
                    },
                    () =>
                    {
                        context.AuthenticationManagedIdentity(new ManagedIdentityAuthenticationConfig { Resource = "resource" });
                    });
            }

            bool Condition(IExpressionContext context) => context.Variables.ContainsKey("retry");
            int GetMaxInterval(IExpressionContext context) => (int)context.Variables["retry-max-interval"];
        }
        """,
        """
        <policies>
            <inbound>
                <retry condition="@(context.Variables.ContainsKey("retry"))" count="10" interval="1" max-interval="@((int)context.Variables["retry-max-interval"])">
                    <authentication-managed-identity resource="resource" />
                </retry>
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile retry policy with expression in max-interval"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context)
            {
                context.Retry(new RetryConfig()
                    {
                        Condition = Condition(context.ExpressionContext),
                        Count = 10,
                        Interval = 1,
                        Delta = 2,
                    },
                    () =>
                    {
                        context.AuthenticationManagedIdentity(new ManagedIdentityAuthenticationConfig { Resource = "resource" });
                    });
            }

            bool Condition(IExpressionContext context) => context.Variables.ContainsKey("retry");
        }
        """,
        """
        <policies>
            <inbound>
                <retry condition="@(context.Variables.ContainsKey("retry"))" count="10" interval="1" delta="2">
                    <authentication-managed-identity resource="resource" />
                </retry>
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile retry policy with delta"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context)
            {
                context.Retry(new RetryConfig()
                    {
                        Condition = Condition(context.ExpressionContext),
                        Count = 10,
                        Interval = 1,
                        Delta = GetDelta(context.ExpressionContext),
                    },
                    () =>
                    {
                        context.AuthenticationManagedIdentity(new ManagedIdentityAuthenticationConfig { Resource = "resource" });
                    });
            }

            bool Condition(IExpressionContext context) => context.Variables.ContainsKey("retry");
            int GetDelta(IExpressionContext context) => (int)context.Variables["retry-delta"];
        }
        """,
        """
        <policies>
            <inbound>
                <retry condition="@(context.Variables.ContainsKey("retry"))" count="10" interval="1" delta="@((int)context.Variables["retry-delta"])">
                    <authentication-managed-identity resource="resource" />
                </retry>
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile retry policy with expression in delta"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context)
            {
                context.Retry(new RetryConfig()
                    {
                        Condition = Condition(context.ExpressionContext),
                        Count = 10,
                        Interval = 1,
                        FirstFastRetry = true,
                    },
                    () =>
                    {
                        context.AuthenticationManagedIdentity(new ManagedIdentityAuthenticationConfig { Resource = "resource" });
                    });
            }

            bool Condition(IExpressionContext context) => context.Variables.ContainsKey("retry");
        }
        """,
        """
        <policies>
            <inbound>
                <retry condition="@(context.Variables.ContainsKey("retry"))" count="10" interval="1" first-fast-retry="true">
                    <authentication-managed-identity resource="resource" />
                </retry>
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile retry policy with first-fast-retry"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context)
            {
                context.Retry(new RetryConfig()
                    {
                        Condition = Condition(context.ExpressionContext),
                        Count = 10,
                        Interval = 1,
                        FirstFastRetry = GetFirstFastRetry(context.ExpressionContext),
                    },
                    () =>
                    {
                        context.AuthenticationManagedIdentity(new ManagedIdentityAuthenticationConfig { Resource = "resource" });
                    });
            }

            bool Condition(IExpressionContext context) => context.Variables.ContainsKey("retry");
            bool GetFirstFastRetry(IExpressionContext context) => (bool)context.Variables["retry-first-fast-retry"];
        }
        """,
        """
        <policies>
            <inbound>
                <retry condition="@(context.Variables.ContainsKey("retry"))" count="10" interval="1" first-fast-retry="@((bool)context.Variables["retry-first-fast-retry"])">
                    <authentication-managed-identity resource="resource" />
                </retry>
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile retry policy with expression in first-fast-retry"
    )]
    public void ShouldCompileRetryPolicy(string code, string expectedXml)
    {
        code.CompileDocument().Should().BeSuccessful().And.DocumentEquivalentTo(expectedXml);
    }
}