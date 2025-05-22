// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling;

[TestClass]
public class CacheStoreTests
{
    [TestMethod]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Outbound(IOutboundContext context)
            {
                context.CacheStore(60, true);
            }
        }
        """,
        """
        <policies>
            <outbound>
                <cache-store duration="60" cache-response="true" />
            </outbound>
        </policies>
        """,
        DisplayName = "Should compile cache-store policy"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Outbound(IOutboundContext context)
            {
                context.CacheStore(60);
            }
        }
        """,
        """
        <policies>
            <outbound>
                <cache-store duration="60" />
            </outbound>
        </policies>
        """,
        DisplayName = "Should compile cache-store policy without cache-response property"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Outbound(IOutboundContext context)
            {
                context.CacheStore(Duration(context.ExpressionContext), StoreResponse(context.ExpressionContext));
            }
            
            uint Duration(IExpressionContext context) => context.User.Email.EndsWith("@contoso.example") ? 10 : 60;
            bool StoreResponse(IExpressionContext context) => context.User.Email.EndsWith("@contoso.example");
        }
        """,
        """
        <policies>
            <outbound>
                <cache-store duration="@(context.User.Email.EndsWith("@contoso.example") ? 10 : 60)" cache-response="@(context.User.Email.EndsWith("@contoso.example"))" />
            </outbound>
        </policies>
        """,
        DisplayName = "Should compile cache-store policy with expressions"
    )]
    public void ShouldCompileCacheStorePolicy(string code, string expectedXml)
    {
        code.CompileDocument().Should().BeSuccessful().And.DocumentEquivalentTo(expectedXml);
    }
}