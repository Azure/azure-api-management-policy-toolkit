// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling;

[TestClass]
public class NamedValueTests
{
    [TestMethod]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.SetVariable("backend-url", BackendUrl(context.ExpressionContext));
            }
            
            [NamedValue("Api-Backend-Url")]
            string BackendUrl(IExpressionContext context) => throw new NotImplementedException();
        }
        """,
        """
        <policies>
            <inbound>
                <set-variable name="backend-url" value="{{Api-Backend-Url}}" />
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile simple NamedValue to {{token}}"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.SetVariable("url", BackendUrl(context.ExpressionContext));
            }
            
            [NamedValue("{{Api-Backend-Url}}/v2.0/prediction")]
            string BackendUrl(IExpressionContext context) => throw new NotImplementedException();
        }
        """,
        """
        <policies>
            <inbound>
                <set-variable name="url" value="{{Api-Backend-Url}}/v2.0/prediction" />
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile NamedValue template with embedded tokens as-is"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.SetHeader("Authorization", AuthHeader(context.ExpressionContext));
            }
            
            [NamedValue("{{Auth-Scheme}} {{Auth-Token}}")]
            string AuthHeader(IExpressionContext context) => throw new NotImplementedException();
        }
        """,
        """
        <policies>
            <inbound>
                <set-header name="Authorization" exists-action="override">
                    <value>{{Auth-Scheme}} {{Auth-Token}}</value>
                </set-header>
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile NamedValue template with multiple tokens"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.SetVariable("key", ApiKey(context.ExpressionContext));
                context.SetVariable("endpoint", Endpoint(context.ExpressionContext));
            }
            
            [NamedValue("My-Api-Key")]
            string ApiKey(IExpressionContext context) => throw new NotImplementedException();
            
            [NamedValue("{{Service-Url}}/api/v1")]
            string Endpoint(IExpressionContext context) => throw new NotImplementedException();
        }
        """,
        """
        <policies>
            <inbound>
                <set-variable name="key" value="{{My-Api-Key}}" />
                <set-variable name="endpoint" value="{{Service-Url}}/api/v1" />
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile mixed simple and template NamedValues"
    )]
    public void ShouldCompileNamedValue(string code, string expectedXml)
    {
        code.CompileDocument().Should().BeSuccessful().And.DocumentEquivalentTo(expectedXml);
    }
}
