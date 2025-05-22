// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling;

[TestClass]
public class CacheStoreValueTests
{
    [TestMethod]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context)
            {
                context.CacheStoreValue(new CacheStoreValueConfig
                {
                    Key = "cacheKey",
                    Value = "value",
                    Duration = 60,
                });
            }
            public void Backend(IBackendContext context)
            {
                context.CacheStoreValue(new CacheStoreValueConfig
                {
                    Key = "cacheKey",
                    Value = "value",
                    Duration = 60,
                });
            }
            public void Outbound(IOutboundContext context)
            {
                context.CacheStoreValue(new CacheStoreValueConfig
                {
                    Key = "cacheKey",
                    Value = "value",
                    Duration = 60,
                });
            }
            public void OnError(IOnErrorContext context)
            {
                context.CacheStoreValue(new CacheStoreValueConfig
                {
                    Key = "cacheKey",
                    Value = "value",
                    Duration = 60,
                });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <cache-store-value key="cacheKey" value="value" duration="60" />
            </inbound>
            <backend>
                <cache-store-value key="cacheKey" value="value" duration="60" />
            </backend>
            <outbound>
                <cache-store-value key="cacheKey" value="value" duration="60" />
            </outbound>
            <on-error>
                <cache-store-value key="cacheKey" value="value" duration="60" />
            </on-error>
        </policies>
        """,
        DisplayName = "Should compile cache-store-value policy in sections"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context)
            {
                context.CacheStoreValue(new CacheStoreValueConfig
                {
                    Key = Exp(context.ExpressionContext),
                    Value = "value",
                    Duration = 60,
                });
            }
            string Exp(IExpressionContext context) => context.User.Id;
        }
        """,
        """
        <policies>
            <inbound>
                <cache-store-value key="@(context.User.Id)" value="value" duration="60" />
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile cache-store-value policy with expression for key"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context)
            {
                context.CacheStoreValue(new CacheStoreValueConfig
                {
                    Key = "cacheKey",
                    Value = Exp(context.ExpressionContext),
                    Duration = 60,
                });
            }
            string Exp(IExpressionContext context) => context.User.Id;
        }
        """,
        """
        <policies>
            <inbound>
                <cache-store-value key="cacheKey" value="@(context.User.Id)" duration="60" />
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile cache-store-value policy with expression for value"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context)
            {
                context.CacheStoreValue(new CacheStoreValueConfig
                {
                    Key = "cacheKey",
                    Value = "value",
                    Duration = Exp(context.ExpressionContext),
                });
            }
            uint Exp(IExpressionContext context) 
                => context.User.Email.EndsWith("@contoso.example") ? 10 : 60;
        }
        """,
        """
        <policies>
            <inbound>
                <cache-store-value key="cacheKey" value="value" duration="@(context.User.Email.EndsWith("@contoso.example") ? 10 : 60)" />
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile cache-store-value policy with expression for value"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context)
            {
                context.CacheStoreValue(new CacheStoreValueConfig
                {
                    Key = "cacheKey",
                    Value = "value",
                    Duration = 60,
                    CachingType = "internal",
                });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <cache-store-value key="cacheKey" value="value" duration="60" caching-type="internal" />
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile cache-store-value policy with caching-type"
    )]
    public void ShouldCompileCacheStoreValuePolicy(string code, string expectedXml)
    {
        code.CompileDocument().Should().BeSuccessful().And.DocumentEquivalentTo(expectedXml);
    }
}