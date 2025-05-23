// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling;

[TestClass]
public class SetQueryParameterCompilationTests
{
    [TestMethod]
    [DataRow("SetQueryParameter", "override")]
    [DataRow("AppendQueryParameter", "append")]
    [DataRow("SetQueryParameterIfNotExist", "skip")]
    public void ShouldCompileSetQueryParameterPolicyInSections(string method, string type)
    {
        var code =
            $$"""
              [Document]
              public class PolicyDocument : IDocument
              {
                  public void Inbound(IInboundContext context) 
                  { 
                      context.{{method}}("param1", "1");
                  }
                  public void Backend(IBackendContext context) 
                  { 
                      context.{{method}}("param1", "1");
                  }
                  public void Outbound(IOutboundContext context) 
                  { 
                      context.{{method}}("param1", "1");
                  }
                  public void OnError(IOnErrorContext context) 
                  { 
                      context.{{method}}("param1", "1");
                  }
              }
              """;

        var result = code.CompileDocument();

        var expectedXml =
            $$"""
              <policies>
                  <inbound>
                      <set-query-parameter name="param1" exists-action="{{type}}">
                          <value>1</value>
                      </set-query-parameter>
                  </inbound>
                  <backend>
                      <set-query-parameter name="param1" exists-action="{{type}}">
                          <value>1</value>
                      </set-query-parameter>
                  </backend>
                  <outbound>
                      <set-query-parameter name="param1" exists-action="{{type}}">
                          <value>1</value>
                      </set-query-parameter>
                  </outbound>
                  <on-error>
                      <set-query-parameter name="param1" exists-action="{{type}}">
                          <value>1</value>
                      </set-query-parameter>
                  </on-error>
              </policies>
              """;
        result.Should().BeSuccessful().And.DocumentEquivalentTo(expectedXml);
    }

    [TestMethod]
    public void ShouldCompileRemoveQueryParameterPolicyInSections()
    {
        var code =
            $$"""
              [Document]
              public class PolicyDocument : IDocument
              {
                  public void Inbound(IInboundContext context) 
                  {
                      context.RemoveQueryParameter("param1");
                  }
                  public void Backend(IBackendContext context) 
                  { 
                      context.RemoveQueryParameter("param1");
                  }
                  public void Outbound(IOutboundContext context) 
                  { 
                      context.RemoveQueryParameter("param1");
                  }
                  public void OnError(IOnErrorContext context) 
                  { 
                      context.RemoveQueryParameter("param1");
                  }
              }
              """;

        var result = code.CompileDocument();

        var expectedXml =
            $"""
             <policies>
                 <inbound>
                     <set-query-parameter name="param1" exists-action="delete" />
                 </inbound>
                 <backend>
                     <set-query-parameter name="param1" exists-action="delete" />
                 </backend>
                 <outbound>
                     <set-query-parameter name="param1" exists-action="delete" />
                 </outbound>
                 <on-error>
                     <set-query-parameter name="param1" exists-action="delete" />
                 </on-error>
             </policies>
             """;
        result.Should().BeSuccessful().And.DocumentEquivalentTo(expectedXml);
    }

    [TestMethod]
    [DataRow("SetQueryParameter", "override")]
    [DataRow("AppendQueryParameter", "append")]
    [DataRow("SetQueryParameterIfNotExist", "skip")]
    public void ShouldCompileSetQueryParameterPolicyWithMultipleParameters(string method, string type)
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
              }
              """;

        var result = code.CompileDocument();

        var expectedXml =
            $"""
             <policies>
                 <inbound>
                     <set-query-parameter name="X-Header" exists-action="{type}">
                         <value>1</value>
                         <value>2</value>
                         <value>3</value>
                     </set-query-parameter>
                 </inbound>
             </policies>
             """;
        result.Should().BeSuccessful().And.DocumentEquivalentTo(expectedXml);
    }

    [TestMethod]
    public void ShouldCompileSetQueryParameterPolicyWithPolicyExpressionInName()
    {
        var code =
            """
            [Document]
            public class PolicyDocument : IDocument
            {
                public void Inbound(IInboundContext context) 
                {
                    context.SetQueryParameter(NameFromExpression(context.ExpressionContext), "1");
                }

                public string NameFromExpression(IExpressionContext context) => "name" + context.RequestId;
            }
            """;

        var result = code.CompileDocument();

        var expectedXml =
            """
            <policies>
                <inbound>
                    <set-query-parameter name="@("name" + context.RequestId)" exists-action="override">
                        <value>1</value>
                    </set-query-parameter>
                </inbound>
            </policies>
            """;
        result.Should().BeSuccessful().And.DocumentEquivalentTo(expectedXml);
    }

    [TestMethod]
    public void ShouldCompileSetQueryParameterPolicyWithPolicyExpressionInValue()
    {
        var code =
            """
            [Document]
            public class PolicyDocument : IDocument
            {
                public void Inbound(IInboundContext context) 
                {
                    context.SetQueryParameter("param1", "1", ValueFromExpression(context.ExpressionContext), "2");
                }

                public string ValueFromExpression(IExpressionContext context) => "value" + context.RequestId;
            }
            """;

        var result = code.CompileDocument();

        var expectedXml =
            """
            <policies>
                <inbound>
                    <set-query-parameter name="param1" exists-action="override">
                        <value>1</value>
                        <value>@("value" + context.RequestId)</value>
                        <value>2</value>
                    </set-query-parameter>
                </inbound>
            </policies>
            """;
        result.Should().BeSuccessful().And.DocumentEquivalentTo(expectedXml);
    }
}