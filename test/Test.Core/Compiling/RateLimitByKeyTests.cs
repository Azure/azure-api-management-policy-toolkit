// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling;

[TestClass]
public class RateLimitByKeyTests
{
    [TestMethod]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.RateLimitByKey(new RateLimitByKeyConfig()
                    {
                        Calls = 100,
                        RenewalPeriod = 10,
                        CounterKey = "key"
                    });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <rate-limit-by-key calls="100" renewal-period="10" counter-key="key" />
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile rate limit by key policy"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.RateLimitByKey(new RateLimitByKeyConfig()
                    {
                        Calls = CallsExp(context.ExpressionContext),
                        RenewalPeriod = RenewalPeriodExp(context.ExpressionContext),
                        CounterKey = CounterKeyExp(context.ExpressionContext)
                    });
            }
            
            int CallsExp(IExpressionContext context) => 100;
            int RenewalPeriodExp(IExpressionContext context) => 10;
            int CounterKeyExp(IExpressionContext context) => context.Product.Name;
        }
        """,
        """
        <policies>
            <inbound>
                <rate-limit-by-key calls="@(100)" renewal-period="@(10)" counter-key="@(context.Product.Name)" />
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile rate limit by key policy with expressions"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.RateLimitByKey(new RateLimitByKeyConfig()
                    {
                        Calls = 100,
                        RenewalPeriod = 10,
                        CounterKey = "key"
                        RetryAfterHeaderName = "header",
                    });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <rate-limit-by-key calls="100" renewal-period="10" counter-key="key" retry-after-header-name="header" />
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile rate limit by key policy with retry after header name"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.RateLimitByKey(new RateLimitByKeyConfig()
                    {
                        Calls = 100,
                        RenewalPeriod = 10,
                        CounterKey = "key"
                        RetryAfterVariableName = "variable",
                    });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <rate-limit-by-key calls="100" renewal-period="10" counter-key="key" retry-after-variable-name="variable" />
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile rate limit by key policy with retry after variable name"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.RateLimitByKey(new RateLimitByKeyConfig()
                    {
                        Calls = 100,
                        RenewalPeriod = 10,
                        CounterKey = "key"
                        RemainingCallsHeaderName = "header",
                    });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <rate-limit-by-key calls="100" renewal-period="10" counter-key="key" remaining-calls-header-name="header" />
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile rate limit by key policy with remaining calls header name"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.RateLimitByKey(new RateLimitByKeyConfig()
                    {
                        Calls = 100,
                        RenewalPeriod = 10,
                        CounterKey = "key"
                        RemainingCallsVariableName = "variable",
                    });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <rate-limit-by-key calls="100" renewal-period="10" counter-key="key" remaining-calls-variable-name="variable" />
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile rate limit by key policy with remaining calls variable name"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.RateLimitByKey(new RateLimitByKeyConfig()
                    {
                        Calls = 100,
                        RenewalPeriod = 10,
                        CounterKey = "key"
                        TotalCallsHeaderName = "header",
                    });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <rate-limit-by-key calls="100" renewal-period="10" counter-key="key" total-calls-header-name="header" />
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile rate limit by key policy with total calls header name"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.RateLimitByKey(new RateLimitByKeyConfig()
                    {
                        Calls = 100,
                        RenewalPeriod = 10,
                        CounterKey = "key"
                        IncrementCondition = ConditionExp(context.ExpressionContext),
                    });
            }
            bool ConditionExp(IExpressionContext context) => context.Product.Name == "test";
        }
        """,
        """
        <policies>
            <inbound>
                <rate-limit-by-key calls="100" renewal-period="10" counter-key="key" increment-condition="@(context.Product.Name == "test")" />
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile rate limit by key policy with increment conditions"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.RateLimitByKey(new RateLimitByKeyConfig()
                    {
                        Calls = 100,
                        RenewalPeriod = 10,
                        CounterKey = "key"
                        IncrementCount = 2,
                    });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <rate-limit-by-key calls="100" renewal-period="10" counter-key="key" increment-count="2" />
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile rate limit by key policy with increment count"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.RateLimitByKey(new RateLimitByKeyConfig()
                    {
                        Calls = 100,
                        RenewalPeriod = 10,
                        CounterKey = "key"
                        IncrementCount = IncrementExp(context.ExpressionContext),
                    });
            }
            int IncrementExp(IExpressionContext context) => context.Product.Name == "test" ? 2 : 1;
        }
        """,
        """
        <policies>
            <inbound>
                <rate-limit-by-key calls="100" renewal-period="10" counter-key="key" increment-count="@(context.Product.Name == "test" ? 2 : 1)" />
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile rate limit by key policy with expression in increment count"
    )]
    public void ShouldCompileRateLimitByKeyPolicy(string code, string expectedXml)
    {
        code.CompileDocument().Should().BeSuccessful().And.DocumentEquivalentTo(expectedXml);
    }
}