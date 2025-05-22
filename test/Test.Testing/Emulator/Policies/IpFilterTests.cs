// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Document;

using Newtonsoft.Json.Linq;

namespace Test.Emulator.Emulator.Policies;

[TestClass]
public class IpFilterTests
{
    class SimpleAllowAddressIpFilter : IDocument
    {
        public void Inbound(IInboundContext context)
        {
            context.IpFilter(new IpFilterConfig() { Action = "allow", Addresses = ["192.168.0.64"] });
        }
    }

    class SimpleAllowRangeIpFilter : IDocument
    {
        public void Inbound(IInboundContext context)
        {
            context.IpFilter(new IpFilterConfig()
            {
                Action = "allow",
                AddressRanges = [new AddressRange() { From = "192.168.0.1", To = "192.168.0.255" }]
            });
        }
    }

    class SimpleForbidAddressIpFilter : IDocument
    {
        public void Inbound(IInboundContext context)
        {
            context.IpFilter(new IpFilterConfig() { Action = "forbid", Addresses = ["192.168.0.64"] });
        }
    }

    class SimpleForbidRangeIpFilter : IDocument
    {
        public void Inbound(IInboundContext context)
        {
            context.IpFilter(new IpFilterConfig()
            {
                Action = "forbid",
                AddressRanges = [new AddressRange() { From = "192.168.0.1", To = "192.168.0.255" }]
            });
        }
    }

    [TestMethod]
    public void IpFilter_Callback()
    {
        var test = new SimpleAllowAddressIpFilter().AsTestDocument();
        var executedCallback = false;
        test.SetupInbound().IpFilter().WithCallback((_, _) =>
        {
            executedCallback = true;
        });

        test.RunInbound();

        executedCallback.Should().BeTrue();
    }

    [TestMethod]
    public void IpFilter_ShouldAllowRequestIpAddress_AllowSingleAddressFilter()
    {
        var test = new SimpleAllowAddressIpFilter().AsTestDocument();
        test.Context.Request.IpAddress = "192.168.0.64";

        test.RunInbound();

        test.Context.Response.StatusCode.Should().NotBe(403);
    }

    [TestMethod]
    public void IpFilter_ShouldAllowRequestIpAddress_AllowRangeFilter()
    {
        var test = new SimpleAllowRangeIpFilter().AsTestDocument();
        test.Context.Request.IpAddress = "192.168.0.64";

        test.RunInbound();

        test.Context.Response.StatusCode.Should().NotBe(403);
    }

    [TestMethod]
    public void IpFilter_ShouldAllowRequestIpAddress_ForbidSingleAddressFilter()
    {
        var test = new SimpleForbidAddressIpFilter().AsTestDocument();
        test.Context.Request.IpAddress = "192.168.0.65";

        test.RunInbound();

        test.Context.Response.StatusCode.Should().NotBe(403);
    }

    [TestMethod]
    public void IpFilter_ShouldAllowRequestIpAddress_ForbidRangeFilter()
    {
        var test = new SimpleForbidRangeIpFilter().AsTestDocument();
        test.Context.Request.IpAddress = "192.169.0.64";

        test.RunInbound();

        test.Context.Response.StatusCode.Should().NotBe(403);
    }

    [TestMethod]
    public void IpFilter_ShouldDenyRequestIpAddress_AllowSingleAddressFilter()
    {
        var test = new SimpleAllowAddressIpFilter().AsTestDocument();
        test.Context.Request.IpAddress = "192.168.0.65";

        test.RunInbound();

        var response = test.Context.Response;
        response.StatusCode.Should().Be(403);
        response.Headers.Should().ContainKey("Content-Type")
            .WhoseValue.Should().ContainSingle()
            .Which.Should().Be("application/json");
        response.Body.Content.Should().NotBeNullOrWhiteSpace();
        var body = response.Body.As<JObject>();
        body.Should().ContainKey("statusCode")
            .WhoseValue.Should().NotBeNull().And
            .Subject.Value<int>().Should().Be(403);
        body.Should().ContainKey("message")
            .WhoseValue.Should().NotBeNull().And
            .Subject.Value<string>().Should().Be("Forbidden");
    }

    [TestMethod]
    public void IpFilter_ShouldDenyRequestIpAddress_AllowRangeFilter()
    {
        var test = new SimpleAllowRangeIpFilter().AsTestDocument();
        test.Context.Request.IpAddress = "192.169.0.64";

        test.RunInbound();

        var response = test.Context.Response;
        response.StatusCode.Should().Be(403);
        response.Headers.Should().ContainKey("Content-Type")
            .WhoseValue.Should().ContainSingle()
            .Which.Should().Be("application/json");
        response.Body.Content.Should().NotBeNullOrWhiteSpace();
        var body = response.Body.As<JObject>();
        body.Should().ContainKey("statusCode")
            .WhoseValue.Should().NotBeNull().And
            .Subject.Value<int>().Should().Be(403);
        body.Should().ContainKey("message")
            .WhoseValue.Should().NotBeNull().And
            .Subject.Value<string>().Should().Be("Forbidden");
    }

    [TestMethod]
    public void IpFilter_ShouldDenyRequestIpAddress_ForbidSingleAddressFilter()
    {
        var test = new SimpleForbidAddressIpFilter().AsTestDocument();
        test.Context.Request.IpAddress = "192.168.0.64";

        test.RunInbound();

        var response = test.Context.Response;
        response.StatusCode.Should().Be(403);
        response.Headers.Should().ContainKey("Content-Type")
            .WhoseValue.Should().ContainSingle()
            .Which.Should().Be("application/json");
        response.Body.Content.Should().NotBeNullOrWhiteSpace();
        var body = response.Body.As<JObject>();
        body.Should().ContainKey("statusCode")
            .WhoseValue.Should().NotBeNull().And
            .Subject.Value<int>().Should().Be(403);
        body.Should().ContainKey("message")
            .WhoseValue.Should().NotBeNull().And
            .Subject.Value<string>().Should().Be("Forbidden");
    }

    [TestMethod]
    public void IpFilter_ShouldDenyRequestIpAddress_ForbidRangeFilter()
    {
        var test = new SimpleForbidRangeIpFilter().AsTestDocument();
        test.Context.Request.IpAddress = "192.168.0.64";

        test.RunInbound();

        var response = test.Context.Response;
        response.StatusCode.Should().Be(403);
        response.Headers.Should().ContainKey("Content-Type")
            .WhoseValue.Should().ContainSingle()
            .Which.Should().Be("application/json");
        response.Body.Content.Should().NotBeNullOrWhiteSpace();
        var body = response.Body.As<JObject>();
        body.Should().ContainKey("statusCode")
            .WhoseValue.Should().NotBeNull().And
            .Subject.Value<int>().Should().Be(403);
        body.Should().ContainKey("message")
            .WhoseValue.Should().NotBeNull().And
            .Subject.Value<string>().Should().Be("Forbidden");
    }
}