// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Document;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator;

namespace Test.Emulator.Emulator.Policies;

[TestClass]
public class InlinePolicyTests
{
    // InlinePolicy is intentionally a compile-time XML passthrough, not an executable XML policy.
    // The emulator dispatches strongly-typed policy handlers against GatewayContext and never
    // parses arbitrary raw XML into a runtime evaluator. Default execution therefore remains a no-op,
    // while an explicit callback receives the raw string unchanged and can mutate state deliberately.
    private const string InboundXml = "<set-header name=\"X-Test\" exists-action=\"override\"><value>test</value></set-header>";
    private const string BackendXml = "<set-backend-service base-url=\"https://backend.example.com\" />";
    private const string OutboundXml = "<set-header name=\"X-Out\" exists-action=\"override\"><value>out</value></set-header>";
    private const string OnErrorXml = "<set-status code=\"500\" reason=\"Error\" />";

    class SimpleInlinePolicy : IDocument
    {
        public void Inbound(IInboundContext context)
        {
            context.InlinePolicy(InboundXml);
        }

        public void Backend(IBackendContext context)
        {
            context.InlinePolicy(BackendXml);
        }

        public void Outbound(IOutboundContext context)
        {
            context.InlinePolicy(OutboundXml);
        }

        public void OnError(IOnErrorContext context)
        {
            context.InlinePolicy(OnErrorXml);
        }
    }

    [TestMethod]
    public void InlinePolicy_DefaultExecution_IsNoOpInSupportedSections()
    {
        var inbound = new SimpleInlinePolicy().AsTestDocument();
        var inboundStatusCode = inbound.Context.Response.StatusCode;
        var inboundStatusReason = inbound.Context.Response.StatusReason;
        var inboundVariablesCount = inbound.Context.Variables.Count;
        var inboundRequestHeaderCount = inbound.Context.Request.Headers.Count;
        var inboundResponseHeaderCount = inbound.Context.Response.Headers.Count;

        inbound.RunInbound();

        inbound.Context.Variables.Count.Should().Be(inboundVariablesCount);
        inbound.Context.Request.Headers.Count.Should().Be(inboundRequestHeaderCount);
        inbound.Context.Response.Headers.Count.Should().Be(inboundResponseHeaderCount);
        inbound.Context.Response.StatusCode.Should().Be(inboundStatusCode);
        inbound.Context.Response.StatusReason.Should().Be(inboundStatusReason);
        inbound.Context.Request.Headers.Should().NotContainKey("X-Test");

        var backend = new SimpleInlinePolicy().AsTestDocument();
        var backendUrl = backend.Context.BackendUrl;
        var backendResponseStatusCode = backend.Context.Response.StatusCode;
        var backendResponseStatusReason = backend.Context.Response.StatusReason;
        backend.RunBackend();
        backend.Context.BackendUrl.Should().Be(backendUrl);
        backend.Context.Response.StatusCode.Should().Be(backendResponseStatusCode);
        backend.Context.Response.StatusReason.Should().Be(backendResponseStatusReason);

        var outbound = new SimpleInlinePolicy().AsTestDocument();
        outbound.Context.Response.Headers["X-Existing"] = ["keep-me"];
        var outboundStatusCode = outbound.Context.Response.StatusCode;
        var outboundStatusReason = outbound.Context.Response.StatusReason;
        var outboundHeaders = new Dictionary<string, string[]>(outbound.Context.Response.Headers);
        outbound.RunOutbound();
        outbound.Context.Response.StatusCode.Should().Be(outboundStatusCode);
        outbound.Context.Response.StatusReason.Should().Be(outboundStatusReason);
        outbound.Context.Response.Headers.Should().NotContainKey("X-Out");
        outbound.Context.Response.Headers.Should().ContainKey("X-Existing").WhoseValue.Should().Equal("keep-me");
        outbound.Context.Response.Headers.Should().BeEquivalentTo(outboundHeaders);

        var onError = new SimpleInlinePolicy().AsTestDocument();
        var onErrorStatusCode = onError.Context.Response.StatusCode;
        var onErrorStatusReason = onError.Context.Response.StatusReason;
        onError.RunOnError();
        onError.Context.Response.StatusCode.Should().Be(onErrorStatusCode);
        onError.Context.Response.StatusReason.Should().Be(onErrorStatusReason);
    }

    [TestMethod]
    public void InlinePolicy_CallbackReceivesRawXmlAndCanMutateState()
    {
        var test = new SimpleInlinePolicy().AsTestDocument();
        var callbackExecuted = false;

        test.SetupInbound()
            .Inline()
            .WithCallback((context, policy) =>
            {
                callbackExecuted = true;
                policy.Should().Be(InboundXml);
                context.Request.Headers["X-Callback"] = ["callback-value"];
                context.Response.StatusCode = 201;
                context.Response.StatusReason = "Callback";
                context.Variables["inline-policy"] = policy;
            });

        test.RunInbound();

        callbackExecuted.Should().BeTrue();
        test.Context.Request.Headers.Should().ContainKey("X-Callback")
            .WhoseValue.Should().ContainSingle().Which.Should().Be("callback-value");
        test.Context.Response.StatusCode.Should().Be(201);
        test.Context.Response.StatusReason.Should().Be("Callback");
        test.Context.Variables["inline-policy"].Should().Be(InboundXml);
    }

    [TestMethod]
    public void InlinePolicy_PredicateCallback_SeesTheRawXmlString()
    {
        var test = new SimpleInlinePolicy().AsTestDocument();
        var matchingCallback = false;
        var nonMatchingCallback = false;

        test.SetupInbound()
            .Inline((_, policy) => policy.Contains("set-header", StringComparison.Ordinal))
            .WithCallback((_, _) => matchingCallback = true);

        test.SetupInbound()
            .Inline((_, policy) => policy.Contains("set-backend-service", StringComparison.Ordinal))
            .WithCallback((_, _) => nonMatchingCallback = true);

        test.RunInbound();

        matchingCallback.Should().BeTrue();
        nonMatchingCallback.Should().BeFalse();
    }

    [TestMethod]
    public void InlinePolicy_SectionRestrictions_AreLimitedToSupportedSections()
    {
        var type = typeof(MockPoliciesProvider<>).Assembly
            .GetType("Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Policies.InlinePolicyHandler", throwOnError: true)!;

        var supportedSections = type
            .GetCustomAttributes(typeof(SectionAttribute), inherit: false)
            .Cast<SectionAttribute>()
            .Select(attribute => attribute.Scope)
            .OrderBy(name => name)
            .ToArray();

        supportedSections.Should().BeEquivalentTo(
            nameof(IInboundContext),
            nameof(IBackendContext),
            nameof(IOnErrorContext),
            nameof(IOutboundContext));
    }
}
