// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text;

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Document;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator;

namespace Test.Emulator.Emulator.Policies;

[TestClass]
public class JsonPTests
{
    private class JsonPDocument : IDocument
    {
        public void Inbound(IInboundContext context) { }

        public void Outbound(IOutboundContext context)
        {
            context.JsonP("callback");
        }

        public void Backend(IBackendContext context) { }

        public void OnError(IOnErrorContext context) { }
    }

    private class WhitespaceCallbackNameDocument : IDocument
    {
        public void Inbound(IInboundContext context) { }

        public void Outbound(IOutboundContext context)
        {
            context.JsonP(" ");
        }

        public void Backend(IBackendContext context) { }

        public void OnError(IOnErrorContext context) { }
    }

    private class MultibyteCallbackNameDocument : IDocument
    {
        public void Inbound(IInboundContext context) { }

        public void Outbound(IOutboundContext context)
        {
            context.JsonP("回调");
        }

        public void Backend(IBackendContext context) { }

        public void OnError(IOnErrorContext context) { }
    }

    [TestMethod]
    public void Outbound_JsonP_ShouldLeavePlainJsonUnchangedWhenCallbackQueryMissing()
    {
        var test = new TestDocument(new JsonPDocument());
        var originalBody = "{\"message\":\"hello\"}";
        test.Context.Response.StatusCode = 201;
        test.Context.Response.StatusReason = "Created";
        test.Context.Response.Body.Content = originalBody;
        test.Context.Response.Headers["Content-Type"] = ["application/json"];
        test.Context.Response.Headers["X-Test"] = ["keep-me"];

        test.RunOutbound();

        test.Context.Response.StatusCode.Should().Be(201);
        test.Context.Response.StatusReason.Should().Be("Created");
        test.Context.Response.Body.Content.Should().Be(originalBody);
        test.Context.Response.Headers["Content-Type"].Should().Equal("application/json");
        test.Context.Response.Headers["X-Test"].Should().Equal("keep-me");
    }

    [TestMethod]
    public void Outbound_JsonP_ShouldWrapValidJsonPayloadPreservingBodyFidelity()
    {
        var test = new TestDocument(new JsonPDocument());
        var originalBody = "{ \"message\": \"你好\", \"text\": \"keep spacing\" }";
        test.Context.Request.Url.Query["callback"] = ["app.cb"];
        test.Context.Response.Body.Content = originalBody;
        test.Context.Response.Headers["X-Test"] = ["keep-me"];

        test.RunOutbound();

        var expectedBody = $"app.cb({originalBody});";
        test.Context.Response.StatusCode.Should().Be(200);
        test.Context.Response.Body.Content.Should().Be(expectedBody);
        test.Context.Response.Headers["Content-Type"].Should().Equal("application/javascript");
        test.Context.Response.Headers["X-Test"].Should().Equal("keep-me");
        test.Context.Response.Headers["Content-Length"].Should().Equal(Encoding.UTF8.GetByteCount(expectedBody).ToString());
    }

    [TestMethod]
    public void Outbound_JsonP_ShouldRejectEmptyCallbackValue()
    {
        var test = new TestDocument(new JsonPDocument());
        test.Context.Request.Url.Query["callback"] = [string.Empty];
        test.Context.Response.Body.Content = "{\"message\":\"hello\"}";

        test.RunOutbound();

        test.Context.Response.StatusCode.Should().Be(400);
        test.Context.Response.Headers["Content-Type"].Should().Equal("application/json");
        test.Context.Response.Body.Content.Should().Contain("callback").And.Contain("invalid");
        test.Context.Response.Headers["Content-Length"].Should().Equal(Encoding.UTF8.GetByteCount(test.Context.Response.Body.Content).ToString());
    }

