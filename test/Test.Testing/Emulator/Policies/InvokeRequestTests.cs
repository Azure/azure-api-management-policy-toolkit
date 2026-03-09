// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net;
using System.Text;

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Document;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Expressions;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Services;

namespace Test.Emulator.Emulator.Policies;

[TestClass]
public class InvokeRequestTests
{
    class TerminalInvokeRequestDocument : IDocument
    {
        public void Inbound(IInboundContext context)
        {
            context.SetVariable("before", "set");
            context.InvokeRequest(new InvokeRequestConfig
            {
                Url = "https://example.com/terminal"
            });
            context.SetVariable("after", "should-not-run");
        }

        public void Outbound(IOutboundContext context) { }
        public void Backend(IBackendContext context) { }
        public void OnError(IOnErrorContext context) { }
    }

    class VariableInvokeRequestDocument : IDocument
    {
        public void Inbound(IInboundContext context)
        {
            context.InvokeRequest(new InvokeRequestConfig
            {
                ResponseVariableName = "invoke-response",
                Url = "https://example.com/variable"
            });
            context.SetVariable("after", "ran");
        }

        public void Outbound(IOutboundContext context) { }
        public void Backend(IBackendContext context) { }
        public void OnError(IOnErrorContext context) { }
    }

    [TestMethod]
    public void ShouldWriteResponseAndStopSectionWhenNoResponseVariableIsConfigured()
    {
        var test = new TerminalInvokeRequestDocument().AsTestDocument();
        test.Context.Services.Register<IHttpClient>(new StubHttpClient(_ => new HttpResponseMessage(HttpStatusCode.Accepted)
        {
            Content = new StringContent("proxied", Encoding.UTF8, "text/plain")
        }));

        test.RunInbound();

        test.Context.Response.StatusCode.Should().Be((int)HttpStatusCode.Accepted);
        test.Context.Response.Body.As<string>().Should().Be("proxied");
        test.Context.Variables.Should().ContainKey("before");
        test.Context.Variables.Should().NotContainKey("after");
    }

    [TestMethod]
    public void ShouldExecuteCallbackAndStillStopSectionForTerminalInvokeRequest()
    {
        var test = new TerminalInvokeRequestDocument().AsTestDocument();
        test.SetupInbound()
            .InvokeRequest()
            .WithCallback((context, _) =>
            {
                context.Response.StatusCode = 204;
                context.Variables["callback"] = "hit";
            });

        test.RunInbound();

        test.Context.Response.StatusCode.Should().Be(204);
        test.Context.Variables.Should().ContainKey("callback").WhoseValue.Should().Be("hit");
        test.Context.Variables.Should().NotContainKey("after");
    }

    [TestMethod]
    public void ShouldStoreResponseVariableAndContinueWhenConfigured()
    {
        var test = new VariableInvokeRequestDocument().AsTestDocument();
        test.Context.Services.Register<IHttpClient>(new StubHttpClient(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"ok\":true}", Encoding.UTF8, "application/json")
        }));

        test.RunInbound();

        test.Context.Variables.Should().ContainKey("invoke-response");
        var response = test.Context.Variables["invoke-response"].Should().BeOfType<MockResponse>().Subject;
        response.StatusCode.Should().Be(200);
        response.Body.As<string>().Should().Be("{\"ok\":true}");
        test.Context.Variables.Should().ContainKey("after").WhoseValue.Should().Be("ran");
    }
}
