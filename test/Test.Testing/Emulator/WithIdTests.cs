// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Document;

namespace Test.Emulator.Emulator.Policies;

[TestClass]
public class WithIdTests
{
    [TestMethod]
    [DataRow("inbound")]
    [DataRow("backend")]
    [DataRow("outbound")]
    [DataRow("on-error")]
    public void WithId_ReturnsTheSameProxyThroughMultipleCallsAndPreservesExpressions(string section)
    {
        var test = new ExecutionTestDocument
        {
            InboundAction = context =>
            {
                context.WithId("first").WithId("last").Should().BeSameAs(context);
                context.WithId("variable").SetVariable("value", context.ExpressionContext.Request.Method);
            },
            BackendAction = context =>
            {
                context.WithId("first").WithId("last").Should().BeSameAs(context);
                context.WithId("variable").SetVariable("value", context.ExpressionContext.Request.Method);
            },
            OutboundAction = context =>
            {
                context.WithId("first").WithId("last").Should().BeSameAs(context);
                context.WithId("variable").SetVariable("value", context.ExpressionContext.Request.Method);
            },
            OnErrorAction = context =>
            {
                context.WithId("first").WithId("last").Should().BeSameAs(context);
                context.WithId("variable").SetVariable("value", context.ExpressionContext.Request.Method);
            }
        }.AsTestDocument();
        test.Context.Request.Method = "PATCH";

        ExecutionTest.RunSection(test, section);

        test.Context.Variables["value"].Should().Be("PATCH");
        test.Context.ResponseTerminated.Should().BeFalse();
    }

    [TestMethod]
    [DataRow("inbound")]
    [DataRow("backend")]
    [DataRow("outbound")]
    [DataRow("on-error")]
    public void WithId_ChainingPreservesPolicyCallbackOverrides(string section)
    {
        var calls = 0;
        var test = new ExecutionTestDocument
        {
            InboundAction = context => context.WithId("variable").SetVariable("id-value", "value"),
            BackendAction = context => context.WithId("variable").SetVariable("id-value", "value"),
            OutboundAction = context => context.WithId("variable").SetVariable("id-value", "value"),
            OnErrorAction = context => context.WithId("variable").SetVariable("id-value", "value")
        }.AsTestDocument();
        void Callback(GatewayContext context, string name, object value)
        {
            calls++;
            name.Should().Be("id-value");
            value.Should().Be("value");
        }
        test.SetupInbound().SetVariable().WithCallback(Callback);
        test.SetupBackend().SetVariable().WithCallback(Callback);
        test.SetupOutbound().SetVariable().WithCallback(Callback);
        test.SetupOnError().SetVariable().WithCallback(Callback);

        ExecutionTest.RunSection(test, section);

        calls.Should().Be(1);
        test.Context.Variables.Should().NotContainKey("id-value");
    }

    [TestMethod]
    [DataRow("inbound")]
    [DataRow("backend")]
    [DataRow("outbound")]
    [DataRow("on-error")]
    public void WithId_FragmentChainingReturnsTheFragmentProxyAndRetainsCallerRouting(string section)
    {
        var test = new ExecutionTestDocument
        {
            InboundAction = context => context.WithId("include").IncludeFragment("id-fragment"),
            BackendAction = context => context.WithId("include").IncludeFragment("id-fragment"),
            OutboundAction = context => context.WithId("include").IncludeFragment("id-fragment"),
            OnErrorAction = context => context.WithId("include").IncludeFragment("id-fragment")
        }.AsTestDocument();
        test.RegisterFragment("id-fragment", new IncludeFragmentTests.CallbackFragment(context =>
        {
            context.WithId("first").WithId("last").Should().BeSameAs(context);
            context.WithId("header").SetHeader("X-Fragment-Id", "value");
        }));

        ExecutionTest.RunSection(test, section);

        var headers = section is "inbound" or "backend"
            ? test.Context.Request.Headers
            : test.Context.Response.Headers;
        headers["X-Fragment-Id"].Should().Equal("value");
        test.Context.ResponseTerminated.Should().BeFalse();
    }

    [TestMethod]
    [DataRow("inbound")]
    [DataRow("backend")]
    [DataRow("outbound")]
    [DataRow("on-error")]
    public void WithId_ChainingToReturnResponseDoesNotLoseEarlyTermination(string section)
    {
        var config = new ReturnResponseConfig
        {
            Status = new StatusConfig { Code = 206, Reason = "Partial Content" }
        };
        var test = new ExecutionTestDocument
        {
            InboundAction = context =>
            {
                context.WithId("first").WithId("last").ReturnResponse(config);
                context.SetVariable("after", true);
            },
            BackendAction = context =>
            {
                context.WithId("first").WithId("last").ReturnResponse(config);
                context.SetVariable("after", true);
            },
            OutboundAction = context =>
            {
                context.WithId("first").WithId("last").ReturnResponse(config);
                context.SetVariable("after", true);
            },
            OnErrorAction = context =>
            {
                context.WithId("first").WithId("last").ReturnResponse(config);
                context.SetVariable("after", true);
            }
        }.AsTestDocument();

        ExecutionTest.RunSection(test, section);

        test.Context.Response.StatusCode.Should().Be(206);
        test.Context.ResponseTerminated.Should().BeTrue();
        test.Context.Variables.Should().NotContainKey("after");
    }
}