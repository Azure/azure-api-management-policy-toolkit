// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling;

[TestClass]
public class AzureOpenAiTokenLimitTests
{
    [TestMethod]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context)
            {
                context.AzureOpenAiTokenLimit(new TokenLimitConfig
                {
                    CounterKey = "counter-key",
                    EstimatePromptToken = false,
                    TokensPerMinute = 5000,
                });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <azure-openai-token-limit counter-key="counter-key" estimate-prompt-token="false" tokens-per-minute="5000" />
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile azure-openai-token-limit policy in section"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context)
            {
                context.AzureOpenAiTokenLimit(new TokenLimitConfig
                {
                    CounterKey = "user-token-counter",
                    EstimatePromptToken = true,
                    TokenQuota = 10000,
                    TokenQuotaPeriod = "Hourly",
                });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <azure-openai-token-limit counter-key="user-token-counter" estimate-prompt-token="true" token-quota="10000" token-quota-period="Hourly" />
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile azure-openai-token-limit policy with token-quota and period"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context)
            {
                context.AzureOpenAiTokenLimit(new TokenLimitConfig
                {
                    CounterKey = GetCounterKey(context.ExpressionContext),
                    EstimatePromptToken = true,
                    TokensPerMinute = 5000,
                });
            }

            string GetCounterKey(IExpressionContext context) => context.User.Id + "-token-counter";
        }
        """,
        """
        <policies>
            <inbound>
                <azure-openai-token-limit counter-key="@(context.User.Id + "-token-counter")" estimate-prompt-token="true" tokens-per-minute="5000" />
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile azure-openai-token-limit policy with expression in counter-key"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context)
            {
                context.AzureOpenAiTokenLimit(new TokenLimitConfig
                {
                    CounterKey = "counter-key",
                    EstimatePromptToken = ShouldEstimateToken(context.ExpressionContext),
                    TokensPerMinute = 5000,
                });
            }

            bool ShouldEstimateToken(IExpressionContext context) => context.Request.Headers.ContainsKey("X-Estimate-Tokens");
        }
        """,
        """
        <policies>
            <inbound>
                <azure-openai-token-limit counter-key="counter-key" estimate-prompt-token="@(context.Request.Headers.ContainsKey("X-Estimate-Tokens"))" tokens-per-minute="5000" />
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile azure-openai-token-limit policy with expression in estimate-prompt-token"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context)
            {
                context.AzureOpenAiTokenLimit(new TokenLimitConfig
                {
                    CounterKey = "counter-key",
                    EstimatePromptToken = true,
                    TokensPerMinute = GetTokenRate(context.ExpressionContext),
                });
            }

            int GetTokenRate(IExpressionContext context) => context.User.Groups.Contains("premium") ? 10000 : 5000;
        }
        """,
        """
        <policies>
            <inbound>
                <azure-openai-token-limit counter-key="counter-key" estimate-prompt-token="true" tokens-per-minute="@(context.User.Groups.Contains("premium") ? 10000 : 5000)" />
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile azure-openai-token-limit policy with expression in tokens-per-minute"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context)
            {
                context.AzureOpenAiTokenLimit(new TokenLimitConfig
                {
                    CounterKey = "counter-key",
                    EstimatePromptToken = true,
                    TokenQuota = GetQuota(context.ExpressionContext),
                    TokenQuotaPeriod = "Daily"
                });
            }

            int GetQuota(IExpressionContext context) => context.User.Groups.Contains("premium") ? 50000 : 20000;
        }
        """,
        """
        <policies>
            <inbound>
                <azure-openai-token-limit counter-key="counter-key" estimate-prompt-token="true" token-quota="@(context.User.Groups.Contains("premium") ? 50000 : 20000)" token-quota-period="Daily" />
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile azure-openai-token-limit policy with expression in token-quota"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context)
            {
                context.AzureOpenAiTokenLimit(new TokenLimitConfig
                {
                    CounterKey = "counter-key",
                    EstimatePromptToken = true,
                    TokensPerMinute = 5000,
                    RetryAfterHeaderName = "X-Retry-After",
                    RetryAfterVariableName = "retryAfter",
                });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <azure-openai-token-limit counter-key="counter-key" estimate-prompt-token="true" tokens-per-minute="5000" retry-after-header-name="X-Retry-After" retry-after-variable-name="retryAfter" />
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile azure-openai-token-limit policy with retry-after configurations"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context)
            {
                context.AzureOpenAiTokenLimit(new TokenLimitConfig
                {
                    CounterKey = "counter-key",
                    EstimatePromptToken = true,
                    TokenQuota = 10000,
                    TokenQuotaPeriod = "Hourly",
                    RemainingQuotaTokensHeaderName = "X-Remaining-Quota-Tokens",
                    RemainingQuotaTokensVariableName = "remainingQuotaTokens"
                });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <azure-openai-token-limit counter-key="counter-key" estimate-prompt-token="true" token-quota="10000" token-quota-period="Hourly" remaining-quota-tokens-header-name="X-Remaining-Quota-Tokens" remaining-quota-tokens-variable-name="remainingQuotaTokens" />
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile azure-openai-token-limit policy with remaining-quota-tokens configurations"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context)
            {
                context.AzureOpenAiTokenLimit(new TokenLimitConfig
                {
                    CounterKey = "counter-key",
                    EstimatePromptToken = true,
                    TokensPerMinute = 5000,
                    RemainingTokensHeaderName = "X-Remaining-Tokens",
                    RemainingTokensVariableName = "remainingTokens"
                });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <azure-openai-token-limit counter-key="counter-key" estimate-prompt-token="true" tokens-per-minute="5000" remaining-tokens-header-name="X-Remaining-Tokens" remaining-tokens-variable-name="remainingTokens" />
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile azure-openai-token-limit policy with remaining-tokens configurations"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context)
            {
                context.AzureOpenAiTokenLimit(new TokenLimitConfig
                {
                    CounterKey = "counter-key",
                    EstimatePromptToken = true,
                    TokensPerMinute = 5000,
                    TokensConsumedHeaderName = "X-Tokens-Consumed",
                    TokensConsumedVariableName = "tokensConsumed"
                });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <azure-openai-token-limit counter-key="counter-key" estimate-prompt-token="true" tokens-per-minute="5000" tokens-consumed-header-name="X-Tokens-Consumed" tokens-consumed-variable-name="tokensConsumed" />
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile azure-openai-token-limit policy with tokens-consumed configurations"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context)
            {
                context.AzureOpenAiTokenLimit(new TokenLimitConfig
                {
                    CounterKey = "comprehensive-counter",
                    EstimatePromptToken = true,
                    TokenQuota = 20000,
                    TokenQuotaPeriod = "Daily",
                    RetryAfterHeaderName = "X-Retry-After",
                    RetryAfterVariableName = "retryAfter",
                    RemainingQuotaTokensHeaderName = "X-Remaining-Quota",
                    RemainingQuotaTokensVariableName = "remainingQuota",
                    RemainingTokensHeaderName = "X-Remaining-Tokens",
                    RemainingTokensVariableName = "remainingTokens",
                    TokensConsumedHeaderName = "X-Consumed-Tokens",
                    TokensConsumedVariableName = "consumedTokens"
                });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <azure-openai-token-limit counter-key="comprehensive-counter" estimate-prompt-token="true" token-quota="20000" token-quota-period="Daily" retry-after-header-name="X-Retry-After" retry-after-variable-name="retryAfter" remaining-quota-tokens-header-name="X-Remaining-Quota" remaining-quota-tokens-variable-name="remainingQuota" remaining-tokens-header-name="X-Remaining-Tokens" remaining-tokens-variable-name="remainingTokens" tokens-consumed-header-name="X-Consumed-Tokens" tokens-consumed-variable-name="consumedTokens" />
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile azure-openai-token-limit policy with comprehensive configuration"
    )]
    public void ShouldCompileAzureOpenAiTokenLimitPolicy(string code, string expectedXml)
    {
        code.CompileDocument().Should().BeSuccessful().And.DocumentEquivalentTo(expectedXml);
    }
}