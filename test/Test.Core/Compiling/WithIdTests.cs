// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling;

[TestClass]
public class WithIdCompilationTests
{
    [TestMethod]
    public void ShouldCompileWithIdOnSetHeaderInInbound()
    {
        var code =
            """
            [Document]
            public class PolicyDocument : IDocument
            {
                public void Inbound(IInboundContext context) 
                { 
                    context.WithId("my-header-id").SetHeader("X-Header", "1");
                }
            }
            """;

        var result = code.CompileDocument();

        var expectedXml =
            """
            <policies>
                <inbound>
                    <set-header id="my-header-id" name="X-Header" exists-action="override">
                        <value>1</value>
                    </set-header>
                </inbound>
            </policies>
            """;
        result.Should().BeSuccessful().And.DocumentEquivalentTo(expectedXml);
    }

    [TestMethod]
    public void ShouldCompileWithIdInAllSections()
    {
        var code =
            """
            [Document]
            public class PolicyDocument : IDocument
            {
                public void Inbound(IInboundContext context) 
                { 
                    context.WithId("inbound-id").SetHeader("X-Header", "1");
                }
                public void Backend(IBackendContext context) 
                { 
                    context.WithId("backend-id").SetHeader("X-Header", "1");
                }
                public void Outbound(IOutboundContext context)
                {
                    context.WithId("outbound-id").SetHeader("X-Header", "1");
                }
                public void OnError(IOnErrorContext context) 
                { 
                    context.WithId("onerror-id").SetHeader("X-Header", "1");
                }
            }
            """;

        var result = code.CompileDocument();

        var expectedXml =
            """
            <policies>
                <inbound>
                    <set-header id="inbound-id" name="X-Header" exists-action="override">
                        <value>1</value>
                    </set-header>
                </inbound>
                <backend>
                    <set-header id="backend-id" name="X-Header" exists-action="override">
                        <value>1</value>
                    </set-header>
                </backend>
                <outbound>
                    <set-header id="outbound-id" name="X-Header" exists-action="override">
                        <value>1</value>
                    </set-header>
                </outbound>
                <on-error>
                    <set-header id="onerror-id" name="X-Header" exists-action="override">
                        <value>1</value>
                    </set-header>
                </on-error>
            </policies>
            """;
        result.Should().BeSuccessful().And.DocumentEquivalentTo(expectedXml);
    }

    [TestMethod]
    public void ShouldUseLastIdWhenChainedWithIdCalls()
    {
        var code =
            """
            [Document]
            public class PolicyDocument : IDocument
            {
                public void Inbound(IInboundContext context) 
                { 
                    context.WithId("first").WithId("second").WithId("last").SetHeader("X-Header", "1");
                }
            }
            """;

        var result = code.CompileDocument();

        var expectedXml =
            """
            <policies>
                <inbound>
                    <set-header id="last" name="X-Header" exists-action="override">
                        <value>1</value>
                    </set-header>
                </inbound>
            </policies>
            """;
        result.Should().BeSuccessful().And.DocumentEquivalentTo(expectedXml);
    }

    [TestMethod]
    public void ShouldOnlyApplyIdToNextPolicy()
    {
        var code =
            """
            [Document]
            public class PolicyDocument : IDocument
            {
                public void Inbound(IInboundContext context) 
                { 
                    context.WithId("first-id").SetHeader("X-First", "1");
                    context.SetHeader("X-Second", "2");
                }
            }
            """;

        var result = code.CompileDocument();

        var expectedXml =
            """
            <policies>
                <inbound>
                    <set-header id="first-id" name="X-First" exists-action="override">
                        <value>1</value>
                    </set-header>
                    <set-header name="X-Second" exists-action="override">
                        <value>2</value>
                    </set-header>
                </inbound>
            </policies>
            """;
        result.Should().BeSuccessful().And.DocumentEquivalentTo(expectedXml);
    }

    [TestMethod]
    public void ShouldCompileWithIdOnDifferentPolicies()
    {
        var code =
            """
            [Document]
            public class PolicyDocument : IDocument
            {
                public void Inbound(IInboundContext context) 
                { 
                    context.WithId("append-id").AppendHeader("X-Append", "1");
                    context.WithId("remove-id").RemoveHeader("X-Remove");
                    context.WithId("skip-id").SetHeaderIfNotExist("X-Skip", "1");
                }
            }
            """;

        var result = code.CompileDocument();

        var expectedXml =
            """
            <policies>
                <inbound>
                    <set-header id="append-id" name="X-Append" exists-action="append">
                        <value>1</value>
                    </set-header>
                    <set-header id="remove-id" name="X-Remove" exists-action="delete" />
                    <set-header id="skip-id" name="X-Skip" exists-action="skip">
                        <value>1</value>
                    </set-header>
                </inbound>
            </policies>
            """;
        result.Should().BeSuccessful().And.DocumentEquivalentTo(expectedXml);
    }

