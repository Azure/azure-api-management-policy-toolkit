// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.ApiManagement.PolicyToolkit.Compiling;

[TestClass]
public class ValidateStatusCodeTests
{
    [TestMethod]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Outbound(IOutboundContext context) {
                context.ValidateStatusCode(new ValidateStatusCodeConfig
                {
                    UnspecifiedStatusCodeAction = "prevent",
                });
            }
            public void OnError(IOnErrorContext context) {
                context.ValidateStatusCode(new ValidateStatusCodeConfig
                {
                    UnspecifiedStatusCodeAction = "detect",
                });
            }
        }
        """,
        """
        <policies>
            <outbound>
                <validate-status-code unspecified-status-code-action="prevent" />
            </outbound>
            <on-error>
                <validate-status-code unspecified-status-code-action="detect" />
            </on-error>
        </policies>
        """,
        DisplayName = "Should compile validate-status-code policy in sections"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Outbound(IOutboundContext context) {
                context.ValidateStatusCode(new ValidateStatusCodeConfig
                {
                    UnspecifiedStatusCodeAction = GetAction(context.ExpressionContext),
                });
            }
            string GetAction(IExpressionContext context) => context.Variables["action"].ToString();
        }
        """,
        """
        <policies>
            <outbound>
                <validate-status-code unspecified-status-code-action="@(context.Variables["action"].ToString())" />
            </outbound>
        </policies>
        """,
        DisplayName = "Should compile validate-status-code policy with expression in unspecified-status-code-action"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Outbound(IOutboundContext context) {
                context.ValidateStatusCode(new ValidateStatusCodeConfig
                {
                    UnspecifiedStatusCodeAction = "prevent",
                    ErrorVariableName = "status-code-validation-errors"
                });
            }
        }
        """,
        """
        <policies>
            <outbound>
                <validate-status-code unspecified-status-code-action="prevent" error-variable-name="status-code-validation-errors" />
            </outbound>
        </policies>
        """,
        DisplayName = "Should compile validate-status-code policy with error-variable-name"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Outbound(IOutboundContext context) {
                context.ValidateStatusCode(new ValidateStatusCodeConfig
                {
                    UnspecifiedStatusCodeAction = "prevent",
                    StatusCodes = [
                        new ValidateStatusCode { Code = 200, Action = "allow" }
                    ]
                });
            }
        }
        """,
        """
        <policies>
            <outbound>
                <validate-status-code unspecified-status-code-action="prevent">
                    <status-code code="200" action="allow" />
                </validate-status-code>
            </outbound>
        </policies>
        """,
        DisplayName = "Should compile validate-status-code policy with a single status code"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Outbound(IOutboundContext context) {
                context.ValidateStatusCode(new ValidateStatusCodeConfig
                {
                    UnspecifiedStatusCodeAction = "prevent",
                    StatusCodes = [
                        new ValidateStatusCode { Code = 200, Action = "allow" },
                        new ValidateStatusCode { Code = 404, Action = "detect" },
                        new ValidateStatusCode { Code = 500, Action = "prevent" }
                    ]
                });
            }
        }
        """,
        """
        <policies>
            <outbound>
                <validate-status-code unspecified-status-code-action="prevent">
                    <status-code code="200" action="allow" />
                    <status-code code="404" action="detect" />
                    <status-code code="500" action="prevent" />
                </validate-status-code>
            </outbound>
        </policies>
        """,
        DisplayName = "Should compile validate-status-code policy with multiple status codes"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Outbound(IOutboundContext context) {
                context.ValidateStatusCode(new ValidateStatusCodeConfig
                {
                    UnspecifiedStatusCodeAction = "prevent",
                    StatusCodes = [
                        new ValidateStatusCode { Code = 200, Action = GetStatusCodeAction(context.ExpressionContext) }
                    ]
                });
            }
            string GetStatusCodeAction(IExpressionContext context) => context.Variables["statusCodeAction"].ToString();
        }
        """,
        """
        <policies>
            <outbound>
                <validate-status-code unspecified-status-code-action="prevent">
                    <status-code code="200" action="@(context.Variables["statusCodeAction"].ToString())" />
                </validate-status-code>
            </outbound>
        </policies>
        """,
        DisplayName = "Should compile validate-status-code policy with expression in status code action"
    )]
    public void ShouldCompileValidateStatusCodePolicy(string code, string expectedXml)
    {
        code.CompileDocument().Should().BeSuccessful().And.DocumentEquivalentTo(expectedXml);
    }
}