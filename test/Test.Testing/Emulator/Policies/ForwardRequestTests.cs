// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Document;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Data;

namespace Test.Emulator.Emulator.Policies;

[TestClass]
public class ForwardRequestTests
{
    class SimpleForwardRequest : IDocument
    {
        public void Backend(IBackendContext context)
        {
            context.ForwardRequest();
        }
    }

    class ForwardRequestWithConfig : IDocument
    {
        public void Backend(IBackendContext context)
        {
            context.ForwardRequest(new ForwardRequestConfig { Timeout = 60 });
        }
    }

    [TestMethod]
    public void ForwardRequest_DefaultResponse()
    {
        var test = new SimpleForwardRequest().AsTestDocument();

        test.RunBackend();

        test.Context.Response.StatusCode.Should().Be(200);
    }

    [TestMethod]
    public void ForwardRequest_MockedResponse()
    {
        var test = new SimpleForwardRequest().AsTestDocument();
        test.SetupForwardRequest().ReturnsDefault(new MockBackendResponse
        {
            StatusCode = 201,
            StatusReason = "Created",
            Body = """{"id": 42}""",
            Headers = { { "Content-Type", ["application/json"] } }
        });

        test.RunBackend();

        test.Context.Response.StatusCode.Should().Be(201);
        test.Context.Response.StatusReason.Should().Be("Created");
        test.Context.Response.Body.Content.Should().Be("""{"id": 42}""");
        test.Context.Response.Headers.Should().ContainKey("Content-Type");
    }

    [TestMethod]
    public void ForwardRequest_SequentialResponses()
    {
        var test = new SimpleForwardRequest().AsTestDocument();
        test.SetupForwardRequest()
            .Returns(new MockBackendResponse { StatusCode = 200, Body = "first" })
            .Returns(new MockBackendResponse { StatusCode = 201, Body = "second" });

        test.RunBackend();
        test.Context.Response.StatusCode.Should().Be(200);
        test.Context.Response.Body.Content.Should().Be("first");

        test.RunBackend();
        test.Context.Response.StatusCode.Should().Be(201);
        test.Context.Response.Body.Content.Should().Be("second");
    }

    [TestMethod]
    public void ForwardRequest_ErrorResponse()
    {
        var test = new SimpleForwardRequest().AsTestDocument();
        test.SetupForwardRequest().ReturnsDefault(new MockBackendResponse
        {
            StatusCode = 500,
            StatusReason = "Internal Server Error",
            Body = "backend error"
        });

        test.RunBackend();

        test.Context.Response.StatusCode.Should().Be(500);
    }

    [TestMethod]
    public void ForwardRequest_WithConfig()
    {
        var test = new ForwardRequestWithConfig().AsTestDocument();
        test.SetupForwardRequest().ReturnsDefault(new MockBackendResponse { StatusCode = 204 });

        test.RunBackend();

        test.Context.Response.StatusCode.Should().Be(204);
    }

    [TestMethod]
    public void ForwardRequest_Callback()
    {
        var test = new SimpleForwardRequest().AsTestDocument();
        var callbackExecuted = false;
        test.SetupBackend().ForwardRequest().WithCallback((context, _) =>
        {
            callbackExecuted = true;
            context.Response.StatusCode = 418;
        });

        test.RunBackend();

        callbackExecuted.Should().BeTrue();
        test.Context.Response.StatusCode.Should().Be(418);
    }
}