    [TestMethod]
    public void ShouldCompileWithIdUsingConstant()
    {
        var code =
            """
            [Document]
            public class PolicyDocument : IDocument
            {
                private const string PolicyId = "constant-id";
                
                public void Inbound(IInboundContext context) 
                { 
                    context.WithId(PolicyId).SetHeader("X-Header", "1");
                }
            }
            """;

        var result = code.CompileDocument();

        var expectedXml =
            """
            <policies>
                <inbound>
                    <set-header id="constant-id" name="X-Header" exists-action="override">
                        <value>1</value>
                    </set-header>
                </inbound>
            </policies>
            """;
        result.Should().BeSuccessful().And.DocumentEquivalentTo(expectedXml);
    }

    [TestMethod]
    public void ShouldNotPropagateIdToNestedScope()
    {
        var code =
            """
            [Document]
            public class PolicyDocument : IDocument
            {
                public void Inbound(IInboundContext context) 
                { 
                    context.WithId("outer-id").SetHeader("X-Outer", "1");
                    if (Condition(context.ExpressionContext))
                    {
                        context.SetHeader("X-Inner", "2");
                    }
                }
                
                bool Condition(IExpressionContext ctx) => true;
            }
            """;

        var result = code.CompileDocument();

        var expectedXml =
            """
            <policies>
                <inbound>
                    <set-header id="outer-id" name="X-Outer" exists-action="override">
                        <value>1</value>
                    </set-header>
                    <choose>
                        <when condition="@(true)">
                            <set-header name="X-Inner" exists-action="override">
                                <value>2</value>
                            </set-header>
                        </when>
                    </choose>
                </inbound>
            </policies>
            """;
        result.Should().BeSuccessful().And.DocumentEquivalentTo(expectedXml);
    }

    [TestMethod]
    public void ShouldCompileWithIdInsideNestedScope()
    {
        var code =
            """
            [Document]
            public class PolicyDocument : IDocument
            {
                public void Inbound(IInboundContext context) 
                { 
                    if (Condition(context.ExpressionContext))
                    {
                        context.WithId("inner-id").SetHeader("X-Inner", "1");
                    }
                }
                
                bool Condition(IExpressionContext ctx) => true;
            }
            """;

        var result = code.CompileDocument();

        var expectedXml =
            """
            <policies>
                <inbound>
                    <choose>
                        <when condition="@(true)">
                            <set-header id="inner-id" name="X-Inner" exists-action="override">
                                <value>1</value>
                            </set-header>
                        </when>
                    </choose>
                </inbound>
            </policies>
            """;
        result.Should().BeSuccessful().And.DocumentEquivalentTo(expectedXml);
    }

    [TestMethod]
    public void ShouldCompileMixedPoliciesWithAndWithoutId()
    {
        var code =
            """
            [Document]
            public class PolicyDocument : IDocument
            {
                public void Inbound(IInboundContext context) 
                { 
                    context.WithId("first-id").SetHeader("X-First", "1");
                    context.SetHeader("X-Second", "2");
                    context.WithId("third-id").SetHeader("X-Third", "3");
                    context.SetHeader("X-Fourth", "4");
                    context.WithId("fifth-id").SetHeader("X-Fifth", "5");
                }
            }
            """;

        var result = code.CompileDocument();

        var expectedXml =
            """
            <policies>
                <inbound>
                    <set-header id="first-id" name="X-First" exists-action="override">
                        <value>1</value>
                    </set-header>
                    <set-header name="X-Second" exists-action="override">
                        <value>2</value>
                    </set-header>
                    <set-header id="third-id" name="X-Third" exists-action="override">
                        <value>3</value>
                    </set-header>
                    <set-header name="X-Fourth" exists-action="override">
                        <value>4</value>
                    </set-header>
                    <set-header id="fifth-id" name="X-Fifth" exists-action="override">
                        <value>5</value>
                    </set-header>
                </inbound>
            </policies>
            """;
        result.Should().BeSuccessful().And.DocumentEquivalentTo(expectedXml);
    }
}