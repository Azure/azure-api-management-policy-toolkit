// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling;

[TestClass]
public class CacheLookupValueTests
{
    [TestMethod]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context)
            {
                context.CacheLookupValue(new CacheLookupValueConfig
                {
                    Key = "cacheKey",
                    VariableName = "cacheVariable",
                });
            }
            public void Backend(IBackendContext context)
            {
                context.CacheLookupValue(new CacheLookupValueConfig
                {
                    Key = "cacheKey",
                    VariableName = "cacheVariable",
                });
            }
            public void Outbound(IOutboundContext context)
            {
                context.CacheLookupValue(new CacheLookupValueConfig
                {
                    Key = "cacheKey",
                    VariableName = "cacheVariable",
                });
            }
            public void OnError(IOnErrorContext context)
            {
                context.CacheLookupValue(new CacheLookupValueConfig
                {
                    Key = "cacheKey",
                    VariableName = "cacheVariable",
                });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <cache-lookup-value key="cacheKey" variable-name="cacheVariable" />
            </inbound>
            <backend>
                <cache-lookup-value key="cacheKey" variable-name="cacheVariable" />
            </backend>
            <outbound>
                <cache-lookup-value key="cacheKey" variable-name="cacheVariable" />
            </outbound>
            <on-error>
                <cache-lookup-value key="cacheKey" variable-name="cacheVariable" />
            </on-error>
        </policies>
        """,
        DisplayName = "Should compile cache-lookup-value policy in sections"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context)
            {
                context.CacheLookupValue(new CacheLookupValueConfig
                {
                    Key = Exp(context.ExpressionContext),
                    VariableName = "cacheVariable",
                });
            }
            string Exp(IExpressionContext context) => context.Api.Id;
        }
        """,
        """
        <policies>
            <inbound>
                <cache-lookup-value key="@(context.Api.Id)" variable-name="cacheVariable" />
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile cache-lookup-value policy with expression in key"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context)
            {
                context.CacheLookupValue(new CacheLookupValueConfig
                {
                    Key = "cacheKey",
                    VariableName = "cacheVariable",
                    DefaultValue = Exp(context.ExpressionContext),
                });
            }
            string Exp(IExpressionContext context) => context.User.Id;
        }
        """,
        """
        <policies>
            <inbound>
                <cache-lookup-value key="cacheKey" variable-name="cacheVariable" default-value="@(context.User.Id)" />
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile cache-lookup-value policy with default-value"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context)
            {
                context.CacheLookupValue(new CacheLookupValueConfig
                {
                    Key = "cacheKey",
                    VariableName = "cacheVariable",
                    CachingType = "internal",
                });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <cache-lookup-value key="cacheKey" variable-name="cacheVariable" caching-type="internal" />
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile cache-lookup-value policy with caching-type"
    )]
    public void ShouldCompileCacheLookupValuePolicy(string code, string expectedXml)
    {
        code.CompileDocument().Should().BeSuccessful().And.DocumentEquivalentTo(expectedXml);
    }
}