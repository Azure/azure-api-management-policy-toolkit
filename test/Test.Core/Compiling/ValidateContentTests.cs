// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling;

[TestClass]
public class ValidateContentTests
{
    [TestMethod]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.ValidateContent(new ValidateContentConfig
                {
                    UnspecifiedContentTypeAction = "detect",
                    MaxSize = 10240,
                    SizeExceededAction = "ignore"
                });
            }
            public void Outbound(IOutboundContext context) {
                context.ValidateContent(new ValidateContentConfig
                {
                    UnspecifiedContentTypeAction = "detect",
                    MaxSize = 10240,
                    SizeExceededAction = "ignore"
                });
            }
            public void OnError(IOnErrorContext context) {
                context.ValidateContent(new ValidateContentConfig
                {
                    UnspecifiedContentTypeAction = "detect",
                    MaxSize = 10240,
                    SizeExceededAction = "ignore"
                });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <validate-content unspecified-content-type-action="detect" max-size="10240" size-exceeded-action="ignore" />
            </inbound>
            <outbound>
                <validate-content unspecified-content-type-action="detect" max-size="10240" size-exceeded-action="ignore" />
            </outbound>
            <on-error>
                <validate-content unspecified-content-type-action="detect" max-size="10240" size-exceeded-action="ignore" />
            </on-error>
        </policies>
        """,
        DisplayName = "Should compile validate-content policy in sections"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.ValidateContent(new ValidateContentConfig
                {
                    UnspecifiedContentTypeAction = GetUnspecifiedContentTypeAction(context.ExpressionContext),
                    MaxSize = 10240,
                    SizeExceededAction = "ignore"
                });
            }
            string GetUnspecifiedContentTypeAction(IExpressionContext context) => context.Variables["action"].ToString();
        }
        """,
        """
        <policies>
            <inbound>
                <validate-content unspecified-content-type-action="@(context.Variables["action"].ToString())" max-size="10240" size-exceeded-action="ignore" />
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile validate-content policy with expression in max-size"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.ValidateContent(new ValidateContentConfig
                {
                    UnspecifiedContentTypeAction = "detect",
                    MaxSize = GetMaxSize(context.ExpressionContext),
                    SizeExceededAction = "ignore"
                });
            }
            int GetMaxSize(IExpressionContext context) => (int)context.Variables["maxSize"];
        }
        """,
        """
        <policies>
            <inbound>
                <validate-content unspecified-content-type-action="detect" max-size="@((int)context.Variables["maxSize"])" size-exceeded-action="ignore" />
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile validate-content policy with expression in unspecified-content-type-action"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.ValidateContent(new ValidateContentConfig
                {
                    UnspecifiedContentTypeAction = "detect",
                    MaxSize = 10240,
                    SizeExceededAction = GetSizeExceededAction(context.ExpressionContext)
                });
            }
            string GetSizeExceededAction(IExpressionContext context) => context.Variables["sizeExceededAction"].ToString();
        }
        """,
        """
        <policies>
            <inbound>
                <validate-content unspecified-content-type-action="detect" max-size="10240" size-exceeded-action="@(context.Variables["sizeExceededAction"].ToString())" />
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile validate-content policy with expression in size-exceeded-action"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.ValidateContent(new ValidateContentConfig
                {
                    UnspecifiedContentTypeAction = "detect",
                    MaxSize = 10240,
                    SizeExceededAction = "ignore",
                    ErrorsVariableName = "content-validation-errors"
                });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <validate-content unspecified-content-type-action="detect" max-size="10240" size-exceeded-action="ignore" errors-variable-name="content-validation-errors" />
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile validate-content policy with errors-variable-name"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.ValidateContent(new ValidateContentConfig
                {
                    UnspecifiedContentTypeAction = "detect",
                    MaxSize = 10240,
                    SizeExceededAction = "ignore",
                    Contents = [
                        new ValidateContent {
                            ValidateAs = "json",
                            Action = "detect",
                        }
                    ]
                });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <validate-content unspecified-content-type-action="detect" max-size="10240" size-exceeded-action="ignore">
                    <content validate-as="json" action="detect" />
                </validate-content>
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile validate-content policy with single content"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.ValidateContent(new ValidateContentConfig
                {
                    UnspecifiedContentTypeAction = "detect",
                    MaxSize = 10240,
                    SizeExceededAction = "ignore",
                    Contents = [
                        new ValidateContent {
                            ValidateAs = "json",
                            Action = "detect",
                            Type = "application/json"
                        },
                        new ValidateContent {
                            ValidateAs = "xml",
                            Action = "prevent"
                        }
                    ]
                });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <validate-content unspecified-content-type-action="detect" max-size="10240" size-exceeded-action="ignore">
                    <content validate-as="json" action="detect" type="application/json" />
                    <content validate-as="xml" action="prevent" />
                </validate-content>
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile validate-content policy with multiple contents"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.ValidateContent(new ValidateContentConfig
                {
                    UnspecifiedContentTypeAction = "detect",
                    MaxSize = 10240,
                    SizeExceededAction = "ignore",
                    Contents = [
                        new ValidateContent {
                            ValidateAs = "json",
                            Action = GetAction(context.ExpressionContext),
                        }
                    ]
                });
            }
            string GetAction(IExpressionContext context) => context.Variables["action"].ToString();
        }
        """,
        """
        <policies>
            <inbound>
                <validate-content unspecified-content-type-action="detect" max-size="10240" size-exceeded-action="ignore">
                    <content validate-as="json" action="@(context.Variables["action"].ToString())" />
                </validate-content>
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile validate-content policy with expression in action in content"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.ValidateContent(new ValidateContentConfig
                {
                    UnspecifiedContentTypeAction = "detect",
                    MaxSize = 10240,
                    SizeExceededAction = "ignore",
                    Contents = [
                        new ValidateContent {
                            ValidateAs = "json",
                            Action = "detect",
                            Type = "application/json"
                        }
                    ]
                });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <validate-content unspecified-content-type-action="detect" max-size="10240" size-exceeded-action="ignore">
                    <content validate-as="json" action="detect" type="application/json" />
                </validate-content>
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile validate-content policy with type in content"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.ValidateContent(new ValidateContentConfig
                {
                    UnspecifiedContentTypeAction = "detect",
                    MaxSize = 10240,
                    SizeExceededAction = "ignore",
                    Contents = [
                        new ValidateContent {
                            ValidateAs = "json",
                            Action = "detect",
                            SchemaId = "schema-id"
                        }
                    ]
                });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <validate-content unspecified-content-type-action="detect" max-size="10240" size-exceeded-action="ignore">
                    <content validate-as="json" action="detect" schema-id="schema-id" />
                </validate-content>
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile validate-content policy with schema-id in content"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.ValidateContent(new ValidateContentConfig
                {
                    UnspecifiedContentTypeAction = "detect",
                    MaxSize = 10240,
                    SizeExceededAction = "ignore",
                    Contents = [
                        new ValidateContent {
                            ValidateAs = "json",
                            Action = "detect",
                            SchemaId = "schema-id",
                            SchemaRef = "#/components/schemas/address"
                        }
                    ]
                });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <validate-content unspecified-content-type-action="detect" max-size="10240" size-exceeded-action="ignore">
                    <content validate-as="json" action="detect" schema-id="schema-id" schema-ref="#/components/schemas/address" />
                </validate-content>
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile validate-content policy with schema-ref in content"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.ValidateContent(new ValidateContentConfig
                {
                    UnspecifiedContentTypeAction = "detect",
                    MaxSize = 10240,
                    SizeExceededAction = "ignore",
                    Contents = [
                        new ValidateContent {
                            ValidateAs = "json",
                            Action = "detect",
                            AllowAdditionalProperties = true
                        }
                    ]
                });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <validate-content unspecified-content-type-action="detect" max-size="10240" size-exceeded-action="ignore">
                    <content validate-as="json" action="detect" allow-additional-properties="true" />
                </validate-content>
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile validate-content policy with allow-additional-properties in content"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.ValidateContent(new ValidateContentConfig
                {
                    UnspecifiedContentTypeAction = "detect",
                    MaxSize = 10240,
                    SizeExceededAction = "ignore",
                    Contents = [
                        new ValidateContent {
                            ValidateAs = "json",
                            Action = "detect",
                            CaseInsensitivePropertyNames = true
                        }
                    ]
                });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <validate-content unspecified-content-type-action="detect" max-size="10240" size-exceeded-action="ignore">
                    <content validate-as="json" action="detect" case-insensitive-property-names="true" />
                </validate-content>
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile validate-content policy with case-insensitive-property-names in content"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.ValidateContent(new ValidateContentConfig
                {
                    UnspecifiedContentTypeAction = "detect",
                    MaxSize = 10240,
                    SizeExceededAction = "ignore",
                    ContentTypeMap = new ContentTypeMapConfig {
                        AnyContentTypeValue = "application/octet-stream",
                    }
                });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <validate-content unspecified-content-type-action="detect" max-size="10240" size-exceeded-action="ignore">
                    <content-type-map any-content-type-value="application/octet-stream" />
                </validate-content>
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile validate-content policy with any-content-type-value in content-type-map"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.ValidateContent(new ValidateContentConfig
                {
                    UnspecifiedContentTypeAction = "detect",
                    MaxSize = 102400,
                    SizeExceededAction = "ignore",
                    ContentTypeMap = new ContentTypeMapConfig {
                        MissingContentTypeValue = "application/json",
                    }
                });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <validate-content unspecified-content-type-action="detect" max-size="102400" size-exceeded-action="ignore">
                    <content-type-map missing-content-type-value="application/json" />
                </validate-content>
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile validate-content policy with missing-content-type-value in content-type-map"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.ValidateContent(new ValidateContentConfig
                {
                    UnspecifiedContentTypeAction = "detect",
                    MaxSize = 102400,
                    SizeExceededAction = "ignore",
                    ContentTypeMap = new ContentTypeMapConfig {
                        Types = [
                            new ContentTypeMap { To = "application/json" },
                        ]
                    }
                });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <validate-content unspecified-content-type-action="detect" max-size="102400" size-exceeded-action="ignore">
                    <content-type-map>
                        <type to="application/json" />
                    </content-type-map>
                </validate-content>
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile validate-content policy with single type in content-type-map"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.ValidateContent(new ValidateContentConfig
                {
                    UnspecifiedContentTypeAction = "detect",
                    MaxSize = 102400,
                    SizeExceededAction = "ignore",
                    ContentTypeMap = new ContentTypeMapConfig {
                        Types = [
                            new ContentTypeMap { To = "application/xml" },
                            new ContentTypeMap { To = "application/json" },
                        ]
                    }
                });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <validate-content unspecified-content-type-action="detect" max-size="102400" size-exceeded-action="ignore">
                    <content-type-map>
                        <type to="application/xml" />
                        <type to="application/json" />
                    </content-type-map>
                </validate-content>
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile validate-content policy with multiple type in content-type-map"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.ValidateContent(new ValidateContentConfig
                {
                    UnspecifiedContentTypeAction = "detect",
                    MaxSize = 102400,
                    SizeExceededAction = "ignore",
                    ContentTypeMap = new ContentTypeMapConfig {
                        Types = [
                            new ContentTypeMap { To = "application/json" From = "text/plain" },
                        ]
                    }
                });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <validate-content unspecified-content-type-action="detect" max-size="102400" size-exceeded-action="ignore">
                    <content-type-map>
                        <type to="application/json" from="text/plain" />
                    </content-type-map>
                </validate-content>
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile validate-content policy wwith from in type in content-type-map"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.ValidateContent(new ValidateContentConfig
                {
                    UnspecifiedContentTypeAction = "detect",
                    MaxSize = 102400,
                    SizeExceededAction = "ignore",
                    ContentTypeMap = new ContentTypeMapConfig {
                        Types = [
                            new ContentTypeMap { To = "application/json" When = true },
                        ]
                    }
                });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <validate-content unspecified-content-type-action="detect" max-size="102400" size-exceeded-action="ignore">
                    <content-type-map>
                        <type to="application/json" when="true" />
                    </content-type-map>
                </validate-content>
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile validate-content policy with from in type in content-type-map"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.ValidateContent(new ValidateContentConfig
                {
                    UnspecifiedContentTypeAction = "detect",
                    MaxSize = 102400,
                    SizeExceededAction = "ignore",
                    ContentTypeMap = new ContentTypeMapConfig {
                        Types = [
                            new ContentTypeMap { To = "application/json" When = GetWhen(context.ExpressionContext) },
                        ]
                    }
                });
            }
            
            bool GetWhen(IExpressionContext context) => (bool)context.Variables["when"];
        }
        """,
        """
        <policies>
            <inbound>
                <validate-content unspecified-content-type-action="detect" max-size="102400" size-exceeded-action="ignore">
                    <content-type-map>
                        <type to="application/json" when="@((bool)context.Variables["when"])" />
                    </content-type-map>
                </validate-content>
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile validate-content policy with expression in from in type in content-type-map"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.ValidateContent(new ValidateContentConfig
                {
                    UnspecifiedContentTypeAction = "prevent",
                    MaxSize = 102400,
                    SizeExceededAction = "prevent",
                    ErrorsVariableName = "requestBodyValidation",
                    ContentTypeMap = new ContentTypeMapConfig {
                        MissingContentTypeValue = "application/json",
                        Types = [
                            new ContentTypeMap { To = "application/json", From = "application/hal+json" }
                        ]
                    },
                    Contents = [
                        new ValidateContent {
                            ValidateAs = "json",
                            Type = "application/json"
                            Action = "detect",
                            AllowAdditionalProperties = false
                        }
                    ]
                });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <validate-content unspecified-content-type-action="prevent" max-size="102400" size-exceeded-action="prevent" errors-variable-name="requestBodyValidation">
                    <content-type-map missing-content-type-value="application/json">
                        <type to="application/json" from="application/hal+json" />
                    </content-type-map>
                    <content validate-as="json" action="detect" type="application/json" allow-additional-properties="false" />
                </validate-content>
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile validate-content policy"
    )]
    public void ShouldCompileValidateContentPolicy(string code, string expectedXml)
    {
        code.CompileDocument().Should().BeSuccessful().And.DocumentEquivalentTo(expectedXml);
    }
}