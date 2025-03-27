// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.ApiManagement.PolicyToolkit.Compiling;

[TestClass]
public class ValidateParametersTests
{
    [TestMethod]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.ValidateParameters(new ValidateParametersConfig
                {
                    SpecifiedParameterAction = "prevent",
                    UnspecifiedParameterAction = "detect",
                });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <validate-parameters specified-parameter-action="prevent" unspecified-parameter-action="detect" />
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile validate-parameters policy"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.ValidateParameters(new ValidateParametersConfig
                {
                    SpecifiedParameterAction = GetAction(context),
                    UnspecifiedParameterAction = "detect",
                });
            }
            string GetAction(IExpressionContext context) => context.Variables["action"].ToString();
        }
        """,
        """
        <policies>
            <inbound>
                <validate-parameters specified-parameter-action="@(context.Variables["action"].ToString())" unspecified-parameter-action="detect" />
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile validate-parameters policy with expression in specified-parameter-action"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.ValidateParameters(new ValidateParametersConfig
                {
                    SpecifiedParameterAction = "prevent",
                    UnspecifiedParameterAction = GetAction(context),
                });
            }
            string GetAction(IExpressionContext context) => context.Variables["action"].ToString();
        }
        """,
        """
        <policies>
            <inbound>
                <validate-parameters specified-parameter-action="prevent" unspecified-parameter-action="@(context.Variables["action"].ToString())" />
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile validate-parameters policy with expression in unspecified-parameter-action"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.ValidateParameters(new ValidateParametersConfig
                {
                    SpecifiedParameterAction = "prevent",
                    UnspecifiedParameterAction = "detect",
                    ErrorsVariableName = "param-validation-errors"
                });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <validate-parameters specified-parameter-action="prevent" unspecified-parameter-action="detect" errors-variable-name="param-validation-errors" />
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile validate-parameters policy with errors-variable-name"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.ValidateParameters(new ValidateParametersConfig
                {
                    SpecifiedParameterAction = "prevent",
                    UnspecifiedParameterAction = "detect",
                    Headers = new ValidateHeaderParameters
                    {
                        SpecifiedParameterAction = "prevent",
                        UnspecifiedParameterAction = "ignore"
                    }
                });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <validate-parameters specified-parameter-action="prevent" unspecified-parameter-action="detect">
                    <headers specified-parameter-action="prevent" unspecified-parameter-action="ignore" />
                </validate-parameters>
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile validate-parameters with headers"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.ValidateParameters(new ValidateParametersConfig
                {
                    SpecifiedParameterAction = "prevent",
                    UnspecifiedParameterAction = "detect",
                    Headers = new ValidateHeaderParameters
                    {
                        SpecifiedParameterAction = GetAction(context),
                        UnspecifiedParameterAction = "ignore"
                    }
                });
            }
            string GetAction(IExpressionContext context) => context.Variables["action"].ToString();
        }
        """,
        """
        <policies>
            <inbound>
                <validate-parameters specified-parameter-action="prevent" unspecified-parameter-action="detect">
                    <headers specified-parameter-action="@(context.Variables["action"].ToString())" unspecified-parameter-action="ignore" />
                </validate-parameters>
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile validate-parameters with headers having expression in specified-parameter-action"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.ValidateParameters(new ValidateParametersConfig
                {
                    SpecifiedParameterAction = "prevent",
                    UnspecifiedParameterAction = "detect",
                    Headers = new ValidateHeaderParameters
                    {
                        SpecifiedParameterAction = "prevent",
                        UnspecifiedParameterAction = GetAction(context)
                    }
                });
            }
            string GetAction(IExpressionContext context) => context.Variables["action"].ToString();
        }
        """,
        """
        <policies>
            <inbound>
                <validate-parameters specified-parameter-action="prevent" unspecified-parameter-action="detect">
                    <headers specified-parameter-action="prevent" unspecified-parameter-action="@(context.Variables["action"].ToString())" />
                </validate-parameters>
            </inbound>
        </policies>
        """,
        DisplayName =
            "Should compile validate-parameters with headers having expression in unspecified-parameter-action"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.ValidateParameters(new ValidateParametersConfig
                {
                    SpecifiedParameterAction = "prevent",
                    UnspecifiedParameterAction = "detect",
                    Headers = new ValidateHeaderParameters
                    {
                        SpecifiedParameterAction = "prevent",
                        UnspecifiedParameterAction = "ignore",
                        Parameters = [
                            new ValidateParameter { Name = "Authorization", Action = "prevent" }
                        ]
                    }
                });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <validate-parameters specified-parameter-action="prevent" unspecified-parameter-action="detect">
                    <headers specified-parameter-action="prevent" unspecified-parameter-action="ignore">
                        <parameter name="Authorization" action="prevent" />
                    </headers>
                </validate-parameters>
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile validate-parameters with headers having a parameter"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.ValidateParameters(new ValidateParametersConfig
                {
                    SpecifiedParameterAction = "prevent",
                    UnspecifiedParameterAction = "detect",
                    Headers = new ValidateHeaderParameters
                    {
                        SpecifiedParameterAction = "prevent",
                        UnspecifiedParameterAction = "ignore",
                        Parameters = [
                            new ValidateParameter { Name = "Authorization", Action = "prevent" },
                            new ValidateParameter { Name = "Content-Type", Action = "ignore" },
                            new ValidateParameter { Name = "X-Custom-Header", Action = "detect" }
                        ]
                    }
                });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <validate-parameters specified-parameter-action="prevent" unspecified-parameter-action="detect">
                    <headers specified-parameter-action="prevent" unspecified-parameter-action="ignore">
                        <parameter name="Authorization" action="prevent" />
                        <parameter name="Content-Type" action="ignore" />
                        <parameter name="X-Custom-Header" action="detect" />
                    </headers>
                </validate-parameters>
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile validate-parameters with headers having multiple parameters"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.ValidateParameters(new ValidateParametersConfig
                {
                    SpecifiedParameterAction = "prevent",
                    UnspecifiedParameterAction = "detect",
                    Headers = new ValidateHeaderParameters
                    {
                        SpecifiedParameterAction = "prevent",
                        UnspecifiedParameterAction = "ignore",
                        Parameters = [
                            new ValidateParameter { Name = "Authorization", Action = GetAction(context) }
                        ]
                    }
                });
            }
            string GetAction(IExpressionContext context) => context.Variables["action"].ToString();
        }
        """,
        """
        <policies>
            <inbound>
                <validate-parameters specified-parameter-action="prevent" unspecified-parameter-action="detect">
                    <headers specified-parameter-action="prevent" unspecified-parameter-action="ignore">
                        <parameter name="Authorization" action="@(context.Variables["action"].ToString())" />
                    </headers>
                </validate-parameters>
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile validate-parameters with headers having parameter with expression in action"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.ValidateParameters(new ValidateParametersConfig
                {
                    SpecifiedParameterAction = "prevent",
                    UnspecifiedParameterAction = "detect",
                    Query = new ValidateQueryParameters
                    {
                        SpecifiedParameterAction = "prevent",
                        UnspecifiedParameterAction = "ignore"
                    }
                });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <validate-parameters specified-parameter-action="prevent" unspecified-parameter-action="detect">
                    <query specified-parameter-action="prevent" unspecified-parameter-action="ignore" />
                </validate-parameters>
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile validate-parameters with query"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.ValidateParameters(new ValidateParametersConfig
                {
                    SpecifiedParameterAction = "prevent",
                    UnspecifiedParameterAction = "detect",
                    Query = new ValidateQueryParameters
                    {
                        SpecifiedParameterAction = GetAction(context),
                        UnspecifiedParameterAction = "ignore"
                    }
                });
            }
            string GetAction(IExpressionContext context) => context.Variables["action"].ToString();
        }
        """,
        """
        <policies>
            <inbound>
                <validate-parameters specified-parameter-action="prevent" unspecified-parameter-action="detect">
                    <query specified-parameter-action="@(context.Variables["action"].ToString())" unspecified-parameter-action="ignore" />
                </validate-parameters>
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile validate-parameters with query having expression in specified-parameter-action"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.ValidateParameters(new ValidateParametersConfig
                {
                    SpecifiedParameterAction = "prevent",
                    UnspecifiedParameterAction = "detect",
                    Query = new ValidateQueryParameters
                    {
                        SpecifiedParameterAction = "prevent",
                        UnspecifiedParameterAction = GetAction(context)
                    }
                });
            }
            string GetAction(IExpressionContext context) => context.Variables["action"].ToString();
        }
        """,
        """
        <policies>
            <inbound>
                <validate-parameters specified-parameter-action="prevent" unspecified-parameter-action="detect">
                    <query specified-parameter-action="prevent" unspecified-parameter-action="@(context.Variables["action"].ToString())" />
                </validate-parameters>
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile validate-parameters with query having expression in unspecified-parameter-action"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.ValidateParameters(new ValidateParametersConfig
                {
                    SpecifiedParameterAction = "prevent",
                    UnspecifiedParameterAction = "detect",
                    Query = new ValidateQueryParameters
                    {
                        SpecifiedParameterAction = "prevent",
                        UnspecifiedParameterAction = "ignore",
                        Parameters = [
                            new ValidateParameter { Name = "api-version", Action = "prevent" }
                        ]
                    }
                });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <validate-parameters specified-parameter-action="prevent" unspecified-parameter-action="detect">
                    <query specified-parameter-action="prevent" unspecified-parameter-action="ignore">
                        <parameter name="api-version" action="prevent" />
                    </query>
                </validate-parameters>
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile validate-parameters with query having a parameter"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.ValidateParameters(new ValidateParametersConfig
                {
                    SpecifiedParameterAction = "prevent",
                    UnspecifiedParameterAction = "detect",
                    Query = new ValidateQueryParameters
                    {
                        SpecifiedParameterAction = "prevent",
                        UnspecifiedParameterAction = "ignore",
                        Parameters = [
                            new ValidateParameter { Name = "api-version", Action = "prevent" },
                            new ValidateParameter { Name = "filter", Action = "ignore" },
                            new ValidateParameter { Name = "select", Action = "detect" }
                        ]
                    }
                });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <validate-parameters specified-parameter-action="prevent" unspecified-parameter-action="detect">
                    <query specified-parameter-action="prevent" unspecified-parameter-action="ignore">
                        <parameter name="api-version" action="prevent" />
                        <parameter name="filter" action="ignore" />
                        <parameter name="select" action="detect" />
                    </query>
                </validate-parameters>
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile validate-parameters with query having multiple parameters"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.ValidateParameters(new ValidateParametersConfig
                {
                    SpecifiedParameterAction = "prevent",
                    UnspecifiedParameterAction = "detect",
                    Path = new ValidatePathParameters
                    {
                        SpecifiedParameterAction = "prevent"
                    }
                });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <validate-parameters specified-parameter-action="prevent" unspecified-parameter-action="detect">
                    <path specified-parameter-action="prevent" />
                </validate-parameters>
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile validate-parameters with path"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.ValidateParameters(new ValidateParametersConfig
                {
                    SpecifiedParameterAction = "prevent",
                    UnspecifiedParameterAction = "detect",
                    Path = new ValidatePathParameters
                    {
                        SpecifiedParameterAction = GetAction(context)
                    }
                });
            }
            string GetAction(IExpressionContext context) => context.Variables["action"].ToString();
        }
        """,
        """
        <policies>
            <inbound>
                <validate-parameters specified-parameter-action="prevent" unspecified-parameter-action="detect">
                    <path specified-parameter-action="@(context.Variables["action"].ToString())" />
                </validate-parameters>
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile validate-parameters with path having expression in specified-parameter-action"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.ValidateParameters(new ValidateParametersConfig
                {
                    SpecifiedParameterAction = "prevent",
                    UnspecifiedParameterAction = "detect",
                    Path = new ValidatePathParameters
                    {
                        SpecifiedParameterAction = "prevent",
                        Parameters = [
                            new ValidateParameter { Name = "id", Action = "prevent" }
                        ]
                    }
                });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <validate-parameters specified-parameter-action="prevent" unspecified-parameter-action="detect">
                    <path specified-parameter-action="prevent">
                        <parameter name="id" action="prevent" />
                    </path>
                </validate-parameters>
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile validate-parameters with path having a parameter"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.ValidateParameters(new ValidateParametersConfig
                {
                    SpecifiedParameterAction = "prevent",
                    UnspecifiedParameterAction = "detect",
                    Path = new ValidatePathParameters
                    {
                        SpecifiedParameterAction = "prevent",
                        Parameters = [
                            new ValidateParameter { Name = "id", Action = "prevent" },
                            new ValidateParameter { Name = "resourceType", Action = "ignore" },
                            new ValidateParameter { Name = "subresource", Action = "detect" }
                        ]
                    }
                });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <validate-parameters specified-parameter-action="prevent" unspecified-parameter-action="detect">
                    <path specified-parameter-action="prevent">
                        <parameter name="id" action="prevent" />
                        <parameter name="resourceType" action="ignore" />
                        <parameter name="subresource" action="detect" />
                    </path>
                </validate-parameters>
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile validate-parameters with path having multiple parameters"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.ValidateParameters(new ValidateParametersConfig
                {
                    SpecifiedParameterAction = "prevent",
                    UnspecifiedParameterAction = "detect",
                    ErrorsVariableName = "param-validation-errors",
                    Headers = new ValidateHeaderParameters
                    {
                        SpecifiedParameterAction = "prevent",
                        UnspecifiedParameterAction = "ignore",
                        Parameters = [
                            new ValidateParameter { Name = "Authorization", Action = "prevent" },
                            new ValidateParameter { Name = "Content-Type", Action = "ignore" }
                        ]
                    },
                    Query = new ValidateQueryParameters
                    {
                        SpecifiedParameterAction = "prevent",
                        UnspecifiedParameterAction = "ignore",
                        Parameters = [
                            new ValidateParameter { Name = "api-version", Action = "prevent" },
                            new ValidateParameter { Name = "filter", Action = "ignore" }
                        ]
                    },
                    Path = new ValidatePathParameters
                    {
                        SpecifiedParameterAction = "prevent",
                        Parameters = [
                            new ValidateParameter { Name = "id", Action = "prevent" },
                            new ValidateParameter { Name = "resourceType", Action = "ignore" }
                        ]
                    }
                });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <validate-parameters specified-parameter-action="prevent" unspecified-parameter-action="detect" errors-variable-name="param-validation-errors">
                    <headers specified-parameter-action="prevent" unspecified-parameter-action="ignore">
                        <parameter name="Authorization" action="prevent" />
                        <parameter name="Content-Type" action="ignore" />
                    </headers>
                    <query specified-parameter-action="prevent" unspecified-parameter-action="ignore">
                        <parameter name="api-version" action="prevent" />
                        <parameter name="filter" action="ignore" />
                    </query>
                    <path specified-parameter-action="prevent">
                        <parameter name="id" action="prevent" />
                        <parameter name="resourceType" action="ignore" />
                    </path>
                </validate-parameters>
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile validate-parameters with all sections"
    )]
    public void ShouldCompileValidateParametersPolicy(string code, string expectedXml)
    {
        code.CompileDocument().Should().BeSuccessful().And.DocumentEquivalentTo(expectedXml);
    }
}