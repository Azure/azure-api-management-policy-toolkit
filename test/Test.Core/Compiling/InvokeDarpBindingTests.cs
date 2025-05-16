// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling;

[TestClass]
public class InvokeDarpBindingTests
{
    [TestMethod]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) { context.InvokeDarpBinding(new InvokeDarpBindingConfig { Name = "inbound" }); }
            public void Outbound(IOutboundContext context) { context.InvokeDarpBinding(new InvokeDarpBindingConfig { Name = "outbound" }); }
            public void OnError(IOnErrorContext context) { context.InvokeDarpBinding(new InvokeDarpBindingConfig { Name = "on-error" }); }
        }
        """,
        """
        <policies>
            <inbound>
                <invoke-darp-binding name="inbound" />
            </inbound>
            <outbound>
                <invoke-darp-binding name="outbound" />
            </outbound>
            <on-error>
                <invoke-darp-binding name="on-error" />
            </on-error>
        </policies>
        """,
        DisplayName = "Should compile invoke-darp-binding policy in sections"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.InvokeDarpBinding(new InvokeDarpBindingConfig {
                    Name = GetBindingName(context.ExpressionContext)
                });
            }
            
            private string GetBindingName(IExpressionContext context) => "binding-" + context.Variables["suffix"];
        }
        """,
        """
        <policies>
            <inbound>
                <invoke-darp-binding name="@("binding-" + context.Variables["suffix"])" />
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile invoke-darp-binding policy with expression in name"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.InvokeDarpBinding(new InvokeDarpBindingConfig {
                    Name = "test-binding",
                    Operation = "operation1"
                });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <invoke-darp-binding name="test-binding" operation="operation1" />
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile invoke-darp-binding policy with operation parameter"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.InvokeDarpBinding(new InvokeDarpBindingConfig {
                    Name = "test-binding",
                    IgnoreError = true
                });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <invoke-darp-binding name="test-binding" ignore-error="true" />
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile invoke-darp-binding policy with ignore-error"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.InvokeDarpBinding(new InvokeDarpBindingConfig {
                    Name = "test-binding",
                    ResponseVariableName = "darp-response"
                });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <invoke-darp-binding name="test-binding" response-variable-name="darp-response" />
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile invoke-darp-binding policy with response-variable-name"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.InvokeDarpBinding(new InvokeDarpBindingConfig {
                    Name = "test-binding",
                    Timeout = 5000
                });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <invoke-darp-binding name="test-binding" timeout="5000" />
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile invoke-darp-binding policy with timeout"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.InvokeDarpBinding(new InvokeDarpBindingConfig {
                    Name = "test-binding",
                    Timeout = GetTimeout(context.ExpressionContext)
                });
            }
            
            private int GetTimeout(IExpressionContext context) => 1000 * int.Parse(context.Variables["multiplier"].ToString());
        }
        """,
        """
        <policies>
            <inbound>
                <invoke-darp-binding name="test-binding" timeout="@(1000 * int.Parse(context.Variables["multiplier"].ToString()))" />
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile invoke-darp-binding policy with expression in timeout"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.InvokeDarpBinding(new InvokeDarpBindingConfig {
                    Name = "test-binding",
                    Template = "liquid"
                });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <invoke-darp-binding name="test-binding" template="liquid" />
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile invoke-darp-binding policy with template"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.InvokeDarpBinding(new InvokeDarpBindingConfig {
                    Name = "test-binding",
                    ContentType = "application/json"
                });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <invoke-darp-binding name="test-binding" content-type="application/json" />
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile invoke-darp-binding policy with content-type"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.InvokeDarpBinding(new InvokeDarpBindingConfig {
                    Name = "test-binding",
                    MetaData = [
                        new DarpMetaData { Key = "key1", Value = "value1" }
                    ]
                });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <invoke-darp-binding name="test-binding">
                    <metadata>
                        <item key="key1">value1</item>
                    </metadata>
                </invoke-darp-binding>
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile invoke-darp-binding policy with single matadata item"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.InvokeDarpBinding(new InvokeDarpBindingConfig {
                    Name = "test-binding",
                    MetaData = [
                        new DarpMetaData { Key = "key1", Value = "value1" },
                        new DarpMetaData { Key = "key2", Value = "value2" },
                        new DarpMetaData { Key = "key3", Value = "value3" }
                    ]
                });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <invoke-darp-binding name="test-binding">
                    <metadata>
                        <item key="key1">value1</item>
                        <item key="key2">value2</item>
                        <item key="key3">value3</item>
                    </metadata>
                </invoke-darp-binding>
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile invoke-darp-binding policy with multiple matadata items"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.InvokeDarpBinding(new InvokeDarpBindingConfig {
                    Name = "test-binding",
                    MetaData = [
                        new DarpMetaData { Key = "key1", Value = GetValue(context.ExpressionContext) }
                    ]
                });
            }
            
            private string GetValue(IExpressionContext context) => context.Request.Headers["X-Custom-Header"];
        }
        """,
        """
        <policies>
            <inbound>
                <invoke-darp-binding name="test-binding">
                    <metadata>
                        <item key="key1">@(context.Request.Headers["X-Custom-Header"])</item>
                    </metadata>
                </invoke-darp-binding>
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile invoke-darp-binding policy with expression in matadata item value"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.InvokeDarpBinding(new InvokeDarpBindingConfig {
                    Name = "test-binding",
                    Data = "data-item1"
                });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <invoke-darp-binding name="test-binding">
                    <data>data-item1</data>
                </invoke-darp-binding>
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile invoke-darp-binding policy with data"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.InvokeDarpBinding(new InvokeDarpBindingConfig {
                    Name = "test-binding",
                    Data = GetDataItem(context.ExpressionContext)
                });
            }
            
            private string GetDataItem(IExpressionContext context) => context.Request.Body.As<string>();
        }
        """,
        """
        <policies>
            <inbound>
                <invoke-darp-binding name="test-binding">
                    <data>@(context.Request.Body.As<string>())</data>
                </invoke-darp-binding>
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile invoke-darp-binding policy with expression in data"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.InvokeDarpBinding(new InvokeDarpBindingConfig {
                    Name = "comprehensive-test",
                    Operation = "test-operation",
                    IgnoreError = true,
                    ResponseVariableName = "response-var",
                    Timeout = 5000,
                    Template = "test-template",
                    ContentType = "application/json",
                    MetaData = [
                        new DarpMetaData { Key = "key1", Value = "value1" },
                        new DarpMetaData { Key = "key2", Value = "value2" }
                    ],
                    Data = "data1"
                });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <invoke-darp-binding name="comprehensive-test" operation="test-operation" ignore-error="true" response-variable-name="response-var" timeout="5000" template="test-template" content-type="application/json">
                    <metadata>
                        <item key="key1">value1</item>
                        <item key="key2">value2</item>
                    </metadata>
                    <data>data1</data>
                </invoke-darp-binding>
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile invoke-darp-binding policy with all properties"
    )]
    public void ShouldCompileInvokeDarpBindingPolicy(string code, string expectedXml)
    {
        code.CompileDocument().Should().BeSuccessful().And.DocumentEquivalentTo(expectedXml);
    }
}