    [TestMethod]
    public void Outbound_JsonP_ShouldRejectUnsafeCallbackIdentifiers()
    {
        var test = new TestDocument(new JsonPDocument());
        test.Context.Request.Url.Query["callback"] = ["alert(1)"];
        test.Context.Response.Body.Content = "{\"message\":\"hello\"}";

        test.RunOutbound();

        test.Context.Response.StatusCode.Should().Be(400);
        test.Context.Response.Headers["Content-Type"].Should().Equal("application/json");
        test.Context.Response.Body.Content.Should().Contain("callback");
        test.Context.Response.Headers["Content-Length"].Should().Equal(Encoding.UTF8.GetByteCount(test.Context.Response.Body.Content).ToString());
    }

    [TestMethod]
    public void Outbound_JsonP_ShouldRejectUnsafeCallbackWithTrailingNewline()
    {
        var test = new TestDocument(new JsonPDocument());
        test.Context.Request.Url.Query["callback"] = ["cb\n"];
        test.Context.Response.Body.Content = "{\"message\":\"hello\"}";

        test.RunOutbound();

        test.Context.Response.StatusCode.Should().Be(400);
        test.Context.Response.Headers["Content-Type"].Should().Equal("application/json");
        test.Context.Response.Body.Content.Should().Contain("invalid");
    }

    [TestMethod]
    public void Outbound_JsonP_ShouldRejectWhitespaceCallbackParameterName()
    {
        var test = new TestDocument(new WhitespaceCallbackNameDocument());

        var act = () => test.RunOutbound();

        var ex = act.Should().Throw<PolicyException>().Which;
        ex.Policy.Should().Be("JsonP");
        ex.InnerException.Should().BeOfType<ArgumentException>()
            .Which.ParamName.Should().Be("callbackParameterName");
    }

    [TestMethod]
    public void Outbound_JsonP_ShouldUseUtf8ByteCountForMultibyteErrorBody()
    {
        var test = new TestDocument(new MultibyteCallbackNameDocument());
        test.Context.Request.Url.Query["回调"] = ["cb\n"];
        test.Context.Response.Body.Content = "{\"message\":\"hello\"}";

        test.RunOutbound();

        var body = test.Context.Response.Body.Content;
        var contentLength = test.Context.Response.Headers["Content-Length"][0];

        body.Should().Contain("回调");
        contentLength.Should().Be(Encoding.UTF8.GetByteCount(body).ToString());
        contentLength.Should().NotBe(body.Length.ToString());
    }

    [TestMethod]
    public void Outbound_JsonP_ShouldRejectNonJsonBody()
    {
        var test = new TestDocument(new JsonPDocument());
        test.Context.Request.Url.Query["callback"] = ["cb"];
        test.Context.Response.Body.Content = "hello world";

        test.RunOutbound();

        test.Context.Response.StatusCode.Should().Be(400);
        test.Context.Response.Headers["Content-Type"].Should().Equal("application/json");
        test.Context.Response.Body.Content.Should().Contain("JSON");
        test.Context.Response.Headers["Content-Length"].Should().Equal(Encoding.UTF8.GetByteCount(test.Context.Response.Body.Content).ToString());
    }

    [TestMethod]
    public void Outbound_JsonP_ShouldAllowCallbackOverrideWithConfiguredName()
    {
        var test = new TestDocument(new JsonPDocument());
        var callbackExecuted = false;

        test.SetupOutbound()
            .JsonP((_, callbackParameterName) => callbackParameterName == "callback")
            .WithCallback((context, callbackName) =>
            {
                callbackExecuted = true;
                context.Response.Body.Content = $"{callbackName}({{\"ok\":true}});";
                context.Response.Headers["Content-Type"] = ["application/javascript"];
            });

        test.Context.Request.Url.Query["callback"] = ["app.cb"];
        test.Context.Response.Body.Content = "{\"message\":\"hello\"}";

        test.RunOutbound();

        callbackExecuted.Should().BeTrue();
        test.Context.Response.Body.Content.Should().Be("callback({\"ok\":true});");
        test.Context.Response.Headers["Content-Type"].Should().Equal("application/javascript");
    }
}
