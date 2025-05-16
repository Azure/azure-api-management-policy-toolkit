// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling;

[TestClass]
public class CacheLookupTests
{
    [TestMethod]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context)
            {
                context.CacheLookup(new CacheLookupConfig
                {
                    VaryByDeveloper = true,
                    VaryByDeveloperGroups = true,
                });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <cache-lookup vary-by-developer="true" vary-by-developer-groups="true" />
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile cache-lookup policy"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context)
            {
                context.CacheLookup(new CacheLookupConfig
                {
                    VaryByDeveloper = Developer(context.ExpressionContext),
                    VaryByDeveloperGroups = Groups(context.ExpressionContext),
                });
            }
            bool Developer(IExpressionContext context) => context.User.Email.EndsWith("@contoso.example");
            bool Groups(IExpressionContext context) => !context.User.Email.EndsWith("@contoso.example");
        }
        """,
        """
        <policies>
            <inbound>
                <cache-lookup vary-by-developer="@(context.User.Email.EndsWith("@contoso.example"))" vary-by-developer-groups="@(!context.User.Email.EndsWith("@contoso.example"))" />
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile cache-lookup policy with expressions"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context)
            {
                context.CacheLookup(new CacheLookupConfig
                {
                    VaryByDeveloper = true,
                    VaryByDeveloperGroups = true,
                    CachingType = "internal",
                });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <cache-lookup vary-by-developer="true" vary-by-developer-groups="true" caching-type="internal" />
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile cache-lookup policy with caching-type"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context)
            {
                context.CacheLookup(new CacheLookupConfig
                {
                    VaryByDeveloper = true,
                    VaryByDeveloperGroups = true,
                    DownstreamCachingType = "internal",
                });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <cache-lookup vary-by-developer="true" vary-by-developer-groups="true" downstream-caching-type="internal" />
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile cache-lookup policy with downstream-caching-type"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context)
            {
                context.CacheLookup(new CacheLookupConfig
                {
                    VaryByDeveloper = true,
                    VaryByDeveloperGroups = true,
                    DownstreamCachingType = DownstreamCachingType(context.ExpressionContext),
                });
            }
            string DownstreamCachingType(IExpressionContext context) => context.User.Email.EndsWith("@contoso.example") ? "internal" : "external";
        }
        """,
        """
        <policies>
            <inbound>
                <cache-lookup vary-by-developer="true" vary-by-developer-groups="true" downstream-caching-type="@(context.User.Email.EndsWith("@contoso.example") ? "internal" : "external")" />
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile cache-lookup policy with expression in downstream-caching-type"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context)
            {
                context.CacheLookup(new CacheLookupConfig
                {
                    VaryByDeveloper = true,
                    VaryByDeveloperGroups = true,
                    MustRevalidate = false,
                });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <cache-lookup vary-by-developer="true" vary-by-developer-groups="true" must-revalidate="false" />
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile cache-lookup policy with must-revalidate"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context)
            {
                context.CacheLookup(new CacheLookupConfig
                {
                    VaryByDeveloper = true,
                    VaryByDeveloperGroups = true,
                    MustRevalidate = MustRevalidate(context.ExpressionContext),
                });
            }
            bool MustRevalidate(IExpressionContext context) => context.User.Email.EndsWith("@contoso.example");
        }
        """,
        """
        <policies>
            <inbound>
                <cache-lookup vary-by-developer="true" vary-by-developer-groups="true" must-revalidate="@(context.User.Email.EndsWith("@contoso.example"))" />
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile cache-lookup policy with expression in must-revalidate"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context)
            {
                context.CacheLookup(new CacheLookupConfig
                {
                    VaryByDeveloper = true,
                    VaryByDeveloperGroups = true,
                    AllowPrivateResponseCaching = true,
                });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <cache-lookup vary-by-developer="true" vary-by-developer-groups="true" allow-private-response-caching="true" />
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile cache-lookup policy with allow-private-response-caching"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context)
            {
                context.CacheLookup(new CacheLookupConfig
                {
                    VaryByDeveloper = true,
                    VaryByDeveloperGroups = true,
                    AllowPrivateResponseCaching = AllowPrivateResponseCaching(context.ExpressionContext),
                });
            }
            bool AllowPrivateResponseCaching(IExpressionContext context) => context.User.Email.EndsWith("@contoso.example");
        }
        """,
        """
        <policies>
            <inbound>
                <cache-lookup vary-by-developer="true" vary-by-developer-groups="true" allow-private-response-caching="@(context.User.Email.EndsWith("@contoso.example"))" />
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile cache-lookup policy with expression in allow-private-response-caching"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context)
            {
                context.CacheLookup(new CacheLookupConfig
                {
                    VaryByDeveloper = true,
                    VaryByDeveloperGroups = true,
                    VaryByHeaders = ["Accept", VaryByHeader(context.ExpressionContext)],
                });
            }
            bool VaryByHeader(IExpressionContext context) => context.User.Email.EndsWith("@contoso.example") ? "X-User-Id" : "X-User-Name";
        }
        """,
        """
        <policies>
            <inbound>
                <cache-lookup vary-by-developer="true" vary-by-developer-groups="true">
                    <vary-by-header>Accept</vary-by-header>
                    <vary-by-header>@(context.User.Email.EndsWith("@contoso.example") ? "X-User-Id" : "X-User-Name")</vary-by-header>
                </cache-lookup>
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile cache-lookup policy with vary-by-header"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context)
            {
                context.CacheLookup(new CacheLookupConfig
                {
                    VaryByDeveloper = true,
                    VaryByDeveloperGroups = true,
                    VaryByQueryParameters = ["id", VaryByParameter(context.ExpressionContext)],
                });
            }
            bool VaryByParameter(IExpressionContext context) => context.User.Email.EndsWith("@contoso.example") ? "userId" : "username";
        }
        """,
        """
        <policies>
            <inbound>
                <cache-lookup vary-by-developer="true" vary-by-developer-groups="true">
                    <vary-by-query-parameter>id</vary-by-query-parameter>
                    <vary-by-query-parameter>@(context.User.Email.EndsWith("@contoso.example") ? "userId" : "username")</vary-by-query-parameter>
                </cache-lookup>
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile cache-lookup policy with vary-by-parameter"
    )]
    public void ShouldCompileCacheLookupPolicy(string code, string expectedXml)
    {
        code.CompileDocument().Should().BeSuccessful().And.DocumentEquivalentTo(expectedXml);
    }
}