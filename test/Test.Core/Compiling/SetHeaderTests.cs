// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling;

[TestClass]
public class SetHeaderCompilationTests
{
    [TestMethod]
    [DataRow("SetHeader", "override")]
    [DataRow("AppendHeader", "append")]
    [DataRow("SetHeaderIfNotExist", "skip")]
    public void ShouldCompileSetHeaderPolicyInSections(string method, string type)
    {
        var code =
            $$"""
              [Document]
              public class PolicyDocument : IDocument
              {
                  public void Inbound(IInboundContext context) 
                  { 
                      context.{{method}}("X-Header", "1");
                  }
                  public void Backend(IBackendContext context) 
                  { 
                      context.{{method}}("X-Header", "1");
                  }
                  public void Outbound(IOutboundContext context)
                  {
                      context.{{method}}("X-Header", "1");
                  }
                  public void OnError(IOnErrorContext context) 
                  { 
                      context.{{method}}("X-Header", "1");
                  }
              }
              """;

        var result = code.CompileDocument();

        var expectedXml =
            $$"""
              <policies>
                  <inbound>
                      <set-header name="X-Header" exists-action="{{type}}">
                          <value>1</value>
                      </set-header>
                  </inbound>
                  <backend>
                      <set-header name="X-Header" exists-action="{{type}}">
                          <value>1</value>
                      </set-header>
                  </backend>
                  <outbound>
                      <set-header name="X-Header" exists-action="{{type}}">
                          <value>1</value>
                      </set-header>
                  </outbound>
                  <on-error>
                      <set-header name="X-Header" exists-action="{{type}}">
                          <value>1</value>
                      </set-header>
                  </on-error>
              </policies>
              """;
        result.Should().BeSuccessful().And.DocumentEquivalentTo(expectedXml);
    }

    [TestMethod]
    public void ShouldCompileRemoveHeaderPolicyInSections()
    {
        var code =
            $$"""
              [Document]
              public class PolicyDocument : IDocument
              {
                  public void Inbound(IInboundContext context) 
                  {
                      context.RemoveHeader("Delete");
                  }
                  public void Backend(IBackendContext context) 
                  { 
                      context.RemoveHeader("Delete");
                  }
                  public void Outbound(IOutboundContext context)
                  {
                      context.RemoveHeader("Delete");
                  }
                  public void OnError(IOnErrorContext context) 
                  { 
                      context.RemoveHeader("Delete");
                  }
              }
              """;

        var result = code.CompileDocument();

        var expectedXml =
            """
            <policies>
                <inbound>
                    <set-header name="Delete" exists-action="delete" />
                </inbound>
                <backend>
                    <set-header name="Delete" exists-action="delete" />
                </backend>
                <outbound>
                    <set-header name="Delete" exists-action="delete" />
                </outbound>
                <on-error>
                    <set-header name="Delete" exists-action="delete" />
                </on-error>
            </policies>
            """;
        result.Should().BeSuccessful().And.DocumentEquivalentTo(expectedXml);
    }

    [TestMethod]
    [DataRow("SetHeader", "override")]
    [DataRow("AppendHeader", "append")]
    [DataRow("SetHeaderIfNotExist", "skip")]
    public void ShouldCompileSetHeaderPolicyWithMultipleParameters(string method, string type)
    {
        var code =
            $$"""
              [Document]
              public class PolicyDocument : IDocument
              {
                  public void Inbound(IInboundContext context) 
                  {
                      context.{{method}}("X-Header", "1", "2", "3");
                  }
                  public void Outbound(IOutboundContext context)
                  {
                      context.{{method}}("X-Header", "3", "2", "1");
                  }
              }
              """;

        var result = code.CompileDocument();

        var expectedXml =
            $"""
             <policies>
                 <inbound>
                     <set-header name="X-Header" exists-action="{type}">
                         <value>1</value>
                         <value>2</value>
                         <value>3</value>
                     </set-header>
                 </inbound>
                 <outbound>
                     <set-header name="X-Header" exists-action="{type}">
                         <value>3</value>
                         <value>2</value>
                         <value>1</value>
                     </set-header>
                 </outbound>
             </policies>
             """;
        result.Should().BeSuccessful().And.DocumentEquivalentTo(expectedXml);
    }

    [TestMethod]
    public void ShouldCompileSetHeaderPolicyWithPolicyExpressionInName()
    {
        var code =
            """
            [Document]
            public class PolicyDocument : IDocument
            {
                public void Inbound(IInboundContext context) 
                {
                    context.SetHeader(NameFromExpression(context.ExpressionContext), "1");
                }
                public void Outbound(IOutboundContext context)
                {
                    context.SetHeader(NameFromExpression(context.ExpressionContext), "1");
                }

                public string NameFromExpression(IExpressionContext context) => "name" + context.RequestId;
            }
            """;

        var result = code.CompileDocument();

        var expectedXml =
            """
            <policies>
                <inbound>
                    <set-header name="@("name" + context.RequestId)" exists-action="override">
                        <value>1</value>
                    </set-header>
                </inbound>
                <outbound>
                    <set-header name="@("name" + context.RequestId)" exists-action="override">
                        <value>1</value>
                    </set-header>
                </outbound>
            </policies>
            """;
        result.Should().BeSuccessful().And.DocumentEquivalentTo(expectedXml);
    }

    [TestMethod]
    public void ShouldCompileSetHeaderPolicyWithPolicyExpressionInValue()
    {
        var code =
            """
            [Document]
            public class PolicyDocument : IDocument
            {
                public void Inbound(IInboundContext context) 
                {
                    context.SetHeader("X-Header", "1", ValueFromExpression(context.ExpressionContext), "2");
                }
                public void Outbound(IOutboundContext context)
                {
                    context.SetHeader("X-Header", "1", ValueFromExpression(context.ExpressionContext), "2");
                }

                public string ValueFromExpression(IExpressionContext context) => "value" + context.RequestId;
            }
            """;

        var result = code.CompileDocument();

        var expectedXml =
            """
            <policies>
                <inbound>
                    <set-header name="X-Header" exists-action="override">
                        <value>1</value>
                        <value>@("value" + context.RequestId)</value>
                        <value>2</value>
                    </set-header>
                </inbound>
                <outbound>
                    <set-header name="X-Header" exists-action="override">
                        <value>1</value>
                        <value>@("value" + context.RequestId)</value>
                        <value>2</value>
                    </set-header>
                </outbound>
            </policies>
            """;
        result.Should().BeSuccessful().And.DocumentEquivalentTo(expectedXml);
    }
}