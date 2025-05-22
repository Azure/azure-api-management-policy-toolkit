// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling;

[TestClass]
public class QuotaByKeyTests
{
    [TestMethod]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.QuotaByKey(new QuotaByKeyConfig()
                    {
                        CounterKey = "my-key",
                        Calls = 100,
                        RenewalPeriod = 300,
                    });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <quota-by-key counter-key="my-key" calls="100" renewal-period="300" />
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile quota-by-key policy with calls"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.QuotaByKey(new QuotaByKeyConfig()
                    {
                        CounterKey = "my-key",
                        Bandwidth = 100,
                        RenewalPeriod = 300,
                    });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <quota-by-key counter-key="my-key" bandwidth="100" renewal-period="300" />
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile quota-by-key policy with bandwidth"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.QuotaByKey(new QuotaByKeyConfig()
                    {
                        CounterKey = "my-key",
                        Calls = 100,
                        Bandwidth = 101,
                        RenewalPeriod = 300,
                    });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <quota-by-key counter-key="my-key" calls="100" bandwidth="101" renewal-period="300" />
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile quota-by-key policy with calls and bandwidth"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.QuotaByKey(new QuotaByKeyConfig()
                    {
                        CounterKey = GetCounterKey(context.ExpressionContext),
                        Calls = 100,
                        RenewalPeriod = 300,
                    });
            }
            string GetCounterKey(IExpressionContext context) =>
                context.Variables["my-key"].ToString();
        }
        """,
        """
        <policies>
            <inbound>
                <quota-by-key counter-key="@(context.Variables["my-key"].ToString())" calls="100" renewal-period="300" />
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile quota-by-key policy with expression for counter-key"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.QuotaByKey(new QuotaByKeyConfig()
                    {
                        CounterKey = "my-key",
                        Calls = 100,
                        RenewalPeriod = 300,
                        IncrementCondition = true,
                    });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <quota-by-key counter-key="my-key" calls="100" renewal-period="300" increment-condition="true" />
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile quota-by-key policy with increment-condition"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.QuotaByKey(new QuotaByKeyConfig()
                    {
                        CounterKey = "my-key",
                        Calls = 100,
                        RenewalPeriod = 300,
                        IncrementCondition = ShouldIncrement(context.ExpressionContext),
                    });
            }
            bool ShouldIncrement(IExpressionContext context) =>
                !context.User.Email.EndsWith("@contoso.example");
        }
        """,
        """
        <policies>
            <inbound>
                <quota-by-key counter-key="my-key" calls="100" renewal-period="300" increment-condition="@(!context.User.Email.EndsWith("@contoso.example"))" />
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile quota-by-key policy with expression in increment-condition"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.QuotaByKey(new QuotaByKeyConfig()
                    {
                        CounterKey = "my-key",
                        Calls = 100,
                        RenewalPeriod = 300,
                        IncrementCount = 5,
                    });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <quota-by-key counter-key="my-key" calls="100" renewal-period="300" increment-count="5" />
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile quota-by-key policy with increment-count"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.QuotaByKey(new QuotaByKeyConfig()
                    {
                        CounterKey = "my-key",
                        Calls = 100,
                        RenewalPeriod = 300,
                        IncrementCount = GetIncrementCount(context.ExpressionContext),
                    });
            }
            bool GetIncrementCount(IExpressionContext context) =>
                !context.User.Email.EndsWith("@contoso.example") ? 5 : 10;
        }
        """,
        """
        <policies>
            <inbound>
                <quota-by-key counter-key="my-key" calls="100" renewal-period="300" increment-count="@(!context.User.Email.EndsWith("@contoso.example") ? 5 : 10)" />
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile quota-by-key policy with expression in increment-count"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.QuotaByKey(new QuotaByKeyConfig()
                    {
                        CounterKey = "my-key",
                        Calls = 100,
                        RenewalPeriod = 300,
                        FirstPeriodStart = "2025-01-01T00:00:00Z",
                    });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <quota-by-key counter-key="my-key" calls="100" renewal-period="300" first-period-start="2025-01-01T00:00:00Z" />
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile quota-by-key policy with first-period-start"
    )]
    public void ShouldCompileQuotaByKeyPolicy(string code, string expectedXml)
    {
        code.CompileDocument().Should().BeSuccessful().And.DocumentEquivalentTo(expectedXml);
    }
}