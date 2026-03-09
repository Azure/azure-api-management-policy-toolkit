// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net;

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Document;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Services;

namespace Test.Emulator.Emulator.Policies;

[TestClass]
public class ForwardRequestTests
{
    class SimpleForwardRequest : IDocument
    {
        public void Inbound(IInboundContext context) { }

        public void Backend(IBackendContext context)
        {
            context.ForwardRequest();
        }

        public void Outbound(IOutboundContext context) { }
        public void OnError(IOnErrorContext context) { }
    }

    [TestMethod]
    public void ForwardRequest_Callback()
    {
        var test = new SimpleForwardRequest().AsTestDocument();
        var executedCallback = false;
        test.SetupBackend().ForwardRequest().WithCallback((_, _) =>
        {
            executedCallback = true;
        });

        test.RunBackend();

        executedCallback.Should().BeTrue();
    }

    [TestMethod]
    public void ForwardRequest_NoHttpClient_NoOps()
    {
        // Arrange
        var test = new SimpleForwardRequest().AsTestDocument();
        var originalStatusCode = test.Context.Response.StatusCode;

        // Act - no IHttpClient registered, should not throw
        test.RunBackend();

        // Assert - response remains unchanged
        test.Context.Response.StatusCode.Should().Be(originalStatusCode);
    }

    [TestMethod]
    public void ForwardRequest_WithHttpClient_CopiesResponseToContext()
    {
        // Arrange
        var test = new SimpleForwardRequest().AsTestDocument();
        test.Context.Request.Body.Content = "";
        var stubClient = new StubHttpClient(req =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.Created)
            {
                Content = new StringContent("response-body"),
            };
            response.Headers.Add("X-Custom-Response", "resp-value");
            return response;
        });
        test.Context.Services.Register<IHttpClient>(stubClient);

        // Act
        test.RunBackend();

        // Assert
        test.Context.Response.StatusCode.Should().Be(201);
        test.Context.Response.Body.Content.Should().Be("response-body");
        test.Context.Response.Headers.Should().ContainKey("X-Custom-Response")
            .WhoseValue.Should().Contain("resp-value");
    }

    [TestMethod]
    public void ForwardRequest_UsesBackendUrl_WhenSet()
    {
        // Arrange
        var test = new SimpleForwardRequest().AsTestDocument();
        test.Context.BackendUrl = "https://backend.example.com";
        test.Context.Request.Body.Content = "";
        var stubClient = new StubHttpClient(req => new HttpResponseMessage(HttpStatusCode.OK));
        test.Context.Services.Register<IHttpClient>(stubClient);

        // Act
        test.RunBackend();

        // Assert
        stubClient.LastRequest.Should().NotBeNull();
        stubClient.LastRequest!.RequestUri!.ToString().Should().StartWith("https://backend.example.com");
    }
}