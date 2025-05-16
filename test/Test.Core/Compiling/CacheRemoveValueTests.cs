// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling;

[TestClass]
public class CacheRemoveValueTests
{
    [TestMethod]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context)
            {
                context.CacheRemoveValue(new CacheRemoveValueConfig
                {
                    Key = "cacheKey"
                });
            }
            public void Backend(IBackendContext context)
            {
                context.CacheRemoveValue(new CacheRemoveValueConfig
                {
                    Key = "cacheKey"
                });
            }
            public void Outbound(IOutboundContext context)
            {
                context.CacheRemoveValue(new CacheRemoveValueConfig
                {
                    Key = "cacheKey"
                });
            }
            public void OnError(IOnErrorContext context)
            {
                context.CacheRemoveValue(new CacheRemoveValueConfig
                {
                    Key = "cacheKey"
                });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <cache-remove-value key="cacheKey" />
            </inbound>
            <backend>
                <cache-remove-value key="cacheKey" />
            </backend>
            <outbound>
                <cache-remove-value key="cacheKey" />
            </outbound>
            <on-error>
                <cache-remove-value key="cacheKey" />
            </on-error>
        </policies>
        """,
        DisplayName = "Should compile cache-remove-value policy in sections"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context)
            {
                context.CacheRemoveValue(new CacheRemoveValueConfig
                {
                    Key = Exp(context.ExpressionContext)
                });
            }
            string Exp(IExpressionContext context) => context.User.Id;
        }
        """,
        """
        <policies>
            <inbound>
                <cache-remove-value key="@(context.User.Id)" />
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile cache-remove-value policy with expression for key"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context)
            {
                context.CacheRemoveValue(new CacheRemoveValueConfig
                {
                    Key = "cacheKey",
                    CachingType = "internal",
                });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <cache-remove-value key="cacheKey" caching-type="internal" />
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile cache-remove-value policy with caching-type"
    )]
    public void ShouldCompileCacheRemoveValuePolicy(string code, string expectedXml)
    {
        code.CompileDocument().Should().BeSuccessful().And.DocumentEquivalentTo(expectedXml);
    }
}