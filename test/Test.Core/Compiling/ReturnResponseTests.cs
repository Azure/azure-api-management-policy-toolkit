// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling;

[TestClass]
public class ReturnResponseTests
{
    [TestMethod]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) { 
                context.ReturnResponse(new ReturnResponseConfig {
                    Status = new StatusConfig {
                        Code = 100,
                        Reason = "Continue"
                    },
                });
            }
            public void Outbound(IOutboundContext context) {
                context.ReturnResponse(new ReturnResponseConfig {
                    Status = new StatusConfig {
                        Code = 200,
                        Reason = "OK"
                    },
                });
            }
            public void Backend(IBackendContext context) {
                context.ReturnResponse(new ReturnResponseConfig {
                    Status = new StatusConfig {
                        Code = 400,
                        Reason = "Bad Request"
                    },
                });
            }
            public void OnError(IOnErrorContext context) {
                context.ReturnResponse(new ReturnResponseConfig {
                    Status = new StatusConfig {
                        Code = 500,
                        Reason = "Internal Server Error"
                    },
                });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <return-response>
                    <set-status code="100" reason="Continue" />
                </return-response>
            </inbound>
            <outbound>
                <return-response>
                    <set-status code="200" reason="OK" />
                </return-response>
            </outbound>
            <backend>
                <return-response>
                    <set-status code="400" reason="Bad Request" />
                </return-response>
            </backend>
            <on-error>
                <return-response>
                    <set-status code="500" reason="Internal Server Error" />
                </return-response>
            </on-error>
        </policies>
        """,
        DisplayName = "Should compile return response policy in sections"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) { 
                context.ReturnResponse(new ReturnResponseConfig {
                    Status = new StatusConfig {
                        Code = 100,
                        Reason = "Continue"
                    },
                });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <return-response>
                    <set-status code="100" reason="Continue" />
                </return-response>
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile return response policy with status"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) { 
                context.ReturnResponse(new ReturnResponseConfig {
                    Status = new StatusConfig {
                        Code = Exp(context.ExpressionContext)
                    },
                });
            }

            private unit Exp(IExpressionContext context) => 180 + 10 + 10;
        }
        """,
        """
        <policies>
            <inbound>
                <return-response>
                    <set-status code="@(180 + 10 + 10)" />
                </return-response>
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile return response policy with expression in status"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) { 
                context.ReturnResponse(new ReturnResponseConfig {
                    ResponseVariableName = "var-name",
                });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <return-response response-variable-name="var-name" />
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile return response policy with response variable name"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) { 
                context.ReturnResponse(new ReturnResponseConfig {
                    Body = new BodyConfig {
                        Template = "liquid",
                        XsiNil = "blank",
                        ParseDate = false,
                        Content = "body",
                    },
                });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <return-response>
                    <set-body template="liquid" xsi-nil="blank" parse-date="false">body</set-body>
                </return-response>
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile return response policy with body"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context)
            {
                context.ReturnResponse(new ReturnResponseConfig {
                    Body = new BodyConfig {
                        Content = Exp(context.ExpressionContext),
                    },
                });
            }
            private string Exp(IExpressionContext context) => "bo" + "dy";
        }
        """,
        """
        <policies>
            <inbound>
                <return-response>
                    <set-body>@("bo" + "dy")</set-body>
                </return-response>
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile return response policy with expression in body"
    )]
    public void ShouldCompileReturnResponsePolicy(string code, string expectedXml)
    {
        code.CompileDocument().Should().BeSuccessful().And.DocumentEquivalentTo(expectedXml);
    }
}