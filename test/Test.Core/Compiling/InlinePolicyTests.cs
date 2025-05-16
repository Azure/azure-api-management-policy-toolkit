// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling;

[TestClass]
public class InlinePolicyTests
{
    [TestMethod]
    public void ShouldCompileInlinePolicyInSections()
    {
        var code =
            """
            [Document]
            public class PolicyDocument : IDocument
            {
              public void Inbound(IInboundContext context) 
              { 
                  context.InlinePolicy("<any-xml />");
              }
              public void Outbound(IOutboundContext context)
              {
                  context.InlinePolicy("<any-xml />");
              }
              public void Backend(IBackendContext context) 
              { 
                  context.InlinePolicy("<any-xml />");
              }
              public void OnError(IOnErrorContext context)
              {
                  context.InlinePolicy("<any-xml />");
              }
            }
            """;

        var result = code.CompileDocument();

        var expectedXml =
            """
            <policies>
                <inbound>
                    <any-xml />
                </inbound>
                <outbound>
                    <any-xml />
                </outbound>
                <backend>
                    <any-xml />
                </backend>
                <on-error>
                    <any-xml />
                </on-error>
            </policies>
            """;
        result.Should().BeSuccessful().And.DocumentEquivalentTo(expectedXml);
    }

    [TestMethod]
    public void ShouldCompileInlinePolicyWithExpressions()
    {
        var code =
            """"
            [Document]
            public class PolicyDocument : IDocument
            {
              public void Inbound(IInboundContext context) 
              { 
                  context.InlinePolicy("""
                                       <set-header name="@("name" + context.RequestId)" exists-action="override">
                                           <value>1</value>
                                           <value>@{return "value" + context.RequestId;}</value>
                                       </set-header>
                                       """);
              }
            }
            """";

        var result = code.CompileDocument();

        var expectedXml =
            """
            <policies>
                <inbound>
                    <set-header name="@("name" + context.RequestId)" exists-action="override">
                        <value>1</value>
                        <value>@{return "value" + context.RequestId;}</value>
                    </set-header>
                </inbound>
            </policies>
            """;
        result.Should().BeSuccessful().And.DocumentEquivalentTo(expectedXml);
    }
}