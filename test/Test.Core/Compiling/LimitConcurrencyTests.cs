// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.ApiManagement.PolicyToolkit.Compiling;

[TestClass]
public class LimitConcurrencyTests
{
    [TestMethod]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context)
            {
                context.LimitConcurrency(new LimitConcurrencyConfig()
                    {
                        Key = "inbound",
                        MaxCount = 10,
                    },
                    () =>
                    {
                        context.AuthenticationManagedIdentity(new ManagedIdentityAuthenticationConfig { Resource = "resource" });
                    });
            }
        
            public void Backend(IBackendContext context)
            {
                context.LimitConcurrency(new LimitConcurrencyConfig()
                    {
                        Key = "backend",
                        MaxCount = 10,
                    },
                    () =>
                    {
                        context.ForwardRequest();
                    });
            }
            
            public void Outbound(IOutboundContext context)
            {
                context.LimitConcurrency(new LimitConcurrencyConfig()
                    {
                        Key = "outbound",
                        MaxCount = 10,
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
                context.LimitConcurrency(new LimitConcurrencyConfig()
                    {
                        Key = "on-error",
                        MaxCount = 10,
                    },
                    () =>
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
                <limit-concurrency key="inbound" max-count="10">
                    <authentication-managed-identity resource="resource" />
                </limit-concurrency>
            </inbound>
            <backend>
                <limit-concurrency key="backend" max-count="10">
                    <forward-request />
                </limit-concurrency>
            </backend>
            <outbound>
                <limit-concurrency key="outbound" max-count="10">
                    <send-request response-variable-name="variable" />
                </limit-concurrency>
            </outbound>
            <on-error>
                <limit-concurrency key="on-error" max-count="10">
                    <send-request response-variable-name="variable" />
                </limit-concurrency>
            </on-error>
        </policies>
        """,
        DisplayName = "Should compile limit-concurrency policy in sections"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context)
            {
                context.LimitConcurrency(new LimitConcurrencyConfig()
                    {
                        Key = GetKey(context),
                        MaxCount = 10,
                    },
                    () =>
                    {
                        context.AuthenticationManagedIdentity(new ManagedIdentityAuthenticationConfig { Resource = "resource" });
                    });
            }
            
            string GetKey(IExpressionContext context) => context.Api.Id;
        }
        """,
        """
        <policies>
            <inbound>
                <limit-concurrency key="@(context.Api.Id)" max-count="10">
                    <authentication-managed-identity resource="resource" />
                </limit-concurrency>
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile limit-concurrency policy with expression in key"
    )]
    public void ShouldCompileLimitConcurrencyPolicy(string code, string expectedXml)
    {
        code.CompileDocument().Should().BeSuccessful().And.DocumentEquivalentTo(expectedXml);
    }
}