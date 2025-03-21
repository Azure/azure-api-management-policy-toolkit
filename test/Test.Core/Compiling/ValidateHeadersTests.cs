// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.ApiManagement.PolicyToolkit.Compiling;

[TestClass]
public class ValidateHeadersTests
{
    [TestMethod]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Outbound(IOutboundContext context) {
                context.ValidateHeaders(new ValidateHeadersConfig
                {
                    SpecifiedHeaderAction = "prevent",
                    UnspecifiedHeaderAction = "ignore",
                });
            }
            public void OnError(IOnErrorContext context) {
                context.ValidateHeaders(new ValidateHeadersConfig
                {
                    SpecifiedHeaderAction = "ignore",
                    UnspecifiedHeaderAction = "detect",
                });
            }
        }
        """,
        """
        <policies>
            <outbound>
                <validate-headers specified-header-action="prevent" unspecified-header-action="ignore" />
            </outbound>
            <on-error>
                <validate-headers specified-header-action="ignore" unspecified-header-action="detect" />
            </on-error>
        </policies>
        """,
        DisplayName = "Should compile validate-headers policy in sections"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Outbound(IOutboundContext context) {
                context.ValidateHeaders(new ValidateHeadersConfig
                {
                    SpecifiedHeaderAction = GetAction(context),
                    UnspecifiedHeaderAction = "ignore",
                });
            }
            string GetAction(IExpressionContext context) => context.Variables["action"].ToString();
        }
        """,
        """
        <policies>
            <outbound>
                <validate-headers specified-header-action="@(context.Variables["action"].ToString())" unspecified-header-action="ignore" />
            </outbound>
        </policies>
        """,
        DisplayName = "Should compile validate-headers policy with expression in specified-header-action"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Outbound(IOutboundContext context) {
                context.ValidateHeaders(new ValidateHeadersConfig
                {
                    SpecifiedHeaderAction = "ignore",
                    UnspecifiedHeaderAction = GetAction(context),
                });
            }
            string GetAction(IExpressionContext context) => context.Variables["action"].ToString();
        }
        """,
        """
        <policies>
            <outbound>
                <validate-headers specified-header-action="ignore" unspecified-header-action="@(context.Variables["action"].ToString())" />
            </outbound>
        </policies>
        """,
        DisplayName = "Should compile validate-headers policy with expression in unspecified-header-action"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Outbound(IOutboundContext context) {
                context.ValidateHeaders(new ValidateHeadersConfig
                {
                    SpecifiedHeaderAction = "prevent",
                    UnspecifiedHeaderAction = "ignore",
                    ErrorsVariableName = "header-validation-errors"
                });
            }
        }
        """,
        """
        <policies>
            <outbound>
                <validate-headers specified-header-action="prevent" unspecified-header-action="ignore" errors-variable-name="header-validation-errors" />
            </outbound>
        </policies>
        """,
        DisplayName = "Should compile validate-headers policy with errors-variable-name"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Outbound(IOutboundContext context) {
                context.ValidateHeaders(new ValidateHeadersConfig
                {
                    SpecifiedHeaderAction = "prevent",
                    UnspecifiedHeaderAction = "ignore",
                    Headers = [
                        new ValidateHeader { Name = "Content-Type", Action = "detect" }
                    ]
                });
            }
        }
        """,
        """
        <policies>
            <outbound>
                <validate-headers specified-header-action="prevent" unspecified-header-action="ignore">
                    <header name="Content-Type" action="detect" />
                </validate-headers>
            </outbound>
        </policies>
        """,
        DisplayName = "Should compile validate-headers policy with a single header"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Outbound(IOutboundContext context) {
                context.ValidateHeaders(new ValidateHeadersConfig
                {
                    SpecifiedHeaderAction = "prevent",
                    UnspecifiedHeaderAction = "ignore",
                    Headers = [
                        new ValidateHeader { Name = "Content-Type", Action = "detect" },
                        new ValidateHeader { Name = "Authorization", Action = "prevent" },
                        new ValidateHeader { Name = "X-Custom-Header", Action = "ignore" }
                    ]
                });
            }
        }
        """,
        """
        <policies>
            <outbound>
                <validate-headers specified-header-action="prevent" unspecified-header-action="ignore">
                    <header name="Content-Type" action="detect" />
                    <header name="Authorization" action="prevent" />
                    <header name="X-Custom-Header" action="ignore" />
                </validate-headers>
            </outbound>
        </policies>
        """,
        DisplayName = "Should compile validate-headers policy with multiple headers"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Outbound(IOutboundContext context) {
                context.ValidateHeaders(new ValidateHeadersConfig
                {
                    SpecifiedHeaderAction = "prevent",
                    UnspecifiedHeaderAction = "ignore",
                    Headers = [
                        new ValidateHeader { Name = "Content-Type", Action = GetHeaderAction(context) }
                    ]
                });
            }
            string GetHeaderAction(IExpressionContext context) => context.Variables["headerAction"].ToString();
        }
        """,
        """
        <policies>
            <outbound>
                <validate-headers specified-header-action="prevent" unspecified-header-action="ignore">
                    <header name="Content-Type" action="@(context.Variables["headerAction"].ToString())" />
                </validate-headers>
            </outbound>
        </policies>
        """,
        DisplayName = "Should compile validate-headers policy with expression in header action"
    )]
    public void ShouldCompileValidateHeadersPolicy(string code, string expectedXml)
    {
        code.CompileDocument().Should().BeSuccessful().And.DocumentEquivalentTo(expectedXml);
    }
}