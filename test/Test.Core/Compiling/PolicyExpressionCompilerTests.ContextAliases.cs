// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling;

public partial class PolicyExpressionCompilerTests
{
    [TestMethod]
    [DataRow("Identity(context.ExpressionContext)")]
    [DataRow("Identity(Identity(context.ExpressionContext))")]
    public void ShouldInlineProvenContextAliasWithoutEmittingSourceHelpers(string expression)
    {
        var document = $$"""
            [Document]
            public class PolicyDocument : IDocument
            {
                public void Inbound(IInboundContext context)
                {
                    context.SetVariable("value", Value({{expression}}));
                }

                private static IExpressionContext Identity(IExpressionContext context) => context;
                private static string Value(IExpressionContext context) => context.Request.Method;
            }
            """;

        document.CompileDocument().Should().BeSuccessful().And.DocumentEquivalentTo(
            """
            <policies>
                <inbound>
                    <set-variable name="value" value="@(context.Request.Method)" />
                </inbound>
            </policies>
            """);
    }

    [TestMethod]
    public void ShouldRejectCapturedContextFactoryInExpressionHelper()
    {
        const string document =
            """
            [Document]
            public class PolicyDocument : IDocument
            {
                private readonly IExpressionContext _outer;

                public PolicyDocument(IExpressionContext outer) => _outer = outer;

                public void Inbound(IInboundContext context)
                {
                    context.SetVariable("value", Value(GetOuter()));
                }

                private IExpressionContext GetOuter() => _outer;
                private static string Value(IExpressionContext context) => context.Request.Method;
            }
            """;

        document.CompileDocument().Errors.Should().Contain(error => error.Id == "APIM2013");
    }

    [TestMethod]
    public void ShouldRejectContextAliasThatDropsEffectfulArgument()
    {
        const string document =
            """
            [Document]
            public class PolicyDocument : IDocument
            {
                public void Inbound(IInboundContext context)
                {
                    context.SetVariable("value", Value(Identity(context.ExpressionContext, SideEffect())));
                }

                private static IExpressionContext Identity(IExpressionContext context, string ignored) => context;
                private static string SideEffect() => throw new InvalidOperationException();
                private static string Value(IExpressionContext context) => context.Request.Method;
            }
            """;

        document.CompileDocument().Errors.Should().Contain(error => error.Id == "APIM2013");
    }
}
