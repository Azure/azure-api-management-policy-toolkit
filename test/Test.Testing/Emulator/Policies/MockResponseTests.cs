// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Globalization;

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring.Expressions;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Document;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Data;

namespace Test.Emulator.Emulator.Policies;

[TestClass]
public class MockResponseTests
{
    class SimpleMockResponse : IDocument
    {
        public void Inbound(IInboundContext context)
        {
            context.MockResponse();
        }

        public void Outbound(IOutboundContext context)
        {
            context.MockResponse();
        }

        public void OnError(IOnErrorContext context)
        {
            context.MockResponse();
        }
    }

    class TerminateSectionMockResponse : IDocument
    {
        public void Inbound(IInboundContext context)
        {
            context.MockResponse();
            context.SetHeader("X-Header", "Value");
        }
    }

    class WithIndexMockResponse : IDocument
    {
        public void Inbound(IInboundContext context)
        {
            context.MockResponse(new MockResponseConfig { Index = 1 });
        }
    }

    class WithCodeMockResponse : IDocument
    {
        public void Inbound(IInboundContext context)
        {
            context.MockResponse(new MockResponseConfig { StatusCode = 201 });
        }
    }

    class WithContentTypeMockResponse : IDocument
    {
        public void Inbound(IInboundContext context)
        {
            context.MockResponse(new MockResponseConfig { ContentType = "plain/text" });
        }
    }

    class WithCodeAndContentTypeMockResponse : IDocument
    {
        public void Inbound(IInboundContext context)
        {
            context.MockResponse(new MockResponseConfig { StatusCode = 201, ContentType = "plain/text" });
        }
    }

    [TestMethod]
    public void MockResponse_Inbound()
    {
        var test = new SimpleMockResponse().AsTestDocument();

        test.RunInbound();

        test.Context.Response.StatusCode.Should().Be(200);
        test.Context.Response.Headers.GetValueOrDefault("Content-Length").Should().Be("0");
    }

    [TestMethod]
    public void MockResponse_Outbound()
    {
        var test = new SimpleMockResponse().AsTestDocument();

        test.RunOutbound();

        test.Context.Response.StatusCode.Should().Be(200);
        test.Context.Response.Headers.GetValueOrDefault("Content-Length").Should().Be("0");
    }

    [TestMethod]
    public void MockResponse_OnError()
    {
        var test = new SimpleMockResponse().AsTestDocument();

        test.RunOnError();

        test.Context.Response.StatusCode.Should().Be(200);
        test.Context.Response.Headers.GetValueOrDefault("Content-Length").Should().Be("0");
    }

    [TestMethod]
    public void MockResponse_TerminateSectionExecution()
    {
        var test = new TerminateSectionMockResponse().AsTestDocument();
        bool executed = false;
        test.SetupInbound().SetHeader().WithCallback((_, _, _) => executed = true);

        test.RunInbound();

        executed.Should().BeFalse();
    }

    [TestMethod]
    public void MockResponse_WithIndexExample()
    {
        var test = new WithIndexMockResponse().AsTestDocument();
        var notUsed = new ResponseExample(500, "{ \"error\": \"error\" }", "application/json");
        var used = new ResponseExample(400, "<validation>not valid</validation>", "application/xml");
        test.SetupResponseExampleStore().Add(test.Context, notUsed, used, notUsed);

        test.RunInbound();

        var response = test.Context.Response;
        response.StatusCode.Should().Be(used.ResponseCode);
        response.Headers.GetValueOrDefault("Content-Type").Should().Be(used.ContentType);
        response.Headers.GetValueOrDefault("Content-Length")
            .Should().Be(used.Sample.Length.ToString(CultureInfo.InvariantCulture));
        response.Body.Content.Should().Be(used.Sample);
    }

    [TestMethod]
    public void MockResponse_WithCodeExample()
    {
        var test = new WithCodeMockResponse().AsTestDocument();
        var notUsed = new ResponseExample(500, "{ \"error\": \"error\" }", "application/json");
        var used = new ResponseExample(201, "<data>complex data</data>", "application/xml");
        test.SetupResponseExampleStore().Add(test.Context, notUsed, used, notUsed);

        test.RunInbound();

        var response = test.Context.Response;
        response.StatusCode.Should().Be(used.ResponseCode);
        response.Headers.GetValueOrDefault("Content-Type").Should().Be(used.ContentType);
        response.Headers.GetValueOrDefault("Content-Length")
            .Should().Be(used.Sample.Length.ToString(CultureInfo.InvariantCulture));
        response.Body.Content.Should().Be(used.Sample);
    }

    [TestMethod]
    public void MockResponse_WithContentExample()
    {
        var test = new WithContentTypeMockResponse().AsTestDocument();
        var notUsed = new ResponseExample(500, "error", "plain/text");
        var used = new ResponseExample(200, "complex data", "plain/text");
        test.SetupResponseExampleStore().Add(test.Context, notUsed, used, notUsed);

        test.RunInbound();

        var response = test.Context.Response;
        response.StatusCode.Should().Be(used.ResponseCode);
        response.Headers.GetValueOrDefault("Content-Type").Should().Be(used.ContentType);
        response.Headers.GetValueOrDefault("Content-Length")
            .Should().Be(used.Sample.Length.ToString(CultureInfo.InvariantCulture));
        response.Body.Content.Should().Be(used.Sample);
    }

    [TestMethod]
    public void MockResponse_WithCodeAndContentExample()
    {
        var test = new WithCodeAndContentTypeMockResponse().AsTestDocument();
        var notUsed = new ResponseExample(500, "error", "plain/text");
        var used = new ResponseExample(201, "complex data", "plain/text");
        test.SetupResponseExampleStore().Add(test.Context, notUsed, used, notUsed);

        test.RunInbound();

        var response = test.Context.Response;
        response.StatusCode.Should().Be(used.ResponseCode);
        response.Headers.GetValueOrDefault("Content-Type").Should().Be(used.ContentType);
        response.Headers.GetValueOrDefault("Content-Length")
            .Should().Be(used.Sample.Length.ToString(CultureInfo.InvariantCulture));
        response.Body.Content.Should().Be(used.Sample);
    }
}