// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Document;

using Newtonsoft.Json.Linq;

namespace Test.Emulator.Emulator.Policies;

[TestClass]
public class CheckHeaderTests
{
    class SimpleCheckHeader : IDocument
    {
        public void Inbound(IInboundContext context)
        {
            context.CheckHeader(new CheckHeaderConfig()
            {
                Name = "Test",
                FailCheckHttpCode = 401,
                FailCheckErrorMessage = "Request do not contain test header",
                IgnoreCase = false,
                Values = []
            });
        }
    }

    [TestMethod]
    public void CheckHeader_Callback()
    {
        var test = new SimpleCheckHeader().AsTestDocument();
        var executedCallback = false;
        test.SetupInbound().CheckHeader().WithCallback((_, _) =>
        {
            executedCallback = true;
        });

        test.RunInbound();

        executedCallback.Should().BeTrue();
    }

    [TestMethod]
    public void CheckHeader_PassExistenceCheck()
    {
        var test = new SimpleCheckHeader().AsTestDocument();
        test.Context.Request.Headers["Test"] = ["test"];

        test.RunInbound();

        var response = test.Context.Response;
        response.StatusCode.Should().NotBe(401);
    }

    [TestMethod]
    public void CheckHeader_FailExistenceCheck()
    {
        var test = new SimpleCheckHeader().AsTestDocument();

        test.RunInbound();

        var response = test.Context.Response;
        response.StatusCode.Should().Be(401);
        response.Headers.Should().ContainKey("Content-Type")
            .WhoseValue.Should().ContainSingle()
            .Which.Should().Be("application/json");
        response.Body.Content.Should().NotBeNullOrWhiteSpace();
        var body = response.Body.As<JObject>();
        body.Should().ContainKey("statusCode")
            .WhoseValue.Should().NotBeNull().And
            .Subject.Value<int>().Should().Be(401);
        body.Should().ContainKey("message")
            .WhoseValue.Should().NotBeNull().And
            .Subject.Value<string>().Should().Be("Request do not contain test header");
    }
}