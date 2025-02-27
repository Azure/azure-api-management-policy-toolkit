namespace Azure.ApiManagement.PolicyToolkit.Compiling;

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
}