// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Document;

namespace Test.Emulator.Emulator.Policies;

[TestClass]
public class SetMethodTests
{
    class SimpleSetMethod : IDocument
    {
        public void Inbound(IInboundContext context)
        {
            context.SetMethod("post");
        }

        public void Backend(IBackendContext context)
        {
            context.SetMethod("put");
        }

        public void Outbound(IOutboundContext context)
        {
            context.SetMethod("delete");
        }

        public void OnError(IOnErrorContext context)
        {
            context.SetMethod("patch");
        }
    }

    [TestMethod]
    public void SetMethod_Inbound_NormalizesAndAssignsMethod()
    {
        var test = new SimpleSetMethod().AsTestDocument();
        test.Context.Request.Method = "GET";

        test.RunInbound();

        test.Context.Request.Method.Should().Be("POST");
    }

    [TestMethod]
    public void SetMethod_Backend_NormalizesAndAssignsMethod()
    {
        var test = new SimpleSetMethod().AsTestDocument();
        test.Context.Request.Method = "GET";

        test.RunBackend();

        test.Context.Request.Method.Should().Be("PUT");
    }

    [TestMethod]
    public void SetMethod_Outbound_NormalizesAndAssignsMethod()
    {
        var test = new SimpleSetMethod().AsTestDocument();
        test.Context.Request.Method = "GET";

        test.RunOutbound();

        test.Context.Request.Method.Should().Be("DELETE");
    }

    [TestMethod]
    public void SetMethod_OnError_NormalizesAndAssignsMethod()
    {
        var test = new SimpleSetMethod().AsTestDocument();
        test.Context.Request.Method = "GET";

        test.RunOnError();

        test.Context.Request.Method.Should().Be("PATCH");
    }

    [TestMethod]
    public void SetMethod_Inbound_CallbackOverrideReplacesDefaultMethod()
    {
        var test = new SimpleSetMethod().AsTestDocument();
        test.Context.Request.Method = "GET";

        test.SetupInbound().SetMethod((_, method) => method == "post").WithCallback((context, method) =>
        {
            context.Request.Method = method.ToUpperInvariant() == "POST" ? "OPTIONS" : method.ToUpperInvariant();
        });

        test.RunInbound();

        test.Context.Request.Method.Should().Be("OPTIONS");
    }
}
