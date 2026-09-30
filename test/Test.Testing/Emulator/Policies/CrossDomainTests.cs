// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Xml;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Document;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator;

namespace Test.Emulator.Emulator.Policies;

[TestClass]
public class CrossDomainTests
{
    private const string ValidPolicyXml = """
        <cross-domain-policy>
          <site-control permitted-cross-domain-policies="master-only" />
          <allow-access-from domain="*.example.com" to-ports="80,443" />
        </cross-domain-policy>
        """;

    private class SimpleCrossDomainDocument : IDocument
    {
        public void Inbound(IInboundContext context)
        {
            context.CrossDomain(ValidPolicyXml);
        }

        public void Outbound(IOutboundContext context) { }

        public void Backend(IBackendContext context) { }

        public void OnError(IOnErrorContext context) { }
    }

    private class InvalidCrossDomainDocument : IDocument
    {
        private readonly string _policy;

        public InvalidCrossDomainDocument(string policy)
        {
            _policy = policy;
        }

        public void Inbound(IInboundContext context)
        {
            context.CrossDomain(_policy);
        }

        public void Outbound(IOutboundContext context) { }

        public void Backend(IBackendContext context) { }

        public void OnError(IOnErrorContext context) { }
    }

    [TestMethod]
    public void CrossDomain_DefaultExecution_ValidatesXmlWithoutMutatingContext()
    {
        var test = new SimpleCrossDomainDocument().AsTestDocument();
        var originalStatusCode = test.Context.Response.StatusCode;
        var originalStatusReason = test.Context.Response.StatusReason;
        var originalHeaders = new Dictionary<string, string[]>(test.Context.Response.Headers);
        var originalVariables = test.Context.Variables.ToDictionary(entry => entry.Key, entry => entry.Value);

        test.RunInbound();

        test.Context.Response.StatusCode.Should().Be(originalStatusCode);
        test.Context.Response.StatusReason.Should().Be(originalStatusReason);
        test.Context.Response.Headers.Should().BeEquivalentTo(originalHeaders);
        test.Context.Variables.Should().BeEquivalentTo(originalVariables);
        test.Context.Request.Headers.Should().NotContainKey("X-CrossDomain");
    }

    [TestMethod]
    [DataRow("")]
    [DataRow(" ")]
    [DataRow("<cross-domain-policy>")]
    [DataRow("<not-cross-domain-policy />")]
    [DataRow("<foo:cross-domain-policy xmlns:foo='urn:unrelated' />")]
    public void CrossDomain_RejectsMalformedEmptyWrongRootAndNamespacedRootXml(string policy)
    {
        var test = new InvalidCrossDomainDocument(policy).AsTestDocument();

        Action action = () => test.RunInbound();

        action.Should().Throw<PolicyException>();
    }

    [TestMethod]
    public void CrossDomain_CallbackReceivesOriginalRawXmlAndCanMutateState()
    {
        var test = new SimpleCrossDomainDocument().AsTestDocument();
        var callbackExecuted = false;

        test.SetupInbound()
            .CrossDomain()
            .WithCallback((context, policy) =>
            {
                callbackExecuted = true;
                policy.Should().Be(ValidPolicyXml);
                context.Request.Headers["X-Callback"] = ["callback-value"];
                context.Response.StatusCode = 201;
                context.Response.StatusReason = "Callback";
                context.Variables["cross-domain-policy"] = policy;
            });

        test.RunInbound();

        callbackExecuted.Should().BeTrue();
        test.Context.Request.Headers.Should().ContainKey("X-Callback")
            .WhoseValue.Should().ContainSingle().Which.Should().Be("callback-value");
        test.Context.Response.StatusCode.Should().Be(201);
        test.Context.Response.StatusReason.Should().Be("Callback");
        test.Context.Variables["cross-domain-policy"].Should().Be(ValidPolicyXml);
    }

    [TestMethod]
    public void CrossDomain_PredicateCallback_SeesTheOriginalXmlString()
    {
        var test = new SimpleCrossDomainDocument().AsTestDocument();
        var validMatch = false;
        var invalidMatch = false;

        test.SetupInbound()
            .CrossDomain((_, policy) => policy.Contains("allow-access-from", StringComparison.Ordinal))
            .WithCallback((_, _) => validMatch = true);

        test.SetupInbound()
            .CrossDomain((_, policy) => policy.Contains("not-found", StringComparison.Ordinal))
            .WithCallback((_, _) => invalidMatch = true);

        test.RunInbound();

        validMatch.Should().BeTrue();
        invalidMatch.Should().BeFalse();
    }

    [TestMethod]
    public void CrossDomain_SectionRestrictions_AreInboundOnly()
    {
        var type = typeof(MockPoliciesProvider<>).Assembly
            .GetType("Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Policies.CrossDomainHandler", throwOnError: true)!;

        var supportedSections = type
            .GetCustomAttributes(typeof(SectionAttribute), inherit: false)
            .Cast<SectionAttribute>()
            .Select(attribute => attribute.Scope)
            .OrderBy(name => name)
            .ToArray();

        supportedSections.Should().BeEquivalentTo(nameof(IInboundContext));
    }
}
