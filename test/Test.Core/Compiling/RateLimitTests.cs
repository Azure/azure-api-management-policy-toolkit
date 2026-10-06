// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling;

[TestClass]
public class RateLimitTests
{
    [TestMethod]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.RateLimit(new RateLimitConfig()
                    {
                        Calls = 100,
                        RenewalPeriod = 10
                    });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <rate-limit calls="100" renewal-period="10" />
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile rate limit policy"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.RateLimit(new RateLimitConfig()
                    {
                        Calls = 100,
                        RenewalPeriod = 10,
                        RetryAfterHeaderName = "header",
                    });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <rate-limit calls="100" renewal-period="10" retry-after-header-name="header" />
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile rate limit policy with retry after header name"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.RateLimit(new RateLimitConfig()
                    {
                        Calls = 100,
                        RenewalPeriod = 10,
                        RetryAfterVariableName = "variable"
                    });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <rate-limit calls="100" renewal-period="10" retry-after-variable-name="variable" />
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile rate limit policy with retry after variable name"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.RateLimit(new RateLimitConfig()
                    {
                        Calls = 100,
                        RenewalPeriod = 10,
                        RemainingCallsHeaderName = "header"
                    });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <rate-limit calls="100" renewal-period="10" remaining-calls-header-name="header" />
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile rate limit policy with remaining calls header name"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.RateLimit(new RateLimitConfig()
                    {
                        Calls = 100,
                        RenewalPeriod = 10,
                        RemainingCallsVariableName = "variable"
                    });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <rate-limit calls="100" renewal-period="10" remaining-calls-variable-name="variable" />
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile rate limit policy with remaining calls variable name"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.RateLimit(new RateLimitConfig()
                    {
                        Calls = 100,
                        RenewalPeriod = 10,
                        TotalCallsHeaderName = "header"
                    });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <rate-limit calls="100" renewal-period="10" total-calls-header-name="header" />
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile rate limit policy with total calls header name"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.RateLimit(new RateLimitConfig()
                    {
                        Calls = 1000,
                        RenewalPeriod = 60,
                        Apis = [
                            new ApiRateLimit()
                            {
                                Name = "echo-api-name",
                                Calls = 50,
                                RenewalPeriod = 10,
                            },
                            new ApiRateLimit()
                            {
                                Id = "weather-forecast-api-id",
                                Calls = 10,
                                RenewalPeriod = 5,
                            }
                        ],
                    });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <rate-limit calls="1000" renewal-period="60">
                    <api name="echo-api-name" calls="50" renewal-period="10" />
                    <api id="weather-forecast-api-id" calls="10" renewal-period="5" />
                </rate-limit>
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile rate limit policy with apis"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.RateLimit(new RateLimitConfig()
                    {
                        Calls = 1000,
                        RenewalPeriod = 60,
                        Apis = [
                            new ApiRateLimit()
                            {
                                Name = "echo-api-name",
                                Calls = 50,
                                RenewalPeriod = 10,
                                Operations = [
                                    new OperationRateLimit()
                                    {
                                        Name = "get-name",
                                        Calls = 25,
                                        RenewalPeriod = 10,
                                    },
                                    new OperationRateLimit()
                                    {
                                        Id = "put-id",
                                        Calls = 10,
                                        RenewalPeriod = 5,
                                    }
                                ]
                            }
                        ],
                    });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <rate-limit calls="1000" renewal-period="60">
                    <api name="echo-api-name" calls="50" renewal-period="10">
                        <operation name="get-name" calls="25" renewal-period="10" />
                        <operation id="put-id" calls="10" renewal-period="5" />
                    </api>
                </rate-limit>
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile rate limit policy with operations in api"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.RateLimit(Limit());
            }
            static RateLimitConfig Limit() => new RateLimitConfig()
                {
                    Calls = 100,
                    RenewalPeriod = 10
                };
        }
        """,
        """
        <policies>
            <inbound>
                <rate-limit calls="100" renewal-period="10" />
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile rate limit policy from expression-bodied config factory"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.RateLimit(Limits.Default());
            }
        }

        public static class Limits
        {
            public static RateLimitConfig Default()
            {
                return new RateLimitConfig()
                {
                    Calls = 100,
                    RenewalPeriod = 10
                };
            }
        }
        """,
        """
        <policies>
            <inbound>
                <rate-limit calls="100" renewal-period="10" />
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile rate limit policy from cross-class block-bodied config factory"
    )]
    public void ShouldCompileRateLimitPolicy(string code, string expectedXml)
    {
        code.CompileDocument().Should().BeSuccessful().And.DocumentEquivalentTo(expectedXml);
    }

    [TestMethod]
    public void ShouldReportConfigFactoryWithNonContextParameter()
    {
        var result = """
            [Document]
            public class PolicyDocument : IDocument
            {
                public void Inbound(IInboundContext context) {
                    context.RateLimit(Limit(100));
                }
                static RateLimitConfig Limit(int calls) => new RateLimitConfig()
                    {
                        Calls = calls,
                        RenewalPeriod = 10
                    };
            }
            """.CompileDocument();

        result.Errors.Should().ContainSingle(error => error.Id == "APIM2002");
    }

    [TestMethod]
    [DataRow("context.RateLimit(new RateLimitConfig { Calls = 1, RenewalPeriod = 301 });", true)]
    [DataRow("context.RateLimit(new RateLimitConfig { Calls = 1, RenewalPeriod = 300 });", false)]
    [DataRow("context.RateLimitByKey(new RateLimitByKeyConfig { Calls = 1, RenewalPeriod = 0, CounterKey = \"k\" });", true)]
    [DataRow("context.QuotaByKey(new QuotaByKeyConfig { Calls = 1, RenewalPeriod = 299, CounterKey = \"k\" });", true)]
    [DataRow("context.QuotaByKey(new QuotaByKeyConfig { Calls = 1, RenewalPeriod = 300, CounterKey = \"k\" });", false)]
    public void ShouldValidateRenewalPeriodRange(string policy, bool rejected)
    {
        var errors = CompilerTestInitialize.InboundDocument(policy).CompileDocument().Errors;

        if (rejected)
        {
            errors.Should().ContainSingle(error => error.Id == "APIM2020");
        }
        else
        {
            errors.Should().BeEmpty();
        }
    }

    [TestMethod]
    public void ShouldReportRateLimitDiagnosticsWithRateLimitPolicyName()
    {
        var result = CompilerTestInitialize.InboundDocument(
            """
            context.RateLimit(new RateLimitConfig { RenewalPeriod = 10 });
            """).CompileDocument();

        result.Errors.Should().ContainSingle(error =>
            error.Id == "APIM2006" && error.GetMessage(null).Contains("'rate-limit'"));
    }

    [TestMethod]
    [DataRow("Apis = GetApis()", "APIM2005")]
    [DataRow("Apis = [GetApi()]", "APIM2002")]
    public void ShouldReportNonInlineRateLimitApis(string apis, string expected)
    {
        var result = CompilerTestInitialize.InboundDocument(
            $$"""
              context.RateLimit(new RateLimitConfig { Calls = 1, RenewalPeriod = 10, {{apis}} });
              """,
            """
            static ApiRateLimit[] GetApis() => new[] { GetApi() };
            static ApiRateLimit GetApi() => new ApiRateLimit { Name = "a", Calls = 1, RenewalPeriod = 10 };
            """).CompileDocument();

        result.Errors.Should().Contain(error => error.Id == expected);
    }
}
