// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Document;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Expressions;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Services;

namespace Test.Emulator.Emulator.Policies;

[TestClass]
public class SendRequestTests
{
    class SimpleSendRequest : IDocument
    {
        public void Inbound(IInboundContext context)
        {
            context.SendRequest(new SendRequestConfig
            {
                ResponseVariableName = "resp",
                Url = "https://example.com/api",
                Method = "GET",
            });
        }

        public void Backend(IBackendContext context) { }
        public void Outbound(IOutboundContext context) { }
        public void OnError(IOnErrorContext context) { }
    }

    class CopyModeSendRequest : IDocument
    {
        public void Inbound(IInboundContext context)
        {
            context.SendRequest(new SendRequestConfig
            {
                ResponseVariableName = "resp",
                Mode = "copy",
                Url = "https://example.com/api",
            });
        }

        public void Backend(IBackendContext context) { }
        public void Outbound(IOutboundContext context) { }
        public void OnError(IOnErrorContext context) { }
    }

    class NewModeSendRequest : IDocument
    {
        public void Inbound(IInboundContext context)
        {
            context.SendRequest(new SendRequestConfig
            {
                ResponseVariableName = "resp",
                Mode = "new",
                Url = "https://example.com/api",
            });
        }

        public void Backend(IBackendContext context) { }
        public void Outbound(IOutboundContext context) { }
        public void OnError(IOnErrorContext context) { }
    }

    [TestMethod]
    public void SendRequest_StoresResponseAsMockResponse()
    {
        // Arrange
        var test = new SimpleSendRequest().AsTestDocument();
        var stubClient = StubHttpClient.Ok("hello");
        test.Context.Services.Register<IHttpClient>(stubClient);

        // Act
        test.RunInbound();

        // Assert
        test.Context.Variables.Should().ContainKey("resp");
        var resp = test.Context.Variables["resp"];
        resp.Should().BeOfType<MockResponse>();
        var mockResponse = (MockResponse)resp;
        mockResponse.StatusCode.Should().Be(200);
        mockResponse.Body.Content.Should().Be("hello");
    }

    [TestMethod]
    public void SendRequest_CopyMode_ClonesCurrentRequest()
    {
        // Arrange
        var test = new CopyModeSendRequest().AsTestDocument();
        test.Context.Request.Headers["X-Custom"] = new[] { "val" };
        test.Context.Request.Body.Content = "";
        var stubClient = StubHttpClient.Ok();
        test.Context.Services.Register<IHttpClient>(stubClient);

        // Act
        test.RunInbound();

        // Assert
        stubClient.LastRequest.Should().NotBeNull();
        stubClient.LastRequest!.Headers.Contains("X-Custom").Should().BeTrue();
    }

    [TestMethod]
    public void SendRequest_NewMode_DoesNotCopyHeaders()
    {
        // Arrange
        var test = new NewModeSendRequest().AsTestDocument();
        test.Context.Request.Headers["X-Custom"] = new[] { "val" };
        var stubClient = StubHttpClient.Ok();
        test.Context.Services.Register<IHttpClient>(stubClient);

        // Act
        test.RunInbound();

        // Assert
        stubClient.LastRequest.Should().NotBeNull();
        stubClient.LastRequest!.Headers.Contains("X-Custom").Should().BeFalse();
    }

    [TestMethod]
    public void SendRequest_NoHttpClient_Throws()
    {
        // Arrange
        var test = new SimpleSendRequest().AsTestDocument();

        // Act & Assert
        var act = () => test.RunInbound();
        act.Should().Throw<PolicyException>()
            .WithInnerException<InvalidOperationException>();
    }
}
