// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.ApiManagement.PolicyToolkit.Compiling;

[TestClass]
public class ValidateOdataRequestTests
{
    [TestMethod]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.ValidateOdataRequest(new ValidateOdataRequestConfig
                {
                    ErrorVariableName = "odata-validation-error"
                });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <validate-odata-request error-variable-name="odata-validation-error" />
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile validate-odata-request policy with error-variable-name"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.ValidateOdataRequest(new ValidateOdataRequestConfig
                {
                    DefaultOdataVersion = "4.0"
                });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <validate-odata-request default-odata-version="4.0" />
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile validate-odata-request policy with default-odata-version"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.ValidateOdataRequest(new ValidateOdataRequestConfig
                {
                    MinOdataVersion = "3.0"
                });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <validate-odata-request min-odata-version="3.0" />
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile validate-odata-request policy with min-odata-version"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.ValidateOdataRequest(new ValidateOdataRequestConfig
                {
                    MaxOdataVersion = "4.01"
                });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <validate-odata-request max-odata-version="4.01" />
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile validate-odata-request policy with max-odata-version"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.ValidateOdataRequest(new ValidateOdataRequestConfig
                {
                    MaxSize = 100000
                });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <validate-odata-request max-size="100000" />
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile validate-odata-request policy with max-size"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.ValidateOdataRequest(new ValidateOdataRequestConfig
                {
                    ErrorVariableName = "odata-error",
                    DefaultOdataVersion = "4.0",
                    MinOdataVersion = "3.0",
                    MaxOdataVersion = "4.01",
                    MaxSize = 50000
                });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <validate-odata-request error-variable-name="odata-error" default-odata-version="4.0" min-odata-version="3.0" max-odata-version="4.01" max-size="50000" />
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile validate-odata-request policy with all properties"
    )]
    public void ShouldCompileValidateOdataRequestPolicy(string code, string expectedXml)
    {
        code.CompileDocument().Should().BeSuccessful().And.DocumentEquivalentTo(expectedXml);
    }
}