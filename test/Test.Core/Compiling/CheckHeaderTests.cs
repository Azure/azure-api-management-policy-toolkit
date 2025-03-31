// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.ApiManagement.PolicyToolkit.Compiling;

[TestClass]
public class CheckHeaderTests
{
    [TestMethod]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context)
            {
                context.CheckHeader(new CheckHeaderConfig
                {
                    Name = "X-Test-Header",
                    FailCheckHttpCode = 400,
                    FailCheckErrorMessage = "Header missing or invalid",
                    IgnoreCase = true,
                    Values = ["value1", "value2"]
                });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <check-header name="X-Test-Header" failed-check-httpcode="400" failed-check-error-message="Header missing or invalid" ignore-case="true">
                    <value>value1</value>
                    <value>value2</value>
                </check-header>
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile check-header policy with all required properties"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context)
            {
                context.CheckHeader(new CheckHeaderConfig
                {
                    Name = GetHeaderName(context.ExpressionContext),
                    FailCheckHttpCode = 401,
                    FailCheckErrorMessage = "Unauthorized",
                    IgnoreCase = false,
                    Values = ["expected-value"]
                });
            }
        
            string GetHeaderName(IExpressionContext context) => context.Variables["headerName"].ToString();
        }
        """,
        """
        <policies>
            <inbound>
                <check-header name="@(context.Variables["headerName"].ToString())" failed-check-httpcode="401" failed-check-error-message="Unauthorized" ignore-case="false">
                    <value>expected-value</value>
                </check-header>
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile check-header policy with expression in header name"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context)
            {
                context.CheckHeader(new CheckHeaderConfig
                {
                    Name = "X-Test-Header",
                    FailCheckHttpCode = GetHttpCode(context.ExpressionContext),
                    FailCheckErrorMessage = "Invalid header",
                    IgnoreCase = true,
                    Values = ["value"]
                });
            }
        
            int GetHttpCode(IExpressionContext context) => (int)context.Variables["httpCode"];
        }
        """,
        """
        <policies>
            <inbound>
                <check-header name="X-Test-Header" failed-check-httpcode="@((int)context.Variables["httpCode"])" failed-check-error-message="Invalid header" ignore-case="true">
                    <value>value</value>
                </check-header>
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile check-header policy with expression in failed-check-httpcode"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context)
            {
                context.CheckHeader(new CheckHeaderConfig
                {
                    Name = "X-Test-Header",
                    FailCheckHttpCode = 403,
                    FailCheckErrorMessage = GetErrorMessage(context.ExpressionContext),
                    IgnoreCase = false,
                    Values = ["value"]
                });
            }
        
            string GetErrorMessage(IExpressionContext context) => context.Variables["errorMessage"].ToString();
        }
        """,
        """
        <policies>
            <inbound>
                <check-header name="X-Test-Header" failed-check-httpcode="403" failed-check-error-message="@(context.Variables["errorMessage"].ToString())" ignore-case="false">
                    <value>value</value>
                </check-header>
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile check-header policy with expression in failed-check-error-message"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context)
            {
                context.CheckHeader(new CheckHeaderConfig
                {
                    Name = "X-Test-Header",
                    FailCheckHttpCode = 400,
                    FailCheckErrorMessage = "Header invalid",
                    IgnoreCase = GetIgnoreCase(context.ExpressionContext),
                    Values = ["value"]
                });
            }
        
            bool GetIgnoreCase(IExpressionContext context) => (bool)context.Variables["ignoreCase"];
        }
        """,
        """
        <policies>
            <inbound>
                <check-header name="X-Test-Header" failed-check-httpcode="400" failed-check-error-message="Header invalid" ignore-case="@((bool)context.Variables["ignoreCase"])">
                    <value>value</value>
                </check-header>
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile check-header policy with expression in ignore-case"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context)
            {
                context.CheckHeader(new CheckHeaderConfig
                {
                    Name = "X-Test-Header",
                    FailCheckHttpCode = 400,
                    FailCheckErrorMessage = "Header invalid",
                    IgnoreCase = true,
                    Values = [GetValue(context.ExpressionContext)]
                });
            }
        
            string GetValue(IExpressionContext context) => context.Variables["headerValue"].ToString();
        }
        """,
        """
        <policies>
            <inbound>
                <check-header name="X-Test-Header" failed-check-httpcode="400" failed-check-error-message="Header invalid" ignore-case="true">
                    <value>@(context.Variables["headerValue"].ToString())</value>
                </check-header>
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile check-header policy with expression in header values"
    )]
    public void ShouldCompileCheckHeaderPolicy(string code, string expectedXml)
    {
        code.CompileDocument().Should().BeSuccessful().And.DocumentEquivalentTo(expectedXml);
    }
}