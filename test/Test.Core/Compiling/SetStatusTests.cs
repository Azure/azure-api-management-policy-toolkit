// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.ApiManagement.PolicyToolkit.Compiling;

[TestClass]
public class SetStatusTests
{
    [TestMethod]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.SetStatus(new StatusConfig
                {
                    Code = 404,
                    Reason = "Not Found"
                });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <set-status code="404" reason="Not Found" />
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile set-status policy with code and reason"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.SetStatus(new StatusConfig
                {
                    Code = GetStatusCode(context.ExpressionContext),
                    Reason = "Not Found"
                });
            }
            
            int GetStatusCode(IExpressionContext context) => context.Request.Url.Path.Contains("notfound") ? 404 : 400;
        }
        """,
        """
        <policies>
            <inbound>
                <set-status code="@(context.Request.Url.Path.Contains("notfound") ? 404 : 400)" reason="Not Found" />
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile set-status policy with expression in code"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.SetStatus(new StatusConfig
                {
                    Code = 400,
                    Reason = GetReason(context.ExpressionContext)
                });
            }
            
            string GetReason(IExpressionContext context) => context.Request.Headers.GetValueOrDefault("X-Error-Reason", "Bad Request");
        }
        """,
        """
        <policies>
            <inbound>
                <set-status code="400" reason="@(context.Request.Headers.GetValueOrDefault("X-Error-Reason", "Bad Request"))" />
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile set-status policy with expression in reason"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.SetStatus(new StatusConfig
                {
                    Code = GetStatusCode(context.ExpressionContext),
                    Reason = GetReason(context.ExpressionContext)
                });
            }
            
            int GetStatusCode(IExpressionContext context) => int.Parse(context.Request.Headers.GetValueOrDefault("X-Status-Code", "500"));
            string GetReason(IExpressionContext context) => context.Request.Headers.GetValueOrDefault("X-Error-Reason", "Internal Server Error");
        }
        """,
        """
        <policies>
            <inbound>
                <set-status code="@(int.Parse(context.Request.Headers.GetValueOrDefault("X-Status-Code", "500")))" reason="@(context.Request.Headers.GetValueOrDefault("X-Error-Reason", "Internal Server Error"))" />
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile set-status policy with expressions in both code and reason"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Outbound(IOutboundContext context) {
                context.SetStatus(new StatusConfig
                {
                    Code = 201,
                    Reason = "Created"
                });
            }
        }
        """,
        """
        <policies>
            <outbound>
                <set-status code="201" reason="Created" />
            </outbound>
        </policies>
        """,
        DisplayName = "Should compile set-status policy in outbound section"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void OnError(IOnErrorContext context) {
                context.SetStatus(new StatusConfig
                {
                    Code = 500,
                    Reason = "Internal Server Error"
                });
            }
        }
        """,
        """
        <policies>
            <on-error>
                <set-status code="500" reason="Internal Server Error" />
            </on-error>
        </policies>
        """,
        DisplayName = "Should compile set-status policy in on-error section"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Backend(IBackendContext context) {
                context.SetStatus(new StatusConfig
                {
                    Code = 503,
                    Reason = "Service Unavailable"
                });
            }
        }
        """,
        """
        <policies>
            <backend>
                <set-status code="503" reason="Service Unavailable" />
            </backend>
        </policies>
        """,
        DisplayName = "Should compile set-status policy in backend section"
    )]
    public void ShouldCompileSetStatusPolicy(string code, string expectedXml)
    {
        code.CompileDocument().Should().BeSuccessful().And.DocumentEquivalentTo(expectedXml);
    }
}