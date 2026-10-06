// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling;

[TestClass]
public class LlmTokenLimitTests
{
    [TestMethod]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context)
            {
                context.LlmTokenLimit(new TokenLimitConfig
                {
                    CounterKey = "counter-key",
                    EstimatePromptTokens = false,
                    TokensPerMinute = 5000,
                });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <llm-token-limit counter-key="counter-key" estimate-prompt-tokens="false" tokens-per-minute="5000" />
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile llm-token-limit policy in section"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context)
            {
                context.LlmTokenLimit(new TokenLimitConfig
                {
                    CounterKey = "user-token-counter",
                    EstimatePromptTokens = true,
                    TokenQuota = 10000,
                    TokenQuotaPeriod = "Hourly",
                });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <llm-token-limit counter-key="user-token-counter" estimate-prompt-tokens="true" token-quota="10000" token-quota-period="Hourly" />
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile llm-token-limit policy with token-quota and period"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context)
            {
                context.LlmTokenLimit(new TokenLimitConfig
                {
                    CounterKey = GetCounterKey(context.ExpressionContext),
                    EstimatePromptTokens = true,
                    TokensPerMinute = 5000,
                });
            }

            string GetCounterKey(IExpressionContext context) => context.User.Id + "-token-counter";
        }
        """,
        """
        <policies>
            <inbound>
                <llm-token-limit counter-key="@(context.User.Id + "-token-counter")" estimate-prompt-tokens="true" tokens-per-minute="5000" />
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile llm-token-limit policy with expression in counter-key"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context)
            {
                context.LlmTokenLimit(new TokenLimitConfig
                {
                    CounterKey = "counter-key",
                    EstimatePromptTokens = ShouldEstimateToken(context.ExpressionContext),
                    TokensPerMinute = 5000,
                });
            }

            bool ShouldEstimateToken(IExpressionContext context) => context.Request.Headers.ContainsKey("X-Estimate-Tokens");
        }
        """,
        """
        <policies>
            <inbound>
                <llm-token-limit counter-key="counter-key" estimate-prompt-tokens="@(context.Request.Headers.ContainsKey("X-Estimate-Tokens"))" tokens-per-minute="5000" />
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile llm-token-limit policy with expression in estimate-prompt-tokens"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context)
            {
                context.LlmTokenLimit(new TokenLimitConfig
                {
                    CounterKey = "counter-key",
                    EstimatePromptTokens = true,
                    TokensPerMinute = GetTokenRate(context.ExpressionContext),
                });
            }

            int GetTokenRate(IExpressionContext context) => context.User.Groups.Contains("premium") ? 10000 : 5000;
        }
        """,
        """
        <policies>
            <inbound>
                <llm-token-limit counter-key="counter-key" estimate-prompt-tokens="true" tokens-per-minute="@(context.User.Groups.Contains("premium") ? 10000 : 5000)" />
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile llm-token-limit policy with expression in tokens-per-minute"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context)
            {
                context.LlmTokenLimit(new TokenLimitConfig
                {
                    CounterKey = "counter-key",
                    EstimatePromptTokens = true,
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
                <llm-token-limit counter-key="counter-key" estimate-prompt-tokens="true" token-quota="@((long)(context.User.Groups.Contains("premium") ? 50000 : 20000))" token-quota-period="Daily" />
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile llm-token-limit policy with expression in token-quota"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context)
            {
                context.LlmTokenLimit(new TokenLimitConfig
                {
                    CounterKey = "counter-key",
                    EstimatePromptTokens = true,
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
                <llm-token-limit counter-key="counter-key" estimate-prompt-tokens="true" tokens-per-minute="5000" retry-after-header-name="X-Retry-After" retry-after-variable-name="retryAfter" />
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile llm-token-limit policy with retry-after configurations"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context)
            {
                context.LlmTokenLimit(new TokenLimitConfig
                {
                    CounterKey = "counter-key",
                    EstimatePromptTokens = true,
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
                <llm-token-limit counter-key="counter-key" estimate-prompt-tokens="true" token-quota="10000" token-quota-period="Hourly" remaining-quota-tokens-header-name="X-Remaining-Quota-Tokens" remaining-quota-tokens-variable-name="remainingQuotaTokens" />
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile llm-token-limit policy with remaining-quota-tokens configurations"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context)
            {
                context.LlmTokenLimit(new TokenLimitConfig
                {
                    CounterKey = "counter-key",
                    EstimatePromptTokens = true,
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
                <llm-token-limit counter-key="counter-key" estimate-prompt-tokens="true" tokens-per-minute="5000" remaining-tokens-header-name="X-Remaining-Tokens" remaining-tokens-variable-name="remainingTokens" />
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile llm-token-limit policy with remaining-tokens configurations"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context)
            {
                context.LlmTokenLimit(new TokenLimitConfig
                {
                    CounterKey = "counter-key",
                    EstimatePromptTokens = true,
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
                <llm-token-limit counter-key="counter-key" estimate-prompt-tokens="true" tokens-per-minute="5000" tokens-consumed-header-name="X-Tokens-Consumed" tokens-consumed-variable-name="tokensConsumed" />
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile llm-token-limit policy with tokens-consumed configurations"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context)
            {
                context.LlmTokenLimit(new TokenLimitConfig
                {
                    CounterKey = "comprehensive-counter",
                    EstimatePromptTokens = true,
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
                <llm-token-limit counter-key="comprehensive-counter" estimate-prompt-tokens="true" token-quota="20000" token-quota-period="Daily" retry-after-header-name="X-Retry-After" retry-after-variable-name="retryAfter" remaining-quota-tokens-header-name="X-Remaining-Quota" remaining-quota-tokens-variable-name="remainingQuota" remaining-tokens-header-name="X-Remaining-Tokens" remaining-tokens-variable-name="remainingTokens" tokens-consumed-header-name="X-Consumed-Tokens" tokens-consumed-variable-name="consumedTokens" />
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile llm-token-limit policy with comprehensive configuration"
    )]
    public void ShouldCompileLlmTokenLimitPolicy(string code, string expectedXml)
    {
        code.CompileDocument().Should().BeSuccessful().And.DocumentEquivalentTo(expectedXml);
    }

    [TestMethod]
    public void ShouldCompileRateLimitTogetherWithQuota()
    {
        var code = CompilerTestInitialize.InboundDocument(
            """
            context.LlmTokenLimit(new TokenLimitConfig
            {
                CounterKey = "key",
                EstimatePromptTokens = true,
                TokensPerMinute = 100,
                TokenQuota = 1000,
                TokenQuotaPeriod = "Daily"
            });
            """);

        code.CompileDocument().Should().BeSuccessful().And.DocumentEquivalentTo(CompilerTestInitialize.InboundXml(
            """
            <llm-token-limit counter-key="key" estimate-prompt-tokens="true" tokens-per-minute="100" token-quota="1000" token-quota-period="Daily" />
            """));
    }

    [TestMethod]
    public void ShouldConvertEveryReturnOfAMultiStatementQuotaHelper()
    {
        var code =
            """
            [Document]
            public class PolicyDocument : IDocument
            {
                public void Inbound(IInboundContext context)
                {
                    context.LlmTokenLimit(new TokenLimitConfig
                    {
                        CounterKey = "counter-key",
                        EstimatePromptTokens = true,
                        TokenQuota = GetQuota(context.ExpressionContext),
                        TokenQuotaPeriod = "Daily"
                    });
                }

                int GetQuota(IExpressionContext context)
                {
                    if (context.User.Groups.Count() > 1)
                    {
                        return 50000;
                    }

                    var each = context.User.Groups.Select(group => { return 1; }).Sum();
                    return each * 1000;
                }
            }
            """;

        var result = code.CompileDocument();

        result.Should().BeSuccessful();
        var quota = result.Document.Descendants("llm-token-limit").Single().Attribute("token-quota")!.Value;
        quota.Should().Contain("return (long)(50000);").And.Contain("return (long)(each * 1000);")
            .And.Contain("return 1;");
    }

    [TestMethod]
    public void ShouldConvertAQuotaHelperThatReadsANamedValue()
    {
        var code =
            """
            [Document]
            public class PolicyDocument : IDocument
            {
                public void Inbound(IInboundContext context)
                {
                    context.LlmTokenLimit(new TokenLimitConfig
                    {
                        CounterKey = "a",
                        EstimatePromptTokens = true,
                        TokenQuota = Block(context.ExpressionContext),
                        TokenQuotaPeriod = "Daily"
                    });
                    context.SetVariable("b", Wide(context.ExpressionContext));
                }

                int Block(IExpressionContext context)
                {
                    if (context.User.Groups.Count() > 1)
                    {
                        return 1;
                    }

                    return (int)context.NamedValue("n");
                }

                long Wide(IExpressionContext context) => Narrow(context);

                [NamedValue("n")]
                int Narrow(IExpressionContext context) => context.NamedValue("n");
            }
            """;

        var result = code.CompileDocument();

        result.Should().BeSuccessful();
        var quota = result.Document.Descendants("llm-token-limit").Single().Attribute("token-quota")!.Value;
        quota.Should().Contain("return (long)(1);").And.Contain("return (long)((int)({{n}}));");
        // the cast applies to all of the text the named value stands for
        result.Document.Descendants("set-variable").Single().Attribute("value")!.Value.Should().Contain("(long)({{n}})");
    }

    [TestMethod]
    public void ShouldReportTokenLimitWithoutRateOrQuota()
    {
        var result = CompilerTestInitialize.InboundDocument(
            """
            context.AzureOpenAiTokenLimit(new TokenLimitConfig
            {
                CounterKey = "key",
                EstimatePromptTokens = true
            });
            """).CompileDocument();

        result.Errors.Should().ContainSingle(error =>
            error.Id == "APIM9996" && error.GetMessage(null).Contains("azure-openai-token-limit"));
    }
}
