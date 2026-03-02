// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Document;

namespace Test.Emulator.Emulator.Policies;

[TestClass]
public class ReturnResponseTests
{
    class SimpleReturnResponse : IDocument
    {
        public void Inbound(IInboundContext context)
        {
            context.ReturnResponse(new ReturnResponseConfig
            {
                Status = new StatusConfig { Code = 200, Reason = "OK" },
                Body = new BodyConfig { Content = "Hello from inbound" }
            });
        }

        public void Backend(IBackendContext context)
        {
            context.ReturnResponse(new ReturnResponseConfig
            {
                Status = new StatusConfig { Code = 201, Reason = "Created" }
            });
        }

        public void Outbound(IOutboundContext context)
        {
            context.ReturnResponse(new ReturnResponseConfig
            {
                Status = new StatusConfig { Code = 202, Reason = "Accepted" }
            });
        }

        public void OnError(IOnErrorContext context)
        {
            context.ReturnResponse(new ReturnResponseConfig
            {
                Status = new StatusConfig { Code = 500, Reason = "Internal Server Error" }
            });
        }
    }

    class TerminateSectionReturnResponse : IDocument
    {
        public void Inbound(IInboundContext context)
        {
            context.ReturnResponse(new ReturnResponseConfig
            {
                Status = new StatusConfig { Code = 200, Reason = "OK" }
            });
            context.SetHeader("X-After", "should-not-execute");
        }
    }

    class WithHeadersReturnResponse : IDocument
    {
        public void Inbound(IInboundContext context)
        {
            context.ReturnResponse(new ReturnResponseConfig
            {
                Status = new StatusConfig { Code = 200, Reason = "OK" },
                Headers =
                [
                    new HeaderConfig
                    {
                        Name = "X-Custom",
                        ExistsAction = "override",
                        Values = ["custom-value"]
                    }
                ]
            });
        }
    }

    [TestMethod]
    public void ReturnResponse_Inbound()
    {
        var test = new SimpleReturnResponse().AsTestDocument();

        test.RunInbound();

        test.Context.Response.StatusCode.Should().Be(200);
        test.Context.Response.StatusReason.Should().Be("OK");
        test.Context.Response.Body.Content.Should().Be("Hello from inbound");
    }

    [TestMethod]
    public void ReturnResponse_Backend()
    {
        var test = new SimpleReturnResponse().AsTestDocument();

        test.RunBackend();

        test.Context.Response.StatusCode.Should().Be(201);
        test.Context.Response.StatusReason.Should().Be("Created");
    }

    [TestMethod]
    public void ReturnResponse_Outbound()
    {
        var test = new SimpleReturnResponse().AsTestDocument();

        test.RunOutbound();

        test.Context.Response.StatusCode.Should().Be(202);
        test.Context.Response.StatusReason.Should().Be("Accepted");
    }

    [TestMethod]
    public void ReturnResponse_OnError()
    {
        var test = new SimpleReturnResponse().AsTestDocument();

        test.RunOnError();

        test.Context.Response.StatusCode.Should().Be(500);
        test.Context.Response.StatusReason.Should().Be("Internal Server Error");
    }

    [TestMethod]
    public void ReturnResponse_TerminatesSectionExecution()
    {
        var test = new TerminateSectionReturnResponse().AsTestDocument();
        var headerExecuted = false;
        test.SetupInbound().SetHeader().WithCallback((_, _, _) => headerExecuted = true);

        test.RunInbound();

        headerExecuted.Should().BeFalse();
    }

    [TestMethod]
    public void ReturnResponse_WithHeaders()
    {
        var test = new WithHeadersReturnResponse().AsTestDocument();

        test.RunInbound();

        test.Context.Response.StatusCode.Should().Be(200);
        test.Context.Response.Headers.Should().ContainKey("X-Custom")
            .WhoseValue.Should().ContainInOrder("custom-value");
    }

    [TestMethod]
    public void ReturnResponse_Callback()
    {
        var test = new SimpleReturnResponse().AsTestDocument();
        var callbackExecuted = false;
        test.SetupInbound().ReturnResponse((_, _) => true).WithCallback((context, _) =>
        {
            callbackExecuted = true;
            context.Response.StatusCode = 418;
            context.Response.StatusReason = "I'm a teapot";
        });

        test.RunInbound();

        callbackExecuted.Should().BeTrue();
        test.Context.Response.StatusCode.Should().Be(418);
        test.Context.Response.StatusReason.Should().Be("I'm a teapot");
    }
}
