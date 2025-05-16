// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling;

[TestClass]
public class ForwardRequestTests
{
    [TestMethod]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Backend(IBackendContext context) { context.ForwardRequest(); }
        }
        """,
        """
        <policies>
            <backend>
                <forward-request />
            </backend>
        </policies>
        """,
        DisplayName = "Should compile forward request policy without arguments"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Backend(IBackendContext context) {
                context.ForwardRequest(new ForwardRequestConfig() { });
            }
        }
        """,
        """
        <policies>
            <backend>
                <forward-request />
            </backend>
        </policies>
        """,
        DisplayName = "Should compile forward request policy with empty config"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Backend(IBackendContext context) {
                context.ForwardRequest(new ForwardRequestConfig()
                    {
                        Timeout = 10,
                    });
            }
        }
        """,
        """
        <policies>
            <backend>
                <forward-request timeout="10" />
            </backend>
        </policies>
        """,
        DisplayName = "Should compile forward request policy with Timeout"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Backend(IBackendContext context) {
                context.ForwardRequest(new ForwardRequestConfig()
                    {
                        Timeout = CalcTimeout(context.ExpressionContext),
                    });
            }

            public uint CalcTimeout(IExpressionContext context) => 9 + 1;
        }
        """,
        """
        <policies>
            <backend>
                <forward-request timeout="@(9 + 1)" />
            </backend>
        </policies>
        """,
        DisplayName = "Should compile forward request policy with expression in Timeout"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Backend(IBackendContext context) {
                context.ForwardRequest(new ForwardRequestConfig()
                    {
                        TimeoutMs = 9000,
                    });
            }
        }
        """,
        """
        <policies>
            <backend>
                <forward-request timeout-ms="9000" />
            </backend>
        </policies>
        """,
        DisplayName = "Should compile forward request policy with TimeoutMs"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Backend(IBackendContext context) {
                context.ForwardRequest(new ForwardRequestConfig()
                    {
                        TimeoutMs = CalcTimeout(context.ExpressionContext),
                    });
            }
            public uint CalcTimeout(IExpressionContext context) => 8999 + 1;
        }
        """,
        """
        <policies>
            <backend>
                <forward-request timeout-ms="@(8999 + 1)" />
            </backend>
        </policies>
        """,
        DisplayName = "Should compile forward request policy with expression in TimeoutMs"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Backend(IBackendContext context) {
                context.ForwardRequest(new ForwardRequestConfig()
                    {
                        ContinueTimeout = 100,
                    });
            }
        }
        """,
        """
        <policies>
            <backend>
                <forward-request continue-timeout="100" />
            </backend>
        </policies>
        """,
        DisplayName = "Should compile forward request policy with ContinueTimeout"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Backend(IBackendContext context) {
                context.ForwardRequest(new ForwardRequestConfig()
                    {
                        ContinueTimeout = CalcTimeout(context.ExpressionContext),
                    });
            }
            public uint CalcTimeout(IExpressionContext context) => 99 + 1;
        }
        """,
        """
        <policies>
            <backend>
                <forward-request continue-timeout="@(99 + 1)" />
            </backend>
        </policies>
        """,
        DisplayName = "Should compile forward request policy with expression in ContinueTimeout"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Backend(IBackendContext context) {
                context.ForwardRequest(new ForwardRequestConfig()
                    {
                        HttpVersion = "2or1",
                    });
            }
        }
        """,
        """
        <policies>
            <backend>
                <forward-request http-version="2or1" />
            </backend>
        </policies>
        """,
        DisplayName = "Should compile forward request policy with HttpVersion"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Backend(IBackendContext context) {
                context.ForwardRequest(new ForwardRequestConfig()
                    {
                        FollowRedirects = true,
                    });
            }
        }
        """,
        """
        <policies>
            <backend>
                <forward-request follow-redirects="true" />
            </backend>
        </policies>
        """,
        DisplayName = "Should compile forward request policy with FollowRedirects"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Backend(IBackendContext context) {
                context.ForwardRequest(new ForwardRequestConfig()
                    {
                        FollowRedirects = Calc(context.ExpressionContext),
                    });
            }
            public bool Calc(IExpressionContext context) => 1 != 0;
        }
        """,
        """
        <policies>
            <backend>
                <forward-request follow-redirects="@(1 != 0)" />
            </backend>
        </policies>
        """,
        DisplayName = "Should compile forward request policy with expression in FollowRedirects"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Backend(IBackendContext context) {
                context.ForwardRequest(new ForwardRequestConfig()
                    {
                        BufferRequestBody = true,
                    });
            }
        }
        """,
        """
        <policies>
            <backend>
                <forward-request buffer-request-body="true" />
            </backend>
        </policies>
        """,
        DisplayName = "Should compile forward request policy with BufferRequestBody"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Backend(IBackendContext context) {
                context.ForwardRequest(new ForwardRequestConfig()
                    {
                        BufferResponse = false,
                    });
            }
        }
        """,
        """
        <policies>
            <backend>
                <forward-request buffer-response="false" />
            </backend>
        </policies>
        """,
        DisplayName = "Should compile forward request policy with BufferResponse"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Backend(IBackendContext context) {
                context.ForwardRequest(new ForwardRequestConfig()
                    {
                        FailOnErrorStatusCode = true,
                    });
            }
        }
        """,
        """
        <policies>
            <backend>
                <forward-request fail-on-error-status-code="true" />
            </backend>
        </policies>
        """,
        DisplayName = "Should compile forward request policy with FailOnErrorStatusCode"
    )]
    public void ShouldCompileForwardRequestPolicy(string code, string expectedXml)
    {
        code.CompileDocument().Should().BeSuccessful().And.DocumentEquivalentTo(expectedXml);
    }
}